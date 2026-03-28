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

    public class TileData
    {
        public TileType Type;
        public int Progress;
        public int MaxProgress;
        public int TurnsSinceTouched;
        public int SanitationTimer;
        public bool IsPolluted;

        public TileData(TileType type, int maxProgress, TileRules rules)
        {
            Type = type;
            Progress = 0;
            MaxProgress = maxProgress;
            TurnsSinceTouched = 0;
            SanitationTimer = type == TileType.Sanitation ? Mathf.Max(1, rules.sanitationTimeoutTurns) : 0;
            IsPolluted = false;
        }

        public virtual bool IsWalkable()
        {
            return Type != TileType.Rock;
        }

        public virtual void ApplyLanding(TileLandingResult result, TileRules rules)
        {
        }

        public virtual void AdvanceUnlandedTurn(TileLandingResult result, TileRules rules, Vector2Int cell)
        {
        }
    }

    public sealed class FarmlandTileData : TileData
    {
        public FarmlandTileData(int maxProgress, TileRules rules)
            : base(TileType.Farmland, maxProgress, rules)
        {
        }

        public override void ApplyLanding(TileLandingResult result, TileRules rules)
        {
            Progress = Mathf.Min(MaxProgress, Progress + 1);
        }
    }

    public sealed class EcosystemTileData : TileData
    {
        public EcosystemTileData(int maxProgress, TileRules rules)
            : base(TileType.Ecosystem, maxProgress, rules)
        {
        }

        public override void ApplyLanding(TileLandingResult result, TileRules rules)
        {
            Progress = Mathf.Min(MaxProgress, Progress + 1);
        }

        public override void AdvanceUnlandedTurn(TileLandingResult result, TileRules rules, Vector2Int cell)
        {
            TurnsSinceTouched++;

            if (Progress > 0 && TurnsSinceTouched >= Mathf.Max(1, rules.ecosystemDecayTurns))
            {
                Progress = Mathf.Max(0, Progress - 1);
                TurnsSinceTouched = 0;
                result.DecayedCells.Add(cell);
            }
        }
    }

    public sealed class SanitationTileData : TileData
    {
        public SanitationTileData(int maxProgress, TileRules rules)
            : base(TileType.Sanitation, maxProgress, rules)
        {
        }

        public override void ApplyLanding(TileLandingResult result, TileRules rules)
        {
            result.LandedCellWasPolluted = IsPolluted;
            IsPolluted = false;
            Progress = MaxProgress;
            SanitationTimer = Mathf.Max(1, rules.sanitationTimeoutTurns);
        }

        public override void AdvanceUnlandedTurn(TileLandingResult result, TileRules rules, Vector2Int cell)
        {
            if (IsPolluted)
            {
                return;
            }

            SanitationTimer--;

            if (SanitationTimer <= 0)
            {
                IsPolluted = true;
                Progress = 0;
                SanitationTimer = 0;
                result.PollutedCells.Add(cell);
            }
        }
    }

    public sealed class MarineTileData : TileData
    {
        public MarineTileData(int maxProgress, TileRules rules)
            : base(TileType.Marine, maxProgress, rules)
        {
        }

        public override void ApplyLanding(TileLandingResult result, TileRules rules)
        {
            Progress = Mathf.Min(MaxProgress, Progress + 1);
        }
    }

    public sealed class RockTileData : TileData
    {
        public RockTileData(int maxProgress, TileRules rules)
            : base(TileType.Rock, maxProgress, rules)
        {
        }

        public override bool IsWalkable()
        {
            return false;
        }
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

            return tile.IsWalkable();
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
            landed.ApplyLanding(result, rules);

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
                tile.AdvanceUnlandedTurn(result, rules, cell);

                tiles[cell] = tile;
            }
        }

        private TileData CreateTileData(TileType type)
        {
            int maxProgress = GetMaxProgress(type);

            switch (type)
            {
                case TileType.Farmland:
                    return new FarmlandTileData(maxProgress, rules);
                case TileType.Ecosystem:
                    return new EcosystemTileData(maxProgress, rules);
                case TileType.Sanitation:
                    return new SanitationTileData(maxProgress, rules);
                case TileType.Marine:
                    return new MarineTileData(maxProgress, rules);
                case TileType.Rock:
                    return new RockTileData(maxProgress, rules);
                case TileType.Filler:
                default:
                    return new TileData(type, maxProgress, rules);
            }
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
