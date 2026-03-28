using System.Collections.Generic;
using UnityEngine;

namespace projectsplippy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameManager))]
    public class GridTileOutlineNeighborMaskUpdater : MonoBehaviour
    {
        private static readonly int NeighborMaskProperty = Shader.PropertyToID("_NeighborMask");

        [SerializeField] private string groundRootName = "GroundRoot";
        [SerializeField] private string tileNamePrefix = "Tile_";
        [SerializeField, Min(1)] private int updatesPerSecond = 8;

        private readonly Dictionary<Vector2Int, Transform> tileTransforms = new Dictionary<Vector2Int, Transform>();
        private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
        private MaterialPropertyBlock propertyBlock;

        private Transform groundRoot;
        private float timer;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            timer = 0f;
            RefreshMasks();
        }

        private void LateUpdate()
        {
            float interval = 1f / Mathf.Max(1, updatesPerSecond);
            timer -= Time.deltaTime;

            if (timer > 0f)
            {
                return;
            }

            timer = interval;
            RefreshMasks();
        }

        private void RefreshMasks()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            if (groundRoot == null)
            {
                groundRoot = transform.Find(groundRootName);

                if (groundRoot == null)
                {
                    return;
                }
            }

            tileTransforms.Clear();
            occupiedCells.Clear();

            for (int i = 0; i < groundRoot.childCount; i++)
            {
                Transform child = groundRoot.GetChild(i);

                if (child == null || !TryParseCell(child.name, out Vector2Int cell))
                {
                    continue;
                }

                tileTransforms[cell] = child;
                occupiedCells.Add(cell);
            }

            foreach (KeyValuePair<Vector2Int, Transform> kv in tileTransforms)
            {
                Vector2Int cell = kv.Key;
                Transform tile = kv.Value;

                if (tile == null)
                {
                    continue;
                }

                Vector4 mask = new Vector4(
                    occupiedCells.Contains(cell + new Vector2Int(-1, 0)) ? 1f : 0f,
                    occupiedCells.Contains(cell + new Vector2Int(1, 0)) ? 1f : 0f,
                    occupiedCells.Contains(cell + new Vector2Int(0, -1)) ? 1f : 0f,
                    occupiedCells.Contains(cell + new Vector2Int(0, 1)) ? 1f : 0f);

                Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(includeInactive: false);

                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];

                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetVector(NeighborMaskProperty, mask);
                    renderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private bool TryParseCell(string tileName, out Vector2Int cell)
        {
            cell = default;

            if (string.IsNullOrEmpty(tileName) || !tileName.StartsWith(tileNamePrefix))
            {
                return false;
            }

            string[] parts = tileName.Split('_');

            if (parts.Length < 3)
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int y))
            {
                return false;
            }

            cell = new Vector2Int(x, y);
            return true;
        }
    }
}
