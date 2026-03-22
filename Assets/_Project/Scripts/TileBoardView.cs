using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace projectsplippy
{
    public class TileBoardView : MonoBehaviour
    {
        [System.Serializable]
        private struct TileTypeMaterial
        {
            public TileType type;
            public Material material;
        }

        private sealed class TileVisual
        {
            public Transform transform;
            public Renderer renderer;
            public GameObject rockTop;
            public LineRenderer ring;
            public Vector3 baseScale;
            public Vector3 baseRotation;
            public Tween scaleTween;
            public Tween rotateTween;
        }

        [Header("Ground")]
        [SerializeField] private Transform groundParent;
        [SerializeField] private GameObject groundTilePrefab;
        [SerializeField] private GameObject[] rockTopPrefabs;
        [SerializeField] private float tileHeight = 0.2f;
        [SerializeField] private float tileYOffset = -0.1f;
        [SerializeField] private float rockTopYOffset = 0.06f;
        [SerializeField] private Vector3 rockTopScale = Vector3.one;
        [SerializeField] private TileTypeMaterial[] groundMaterials;

        [Header("Feedback")]
        [SerializeField] private float tileTapScaleMultiplier = 1.08f;
        [SerializeField] private float tileTapDuration = 0.1f;
        [SerializeField] private float tileLandingScaleMultiplier = 0.9f;
        [SerializeField] private float tileLandingDuration = 0.08f;
        [SerializeField] private float tileReplaceFlipDuration = 0.22f;
        [SerializeField] private Ease tileReplaceFlipInEase = Ease.InBack;
        [SerializeField] private Ease tileReplaceFlipOutEase = Ease.OutBack;

        [Header("Progress Ring")]
        [SerializeField] private bool showProgressRings = true;
        [SerializeField] private float ringYOffset = 0.12f;
        [SerializeField] private float ringRadius = 0.34f;
        [SerializeField] private float ringWidth = 0.035f;
        [SerializeField] private int ringSegments = 24;
        [SerializeField] private Color farmlandRingColor = new Color(0.43f, 0.86f, 0.35f, 1f);
        [SerializeField] private Color ecosystemRingColor = new Color(0.18f, 0.62f, 0.28f, 1f);
        [SerializeField] private Color sanitationRingColor = new Color(0.18f, 0.78f, 0.95f, 1f);
        [SerializeField] private Color marineRingColor = new Color(0.2f, 0.45f, 1f, 1f);

        public float GroundTopYOffset => tileYOffset + (tileHeight * 0.5f);

        private readonly Dictionary<TileType, Material> materialByType = new Dictionary<TileType, Material>();
        private readonly Dictionary<Vector2Int, TileVisual> tileVisuals = new Dictionary<Vector2Int, TileVisual>();

        private int gridSize;
        private float cellSize;
        private Vector3 gridCenter;

        public void BuildBoard(int gridSize, float cellSize, Vector3 gridCenter, TileBoardSystem boardSystem)
        {
            this.gridSize = gridSize;
            this.cellSize = cellSize;
            this.gridCenter = gridCenter;

            CacheGroundMaterials();
            EnsureGroundParent();
            ClearExistingGround();
            tileVisuals.Clear();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    TileType type = boardSystem.GetTileType(cell);

                    GameObject tileObject = CreateGroundTile();
                    tileObject.name = $"Tile_{x}_{y}_{type}";
                    tileObject.transform.SetParent(groundParent, true);
                    tileObject.transform.position = CellToWorld(cell) + new Vector3(0f, tileYOffset, 0f);
                    tileObject.transform.localScale = new Vector3(cellSize, tileHeight, cellSize);
                    tileObject.transform.localEulerAngles = Vector3.zero;

                    var visual = new TileVisual
                    {
                        transform = tileObject.transform,
                        renderer = tileObject.GetComponent<Renderer>(),
                        baseScale = tileObject.transform.localScale,
                        baseRotation = tileObject.transform.localEulerAngles,
                        ring = CreateProgressRing(tileObject.transform)
                    };

                    tileVisuals[cell] = visual;
                    ApplyTileMaterial(cell, type);
                    SyncRockTopVisual(cell, type);
                }
            }

            RefreshProgressVisuals(boardSystem);
        }

        public void RefreshProgressVisuals(TileBoardSystem boardSystem)
        {
            if (!showProgressRings)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, TileVisual> kv in tileVisuals)
            {
                Vector2Int cell = kv.Key;
                TileVisual visual = kv.Value;

                if (visual.ring == null)
                {
                    continue;
                }

                if (!boardSystem.TryGetTile(cell, out TileData tile))
                {
                    visual.ring.enabled = false;
                    continue;
                }

                UpdateProgressRing(visual.ring, tile);
            }
        }

        public void PlayTileTapFeedback(Vector2Int cell)
        {
            PlayTileScaleFeedback(cell, tileTapScaleMultiplier, tileTapDuration);
        }

        public void PlayTileLandingFeedback(Vector2Int cell)
        {
            PlayTileScaleFeedback(cell, tileLandingScaleMultiplier, tileLandingDuration);
        }

        public void PlayTileReplacementFlip(Vector2Int cell, TileType newType)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual visual))
            {
                return;
            }

            if (visual.rotateTween.isAlive)
            {
                visual.rotateTween.Stop();
            }

            float halfDuration = Mathf.Max(0.05f, tileReplaceFlipDuration * 0.5f);
            Vector3 foldIn = visual.baseRotation + new Vector3(90f, 0f, 0f);
            Vector3 foldOutStart = visual.baseRotation + new Vector3(-90f, 0f, 0f);

            visual.rotateTween = Tween.LocalEulerAngles(visual.transform, visual.baseRotation, foldIn, halfDuration, tileReplaceFlipInEase)
                .OnComplete(() =>
                {
                    ApplyTileMaterial(cell, newType);
                    SyncRockTopVisual(cell, newType);
                    visual.transform.localEulerAngles = foldOutStart;

                    visual.rotateTween = Tween.LocalEulerAngles(visual.transform, foldOutStart, visual.baseRotation, halfDuration, tileReplaceFlipOutEase)
                        .OnComplete(() =>
                        {
                            visual.transform.localEulerAngles = visual.baseRotation;
                        });
                });
        }

        private void PlayTileScaleFeedback(Vector2Int cell, float scaleMultiplier, float duration)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual visual))
            {
                return;
            }

            if (visual.scaleTween.isAlive)
            {
                visual.scaleTween.Stop();
            }

            visual.transform.localScale = visual.baseScale;

            float halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            Vector3 targetScale = visual.baseScale * scaleMultiplier;

            visual.scaleTween = Tween.Scale(visual.transform, targetScale, halfDuration, cycles: 2, cycleMode: CycleMode.Yoyo)
                .OnComplete(() =>
                {
                    visual.transform.localScale = visual.baseScale;
                });
        }

        private void ApplyTileMaterial(Vector2Int cell, TileType type)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual visual) || visual.renderer == null)
            {
                return;
            }

            if (materialByType.TryGetValue(type, out Material material) && material != null)
            {
                visual.renderer.sharedMaterial = material;
            }
        }

        private void SyncRockTopVisual(Vector2Int cell, TileType type)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual visual))
            {
                return;
            }

            if (type != TileType.Rock)
            {
                if (visual.rockTop != null)
                {
                    Destroy(visual.rockTop);
                    visual.rockTop = null;
                }

                return;
            }

            if (visual.rockTop != null)
            {
                return;
            }

            GameObject prefab = PickRockTopPrefab();

            if (prefab == null)
            {
                return;
            }

            visual.rockTop = Instantiate(prefab, groundParent);

            float topY = (tileHeight * 0.5f) + rockTopYOffset;
            visual.rockTop.transform.position = visual.transform.position + new Vector3(0f, topY, 0f);
            visual.rockTop.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            visual.rockTop.transform.localScale = rockTopScale;
        }

        private LineRenderer CreateProgressRing(Transform parent)
        {
            if (!showProgressRings)
            {
                return null;
            }

            var go = new GameObject("ProgressRing");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, ringYOffset, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = ringWidth;
            line.loop = false;
            line.positionCount = 0;
            line.enabled = false;

            return line;
        }

        private void UpdateProgressRing(LineRenderer ring, TileData tile)
        {
            if (tile.MaxProgress <= 0 || tile.Progress <= 0)
            {
                ring.enabled = false;
                return;
            }

            if (tile.Type == TileType.Filler || tile.Type == TileType.Rock)
            {
                ring.enabled = false;
                return;
            }

            float ratio = Mathf.Clamp01(tile.Progress / (float)tile.MaxProgress);
            int segments = Mathf.Max(6, ringSegments);
            int points = Mathf.Max(2, Mathf.CeilToInt(segments * ratio) + 1);

            ring.enabled = true;
            ring.loop = ratio >= 0.999f;
            ring.positionCount = points;
            Color color = GetRingColor(tile.Type);
            ring.startColor = color;
            ring.endColor = color;

            float startAngle = -90f * Mathf.Deg2Rad;
            float endAngle = startAngle + (Mathf.PI * 2f * ratio);

            for (int i = 0; i < points; i++)
            {
                float t = points <= 1 ? 0f : (float)i / (points - 1);
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                float x = Mathf.Cos(angle) * ringRadius;
                float y = Mathf.Sin(angle) * ringRadius;
                ring.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private Color GetRingColor(TileType type)
        {
            switch (type)
            {
                case TileType.Farmland:
                    return farmlandRingColor;
                case TileType.Ecosystem:
                    return ecosystemRingColor;
                case TileType.Sanitation:
                    return sanitationRingColor;
                case TileType.Marine:
                    return marineRingColor;
                default:
                    return Color.white;
            }
        }

        private void CacheGroundMaterials()
        {
            materialByType.Clear();

            for (int i = 0; i < groundMaterials.Length; i++)
            {
                TileTypeMaterial entry = groundMaterials[i];

                if (entry.material == null)
                {
                    continue;
                }

                materialByType[entry.type] = entry.material;
            }
        }

        private void EnsureGroundParent()
        {
            if (groundParent != null)
            {
                return;
            }

            GameObject root = new GameObject("GroundRoot");
            groundParent = root.transform;
            groundParent.SetParent(transform, false);
        }

        private void ClearExistingGround()
        {
            if (groundParent == null)
            {
                return;
            }

            for (int i = groundParent.childCount - 1; i >= 0; i--)
            {
                Destroy(groundParent.GetChild(i).gameObject);
            }
        }

        private GameObject CreateGroundTile()
        {
            if (groundTilePrefab != null)
            {
                return Instantiate(groundTilePrefab);
            }

            return GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            float halfSpan = (gridSize - 1) * 0.5f;
            float worldX = (cell.x - halfSpan) * cellSize;
            float worldZ = (cell.y - halfSpan) * cellSize;
            return gridCenter + new Vector3(worldX, 0f, worldZ);
        }

        private GameObject PickRockTopPrefab()
        {
            if (rockTopPrefabs == null || rockTopPrefabs.Length == 0)
            {
                return null;
            }

            var validPrefabs = new List<GameObject>();

            for (int i = 0; i < rockTopPrefabs.Length; i++)
            {
                if (rockTopPrefabs[i] != null)
                {
                    validPrefabs.Add(rockTopPrefabs[i]);
                }
            }

            if (validPrefabs.Count == 0)
            {
                return null;
            }

            int index = Random.Range(0, validPrefabs.Count);
            return validPrefabs[index];
        }
    }
}
