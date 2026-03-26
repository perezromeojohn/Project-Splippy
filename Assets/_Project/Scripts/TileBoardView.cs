using System.Collections.Generic;
using System.Collections;
using PrimeTween;
using UnityEngine;

namespace projectsplippy
{
    public class TileBoardView : MonoBehaviour
    {
        [System.Serializable]
        private struct TileTypePrefab
        {
            public TileType type;
            public GameObject prefab;
        }

        private sealed class TileVisual
        {
            public Transform transform;
            public LineRenderer ring;
            public readonly Dictionary<int, GameObject> hydrationLevels = new Dictionary<int, GameObject>();
            public int shownHydrationLevel;
            public Vector3 baseScale;
            public Vector3 baseRotation;
            public Tween scaleTween;
            public Tween rotateTween;
            public Tween levelTween;
        }

        [Header("Ground")]
        [SerializeField] private Transform groundParent;
        [SerializeField] private GameObject fallbackGroundTilePrefab;
        [SerializeField] private TileTypePrefab[] tilePrefabs;
        [SerializeField] private float tileYOffset = -0.1f;
        [SerializeField] private float groundTopYOffset = 0.1f;

        [Header("Feedback")]
        [SerializeField] private float tileTapScaleMultiplier = 1.08f;
        [SerializeField] private float tileTapDuration = 0.1f;
        [SerializeField] private float tileLandingScaleMultiplier = 0.9f;
        [SerializeField] private float tileLandingDuration = 0.08f;
        [SerializeField] private float tileReplaceFlipDuration = 0.22f;
        [SerializeField] private Ease tileReplaceFlipInEase = Ease.InBack;
        [SerializeField] private Ease tileReplaceFlipOutEase = Ease.OutBack;
        [SerializeField] private float spreadPulseScaleMultiplier = 1.2f;
        [SerializeField] private float spreadPulseDuration = 0.12f;
        [SerializeField] private float hydrationLevelPulseDuration = 0.1f;
        [SerializeField] private float hydrationLevelPulseScaleMultiplier = 1.2f;
        [SerializeField] private Ease materializeRiseEase = Ease.OutCubic;

        [Header("Progress Ring")]
        [SerializeField] private bool showProgressRings = false;
        [SerializeField] private float ringYOffset = 0.12f;
        [SerializeField] private float ringRadius = 0.34f;
        [SerializeField] private float ringWidth = 0.035f;
        [SerializeField] private int ringSegments = 24;
        [SerializeField] private Color farmlandRingColor = new Color(0.43f, 0.86f, 0.35f, 1f);
        [SerializeField] private Color ecosystemRingColor = new Color(0.18f, 0.62f, 0.28f, 1f);
        [SerializeField] private Color sanitationRingColor = new Color(0.18f, 0.78f, 0.95f, 1f);
        [SerializeField] private Color marineRingColor = new Color(0.2f, 0.45f, 1f, 1f);

        [Header("Hover Preview")]
        [SerializeField] private bool showHoverPathPreview = true;
        [SerializeField] private Material hoverPreviewMaterial;
        [SerializeField] private Color hoverPreviewColor = new Color(0.2f, 0.95f, 1f, 0.72f);
        [SerializeField] private Color hoverPreviewDestinationColor = new Color(1f, 0.95f, 0.35f, 0.85f);
        [SerializeField] private float hoverPreviewYOffset = 0.03f;
        [SerializeField] private float hoverPreviewSize = 0.8f;
        [SerializeField] private float hoverPreviewDestinationSize = 1.02f;

        public float GroundTopYOffset => tileYOffset + groundTopYOffset;

        private readonly Dictionary<TileType, GameObject> prefabByType = new Dictionary<TileType, GameObject>();
        private readonly Dictionary<Vector2Int, TileVisual> tileVisuals = new Dictionary<Vector2Int, TileVisual>();
        private readonly List<GameObject> hoverPreviewMarkers = new List<GameObject>();
        private MaterialPropertyBlock hoverMarkerPropertyBlock;

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        private int gridSize;
        private float cellSize;
        private Vector3 gridCenter;

        public void BuildBoard(int gridSize, float cellSize, Vector3 gridCenter, TileBoardSystem boardSystem)
        {
            BuildBoard(gridSize, cellSize, gridCenter, boardSystem, null, null);
        }

        public void BuildBoard(
            int gridSize,
            float cellSize,
            Vector3 gridCenter,
            TileBoardSystem boardSystem,
            HashSet<Vector2Int> includedCells,
            Dictionary<Vector2Int, GameObject> cellPrefabOverrides)
        {
            this.gridSize = gridSize;
            this.cellSize = Mathf.Max(0.1f, cellSize);
            this.gridCenter = gridCenter;

            CachePrefabs();
            EnsureGroundParent();
            StopAllVisualTweens();
            ClearHoverPathPreview();
            ClearExistingGround();
            tileVisuals.Clear();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (includedCells != null && !includedCells.Contains(cell))
                    {
                        continue;
                    }

                    TileType type = boardSystem.GetTileType(cell);
                    GameObject overridePrefab = null;

                    if (cellPrefabOverrides != null)
                    {
                        cellPrefabOverrides.TryGetValue(cell, out overridePrefab);
                    }

                    tileVisuals[cell] = CreateVisual(cell, type, overridePrefab);
                }
            }

            RefreshProgressVisuals(boardSystem);
        }

        public void ShowHoverPathPreview(IReadOnlyList<Vector2Int> path)
        {
            if (!showHoverPathPreview || path == null || path.Count <= 1)
            {
                ClearHoverPathPreview();
                return;
            }

            EnsureHoverMarkerCount(path.Count - 1);

            for (int i = 1; i < path.Count; i++)
            {
                GameObject marker = hoverPreviewMarkers[i - 1];
                Vector2Int cell = path[i];
                bool isDestination = i == path.Count - 1;
                float sizeFactor = isDestination ? hoverPreviewDestinationSize : hoverPreviewSize;
                float markerY = GetCellTopY(cell) + hoverPreviewYOffset;

                marker.SetActive(true);
                marker.transform.position = new Vector3(CellToWorld(cell).x, markerY, CellToWorld(cell).z);
                marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                marker.transform.localScale = Vector3.one * (cellSize * sizeFactor);

                Renderer renderer = marker.GetComponent<Renderer>();

                if (renderer != null)
                {
                    Color tint = isDestination ? hoverPreviewDestinationColor : hoverPreviewColor;
                    MaterialPropertyBlock block = GetHoverMarkerPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorProperty, tint);
                    renderer.SetPropertyBlock(block);
                }
            }

            for (int i = path.Count - 1; i < hoverPreviewMarkers.Count; i++)
            {
                hoverPreviewMarkers[i].SetActive(false);
            }
        }

        public void ClearHoverPathPreview()
        {
            for (int i = 0; i < hoverPreviewMarkers.Count; i++)
            {
                if (hoverPreviewMarkers[i] != null)
                {
                    hoverPreviewMarkers[i].SetActive(false);
                }
            }
        }

        public IEnumerator PlayMaterializeRise(Vector2 startYOffsetRange, float totalDuration)
        {
            float duration = Mathf.Max(0.1f, totalDuration);
            float maxDelay = duration * 0.2f;
            float minYOffset = Mathf.Min(startYOffsetRange.x, startYOffsetRange.y);
            float maxYOffset = Mathf.Max(startYOffsetRange.x, startYOffsetRange.y);
            var visuals = new List<TileVisual>(tileVisuals.Values);

            // Randomized reveal order while guaranteeing all tiles complete within totalDuration.
            for (int i = 0; i < visuals.Count; i++)
            {
                TileVisual visual = visuals[i];

                if (visual.transform == null)
                {
                    continue;
                }

                Vector3 target = visual.transform.position;
                Vector3 targetScale = visual.baseScale;
                float startYOffset = Random.Range(minYOffset, maxYOffset);
                visual.transform.position = target + new Vector3(0f, startYOffset, 0f);
                visual.transform.localScale = Vector3.zero;

                float delay = Random.Range(0f, maxDelay);
                float riseDuration = Mathf.Max(0.1f, duration - delay);
                StartCoroutine(RiseTile(visual.transform, target, targetScale, delay, riseDuration, materializeRiseEase));
            }

            yield return new WaitForSeconds(duration);
        }

        public void RefreshProgressVisuals(TileBoardSystem boardSystem)
        {
            foreach (KeyValuePair<Vector2Int, TileVisual> kv in tileVisuals)
            {
                Vector2Int cell = kv.Key;
                TileVisual visual = kv.Value;

                if (!boardSystem.TryGetTile(cell, out TileData tile))
                {
                    if (visual.ring != null)
                    {
                        visual.ring.enabled = false;
                    }

                    SetHydrationLevelVisual(visual, 0);
                    continue;
                }

                if (showProgressRings && visual.ring != null)
                {
                    UpdateProgressRing(visual.ring, tile);
                }
                else if (visual.ring != null)
                {
                    visual.ring.enabled = false;
                }

                int hydrationToShow = tile.Progress > 0 ? tile.Progress : 0;
                SetHydrationLevelVisual(visual, hydrationToShow);
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

        public void PlayTileReplacementFlip(Vector2Int cell, TileType newType, bool pulseAfterReplace = false)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual visual))
            {
                tileVisuals[cell] = CreateVisual(cell, TileType.Filler);
                visual = tileVisuals[cell];
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
                    ReplaceVisualModel(cell, newType);

                    if (!tileVisuals.TryGetValue(cell, out TileVisual replaced))
                    {
                        return;
                    }

                    replaced.transform.localEulerAngles = foldOutStart;
                    replaced.rotateTween = Tween.LocalEulerAngles(replaced.transform, foldOutStart, replaced.baseRotation, halfDuration, tileReplaceFlipOutEase)
                        .OnComplete(() =>
                        {
                            replaced.transform.localEulerAngles = replaced.baseRotation;

                            if (pulseAfterReplace)
                            {
                                PlayTileScaleFeedback(cell, spreadPulseScaleMultiplier, spreadPulseDuration);
                            }
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

            visual.scaleTween = Tween.Scale(visual.transform, targetScale, halfDuration, cycles: 2, cycleMode: CycleMode.Yoyo);
        }

        private TileVisual CreateVisual(Vector2Int cell, TileType type, GameObject prefabOverride = null)
        {
            GameObject tileObject = CreateTileObject(type, prefabOverride);
            tileObject.name = $"Tile_{cell.x}_{cell.y}_{type}";
            tileObject.transform.SetParent(groundParent, true);
            tileObject.transform.position = CellToWorld(cell) + new Vector3(0f, tileYOffset, 0f);
            tileObject.transform.localEulerAngles = Vector3.zero;

            Vector3 originalScale = tileObject.transform.localScale;
            tileObject.transform.localScale = originalScale * cellSize;

            var visual = new TileVisual
            {
                transform = tileObject.transform,
                ring = CreateProgressRing(tileObject.transform),
                baseScale = tileObject.transform.localScale,
                baseRotation = tileObject.transform.localEulerAngles,
                shownHydrationLevel = -1
            };

            CacheHydrationLevelObjects(visual);
            SetHydrationLevelVisual(visual, 0);
            return visual;
        }

        private void ReplaceVisualModel(Vector2Int cell, TileType newType)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual oldVisual))
            {
                return;
            }

            if (oldVisual.levelTween.isAlive)
            {
                oldVisual.levelTween.Stop();
            }

            if (oldVisual.scaleTween.isAlive)
            {
                oldVisual.scaleTween.Stop();
            }

            if (oldVisual.rotateTween.isAlive)
            {
                oldVisual.rotateTween.Stop();
            }

            if (oldVisual.transform != null)
            {
                Destroy(oldVisual.transform.gameObject);
            }

            tileVisuals[cell] = CreateVisual(cell, newType);
        }

        private void SetHydrationLevelVisual(TileVisual visual, int hydrationLevel)
        {
            int clampedLevel = Mathf.Max(0, hydrationLevel);

            if (visual.shownHydrationLevel == clampedLevel)
            {
                return;
            }

            foreach (KeyValuePair<int, GameObject> kv in visual.hydrationLevels)
            {
                if (kv.Value != null)
                {
                    kv.Value.SetActive(false);
                }
            }

            if (clampedLevel > 0 && visual.hydrationLevels.TryGetValue(clampedLevel, out GameObject levelObject) && levelObject != null)
            {
                levelObject.SetActive(true);

                if (clampedLevel > visual.shownHydrationLevel)
                {
                    PlayHydrationLevelPulse(visual, levelObject.transform);
                }
            }

            visual.shownHydrationLevel = clampedLevel;
        }

        private void PlayHydrationLevelPulse(TileVisual visual, Transform levelTransform)
        {
            if (levelTransform == null)
            {
                return;
            }

            if (visual.levelTween.isAlive)
            {
                visual.levelTween.Stop();
            }

            Vector3 baseScale = levelTransform.localScale;
            float halfDuration = Mathf.Max(0.03f, hydrationLevelPulseDuration * 0.5f);
            Vector3 pulseScale = baseScale * hydrationLevelPulseScaleMultiplier;

            visual.levelTween = Tween.Scale(levelTransform, pulseScale, halfDuration, cycles: 2, cycleMode: CycleMode.Yoyo);
        }

        private void StopAllVisualTweens()
        {
            foreach (KeyValuePair<Vector2Int, TileVisual> kv in tileVisuals)
            {
                TileVisual visual = kv.Value;

                if (visual.scaleTween.isAlive)
                {
                    visual.scaleTween.Stop();
                }

                if (visual.rotateTween.isAlive)
                {
                    visual.rotateTween.Stop();
                }

                if (visual.levelTween.isAlive)
                {
                    visual.levelTween.Stop();
                }
            }
        }

        private void EnsureHoverMarkerCount(int count)
        {
            int desired = Mathf.Max(0, count);

            while (hoverPreviewMarkers.Count < desired)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "HoverPathMarker";
                marker.transform.SetParent(groundParent, true);

                Collider collider = marker.GetComponent<Collider>();

                if (collider != null)
                {
                    Destroy(collider);
                }

                Renderer renderer = marker.GetComponent<Renderer>();

                if (renderer != null)
                {
                    renderer.sharedMaterial = ResolveHoverPreviewMaterial();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                marker.SetActive(false);
                hoverPreviewMarkers.Add(marker);
            }
        }

        private Material ResolveHoverPreviewMaterial()
        {
            if (hoverPreviewMaterial != null)
            {
                return hoverPreviewMaterial;
            }

            Shader shader = Shader.Find("ProjectSplippy/HoverPathPreview");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            hoverPreviewMaterial = new Material(shader);
            hoverPreviewMaterial.SetColor(BaseColorProperty, hoverPreviewColor);
            return hoverPreviewMaterial;
        }

        private float GetCellTopY(Vector2Int cell)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual visual) || visual.transform == null)
            {
                return CellToWorld(cell).y + GroundTopYOffset;
            }

            Renderer[] renderers = visual.transform.GetComponentsInChildren<Renderer>();

            if (renderers == null || renderers.Length == 0)
            {
                return CellToWorld(cell).y + GroundTopYOffset;
            }

            float maxY = float.MinValue;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];

                if (r == null)
                {
                    continue;
                }

                maxY = Mathf.Max(maxY, r.bounds.max.y);
            }

            if (maxY == float.MinValue)
            {
                return CellToWorld(cell).y + GroundTopYOffset;
            }

            return maxY;
        }

        private MaterialPropertyBlock GetHoverMarkerPropertyBlock()
        {
            if (hoverMarkerPropertyBlock == null)
            {
                hoverMarkerPropertyBlock = new MaterialPropertyBlock();
            }

            return hoverMarkerPropertyBlock;
        }

        private void CacheHydrationLevelObjects(TileVisual visual)
        {
            visual.hydrationLevels.Clear();

            if (visual.transform == null)
            {
                return;
            }

            Transform[] children = visual.transform.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];

                if (child == visual.transform)
                {
                    continue;
                }

                if (!TryParseLevelName(child.name, out int levelIndex))
                {
                    continue;
                }

                visual.hydrationLevels[levelIndex] = child.gameObject;
                child.gameObject.SetActive(false);
            }
        }

        private static bool TryParseLevelName(string name, out int levelIndex)
        {
            levelIndex = 0;

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();

            if (!lower.StartsWith("level"))
            {
                return false;
            }

            string suffix = lower.Substring(5).Trim();

            if (suffix.StartsWith("_"))
            {
                suffix = suffix.Substring(1);
            }

            if (!int.TryParse(suffix, out int parsed) || parsed <= 0)
            {
                return false;
            }

            levelIndex = parsed;
            return true;
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

        private void CachePrefabs()
        {
            prefabByType.Clear();

            for (int i = 0; i < tilePrefabs.Length; i++)
            {
                TileTypePrefab entry = tilePrefabs[i];

                if (entry.prefab == null)
                {
                    continue;
                }

                prefabByType[entry.type] = entry.prefab;
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

        private static IEnumerator RiseTile(Transform tileTransform, Vector3 target, Vector3 targetScale, float delay, float duration, Ease ease)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (tileTransform != null)
            {
                Tween.Position(tileTransform, target, duration, ease);
                Tween.Scale(tileTransform, targetScale, duration, ease);
            }
        }

        private GameObject CreateTileObject(TileType type, GameObject prefabOverride = null)
        {
            if (prefabOverride != null)
            {
                return Instantiate(prefabOverride);
            }

            if (prefabByType.TryGetValue(type, out GameObject prefab) && prefab != null)
            {
                return Instantiate(prefab);
            }

            if (fallbackGroundTilePrefab != null)
            {
                return Instantiate(fallbackGroundTilePrefab);
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
    }
}
