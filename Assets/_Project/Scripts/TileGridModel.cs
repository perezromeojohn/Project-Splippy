using System.Collections.Generic;
using UnityEngine;

namespace projectsplippy
{
    public enum TileType
    {
        Filler,
        Farmland,
        Ecosystem,
        Sanitation,
        Marine,
        Rock
    }

    [System.Serializable]
    public struct TileRules
    {
        public int farmlandMaxProgress;
        public int ecosystemMaxProgress;
        public int marineMaxProgress;
        public int ecosystemDecayTurns;
        public int sanitationTimeoutTurns;

        public static TileRules Default => new TileRules
        {
            farmlandMaxProgress = 3,
            ecosystemMaxProgress = 4,
            marineMaxProgress = 3,
            ecosystemDecayTurns = 3,
            sanitationTimeoutTurns = 4
        };
    }

    public sealed class TileData
    {
        public TileType Type;
        public int Progress;
        public int MaxProgress;
        public int TurnsSinceTouched;
        public int SanitationTimer;
        public bool IsPolluted;
    }

    public sealed class TileLandingResult
    {
        public Vector2Int LandedCell;
        public bool LandedCellBloomed;
        public bool LandedCellWasPolluted;
        public readonly List<Vector2Int> DecayedCells = new List<Vector2Int>();
        public readonly List<Vector2Int> PollutedCells = new List<Vector2Int>();
    }

    public sealed class TileGridModel
    {
        private readonly TileRules rules;
        private readonly Dictionary<Vector2Int, TileData> tiles = new Dictionary<Vector2Int, TileData>();

        public TileGridModel(int gridSize, TileRules rules)
        {
            this.rules = rules;
        }

        public void SetTileType(Vector2Int cell, TileType type)
        {
            tiles[cell] = CreateTileData(type);
        }

        public bool TryGetTile(Vector2Int cell, out TileData tile)
        {
            return tiles.TryGetValue(cell, out tile);
        }

        public TileType GetTileType(Vector2Int cell)
        {
            return tiles.TryGetValue(cell, out TileData tile) ? tile.Type : TileType.Filler;
        }

        public bool IsWalkable(Vector2Int cell)
        {
            if (!tiles.TryGetValue(cell, out TileData tile))
            {
                return false;
            }

            return tile.Type != TileType.Rock;
        }

        public TileLandingResult ProcessLanding(Vector2Int landedCell)
        {
            var result = new TileLandingResult
            {
                LandedCell = landedCell
            };

            AdvanceDecayAndTimers(landedCell, result);

            if (!tiles.TryGetValue(landedCell, out TileData landed))
            {
                return result;
            }

            landed.TurnsSinceTouched = 0;

            switch (landed.Type)
            {
                case TileType.Farmland:
                case TileType.Ecosystem:
                case TileType.Marine:
                    landed.Progress = Mathf.Min(landed.MaxProgress, landed.Progress + 1);
                    break;
                case TileType.Sanitation:
                    result.LandedCellWasPolluted = landed.IsPolluted;
                    landed.IsPolluted = false;
                    landed.Progress = landed.MaxProgress;
                    landed.SanitationTimer = Mathf.Max(1, rules.sanitationTimeoutTurns);
                    break;
                case TileType.Filler:
                case TileType.Rock:
                    break;
            }

            if (landed.Progress >= landed.MaxProgress)
            {
                result.LandedCellBloomed = true;
                landed.Progress = 0;

                if (landed.Type == TileType.Sanitation)
                {
                    landed.SanitationTimer = Mathf.Max(1, rules.sanitationTimeoutTurns);
                }
            }

            tiles[landedCell] = landed;
            return result;
        }

        public List<Vector2Int> ReduceHydrationAll(int amount)
        {
            int delta = Mathf.Max(0, amount);
            var changedCells = new List<Vector2Int>();

            if (delta <= 0)
            {
                return changedCells;
            }

            var keys = new List<Vector2Int>(tiles.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                Vector2Int cell = keys[i];
                TileData tile = tiles[cell];

                if (tile.Progress <= 0)
                {
                    continue;
                }

                int before = tile.Progress;
                tile.Progress = Mathf.Max(0, tile.Progress - delta);

                if (tile.Progress != before)
                {
                    changedCells.Add(cell);
                }

                tiles[cell] = tile;
            }

            return changedCells;
        }

        private void AdvanceDecayAndTimers(Vector2Int landedCell, TileLandingResult result)
        {
            var keys = new List<Vector2Int>(tiles.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                Vector2Int cell = keys[i];

                if (cell == landedCell)
                {
                    continue;
                }

                TileData tile = tiles[cell];

                switch (tile.Type)
                {
                    case TileType.Ecosystem:
                        tile.TurnsSinceTouched++;

                        if (tile.Progress > 0 && tile.TurnsSinceTouched >= Mathf.Max(1, rules.ecosystemDecayTurns))
                        {
                            tile.Progress = Mathf.Max(0, tile.Progress - 1);
                            tile.TurnsSinceTouched = 0;
                            result.DecayedCells.Add(cell);
                        }

                        break;
                    case TileType.Sanitation:
                        if (!tile.IsPolluted)
                        {
                            tile.SanitationTimer--;

                            if (tile.SanitationTimer <= 0)
                            {
                                tile.IsPolluted = true;
                                tile.Progress = 0;
                                tile.SanitationTimer = 0;
                                result.PollutedCells.Add(cell);
                            }
                        }

                        break;
                }

                tiles[cell] = tile;
            }
        }

        private TileData CreateTileData(TileType type)
        {
            int maxProgress = GetMaxProgress(type);

            return new TileData
            {
                Type = type,
                Progress = 0,
                MaxProgress = maxProgress,
                TurnsSinceTouched = 0,
                SanitationTimer = type == TileType.Sanitation ? Mathf.Max(1, rules.sanitationTimeoutTurns) : 0,
                IsPolluted = false
            };
        }

        private int GetMaxProgress(TileType type)
        {
            switch (type)
            {
                case TileType.Filler:
                    return 0;
                case TileType.Farmland:
                    return Mathf.Max(1, rules.farmlandMaxProgress);
                case TileType.Ecosystem:
                    return Mathf.Max(1, rules.ecosystemMaxProgress);
                case TileType.Marine:
                    return Mathf.Max(1, rules.marineMaxProgress);
                case TileType.Sanitation:
                    return 1;
                case TileType.Rock:
                    return 0;
                default:
                    return 1;
            }
        }
    }
}
