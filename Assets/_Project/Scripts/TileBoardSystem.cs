using System;
using System.Collections.Generic;
using UnityEngine;

namespace projectsplippy
{
    [Serializable]
    public struct TileSpawnWeights
    {
        public float fillerWeight;
        public float farmlandWeight;
        public float marineWeight;

        public static TileSpawnWeights EarlyGameDefault => new TileSpawnWeights
        {
            fillerWeight = 0.6f,
            farmlandWeight = 0.32f,
            marineWeight = 0.08f
        };
    }

    [Serializable]
    public struct BoardTurnRules
    {
        public int replacementsPerTurn;
        public int farmlandReplaceLockTurns;
        public float rockChanceFromFiller;

        public static BoardTurnRules Default => new BoardTurnRules
        {
            replacementsPerTurn = 2,
            farmlandReplaceLockTurns = 2,
            rockChanceFromFiller = 0.12f
        };
    }

    public sealed class BoardTurnResult
    {
        public TileType LandedTileType;
        public TileLandingResult LandingResult;
        public int AdjacencyClusterSize;
        public int AdjacencyBonusScore;
        public readonly Dictionary<Vector2Int, TileType> ReplacedTiles = new Dictionary<Vector2Int, TileType>();
    }

    public sealed class TileStepResult
    {
        public Vector2Int Cell;
        public TileType EnteredType;
        public TileType CurrentType;
        public TileLandingResult LandingResult;
        public bool MarineConsumed;
        public int ConnectedClusterSize;
    }

    public sealed class DroughtResult
    {
        public readonly List<Vector2Int> DehydratedCells = new List<Vector2Int>();
        public readonly Dictionary<Vector2Int, TileType> ReplacedTiles = new Dictionary<Vector2Int, TileType>();
    }

    public sealed class TileBoardSystem
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        private readonly int gridSize;
        private readonly TileGridModel model;
        private readonly BoardTurnRules turnRules;
        private readonly TileSpawnWeights spawnWeights;
        private readonly Dictionary<Vector2Int, int> replaceLockTurns = new Dictionary<Vector2Int, int>();
        private readonly System.Random random;

        public TileBoardSystem(int gridSize, TileRules tileRules, BoardTurnRules turnRules, TileSpawnWeights spawnWeights, int seed = 0)
        {
            this.gridSize = Mathf.Max(1, gridSize);
            this.turnRules = ResolveTurnRules(turnRules);
            this.spawnWeights = ResolveSpawnWeights(spawnWeights);
            model = new TileGridModel(this.gridSize, tileRules);
            random = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public void InitializeBoard(Vector2Int? protectedCell = null)
        {
            replaceLockTurns.Clear();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var cell = new Vector2Int(x, y);
                    TileType type = RollInitialTileType();

                    if (protectedCell.HasValue && cell == protectedCell.Value && type == TileType.Rock)
                    {
                        type = TileType.Filler;
                    }

                    model.SetTileType(cell, type);
                    replaceLockTurns[cell] = 0;
                }
            }
        }

        public void ApplyLobbyMask(HashSet<Vector2Int> walkableCells)
        {
            var allowed = walkableCells ?? new HashSet<Vector2Int>();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    TileType type = allowed.Contains(cell) ? TileType.Filler : TileType.Rock;
                    model.SetTileType(cell, type);
                }
            }
        }

        public TileType GetTileType(Vector2Int cell)
        {
            return model.GetTileType(cell);
        }

        public bool IsWalkable(Vector2Int cell)
        {
            return model.IsWalkable(cell);
        }

        public bool TryGetTile(Vector2Int cell, out TileData tile)
        {
            return model.TryGetTile(cell, out tile);
        }

        public TileStepResult ProcessStep(Vector2Int cell)
        {
            TileType enteredType = model.GetTileType(cell);
            TileLandingResult landingResult = model.ProcessLanding(cell);
            int connectedClusterSize = CountConnectedSameType(cell, enteredType);

            bool marineConsumed = enteredType == TileType.Marine;
            bool farmlandCleared = enteredType == TileType.Farmland && landingResult.LandedCellBloomed;
            bool sanitationCleared = enteredType == TileType.Sanitation && landingResult.LandedCellBloomed;

            if (marineConsumed)
            {
                model.SetTileType(cell, TileType.Filler);
            }
            else if (farmlandCleared)
            {
                model.SetTileType(cell, TileType.Filler);
            }
            else if (sanitationCleared)
            {
                model.SetTileType(cell, TileType.Filler);
            }

            return new TileStepResult
            {
                Cell = cell,
                EnteredType = enteredType,
                CurrentType = model.GetTileType(cell),
                LandingResult = landingResult,
                MarineConsumed = marineConsumed,
                ConnectedClusterSize = connectedClusterSize
            };
        }

        public List<Vector2Int> SpawnSanitationAfterMove(float chancePerFillerTile, int maxSpawns, Vector2Int? protectedCell = null)
        {
            float clampedChance = Mathf.Clamp01(chancePerFillerTile);
            int allowedSpawns = Mathf.Max(0, maxSpawns);
            var spawned = new List<Vector2Int>();

            if (allowedSpawns <= 0 || clampedChance <= 0f)
            {
                return spawned;
            }

            var candidates = new List<Vector2Int>();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var cell = new Vector2Int(x, y);

                    if (protectedCell.HasValue && cell == protectedCell.Value)
                    {
                        continue;
                    }

                    if (model.GetTileType(cell) == TileType.Filler)
                    {
                        candidates.Add(cell);
                    }
                }
            }

            Shuffle(candidates);

            for (int i = 0; i < candidates.Count && spawned.Count < allowedSpawns; i++)
            {
                if (Random01() > clampedChance)
                {
                    continue;
                }

                Vector2Int cell = candidates[i];
                model.SetTileType(cell, TileType.Sanitation);
                spawned.Add(cell);
            }

            return spawned;
        }

        public List<Vector2Int> SpreadSanitationToAdjacent(IReadOnlyList<Vector2Int> sourceCells, Vector2Int? protectedCell = null)
        {
            var spread = new List<Vector2Int>();

            if (sourceCells == null || sourceCells.Count == 0)
            {
                return spread;
            }

            var unique = new HashSet<Vector2Int>();

            for (int i = 0; i < sourceCells.Count; i++)
            {
                Vector2Int source = sourceCells[i];

                for (int d = 0; d < CardinalDirections.Length; d++)
                {
                    Vector2Int next = source + CardinalDirections[d];

                    if (next.x < 0 || next.y < 0 || next.x >= gridSize || next.y >= gridSize)
                    {
                        continue;
                    }

                    if (protectedCell.HasValue && next == protectedCell.Value)
                    {
                        continue;
                    }

                    if (model.GetTileType(next) != TileType.Filler)
                    {
                        continue;
                    }

                    if (!unique.Add(next))
                    {
                        continue;
                    }

                    model.SetTileType(next, TileType.Sanitation);
                    spread.Add(next);
                }
            }

            return spread;
        }

        public DroughtResult ApplyDrought(Vector2Int? protectedCell, int hydrationLoss, int newTilesCount)
        {
            var result = new DroughtResult();
            result.DehydratedCells.AddRange(model.ReduceHydrationAll(hydrationLoss));

            int targetCount = Mathf.Max(0, newTilesCount);

            if (targetCount <= 0)
            {
                return result;
            }

            var candidates = new List<Vector2Int>();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (protectedCell.HasValue && cell == protectedCell.Value)
                    {
                        continue;
                    }

                    if (model.GetTileType(cell) == TileType.Rock)
                    {
                        continue;
                    }

                    if (model.TryGetTile(cell, out TileData tile) && tile.Progress > 0)
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            Shuffle(candidates);

            for (int i = 0; i < candidates.Count && result.ReplacedTiles.Count < targetCount; i++)
            {
                Vector2Int cell = candidates[i];
                TileType oldType = model.GetTileType(cell);
                TileType newType = RollWeightedTileType();

                if (newType == oldType)
                {
                    continue;
                }

                model.SetTileType(cell, newType);
                result.ReplacedTiles[cell] = newType;
            }

            return result;
        }

        public BoardTurnResult ResolveEndTurn(Vector2Int landedCell)
        {
            var result = new BoardTurnResult
            {
                LandedTileType = model.GetTileType(landedCell),
                LandingResult = model.ProcessLanding(landedCell)
            };

            result.AdjacencyClusterSize = CountConnectedSameType(landedCell, result.LandedTileType);
            result.AdjacencyBonusScore = ComputeAdjacencyBonus(result.LandedTileType, result.AdjacencyClusterSize);

            if (result.LandedTileType == TileType.Farmland && result.LandingResult.LandedCellBloomed)
            {
                TileType bloomReplacement = RollFarmlandBloomReplacement();
                model.SetTileType(landedCell, bloomReplacement);
                replaceLockTurns[landedCell] = 0;
                result.ReplacedTiles[landedCell] = bloomReplacement;
            }

            if (result.LandedTileType == TileType.Marine)
            {
                TileType marineReplacement = RollNonMarineNonRockTileType();
                model.SetTileType(landedCell, marineReplacement);
                replaceLockTurns[landedCell] = 0;
                result.ReplacedTiles[landedCell] = marineReplacement;
            }

            TickReplaceLocks();

            if (result.LandedTileType == TileType.Farmland)
            {
                replaceLockTurns[landedCell] = turnRules.farmlandReplaceLockTurns;
            }

            ReplaceRandomTiles(landedCell, result);
            return result;
        }

        private void TickReplaceLocks()
        {
            var keys = new List<Vector2Int>(replaceLockTurns.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                Vector2Int cell = keys[i];

                if (replaceLockTurns[cell] > 0)
                {
                    replaceLockTurns[cell]--;
                }
            }
        }

        private void ReplaceRandomTiles(Vector2Int landedCell, BoardTurnResult result)
        {
            var candidates = new List<Vector2Int>();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var cell = new Vector2Int(x, y);

                    if (cell == landedCell)
                    {
                        continue;
                    }

                    if (replaceLockTurns.TryGetValue(cell, out int lockTurns) && lockTurns > 0)
                    {
                        continue;
                    }

                    TileType currentType = model.GetTileType(cell);

                    if (currentType == TileType.Marine)
                    {
                        continue;
                    }

                    if (currentType == TileType.Farmland && model.TryGetTile(cell, out TileData farmlandTile) && farmlandTile.Progress > 0)
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            Shuffle(candidates);

            int desiredReplacements = Mathf.Min(turnRules.replacementsPerTurn, candidates.Count);
            int replacementsDone = 0;

            for (int i = 0; i < candidates.Count && replacementsDone < desiredReplacements; i++)
            {
                Vector2Int cell = candidates[i];
                TileType oldType = model.GetTileType(cell);
                TileType newType = RollReplacementTileType();

                if (newType == TileType.Filler && Random01() <= turnRules.rockChanceFromFiller)
                {
                    newType = TileType.Rock;
                }

                if (newType == oldType)
                {
                    continue;
                }

                model.SetTileType(cell, newType);

                if (WouldLockOutPlayer(landedCell))
                {
                    model.SetTileType(cell, oldType);
                    continue;
                }

                replaceLockTurns[cell] = 0;
                result.ReplacedTiles[cell] = newType;
                replacementsDone++;
            }
        }

        private bool WouldLockOutPlayer(Vector2Int landedCell)
        {
            if (!model.IsWalkable(landedCell))
            {
                return true;
            }

            int reachableWalkableTiles = CountReachableWalkableTiles(landedCell);

            // Need at least one other reachable tile to allow the next move.
            return reachableWalkableTiles <= 1;
        }

        private int CountReachableWalkableTiles(Vector2Int origin)
        {
            var visited = new bool[gridSize, gridSize];
            var frontier = new Queue<Vector2Int>();
            int count = 0;

            frontier.Enqueue(origin);
            visited[origin.x, origin.y] = true;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();

                if (!model.IsWalkable(current))
                {
                    continue;
                }

                count++;

                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    Vector2Int next = current + CardinalDirections[i];

                    if (next.x < 0 || next.y < 0 || next.x >= gridSize || next.y >= gridSize)
                    {
                        continue;
                    }

                    if (visited[next.x, next.y])
                    {
                        continue;
                    }

                    visited[next.x, next.y] = true;
                    frontier.Enqueue(next);
                }
            }

            return count;
        }

        private TileType RollInitialTileType()
        {
            TileType rolled = RollWeightedTileType();

            if (rolled == TileType.Filler && Random01() <= turnRules.rockChanceFromFiller)
            {
                return TileType.Rock;
            }

            return rolled;
        }

        private TileType RollReplacementTileType()
        {
            return RollWeightedTileType();
        }

        private TileType RollFarmlandBloomReplacement()
        {
            // By design: when Farmland completes, replace to a neutral/non-refill, non-obstacle tile.
            return TileType.Filler;
        }

        private TileType RollNonMarineNonRockTileType()
        {
            float filler = Mathf.Max(0f, spawnWeights.fillerWeight);
            float farmland = Mathf.Max(0f, spawnWeights.farmlandWeight);
            float total = filler + farmland;

            if (total <= 0f)
            {
                return TileType.Farmland;
            }

            float roll = Random01() * total;
            return roll < filler ? TileType.Filler : TileType.Farmland;
        }

        private TileType RollWeightedTileType()
        {
            float total = spawnWeights.fillerWeight + spawnWeights.farmlandWeight + spawnWeights.marineWeight;
            float roll = Random01() * total;

            if (roll < spawnWeights.fillerWeight)
            {
                return TileType.Filler;
            }

            roll -= spawnWeights.fillerWeight;

            if (roll < spawnWeights.farmlandWeight)
            {
                return TileType.Farmland;
            }

            return TileType.Marine;
        }

        private float Random01()
        {
            return (float)random.NextDouble();
        }

        private void Shuffle(List<Vector2Int> cells)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
        }

        private int CountConnectedSameType(Vector2Int origin, TileType targetType)
        {
            if (!IsScoreableType(targetType))
            {
                return 0;
            }

            var visited = new bool[gridSize, gridSize];
            var frontier = new Queue<Vector2Int>();
            int clusterSize = 0;

            frontier.Enqueue(origin);
            visited[origin.x, origin.y] = true;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();

                if (model.GetTileType(current) != targetType)
                {
                    continue;
                }

                clusterSize++;

                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    Vector2Int next = current + CardinalDirections[i];

                    if (next.x < 0 || next.y < 0 || next.x >= gridSize || next.y >= gridSize)
                    {
                        continue;
                    }

                    if (visited[next.x, next.y])
                    {
                        continue;
                    }

                    visited[next.x, next.y] = true;
                    frontier.Enqueue(next);
                }
            }

            return clusterSize;
        }

        private static bool IsScoreableType(TileType type)
        {
            return type != TileType.Filler && type != TileType.Rock;
        }

        private static int ComputeAdjacencyBonus(TileType landedType, int clusterSize)
        {
            if (!IsScoreableType(landedType) || clusterSize <= 1)
            {
                return 0;
            }

            int n = clusterSize - 1;
            return n * n;
        }

        private static BoardTurnRules ResolveTurnRules(BoardTurnRules rules)
        {
            BoardTurnRules fallback = BoardTurnRules.Default;

            if (rules.replacementsPerTurn <= 0)
            {
                rules.replacementsPerTurn = fallback.replacementsPerTurn;
            }

            if (rules.farmlandReplaceLockTurns < 0)
            {
                rules.farmlandReplaceLockTurns = fallback.farmlandReplaceLockTurns;
            }

            if (rules.rockChanceFromFiller < 0f)
            {
                rules.rockChanceFromFiller = fallback.rockChanceFromFiller;
            }

            rules.rockChanceFromFiller = Mathf.Clamp01(rules.rockChanceFromFiller);

            return rules;
        }

        private static TileSpawnWeights ResolveSpawnWeights(TileSpawnWeights weights)
        {
            TileSpawnWeights fallback = TileSpawnWeights.EarlyGameDefault;

            if (weights.fillerWeight <= 0f)
            {
                weights.fillerWeight = fallback.fillerWeight;
            }

            if (weights.farmlandWeight <= 0f)
            {
                weights.farmlandWeight = fallback.farmlandWeight;
            }

            if (weights.marineWeight <= 0f)
            {
                weights.marineWeight = fallback.marineWeight;
            }

            return weights;
        }
    }
}
