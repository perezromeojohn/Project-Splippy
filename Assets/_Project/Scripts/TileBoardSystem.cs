using System;
using System.Collections.Generic;
using UnityEngine;

namespace projectsplippy
{
    [Serializable]
    public struct TileSpawnWeights
    {
        public float farmlandWeight;
        public float ecosystemWeight;
        public float sanitationWeight;
        public float worstSanitationWeight;
        public float marineWeight;

        public static TileSpawnWeights Default => new TileSpawnWeights
        {
            farmlandWeight = 0.72f,
            ecosystemWeight = 0.06f,
            sanitationWeight = 0.08f,
            worstSanitationWeight = 0.03f,
            marineWeight = 0.14f
        };
    }

    public sealed class TileStepResult
    {
        public Vector2Int Cell;
        public TileType EnteredType;
        public int EnteredCropVariantIndex;
        public TileLandingResult LandingResult;
    }

    public sealed class TileBoardSystem
    {
        private readonly int gridSize;
        private readonly TileGridModel model;
        private readonly TileSpawnWeights spawnWeights;
        private readonly System.Random random;

        public int GridSize => gridSize;

        public TileBoardSystem(int gridSize, TileRules tileRules, TileSpawnWeights spawnWeights, int seed = 0)
        {
            this.gridSize = Mathf.Max(1, gridSize);
            this.spawnWeights = ResolveSpawnWeights(spawnWeights);
            model = new TileGridModel(this.gridSize, tileRules);
            random = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public void InitializeBoard(Vector2Int? protectedCell = null)
        {
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var cell = new Vector2Int(x, y);
                    TileType type = RollWeightedTileType();

                    if (protectedCell.HasValue && cell == protectedCell.Value)
                    {
                        type = RollWalkableTileType();
                    }

                    model.SetTileType(cell, type);
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

        public TileStepResult ProcessStep(
            Vector2Int cell,
            bool advanceTurnDecay = true,
            IReadOnlyList<Vector2Int> touchedCellsThisTurn = null)
        {
            TileType enteredType = model.GetTileType(cell);
            int enteredCropVariantIndex = -1;

            if (model.TryGetTile(cell, out TileData enteredTile))
            {
                enteredCropVariantIndex = enteredTile.CropVariantIndex;
            }

            TileLandingResult landingResult = model.ProcessLanding(cell, advanceTurnDecay, touchedCellsThisTurn);

            return new TileStepResult
            {
                Cell = cell,
                EnteredType = enteredType,
                EnteredCropVariantIndex = enteredCropVariantIndex,
                LandingResult = landingResult
            };
        }

        public Dictionary<Vector2Int, TileType> ReplaceTraversedTiles(
            IReadOnlyList<Vector2Int> traversedCells,
            Vector2Int protectedCell,
            ISet<Vector2Int> forceFarmlandCells = null)
        {
            var replaced = new Dictionary<Vector2Int, TileType>();

            if (traversedCells == null || traversedCells.Count == 0)
            {
                return replaced;
            }

            var unique = new HashSet<Vector2Int>();

            for (int i = 0; i < traversedCells.Count; i++)
            {
                Vector2Int cell = traversedCells[i];

                if (!unique.Add(cell))
                {
                    continue;
                }

                bool forceFarmland = forceFarmlandCells != null && forceFarmlandCells.Contains(cell);
                TileType newType = forceFarmland
                    ? TileType.Farmland
                    : (cell == protectedCell ? RollWalkableTileType() : RollWeightedTileType());
                model.SetTileType(cell, newType);
                replaced[cell] = newType;
            }

            return replaced;
        }

        public Dictionary<Vector2Int, TileType> ClearHazardsInCross(Vector2Int center)
        {
            return ResolveMarineCross(center, flipNonHazardsToFarmland: false);
        }

        public Dictionary<Vector2Int, TileType> ResolveMarineCross(Vector2Int center, bool flipNonHazardsToFarmland = true)
        {
            var replaced = new Dictionary<Vector2Int, TileType>();

            for (int x = 0; x < gridSize; x++)
            {
                TryResolveMarineCrossCell(new Vector2Int(x, center.y), replaced, flipNonHazardsToFarmland);
            }

            for (int y = 0; y < gridSize; y++)
            {
                TryResolveMarineCrossCell(new Vector2Int(center.x, y), replaced, flipNonHazardsToFarmland);
            }

            return replaced;
        }

        public Dictionary<Vector2Int, TileType> SpawnSanitationTiles(int amount, Vector2Int protectedCell, float worstSanitationChance = 0f)
        {
            var replaced = new Dictionary<Vector2Int, TileType>();
            int target = Mathf.Max(0, amount);
            float worstChance = Mathf.Clamp01(worstSanitationChance);

            if (target <= 0)
            {
                return replaced;
            }

            var candidates = new List<Vector2Int>();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (cell == protectedCell)
                    {
                        continue;
                    }

                    TileType type = model.GetTileType(cell);

                    if (type == TileType.Rock || type == TileType.Trash || type == TileType.Sanitation || type == TileType.WorstSanitation)
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            int spawnCount = Mathf.Min(target, candidates.Count);

            for (int i = 0; i < spawnCount; i++)
            {
                int randomIndex = random.Next(candidates.Count);
                Vector2Int cell = candidates[randomIndex];
                candidates.RemoveAt(randomIndex);

                TileType spawnType = Random01() <= worstChance
                    ? TileType.WorstSanitation
                    : TileType.Sanitation;

                model.SetTileType(cell, spawnType);
                replaced[cell] = spawnType;
            }

            return replaced;
        }

        private void TryResolveMarineCrossCell(
            Vector2Int cell,
            Dictionary<Vector2Int, TileType> replaced,
            bool flipNonHazardsToFarmland)
        {
            TileType current = model.GetTileType(cell);
            bool isHazard =
                current == TileType.Sanitation ||
                current == TileType.WorstSanitation ||
                current == TileType.Trash;

            if (!isHazard && !flipNonHazardsToFarmland)
            {
                return;
            }

            if (current == TileType.Rock || current == TileType.Filler)
            {
                return;
            }

            model.SetTileType(cell, TileType.Farmland);
            replaced[cell] = TileType.Farmland;
        }

        private TileType RollWalkableTileType()
        {
            int safety = 16;

            while (safety-- > 0)
            {
                TileType candidate = RollWeightedTileType();

                if (IsWalkableType(candidate))
                {
                    return candidate;
                }
            }

            return TileType.Farmland;
        }

        private TileType RollWeightedTileType()
        {
            float total =
                Mathf.Max(0f, spawnWeights.farmlandWeight) +
                Mathf.Max(0f, spawnWeights.ecosystemWeight) +
                Mathf.Max(0f, spawnWeights.sanitationWeight) +
                Mathf.Max(0f, spawnWeights.worstSanitationWeight) +
                Mathf.Max(0f, spawnWeights.marineWeight);

            if (total <= 0f)
            {
                return TileType.Farmland;
            }

            float roll = Random01() * total;

            roll -= Mathf.Max(0f, spawnWeights.farmlandWeight);
            if (roll <= 0f)
            {
                return TileType.Farmland;
            }

            roll -= Mathf.Max(0f, spawnWeights.ecosystemWeight);
            if (roll <= 0f)
            {
                return TileType.Ecosystem;
            }

            roll -= Mathf.Max(0f, spawnWeights.sanitationWeight);
            if (roll <= 0f)
            {
                return TileType.Sanitation;
            }

            roll -= Mathf.Max(0f, spawnWeights.worstSanitationWeight);
            if (roll <= 0f)
            {
                return TileType.WorstSanitation;
            }

            return TileType.Marine;
        }

        private float Random01()
        {
            return (float)random.NextDouble();
        }

        private static TileSpawnWeights ResolveSpawnWeights(TileSpawnWeights weights)
        {
            TileSpawnWeights fallback = TileSpawnWeights.Default;

            float sum =
                Mathf.Max(0f, weights.farmlandWeight) +
                Mathf.Max(0f, weights.ecosystemWeight) +
                Mathf.Max(0f, weights.sanitationWeight) +
                Mathf.Max(0f, weights.worstSanitationWeight) +
                Mathf.Max(0f, weights.marineWeight);

            if (sum <= 0f)
            {
                return fallback;
            }

            return weights;
        }

        private static bool IsWalkableType(TileType type)
        {
            return type != TileType.Rock && type != TileType.Trash;
        }
    }
}
