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
        Rock,
        Trash
    }

    [System.Serializable]
    public struct TileRules
    {
        public int sanitationTimeoutTurns;
        public int farmlandCropVariantCount;

        public static TileRules Default => new TileRules
        {
            sanitationTimeoutTurns = 2,
            farmlandCropVariantCount = 3
        };
    }

    public class TileData
    {
        public TileType Type;
        public int TurnsSinceTouched;
        public int SanitationTimer;
        public int CropVariantIndex;

        public TileData(TileType type, TileRules rules)
        {
            Type = type;
            TurnsSinceTouched = 0;
            SanitationTimer = type == TileType.Sanitation ? Mathf.Max(1, rules.sanitationTimeoutTurns) : 0;
            CropVariantIndex = -1;
        }

        public virtual bool IsWalkable()
        {
            return Type != TileType.Rock && Type != TileType.Trash;
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
        public FarmlandTileData(TileRules rules, int cropVariantIndex)
            : base(TileType.Farmland, rules)
        {
            CropVariantIndex = cropVariantIndex;
        }
    }

    public sealed class EcosystemTileData : TileData
    {
        public EcosystemTileData(TileRules rules)
            : base(TileType.Ecosystem, rules)
        {
        }

    }

    public sealed class SanitationTileData : TileData
    {
        public SanitationTileData(TileRules rules)
            : base(TileType.Sanitation, rules)
        {
        }

        public override void ApplyLanding(TileLandingResult result, TileRules rules)
        {
            SanitationTimer = Mathf.Max(1, rules.sanitationTimeoutTurns);
        }

        public override void AdvanceUnlandedTurn(TileLandingResult result, TileRules rules, Vector2Int cell)
        {
            SanitationTimer--;

            if (SanitationTimer <= 0)
            {
                SanitationTimer = 0;
                result.ExpiredToTrashCells.Add(cell);
            }
        }
    }

    public sealed class MarineTileData : TileData
    {
        public MarineTileData(TileRules rules)
            : base(TileType.Marine, rules)
        {
        }
    }

    public sealed class RockTileData : TileData
    {
        public RockTileData(TileRules rules)
            : base(TileType.Rock, rules)
        {
        }

        public override bool IsWalkable()
        {
            return false;
        }
    }

    public sealed class TrashTileData : TileData
    {
        public TrashTileData(TileRules rules)
            : base(TileType.Trash, rules)
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
        public readonly List<Vector2Int> ExpiredToTrashCells = new List<Vector2Int>();
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

            for (int i = 0; i < result.ExpiredToTrashCells.Count; i++)
            {
                Vector2Int expiredCell = result.ExpiredToTrashCells[i];

                if (expiredCell == landedCell)
                {
                    continue;
                }

                SetTileType(expiredCell, TileType.Trash);
            }

            if (!tiles.TryGetValue(landedCell, out TileData landed))
            {
                return result;
            }

            landed.TurnsSinceTouched = 0;
            landed.ApplyLanding(result, rules);

            tiles[landedCell] = landed;
            return result;
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
            switch (type)
            {
                case TileType.Farmland:
                    return new FarmlandTileData(rules, RollCropVariantIndex(rules.farmlandCropVariantCount));
                case TileType.Ecosystem:
                    return new EcosystemTileData(rules);
                case TileType.Sanitation:
                    return new SanitationTileData(rules);
                case TileType.Marine:
                    return new MarineTileData(rules);
                case TileType.Rock:
                    return new RockTileData(rules);
                case TileType.Trash:
                    return new TrashTileData(rules);
                case TileType.Filler:
                default:
                    return new TileData(type, rules);
            }
        }

        private static int RollCropVariantIndex(int variantCount)
        {
            int count = Mathf.Max(1, variantCount);
            return Random.Range(0, count);
        }

        public static int NormalizeCropVariantIndex(int variantIndex, int variantCount)
        {
            int count = Mathf.Max(1, variantCount);

            if (variantIndex < 0)
            {
                return 0;
            }

            return variantIndex % count;
        }
    }
}
