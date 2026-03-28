using System.Collections.Generic;
using System.Collections;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

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
            public readonly List<Transform> farmlandBillboards = new List<Transform>();
            public readonly List<SpriteRenderer> farmlandBillboardRenderers = new List<SpriteRenderer>();
            public Vector3[] farmlandOffsets;
            public int[] farmlandCropIndices;
            public int billboardHydrationLevel = -1;
            public bool isSwapping;
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

        [Header("Farmland Billboards")]
        [SerializeField] private bool useFarmlandBillboards = true;
        [SerializeField] private Material farmlandBillboardMaterial;
        [SerializeField] private Sprite farmlandSproutSprite;
        [SerializeField] private Sprite[] farmlandCropSprites;
        [SerializeField, Min(1)] private int farmlandBillboardCount = 3;
        [SerializeField, Range(0f, 0.49f)] private float farmlandBillboardMarginPercent = 0.02f;
        [SerializeField] private float farmlandBillboardBaseYOffset = 0.008f;
        [SerializeField] private Vector2 farmlandBillboardSize = new Vector2(0.35f, 0.5f);
        [SerializeField, Range(0f, 1f)] private float farmlandBillboardSeparationRadiusPercent = 0.2f;
        [SerializeField] private int farmlandBillboardPlacementAttempts = 24;
        [SerializeField, Range(-0.49f, 0.49f)] private float farmlandBillboardDepthBiasPercent = -0.15f;
        [SerializeField] private float farmlandBillboardTilt = 0f;
        [SerializeField] private float farmlandBillboardRandomYaw = 12f;
        [SerializeField] private Color farmlandBillboardTint = Color.white;
        [SerializeField] private float billboardInteractorRadius = 1.6f;
        [SerializeField] private bool farmlandBillboardCastShadows = true;
        [SerializeField] private bool farmlandBillboardReceiveShadows = true;
        [SerializeField] private int farmlandBillboardSortingBase = 200;
        [SerializeField] private float farmlandBillboardSortPerUnit = 60f;
        [SerializeField] private float farmlandBillboardPopDuration = 0.2f;
        [SerializeField] private float farmlandBillboardPopStagger = 0.03f;
        [SerializeField] private Ease farmlandBillboardPopEase = Ease.OutBack;

        [Header("Hover Preview")]
        [SerializeField] private bool showHoverPathPreview = true;
        [SerializeField] private Material hoverPreviewMaterial;
        [SerializeField] private Color hoverPreviewColor = new Color(0.2f, 0.95f, 1f, 0.92f);
        [SerializeField] private Color hoverPreviewDestinationColor = new Color(1f, 0.95f, 0.35f, 0.95f);
        [SerializeField] private float hoverPreviewYOffset = 0.03f;
        [SerializeField] private float hoverPreviewDestinationSize = 0.95f;
        [SerializeField, Range(0.1f, 1f)] private float hoverPreviewPreviousNodeScale = 0.5f;
        [SerializeField] private float hoverPreviewLinkWidth = 0.42f;
        [SerializeField] private float hoverPreviewStrokeWidth = 0.36f;
        [SerializeField] private float hoverPreviewJitterAmount = 0.015f;
        [SerializeField] private float hoverPreviewJitterFrequency = 7f;
        [SerializeField] private float hoverPreviewJitterSpeed = 3f;
        [SerializeField] private float hoverPreviewFillAlpha = 0.14f;
        [SerializeField] private float hoverPreviewFollowSpeed = 18f;
        [SerializeField] private float hoverPreviewScaleSpeed = 20f;
        [SerializeField, Range(0f, 1f)] private float hoverPreviewSpawnScale = 0.15f;
        [SerializeField] private float hoverPreviewConsumeDuration = 0.08f;

        public float GroundTopYOffset => tileYOffset + groundTopYOffset;

        private readonly Dictionary<TileType, GameObject> prefabByType = new Dictionary<TileType, GameObject>();
        private readonly Dictionary<Vector2Int, TileVisual> tileVisuals = new Dictionary<Vector2Int, TileVisual>();
        private readonly List<GameObject> hoverPreviewMarkers = new List<GameObject>();
        private readonly List<GameObject> hoverPreviewLinks = new List<GameObject>();
        private MaterialPropertyBlock hoverMarkerPropertyBlock;
        private MaterialPropertyBlock billboardPropertyBlock;
        private bool hoverPreviewSnapNextUpdate;
        private int hoverPreviewConsumeVersion;
        private Camera cachedCamera;
        private Vector3 currentInteractorPosition;

        private static readonly int InkColorProperty = Shader.PropertyToID("_InkColor");
        private static readonly int ModeProperty = Shader.PropertyToID("_Mode");
        private static readonly int StrokeWidthProperty = Shader.PropertyToID("_StrokeWidth");
        private static readonly int JitterAmpProperty = Shader.PropertyToID("_JitterAmp");
        private static readonly int JitterFreqProperty = Shader.PropertyToID("_JitterFreq");
        private static readonly int JitterSpeedProperty = Shader.PropertyToID("_JitterSpeed");
        private static readonly int SeedProperty = Shader.PropertyToID("_Seed");
        private static readonly int FillAlphaProperty = Shader.PropertyToID("_FillAlpha");
        private static readonly int BaseMapProperty = Shader.PropertyToID("_BaseMap");
        private static readonly int InteractorProperty = Shader.PropertyToID("_SplippyInteractor");

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
            cachedCamera = Camera.main;

            CachePrefabs();
            EnsureGroundParent();
            StopAllVisualTweens();
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

            int nodeCount = path.Count;
            int linkCount = Mathf.Max(0, nodeCount - 1);
            EnsureHoverMarkerCount(nodeCount);
            EnsureHoverLinkMarkerCount(linkCount);
            var nodePositions = new Vector3[nodeCount];
            bool snapThisUpdate = hoverPreviewSnapNextUpdate;
            hoverPreviewSnapNextUpdate = false;
            float posT = snapThisUpdate ? 1f : (1f - Mathf.Exp(-Mathf.Max(0.01f, hoverPreviewFollowSpeed) * Time.deltaTime));
            float scaleT = snapThisUpdate ? 1f : (1f - Mathf.Exp(-Mathf.Max(0.01f, hoverPreviewScaleSpeed) * Time.deltaTime));

            for (int i = 0; i < path.Count; i++)
            {
                GameObject marker = hoverPreviewMarkers[i];
                Vector2Int cell = path[i];
                bool isDestination = i == path.Count - 1;
                float sizeFactor = isDestination ? hoverPreviewDestinationSize : (hoverPreviewDestinationSize * hoverPreviewPreviousNodeScale);
                float markerY = GetCellTopY(cell) + hoverPreviewYOffset;
                Vector3 markerPosition = new Vector3(CellToWorld(cell).x, markerY, CellToWorld(cell).z);
                Vector3 targetScale = Vector3.one * (cellSize * sizeFactor);

                if (!marker.activeSelf)
                {
                    marker.SetActive(true);
                    marker.transform.position = markerPosition;
                    marker.transform.localScale = targetScale * hoverPreviewSpawnScale;
                }

                marker.transform.position = Vector3.Lerp(marker.transform.position, markerPosition, posT);
                marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                marker.transform.localScale = Vector3.Lerp(marker.transform.localScale, targetScale, scaleT);
                nodePositions[i] = marker.transform.position;

                Renderer renderer = marker.GetComponent<Renderer>();

                if (renderer != null)
                {
                    Color tint = isDestination ? hoverPreviewDestinationColor : hoverPreviewColor;
                    MaterialPropertyBlock block = GetHoverMarkerPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(InkColorProperty, tint);
                    block.SetFloat(ModeProperty, 0f);
                    block.SetFloat(StrokeWidthProperty, hoverPreviewStrokeWidth);
                    block.SetFloat(JitterAmpProperty, hoverPreviewJitterAmount);
                    block.SetFloat(JitterFreqProperty, hoverPreviewJitterFrequency);
                    block.SetFloat(JitterSpeedProperty, hoverPreviewJitterSpeed);
                    block.SetFloat(FillAlphaProperty, hoverPreviewFillAlpha);
                    block.SetFloat(SeedProperty, (i + 1) * 17.31f);
                    renderer.SetPropertyBlock(block);
                }
            }

            for (int i = nodeCount; i < hoverPreviewMarkers.Count; i++)
            {
                hoverPreviewMarkers[i].SetActive(false);
            }

            for (int i = 0; i < linkCount; i++)
            {
                GameObject link = hoverPreviewLinks[i];
                Vector3 a = nodePositions[i];
                Vector3 b = nodePositions[i + 1];
                Vector3 delta = b - a;
                float length = new Vector2(delta.x, delta.z).magnitude;

                if (length <= 0.0001f)
                {
                    link.SetActive(false);
                    continue;
                }

                float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                Vector3 linkTargetPos = (a + b) * 0.5f;
                Vector3 linkTargetScale = new Vector3(cellSize * hoverPreviewLinkWidth, length, 1f);

                if (!link.activeSelf)
                {
                    link.SetActive(true);
                    link.transform.position = linkTargetPos;
                    link.transform.localScale = linkTargetScale * hoverPreviewSpawnScale;
                }

                link.transform.position = Vector3.Lerp(link.transform.position, linkTargetPos, posT);
                link.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                link.transform.localScale = Vector3.Lerp(link.transform.localScale, linkTargetScale, scaleT);

                Renderer renderer = link.GetComponent<Renderer>();

                if (renderer != null)
                {
                    MaterialPropertyBlock block = GetHoverMarkerPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(InkColorProperty, hoverPreviewColor);
                    block.SetFloat(ModeProperty, 1f);
                    block.SetFloat(StrokeWidthProperty, hoverPreviewStrokeWidth);
                    block.SetFloat(JitterAmpProperty, hoverPreviewJitterAmount);
                    block.SetFloat(JitterFreqProperty, hoverPreviewJitterFrequency);
                    block.SetFloat(JitterSpeedProperty, hoverPreviewJitterSpeed);
                    block.SetFloat(FillAlphaProperty, hoverPreviewFillAlpha);
                    block.SetFloat(SeedProperty, (i + 1) * 31.73f);
                    renderer.SetPropertyBlock(block);
                }
            }

            for (int i = linkCount; i < hoverPreviewLinks.Count; i++)
            {
                hoverPreviewLinks[i].SetActive(false);
            }
        }

        public void ShowHoverPathPreviewImmediate(IReadOnlyList<Vector2Int> path)
        {
            hoverPreviewSnapNextUpdate = true;
            ShowHoverPathPreview(path);
        }

        public void ConsumeHoverPreviewStep(IReadOnlyList<Vector2Int> remainingPath)
        {
            if (!showHoverPathPreview || remainingPath == null || remainingPath.Count <= 1)
            {
                ClearHoverPathPreview();
                return;
            }

            bool hasActiveNode = hoverPreviewMarkers.Count > 0 && hoverPreviewMarkers[0] != null && hoverPreviewMarkers[0].activeSelf;
            bool hasActiveLink = hoverPreviewLinks.Count > 0 && hoverPreviewLinks[0] != null && hoverPreviewLinks[0].activeSelf;

            if (!hasActiveNode && !hasActiveLink)
            {
                ShowHoverPathPreviewImmediate(remainingPath);
                return;
            }

            int version = ++hoverPreviewConsumeVersion;
            float duration = Mathf.Max(0.02f, hoverPreviewConsumeDuration);

            if (hasActiveNode)
            {
                Tween.Scale(hoverPreviewMarkers[0].transform, Vector3.zero, duration, Ease.InBack);
            }

            if (hasActiveLink)
            {
                Tween.Scale(hoverPreviewLinks[0].transform, Vector3.zero, duration, Ease.InBack);
            }

            Tween.Delay(duration, () =>
            {
                if (version != hoverPreviewConsumeVersion)
                {
                    return;
                }

                ShowHoverPathPreviewImmediate(remainingPath);
            });
        }

        public void ClearHoverPathPreview()
        {
            hoverPreviewConsumeVersion++;

            for (int i = 0; i < hoverPreviewMarkers.Count; i++)
            {
                if (hoverPreviewMarkers[i] != null)
                {
                    hoverPreviewMarkers[i].SetActive(false);
                }
            }

            for (int i = 0; i < hoverPreviewLinks.Count; i++)
            {
                if (hoverPreviewLinks[i] != null)
                {
                    hoverPreviewLinks[i].SetActive(false);
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
                    UpdateFarmlandBillboards(visual, null);
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
                bool useBillboardFarmlandVisuals = useFarmlandBillboards && tile.Type == TileType.Farmland;
                SetHydrationLevelVisual(visual, useBillboardFarmlandVisuals ? 0 : hydrationToShow);
                UpdateFarmlandBillboards(visual, tile);
            }
        }

        private void LateUpdate()
        {
            if (!useFarmlandBillboards)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera == null)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, TileVisual> kv in tileVisuals)
            {
                TileVisual visual = kv.Value;

                for (int i = 0; i < visual.farmlandBillboards.Count; i++)
                {
                    Transform billboard = visual.farmlandBillboards[i];

                    if (billboard == null || !billboard.gameObject.activeSelf)
                    {
                        continue;
                    }

                    Vector3 cameraFacingDir = -cachedCamera.transform.forward;
                    billboard.rotation = Quaternion.LookRotation(cameraFacingDir.normalized, Vector3.up);

                    // Treat tilt as roll so sprites stay upright and do not creep upward visually.
                    if (Mathf.Abs(farmlandBillboardTilt) > 0.001f)
                    {
                        billboard.Rotate(0f, 0f, farmlandBillboardTilt, Space.Self);
                    }

                    if (i < visual.farmlandBillboardRenderers.Count && visual.farmlandBillboardRenderers[i] != null)
                    {
                        visual.farmlandBillboardRenderers[i].sortingOrder = CalculateBillboardSortingOrder(billboard.position, i);
                    }
                }
            }

            Shader.SetGlobalVector(InteractorProperty, new Vector4(
                currentInteractorPosition.x,
                currentInteractorPosition.y,
                currentInteractorPosition.z,
                Mathf.Max(0.01f, billboardInteractorRadius)));
        }

        public void UpdateBillboardInteractor(Vector3 worldPosition)
        {
            currentInteractorPosition = worldPosition;
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

            visual.isSwapping = true;
            PlayFarmlandBillboardShrink(visual, Mathf.Max(0.05f, tileReplaceFlipDuration * 0.35f));

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

                    replaced.isSwapping = true;

                    replaced.transform.localEulerAngles = foldOutStart;
                    replaced.rotateTween = Tween.LocalEulerAngles(replaced.transform, foldOutStart, replaced.baseRotation, halfDuration, tileReplaceFlipOutEase)
                        .OnComplete(() =>
                        {
                            replaced.transform.localEulerAngles = replaced.baseRotation;
                            replaced.isSwapping = false;
                            InitializeReplacementBillboardVisuals(replaced, newType);

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

        private void UpdateFarmlandBillboards(TileVisual visual, TileData tile)
        {
            if (visual == null)
            {
                return;
            }

            if (visual.isSwapping)
            {
                SetFarmlandBillboardsActive(visual, false);
                return;
            }

            bool isFarmland = useFarmlandBillboards && tile != null && tile.Type == TileType.Farmland;

            if (!isFarmland)
            {
                SetFarmlandBillboardsActive(visual, false);
                visual.billboardHydrationLevel = -1;
                return;
            }

            EnsureFarmlandBillboards(visual);
            bool activated = SetFarmlandBillboardsActive(visual, true);

            if (activated && !visual.isSwapping)
            {
                PlayFarmlandBillboardPop(visual);
            }

            int hydration = Mathf.Max(0, tile.Progress);

            if (visual.billboardHydrationLevel == hydration)
            {
                return;
            }

            visual.billboardHydrationLevel = hydration;
            bool showSprout = hydration <= 1;

            for (int i = 0; i < visual.farmlandBillboards.Count; i++)
            {
                Transform billboard = visual.farmlandBillboards[i];

                if (billboard == null)
                {
                    continue;
                }

                SpriteRenderer renderer = billboard.GetComponent<SpriteRenderer>();

                if (renderer == null)
                {
                    continue;
                }

                renderer.sprite = showSprout ? farmlandSproutSprite : GetCropSprite(visual, i);
                renderer.color = farmlandBillboardTint;
            }
        }

        private void EnsureFarmlandBillboards(TileVisual visual)
        {
            if (visual.farmlandBillboards.Count > 0)
            {
                return;
            }

            int count = Mathf.Max(1, farmlandBillboardCount);
            visual.farmlandOffsets = new Vector3[count];
            visual.farmlandCropIndices = new int[count];

            int seed = (visual.transform.position.GetHashCode() * 397) ^ count;
            Random.State oldState = Random.state;
            Random.InitState(seed);

            float margin = Mathf.Clamp01(farmlandBillboardMarginPercent) * cellSize;
            float half = Mathf.Max(0.02f, (cellSize * 0.5f) - margin);
            float separationRadius = Mathf.Clamp01(farmlandBillboardSeparationRadiusPercent) * cellSize;
            float topWorldY = GetVisualTopY(visual);
            float localTopY = visual.transform.InverseTransformPoint(new Vector3(
                visual.transform.position.x,
                topWorldY,
                visual.transform.position.z)).y;
            float billboardLocalY = localTopY + farmlandBillboardBaseYOffset;

            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"FarmlandBillboard_{i}");
                go.layer = visual.transform.gameObject.layer;
                go.transform.SetParent(visual.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = CalculateBillboardSortingOrder(go.transform.position, i);
                sr.color = farmlandBillboardTint;
                sr.shadowCastingMode = farmlandBillboardCastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                sr.receiveShadows = farmlandBillboardReceiveShadows;

                sr.sharedMaterial = ResolveFarmlandBillboardMaterial();

                Vector2 planarOffset = FindPlanarOffset(i, visual.farmlandOffsets, half, separationRadius, farmlandBillboardDepthBiasPercent, visual.transform);
                float offsetX = planarOffset.x;
                float offsetZ = planarOffset.y;
                Vector3 localOffset = new Vector3(offsetX, billboardLocalY, offsetZ);
                visual.farmlandOffsets[i] = localOffset;
                go.transform.localPosition = localOffset;
                go.transform.localScale = new Vector3(farmlandBillboardSize.x, farmlandBillboardSize.y, 1f);
                go.transform.localEulerAngles = Vector3.zero;

                int cropCount = farmlandCropSprites != null ? farmlandCropSprites.Length : 0;
                visual.farmlandCropIndices[i] = cropCount > 0 ? Random.Range(0, cropCount) : -1;
                visual.farmlandBillboards.Add(go.transform);
                visual.farmlandBillboardRenderers.Add(sr);
            }

            Random.state = oldState;
            visual.billboardHydrationLevel = -1;
        }

        private bool SetFarmlandBillboardsActive(TileVisual visual, bool isActive)
        {
            bool anyActivated = false;

            for (int i = 0; i < visual.farmlandBillboards.Count; i++)
            {
                Transform billboard = visual.farmlandBillboards[i];

                if (billboard != null)
                {
                    if (isActive && !billboard.gameObject.activeSelf)
                    {
                        anyActivated = true;
                    }

                    billboard.gameObject.SetActive(isActive);
                }
            }

            return anyActivated;
        }

        private void PlayFarmlandBillboardPop(TileVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            float duration = Mathf.Max(0.05f, farmlandBillboardPopDuration);
            float stagger = Mathf.Max(0f, farmlandBillboardPopStagger);
            Vector3 targetScale = new Vector3(farmlandBillboardSize.x, farmlandBillboardSize.y, 1f);

            for (int i = 0; i < visual.farmlandBillboards.Count; i++)
            {
                Transform billboard = visual.farmlandBillboards[i];

                if (billboard == null || !billboard.gameObject.activeSelf)
                {
                    continue;
                }

                billboard.localScale = Vector3.zero;

                float delay = i * stagger;

                if (delay > 0f)
                {
                    Transform capturedBillboard = billboard;
                    TileVisual capturedVisual = visual;
                    Tween.Delay(delay, () =>
                    {
                        if (capturedBillboard != null && capturedBillboard.gameObject.activeSelf && capturedVisual != null && !capturedVisual.isSwapping)
                        {
                            Tween.Scale(capturedBillboard, targetScale, duration, farmlandBillboardPopEase);
                        }
                    });
                }
                else
                {
                    if (!visual.isSwapping)
                    {
                        Tween.Scale(billboard, targetScale, duration, farmlandBillboardPopEase);
                    }
                }
            }
        }

        private void PlayFarmlandBillboardShrink(TileVisual visual, float duration)
        {
            if (visual == null)
            {
                return;
            }

            float clampedDuration = Mathf.Max(0.03f, duration);

            for (int i = 0; i < visual.farmlandBillboards.Count; i++)
            {
                Transform billboard = visual.farmlandBillboards[i];

                if (billboard == null || !billboard.gameObject.activeSelf)
                {
                    continue;
                }

                Tween.Scale(billboard, Vector3.zero, clampedDuration, Ease.InBack);
            }
        }

        private void InitializeReplacementBillboardVisuals(TileVisual visual, TileType newType)
        {
            if (visual == null)
            {
                return;
            }

            if (!useFarmlandBillboards || newType != TileType.Farmland)
            {
                SetFarmlandBillboardsActive(visual, false);
                return;
            }

            EnsureFarmlandBillboards(visual);
            SetFarmlandBillboardsActive(visual, true);
            visual.billboardHydrationLevel = 0;

            for (int i = 0; i < visual.farmlandBillboardRenderers.Count; i++)
            {
                SpriteRenderer renderer = visual.farmlandBillboardRenderers[i];

                if (renderer == null)
                {
                    continue;
                }

                renderer.sprite = farmlandSproutSprite;
                renderer.color = farmlandBillboardTint;
            }

            if (!visual.isSwapping)
            {
                PlayFarmlandBillboardPop(visual);
            }
        }

        private Vector2 FindPlanarOffset(int index, Vector3[] existingOffsets, float half, float separationRadius, float depthBiasPercent, Transform tileTransform)
        {
            int attempts = Mathf.Max(1, farmlandBillboardPlacementAttempts);
            float depthBias = Mathf.Clamp(depthBiasPercent, -0.25f, 0.25f) * (half * 2f);
            float sizeDrivenSeparation = Mathf.Max(0f, farmlandBillboardSize.x * 0.95f);
            float effectiveSeparation = Mathf.Max(separationRadius, sizeDrivenSeparation);
            Vector2 biasDir = Vector2.up;

            if (cachedCamera != null && tileTransform != null)
            {
                Vector3 localFacing = tileTransform.InverseTransformDirection(-cachedCamera.transform.forward);
                Vector2 projected = new Vector2(localFacing.x, localFacing.z);

                if (projected.sqrMagnitude > 0.0001f)
                {
                    biasDir = projected.normalized;
                }
            }

            if (effectiveSeparation <= 0.0001f || index <= 0 || existingOffsets == null)
            {
                Vector2 direct = new Vector2(Random.Range(-half, half), Random.Range(-half, half));
                direct += biasDir * depthBias;
                direct.x = Mathf.Clamp(direct.x, -half, half);
                direct.y = Mathf.Clamp(direct.y, -half, half);
                return direct;
            }

            Vector2 bestCandidate = Vector2.zero;
            float bestNearestDist = -1f;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector2 candidate = new Vector2(Random.Range(-half, half), Random.Range(-half, half));
                candidate += biasDir * depthBias;
                candidate.x = Mathf.Clamp(candidate.x, -half, half);
                candidate.y = Mathf.Clamp(candidate.y, -half, half);
                float nearest = float.MaxValue;

                for (int prev = 0; prev < index; prev++)
                {
                    Vector2 prevOffset = new Vector2(existingOffsets[prev].x, existingOffsets[prev].z);
                    float d = Vector2.Distance(candidate, prevOffset);

                    if (d < nearest)
                    {
                        nearest = d;
                    }
                }

                if (nearest > bestNearestDist)
                {
                    bestNearestDist = nearest;
                    bestCandidate = candidate;
                }

                if (nearest >= effectiveSeparation)
                {
                    return candidate;
                }
            }

            return bestCandidate;
        }

        private Sprite GetCropSprite(TileVisual visual, int billboardIndex)
        {
            if (farmlandCropSprites == null || farmlandCropSprites.Length == 0)
            {
                return farmlandSproutSprite;
            }

            if (visual.farmlandCropIndices == null || billboardIndex < 0 || billboardIndex >= visual.farmlandCropIndices.Length)
            {
                return farmlandCropSprites[0];
            }

            int cropIndex = Mathf.Clamp(visual.farmlandCropIndices[billboardIndex], 0, farmlandCropSprites.Length - 1);
            Sprite selected = farmlandCropSprites[cropIndex];
            return selected != null ? selected : farmlandSproutSprite;
        }

        private int CalculateBillboardSortingOrder(Vector3 worldPosition, int indexOffset)
        {
            // Lower world Z is visually closer on our board, so it should draw on top.
            int depthOrder = Mathf.RoundToInt(-worldPosition.z * Mathf.Max(1f, farmlandBillboardSortPerUnit));
            return farmlandBillboardSortingBase + depthOrder + indexOffset;
        }

        private Material ResolveFarmlandBillboardMaterial()
        {
            if (farmlandBillboardMaterial != null)
            {
                return farmlandBillboardMaterial;
            }

            Shader shader = Shader.Find("ProjectSplippy/FarmlandBillboardLitSway");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            }

            farmlandBillboardMaterial = new Material(shader);
            return farmlandBillboardMaterial;
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
                hoverPreviewMarkers.Add(CreateHoverPrimitive("HoverPathMarker"));
            }
        }

        private void EnsureHoverLinkMarkerCount(int count)
        {
            int desired = Mathf.Max(0, count);

            while (hoverPreviewLinks.Count < desired)
            {
                hoverPreviewLinks.Add(CreateHoverPrimitive("HoverPathLink"));
            }
        }

        private GameObject CreateHoverPrimitive(string name)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = name;
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
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            marker.SetActive(false);
            return marker;
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
            hoverPreviewMaterial.SetColor(InkColorProperty, hoverPreviewColor);
            hoverPreviewMaterial.SetFloat(StrokeWidthProperty, hoverPreviewStrokeWidth);
            hoverPreviewMaterial.SetFloat(JitterAmpProperty, hoverPreviewJitterAmount);
            hoverPreviewMaterial.SetFloat(JitterFreqProperty, hoverPreviewJitterFrequency);
            hoverPreviewMaterial.SetFloat(JitterSpeedProperty, hoverPreviewJitterSpeed);
            hoverPreviewMaterial.SetFloat(FillAlphaProperty, hoverPreviewFillAlpha);
            return hoverPreviewMaterial;
        }

        private MaterialPropertyBlock GetHoverMarkerPropertyBlock()
        {
            if (hoverMarkerPropertyBlock == null)
            {
                hoverMarkerPropertyBlock = new MaterialPropertyBlock();
            }

            return hoverMarkerPropertyBlock;
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

        private float GetVisualTopY(TileVisual visual)
        {
            if (visual == null || visual.transform == null)
            {
                return 0f;
            }

            Renderer[] renderers = visual.transform.GetComponentsInChildren<Renderer>();

            if (renderers == null || renderers.Length == 0)
            {
                return visual.transform.position.y;
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

            return maxY == float.MinValue ? visual.transform.position.y : maxY;
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
