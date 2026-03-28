using System.Collections.Generic;
using System.Collections;
using PrimeTween;
using TMPro;
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

        [System.Serializable]
        private struct FarmlandCropSpriteEntry
        {
            public TILE.FarmlandCropType crop;
            public Sprite sprite;
        }

        private sealed class TileVisual
        {
            public Transform transform;
            public readonly List<SpriteRenderer> farmlandPrefabSprites = new List<SpriteRenderer>();
            public TILE helper;
            public TextMeshPro sanitationTurnLabel;
            public int farmlandCropVariantIndex = -1;
            public bool isSwapping;
            public Vector3 baseScale;
            public Vector3 baseRotation;
            public Tween scaleTween;
            public Tween rotateTween;
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
        [SerializeField] private Ease materializeRiseEase = Ease.OutCubic;

        [Header("Farmland Sprites")]
        [SerializeField] private Material farmlandBillboardMaterial;
        [SerializeField] private List<FarmlandCropSpriteEntry> farmlandCropSpriteEntries = new List<FarmlandCropSpriteEntry>();
        [SerializeField] private Color farmlandBillboardTint = Color.white;
        [SerializeField] private float billboardInteractorRadius = 1.6f;
        [SerializeField] private bool farmlandBillboardCastShadows = true;
        [SerializeField] private bool farmlandBillboardReceiveShadows = true;

        public int AvailableCropSpriteCount => Mathf.Max(1, farmlandCropSpriteEntries != null ? farmlandCropSpriteEntries.Count : 0);

        public string GetCropVariantLabel(int variantIndex)
        {
            if (farmlandCropSpriteEntries == null || farmlandCropSpriteEntries.Count == 0)
            {
                return "Crop";
            }

            int index = TileGridModel.NormalizeCropVariantIndex(variantIndex, farmlandCropSpriteEntries.Count);
            return farmlandCropSpriteEntries[index].crop.ToString();
        }

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
        [SerializeField] private TMP_FontAsset hoverScorePreviewFont;
        [SerializeField] private float hoverScorePreviewTextSize = 4f;
        [SerializeField] private float hoverScorePreviewYOffset = 0.45f;

        [Header("Sanitation Timer Label")]
        [SerializeField] private TMP_FontAsset sanitationTurnLabelFont;
        [SerializeField] private float sanitationTurnLabelSize = 3.5f;
        [SerializeField] private float sanitationTurnLabelYOffset = 0.42f;

        public float GroundTopYOffset => tileYOffset + groundTopYOffset;

        private readonly Dictionary<TileType, GameObject> prefabByType = new Dictionary<TileType, GameObject>();
        private readonly Dictionary<Vector2Int, TileVisual> tileVisuals = new Dictionary<Vector2Int, TileVisual>();
        private readonly List<GameObject> hoverPreviewMarkers = new List<GameObject>();
        private readonly List<GameObject> hoverPreviewLinks = new List<GameObject>();
        private readonly List<TextMeshPro> hoverPreviewScoreLabels = new List<TextMeshPro>();
        private readonly List<int> frozenHoverScorePreview = new List<int>();
        private readonly List<Color> frozenHoverScoreColors = new List<Color>();
        private MaterialPropertyBlock hoverMarkerPropertyBlock;
        private bool hoverPreviewSnapNextUpdate;
        private bool hoverScorePreviewFrozen;
        private int hoverPreviewConsumeVersion;
        private Vector3 currentInteractorPosition;

        private static readonly int InkColorProperty = Shader.PropertyToID("_InkColor");
        private static readonly int ModeProperty = Shader.PropertyToID("_Mode");
        private static readonly int StrokeWidthProperty = Shader.PropertyToID("_StrokeWidth");
        private static readonly int JitterAmpProperty = Shader.PropertyToID("_JitterAmp");
        private static readonly int JitterFreqProperty = Shader.PropertyToID("_JitterFreq");
        private static readonly int JitterSpeedProperty = Shader.PropertyToID("_JitterSpeed");
        private static readonly int SeedProperty = Shader.PropertyToID("_Seed");
        private static readonly int FillAlphaProperty = Shader.PropertyToID("_FillAlpha");
        private static readonly int InteractorProperty = Shader.PropertyToID("_SplippyInteractor");

        private int gridSize;
        private float cellSize;
        private Vector3 gridCenter;

        private void OnValidate()
        {
            EnsureFarmlandEntryDefaults();
        }

        private void Awake()
        {
            EnsureFarmlandEntryDefaults();
        }

        private void EnsureFarmlandEntryDefaults()
        {
            if (farmlandCropSpriteEntries == null)
            {
                farmlandCropSpriteEntries = new List<FarmlandCropSpriteEntry>();
            }

            EnsureFarmlandEntryExists(TILE.FarmlandCropType.Wheat);
            EnsureFarmlandEntryExists(TILE.FarmlandCropType.Sprout);
            EnsureFarmlandEntryExists(TILE.FarmlandCropType.Corn);
            EnsureFarmlandEntryExists(TILE.FarmlandCropType.Carrot);
        }

        private void EnsureFarmlandEntryExists(TILE.FarmlandCropType cropType)
        {
            for (int i = 0; i < farmlandCropSpriteEntries.Count; i++)
            {
                if (farmlandCropSpriteEntries[i].crop == cropType)
                {
                    return;
                }
            }

            farmlandCropSpriteEntries.Add(new FarmlandCropSpriteEntry
            {
                crop = cropType,
                sprite = null
            });
        }

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
            EnsureHoverScoreLabelCount(nodeCount);
            var nodePositions = new Vector3[nodeCount];
            int[] scorePreview = GetHoverScorePreview(path);
            Color[] scoreColors = GetHoverScoreColors(path);
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

                TextMeshPro scoreLabel = hoverPreviewScoreLabels[i];

                if (scoreLabel != null)
                {
                    bool showScore = i > 0 && i < scorePreview.Length;

                    if (showScore)
                    {
                        Vector3 labelPosition = markerPosition + new Vector3(0f, hoverScorePreviewYOffset, 0f);

                        if (!scoreLabel.gameObject.activeSelf)
                        {
                            scoreLabel.gameObject.SetActive(true);
                            scoreLabel.transform.position = labelPosition;
                            scoreLabel.transform.localScale = Vector3.one * hoverPreviewSpawnScale;
                        }

                        scoreLabel.transform.position = Vector3.Lerp(scoreLabel.transform.position, labelPosition, posT);
                        scoreLabel.transform.localScale = Vector3.Lerp(scoreLabel.transform.localScale, Vector3.one, scaleT);
                        scoreLabel.text = $"+{scorePreview[i]}";
                        scoreLabel.color = i < scoreColors.Length ? scoreColors[i] : Color.white;
                        scoreLabel.fontSize = hoverScorePreviewTextSize;

                        if (hoverScorePreviewFont != null)
                        {
                            scoreLabel.font = hoverScorePreviewFont;
                        }
                    }
                    else
                    {
                        scoreLabel.gameObject.SetActive(false);
                    }
                }

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

            for (int i = nodeCount; i < hoverPreviewScoreLabels.Count; i++)
            {
                if (hoverPreviewScoreLabels[i] != null)
                {
                    hoverPreviewScoreLabels[i].gameObject.SetActive(false);
                }
            }
        }

        public void ShowHoverPathPreviewImmediate(IReadOnlyList<Vector2Int> path)
        {
            hoverPreviewSnapNextUpdate = true;
            ShowHoverPathPreview(path);
        }

        public void ShowHoverPathPreviewImmediateFrozen(IReadOnlyList<Vector2Int> path)
        {
            FreezeHoverScorePreview(path);
            ShowHoverPathPreviewImmediate(path);
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

                ConsumeFrozenHoverScoreStep();

                ShowHoverPathPreviewImmediate(remainingPath);
            });
        }

        public void ClearHoverPathPreview()
        {
            hoverPreviewConsumeVersion++;
            hoverScorePreviewFrozen = false;
            frozenHoverScorePreview.Clear();
            frozenHoverScoreColors.Clear();

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

            for (int i = 0; i < hoverPreviewScoreLabels.Count; i++)
            {
                if (hoverPreviewScoreLabels[i] != null)
                {
                    hoverPreviewScoreLabels[i].gameObject.SetActive(false);
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
                    UpdateFarmlandPrefabSprites(visual, null);
                    UpdateSanitationTurnLabel(visual, null);
                    continue;
                }

                UpdateFarmlandPrefabSprites(visual, tile);
                UpdateSanitationTurnLabel(visual, tile);
            }
        }

        private void LateUpdate()
        {
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

        public void PlayTileReplacementFlip(
            Vector2Int cell,
            TileType newType,
            bool pulseAfterReplace = false,
            int forcedFarmlandCropVariantIndex = -1,
            int forcedSanitationTurns = -1)
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
            TweenOutChildSpriteRenderers(visual.transform, Mathf.Max(0.05f, tileReplaceFlipDuration * 0.35f));

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

                    if (newType == TileType.Farmland)
                    {
                        ApplyFarmlandVariantToVisual(replaced, forcedFarmlandCropVariantIndex);
                    }
                    else
                    {
                        replaced.farmlandCropVariantIndex = -1;

                        if (replaced.helper != null)
                        {
                            replaced.helper.SetFarmlandCrop(TILE.FarmlandCropType.Sprout);
                        }
                    }

                    if (newType == TileType.Sanitation)
                    {
                        int turns = forcedSanitationTurns > 0 ? forcedSanitationTurns : 2;
                        UpdateSanitationTurnLabelForTurns(replaced, turns);
                    }
                    else
                    {
                        UpdateSanitationTurnLabel(replaced, null);
                    }

                    replaced.isSwapping = true;

                    replaced.transform.localEulerAngles = foldOutStart;
                    replaced.rotateTween = Tween.LocalEulerAngles(replaced.transform, foldOutStart, replaced.baseRotation, halfDuration, tileReplaceFlipOutEase)
                        .OnComplete(() =>
                        {
                            replaced.transform.localEulerAngles = replaced.baseRotation;
                            replaced.isSwapping = false;

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
                helper = EnsureTileHelper(tileObject, type),
                sanitationTurnLabel = CreateSanitationTurnLabel(tileObject.transform),
                baseScale = tileObject.transform.localScale,
                baseRotation = tileObject.transform.localEulerAngles
            };

            ConfigureChildSpriteShadows(tileObject.transform);
            CacheFarmlandPrefabSprites(visual);

            return visual;
        }

        private void ReplaceVisualModel(Vector2Int cell, TileType newType)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual oldVisual))
            {
                return;
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

        private static void TweenOutChildSpriteRenderers(Transform root, float duration)
        {
            if (root == null)
            {
                return;
            }

            float clampedDuration = Mathf.Max(0.03f, duration);
            SpriteRenderer[] childSprites = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            var seen = new HashSet<Transform>();

            for (int i = 0; i < childSprites.Length; i++)
            {
                SpriteRenderer sr = childSprites[i];

                if (sr == null || sr.transform == root)
                {
                    continue;
                }

                if (!seen.Add(sr.transform))
                {
                    continue;
                }

                Tween.Scale(sr.transform, Vector3.zero, clampedDuration, Ease.InBack);
            }
        }

        private void ApplyFarmlandVariantToVisual(TileVisual visual, int variantIndex)
        {
            if (visual == null)
            {
                return;
            }

            int resolvedVariantIndex = TileGridModel.NormalizeCropVariantIndex(variantIndex, Mathf.Max(1, AvailableCropSpriteCount));
            visual.farmlandCropVariantIndex = resolvedVariantIndex;
            Sprite speciesSprite = GetCropSpriteForVariant(resolvedVariantIndex);
            TILE.FarmlandCropType cropType = GetCropTypeForVariant(resolvedVariantIndex);

            for (int i = 0; i < visual.farmlandPrefabSprites.Count; i++)
            {
                SpriteRenderer sr = visual.farmlandPrefabSprites[i];

                if (sr == null)
                {
                    continue;
                }

                sr.sprite = speciesSprite;
                sr.enabled = speciesSprite != null;
                sr.color = farmlandBillboardTint;
                sr.shadowCastingMode = farmlandBillboardCastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                sr.receiveShadows = farmlandBillboardReceiveShadows;

                if (farmlandBillboardMaterial != null)
                {
                    sr.sharedMaterial = farmlandBillboardMaterial;
                }
                else
                {
                    sr.sharedMaterial = ResolveFarmlandBillboardMaterial();
                }
            }

            if (visual.helper != null)
            {
                visual.helper.SetFarmlandCrop(cropType);
            }
        }

        private static TILE EnsureTileHelper(GameObject tileObject, TileType type)
        {
            if (tileObject == null)
            {
                return null;
            }

            TILE helper = tileObject.GetComponent<TILE>();

            if (helper == null)
            {
                helper = tileObject.AddComponent<TILE>();
            }

            helper.SetTileType(type);
            return helper;
        }

        private void CacheFarmlandPrefabSprites(TileVisual visual)
        {
            visual.farmlandPrefabSprites.Clear();

            if (visual == null || visual.transform == null)
            {
                return;
            }

            SpriteRenderer[] sprites = visual.transform.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sr = sprites[i];

                if (sr == null)
                {
                    continue;
                }

                visual.farmlandPrefabSprites.Add(sr);
            }
        }

        private void UpdateFarmlandPrefabSprites(TileVisual visual, TileData tile)
        {
            if (visual == null)
            {
                return;
            }

            bool isFarmland = tile != null && tile.Type == TileType.Farmland;

            if (!isFarmland)
            {
                visual.farmlandCropVariantIndex = -1;

                if (visual.helper != null)
                {
                    visual.helper.SetFarmlandCrop(TILE.FarmlandCropType.Sprout);
                }

                return;
            }

            int variantIndex = tile.CropVariantIndex;

            if (visual.farmlandCropVariantIndex == variantIndex)
            {
                return;
            }

            ApplyFarmlandVariantToVisual(visual, variantIndex);
        }

        private void ConfigureChildSpriteShadows(Transform root)
        {
            if (root == null)
            {
                return;
            }

            SpriteRenderer[] childSprites = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

            for (int i = 0; i < childSprites.Length; i++)
            {
                SpriteRenderer sr = childSprites[i];

                if (sr == null)
                {
                    continue;
                }

                sr.shadowCastingMode = farmlandBillboardCastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                sr.receiveShadows = farmlandBillboardReceiveShadows;
            }
        }

        private TextMeshPro CreateSanitationTurnLabel(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject labelObject = new GameObject("SanitationTurnLabel");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            var label = labelObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = Color.white;
            label.fontSize = sanitationTurnLabelSize;
            label.text = string.Empty;

            if (sanitationTurnLabelFont != null)
            {
                label.font = sanitationTurnLabelFont;
            }

            labelObject.SetActive(false);
            return label;
        }

        private void UpdateSanitationTurnLabel(TileVisual visual, TileData tile)
        {
            if (visual == null || visual.sanitationTurnLabel == null)
            {
                return;
            }

            bool show = tile != null && tile.Type == TileType.Sanitation;

            if (!show)
            {
                visual.sanitationTurnLabel.gameObject.SetActive(false);
                return;
            }

            int turns = Mathf.Max(0, tile.SanitationTimer);
            UpdateSanitationTurnLabelForTurns(visual, turns);
        }

        private void UpdateSanitationTurnLabelForTurns(TileVisual visual, int turns)
        {
            if (visual == null || visual.sanitationTurnLabel == null)
            {
                return;
            }

            int clampedTurns = Mathf.Max(0, turns);
            string unit = clampedTurns == 1 ? "turn" : "turns";
            visual.sanitationTurnLabel.text = $"({clampedTurns} {unit})";
            visual.sanitationTurnLabel.color = Color.white;
            visual.sanitationTurnLabel.fontSize = sanitationTurnLabelSize;

            if (sanitationTurnLabelFont != null)
            {
                visual.sanitationTurnLabel.font = sanitationTurnLabelFont;
            }

            Vector3 pos = visual.transform.position;
            pos.y = GetVisualTopY(visual) + sanitationTurnLabelYOffset;
            visual.sanitationTurnLabel.transform.position = pos;
            visual.sanitationTurnLabel.gameObject.SetActive(true);
        }


        private Sprite GetCropSpriteForVariant(int variantIndex)
        {
            if (farmlandCropSpriteEntries == null || farmlandCropSpriteEntries.Count == 0)
            {
                return null;
            }

            int index = TileGridModel.NormalizeCropVariantIndex(variantIndex, farmlandCropSpriteEntries.Count);
            index = Mathf.Clamp(index, 0, farmlandCropSpriteEntries.Count - 1);
            Sprite selected = farmlandCropSpriteEntries[index].sprite;

            if (selected != null)
            {
                return selected;
            }

            for (int i = 0; i < farmlandCropSpriteEntries.Count; i++)
            {
                if (farmlandCropSpriteEntries[i].sprite != null)
                {
                    return farmlandCropSpriteEntries[i].sprite;
                }
            }

            return null;
        }

        private TILE.FarmlandCropType GetCropTypeForVariant(int variantIndex)
        {
            if (farmlandCropSpriteEntries == null || farmlandCropSpriteEntries.Count == 0)
            {
                return TILE.FarmlandCropType.Sprout;
            }

            int index = TileGridModel.NormalizeCropVariantIndex(variantIndex, farmlandCropSpriteEntries.Count);
            index = Mathf.Clamp(index, 0, farmlandCropSpriteEntries.Count - 1);
            return farmlandCropSpriteEntries[index].crop;
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

        private void EnsureHoverScoreLabelCount(int count)
        {
            int desired = Mathf.Max(0, count);

            while (hoverPreviewScoreLabels.Count < desired)
            {
                hoverPreviewScoreLabels.Add(CreateHoverScoreLabel("HoverScoreLabel"));
            }
        }

        private TextMeshPro CreateHoverScoreLabel(string name)
        {
            GameObject labelObject = new GameObject(name);
            labelObject.transform.SetParent(groundParent, true);
            labelObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            var text = labelObject.AddComponent<TextMeshPro>();
            text.text = "+1";
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.horizontalAlignment = HorizontalAlignmentOptions.Center;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSize = hoverScorePreviewTextSize;
            text.outlineWidth = 0f;

            if (hoverScorePreviewFont != null)
            {
                text.font = hoverScorePreviewFont;
            }

            labelObject.SetActive(false);
            return text;
        }

        private int[] ComputeHoverScorePreview(IReadOnlyList<Vector2Int> path)
        {
            int count = path != null ? path.Count : 0;
            var scoreDeltas = new int[count];

            if (path == null || path.Count <= 1)
            {
                return scoreDeltas;
            }

            int? previousEffectiveCrop = null;
            int streakLength = 0;

            for (int i = 1; i < path.Count; i++)
            {
                Vector2Int cell = path[i];
                if (!tileVisuals.TryGetValue(cell, out TileVisual visual) || visual == null || visual.helper == null)
                {
                    scoreDeltas[i] = 1;
                    previousEffectiveCrop = null;
                    streakLength = 0;
                    continue;
                }

                TILE.TileInspectorType type = visual.helper.CurrentTileType;
                int? effectiveCrop = null;

                if (type == TILE.TileInspectorType.Farmland)
                {
                    effectiveCrop = (int)visual.helper.CurrentFarmlandCrop;
                }
                else if (type == TILE.TileInspectorType.Ecosystem)
                {
                    effectiveCrop = previousEffectiveCrop;
                }

                if (!effectiveCrop.HasValue)
                {
                    scoreDeltas[i] = 1;
                    previousEffectiveCrop = null;
                    streakLength = 0;
                    continue;
                }

                if (previousEffectiveCrop.HasValue && previousEffectiveCrop.Value == effectiveCrop.Value)
                {
                    streakLength = Mathf.Max(1, streakLength + 1);
                    scoreDeltas[i] = streakLength;
                }
                else
                {
                    streakLength = 1;
                    scoreDeltas[i] = 1;
                }

                previousEffectiveCrop = effectiveCrop.Value;
            }

            return scoreDeltas;
        }

        private int[] GetHoverScorePreview(IReadOnlyList<Vector2Int> path)
        {
            int nodeCount = path != null ? path.Count : 0;

            if (nodeCount <= 0)
            {
                return new int[0];
            }

            if (hoverScorePreviewFrozen && frozenHoverScorePreview.Count == nodeCount)
            {
                return frozenHoverScorePreview.ToArray();
            }

            return ComputeHoverScorePreview(path);
        }

        private Color[] GetHoverScoreColors(IReadOnlyList<Vector2Int> path)
        {
            int count = path != null ? path.Count : 0;
            var colors = new Color[count];

            if (count <= 0)
            {
                return colors;
            }

            if (hoverScorePreviewFrozen && frozenHoverScoreColors.Count == count)
            {
                return frozenHoverScoreColors.ToArray();
            }

            for (int i = 0; i < count; i++)
            {
                colors[i] = GetScoreColorForCell(path[i]);
            }

            return colors;
        }

        private Color GetScoreColorForCell(Vector2Int cell)
        {
            if (!tileVisuals.TryGetValue(cell, out TileVisual visual) || visual == null || visual.helper == null)
            {
                return Color.white;
            }

            switch (visual.helper.CurrentTileType)
            {
                case TILE.TileInspectorType.Marine:
                    return new Color(0.35f, 0.72f, 1f, 1f);
                case TILE.TileInspectorType.Ecosystem:
                    return new Color(0.45f, 0.9f, 0.45f, 1f);
                case TILE.TileInspectorType.Sanitation:
                    return new Color(1f, 0.95f, 0.25f, 1f);
                default:
                    return Color.white;
            }
        }

        private void FreezeHoverScorePreview(IReadOnlyList<Vector2Int> path)
        {
            frozenHoverScorePreview.Clear();
            frozenHoverScoreColors.Clear();

            int[] preview = ComputeHoverScorePreview(path);

            for (int i = 0; i < preview.Length; i++)
            {
                frozenHoverScorePreview.Add(preview[i]);
                frozenHoverScoreColors.Add(path != null && i < path.Count ? GetScoreColorForCell(path[i]) : Color.white);
            }

            hoverScorePreviewFrozen = true;
        }

        private void ConsumeFrozenHoverScoreStep()
        {
            if (!hoverScorePreviewFrozen || frozenHoverScorePreview.Count == 0)
            {
                return;
            }

            frozenHoverScorePreview.RemoveAt(0);

            if (frozenHoverScoreColors.Count > 0)
            {
                frozenHoverScoreColors.RemoveAt(0);
            }

            if (frozenHoverScorePreview.Count == 0)
            {
                hoverScorePreviewFrozen = false;
                frozenHoverScoreColors.Clear();
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
