using UnityEngine;

namespace projectsplippy
{
    public class TILE : MonoBehaviour
    {
        public enum TileInspectorType
        {
            Filler,
            Farmland,
            Ecosystem,
            Sanitation,
            WorstSanitation,
            Marine,
            Rock,
            Trash
        }

        public enum FarmlandCropType
        {
            Wheat,
            Sprout,
            Corn,
            Carrot
        }

        [SerializeField] private TileInspectorType tileType;
        [SerializeField] private FarmlandCropType farmlandCrop = FarmlandCropType.Sprout;

        public TileInspectorType CurrentTileType => tileType;
        public FarmlandCropType CurrentFarmlandCrop => farmlandCrop;

        public void SetTileType(TileType sourceType)
        {
            switch (sourceType)
            {
                case TileType.Filler:
                    tileType = TileInspectorType.Filler;
                    break;
                case TileType.Farmland:
                    tileType = TileInspectorType.Farmland;
                    break;
                case TileType.Ecosystem:
                    tileType = TileInspectorType.Ecosystem;
                    break;
                case TileType.Sanitation:
                    tileType = TileInspectorType.Sanitation;
                    break;
                case TileType.WorstSanitation:
                    tileType = TileInspectorType.WorstSanitation;
                    break;
                case TileType.Marine:
                    tileType = TileInspectorType.Marine;
                    break;
                case TileType.Rock:
                    tileType = TileInspectorType.Rock;
                    break;
                case TileType.Trash:
                default:
                    tileType = TileInspectorType.Trash;
                    break;
            }
        }

        public void SetFarmlandCrop(FarmlandCropType crop)
        {
            farmlandCrop = crop;
        }
    }
}
