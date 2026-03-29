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

        private static readonly TILE.FarmlandCropType[] SupportedFarmlandCrops =
        {
            TILE.FarmlandCropType.Wheat,
            TILE.FarmlandCropType.Corn,
            TILE.FarmlandCropType.Carrot
        };

        private sealed class TileVisual
        {
            public Vector2Int cell;
            public Transform transform;
            public readonly List<SpriteRenderer> farmlandPrefabSprites = new List<SpriteRenderer>();
            public TILE helper;
            public TextMeshPro sanitationTurnLabel;
            public SpriteRenderer trashBlockedIcon;
            public SpriteRenderer worstSanitationMarker;
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

        [Header("Audio")]
        [SerializeField] private AudioSource tileSwapAudioSource;

        [Header("Farmland Sprites")]
        [SerializeField] private Material farmlandBillboardMaterial;
        [SerializeField] private List<FarmlandCropSpriteEntry> farmlandCropSpriteEntries = new List<FarmlandCropSpriteEntry>();
        [SerializeField] private Color farmlandBillboardTint = Color.white;
        [SerializeField] private float billboardInteractorRadius = 1.6f;
        [SerializeField] private bool farmlandBillboardCastShadows = true;
        [SerializeField] private bool farmlandBillboardReceiveShadows = true;

        public int AvailableCropSpriteCount => SupportedFarmlandCrops.Length;

        public string GetCropVariantLabel(int variantIndex)
        {
            return GetCropTypeForVariant(variantIndex).ToString();
        }

        [Header("Hover Preview")]
        [SerializeField] private bool showHoverPathPreview = true;
        [SerializeField] private Material hoverPreviewMaterial;
        [SerializeField] private Color hoverPreviewColor = new Color(0.2f, 0.95f, 1f, 0.92f);
        [SerializeField] private Color hoverPreviewDestinationColor = new Color(1f, 0.95f, 0.35f, 0.95f);
        [SerializeField] private bool hoverPreviewAnchorToTileSurface = true;
        [SerializeField] private float hoverPreviewYOffset = 0.06f;
        [SerializeField] private int hoverPreviewSortingOrder = 8;
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
        [SerializeField] private float hoverScorePreviewYOffset = 0.32f;
        [SerializeField] private int hoverScorePreviewSortingOrder = 12;
        [SerializeField] private AudioSource hoverPreviewAudioSource;
        [SerializeField] private Color hoverScoreFlatColor = Color.white;
        [SerializeField] private Gradient hoverScoreUpwardGradient = new Gradient();

        [Header("Sanitation Timer Label")]
        [SerializeField] private TMP_FontAsset sanitationTurnLabelFont;
        [SerializeField] private float sanitationTurnLabelSize = 4.4f;
        [SerializeField] private float sanitationTurnLabelYOffset = 0.42f;
        [SerializeField] private Color sanitationTurnLabelColor = new Color32(0x7C, 0x8E, 0x02, 0xFF);
        [SerializeField] private Color worstSanitationTurnLabelColor = new Color32(0xD8, 0x2A, 0x2A, 0xFF);

        [Header("Worst Sanitation Marker")]
        [SerializeField] private Sprite worstSanitationMarkerSprite;
        [SerializeField] private float worstSanitationMarkerSize = 0.5f;
        [SerializeField] private float worstSanitationMarkerYOffset = 0.27f;
        [SerializeField] private Color worstSanitationMarkerColor = new Color32(0xD8, 0x2A, 0x2A, 0xFF);
        [SerializeField] private int worstSanitationMarkerSortingOrder = 11;

        [Header("Trash Block Icon")]
        [SerializeField] private Sprite trashBlockedIconSprite;
        [SerializeField] private float trashBlockedIconSize = 0.55f;
        [SerializeField] private float trashBlockedIconYOffset = 0.2f;
        [SerializeField] private Color trashBlockedIconColor = Color.white;
        [SerializeField] private int trashBlockedIconSortingOrder = 10;

        [Header("Floating Event Text")]
        [SerializeField] private TMP_FontAsset floatingEventFont;
        [SerializeField] private float floatingEventTextSize = 4.8f;
        [SerializeField] private float floatingEventTextYOffset = 1.35f;
        [SerializeField] private float floatingEventTextRiseDistance = 0.35f;
        [SerializeField] private float floatingEventTextDuration = 0.55f;
        [SerializeField] private Ease floatingEventTextEase = Ease.OutCubic;
        [SerializeField] private int floatingEventTextSortingOrder = 14;

        public float GroundTopYOffset => tileYOffset + groundTopYOffset;
        public float TileReplacementFlipDuration => Mathf.Max(0.05f, tileReplaceFlipDuration);

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
        private int lastTileSwapSfxFrame = -1;

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
        private float cellPadding;
        private Vector3 gridCenter;

        private void OnValidate()
        {
            EnsureFarmlandEntryDefaults();
            EnsureScoreGradientDefaults();
        }

        private void Awake()
        {
            EnsureFarmlandEntryDefaults();
            EnsureScoreGradientDefaults();
            TryAutoAssignTileSwapAudioSource();
        }

        private void EnsureFarmlandEntryDefaults()
        {
            if (farmlandCropSpriteEntries == null)
            {
                farmlandCropSpriteEntries = new List<FarmlandCropSpriteEntry>();
            }

            var spritesByCrop = new Dictionary<TILE.FarmlandCropType, Sprite>();

            for (int i = 0; i < farmlandCropSpriteEntries.Count; i++)
            {
                FarmlandCropSpriteEntry entry = farmlandCropSpriteEntries[i];

                if (!IsSupportedFarmlandCrop(entry.crop))
                {
                    continue;
                }

                if (!spritesByCrop.TryGetValue(entry.crop, out Sprite existing) || (existing == null && entry.sprite != null))
                {
                    spritesByCrop[entry.crop] = entry.sprite;
                }
            }

            var normalizedEntries = new List<FarmlandCropSpriteEntry>(SupportedFarmlandCrops.Length);

            for (int i = 0; i < SupportedFarmlandCrops.Length; i++)
            {
                TILE.FarmlandCropType crop = SupportedFarmlandCrops[i];
                spritesByCrop.TryGetValue(crop, out Sprite sprite);
                normalizedEntries.Add(new FarmlandCropSpriteEntry
                {
                    crop = crop,
                    sprite = sprite
                });
            }

            farmlandCropSpriteEntries = normalizedEntries;
        }

        private void EnsureScoreGradientDefaults()
        {
            if (hoverScoreUpwardGradient == null)
            {
                hoverScoreUpwardGradient = new Gradient();
            }

            if (ShouldAssignDefaultScoreGradient(hoverScoreUpwardGradient))
            {
                hoverScoreUpwardGradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color32(0x6A, 0xD6, 0xFF, 0xFF), 0f),
                        new GradientColorKey(new Color32(0xFF, 0xD6, 0x3A, 0xFF), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });
            }
        }

        private static bool IsSupportedFarmlandCrop(TILE.FarmlandCropType cropType)
        {
            for (int i = 0; i < SupportedFarmlandCrops.Length; i++)
            {
                if (SupportedFarmlandCrops[i] == cropType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldAssignDefaultScoreGradient(Gradient gradient)
        {
            if (gradient == null)
            {
                return true;
            }

            GradientColorKey[] colorKeys = gradient.colorKeys;
            GradientAlphaKey[] alphaKeys = gradient.alphaKeys;

            if (colorKeys == null || colorKeys.Length == 0)
            {
                return true;
            }

            if (colorKeys.Length != 2 || alphaKeys == null || alphaKeys.Length != 2)
            {
                return false;
            }

            bool defaultColors =
                Mathf.Approximately(colorKeys[0].time, 0f) &&
                Mathf.Approximately(colorKeys[1].time, 1f) &&
                IsApproximatelyWhite(colorKeys[0].color) &&
                IsApproximatelyWhite(colorKeys[1].color);

            bool defaultAlphas =
                Mathf.Approximately(alphaKeys[0].time, 0f) &&
                Mathf.Approximately(alphaKeys[1].time, 1f) &&
                Mathf.Approximately(alphaKeys[0].alpha, 1f) &&
                Mathf.Approximately(alphaKeys[1].alpha, 1f);

            return defaultColors && defaultAlphas;
        }

        private static bool IsApproximatelyWhite(Color color)
        {
            return
                Mathf.Abs(color.r - 1f) <= 0.0001f &&
                Mathf.Abs(color.g - 1f) <= 0.0001f &&
                Mathf.Abs(color.b - 1f) <= 0.0001f &&
                Mathf.Abs(color.a - 1f) <= 0.0001f;
        }

        public void BuildBoard(int gridSize, float cellSize, Vector3 gridCenter, TileBoardSystem boardSystem, float cellPadding = 0f)
        {
            BuildBoard(gridSize, cellSize, gridCenter, boardSystem, null, null, cellPadding);
        }

        public void BuildBoard(
            int gridSize,
            float cellSize,
            Vector3 gridCenter,
            TileBoardSystem boardSystem,
            HashSet<Vector2Int> includedCells,
            Dictionary<Vector2Int, GameObject> cellPrefabOverrides,
            float cellPadding = 0f)
        {
            this.gridSize = gridSize;
            this.cellSize = Mathf.Max(0.1f, cellSize);
            this.cellPadding = Mathf.Max(0f, cellPadding);
            this.gridCenter = gridCenter;

            CachePrefabs();
            EnsureGroundParent();
            StopAllVisualTweens();
            ClearExistingGround();
            tileVisuals.Clear();

            if (includedCells != null)
            {
                foreach (Vector2Int cell in includedCells)
                {
                    TileType type = boardSystem.GetTileType(cell);
                    GameObject overridePrefab = null;

                    if (cellPrefabOverrides != null)
                    {
                        cellPrefabOverrides.TryGetValue(cell, out overridePrefab);
                    }

                    tileVisuals[cell] = CreateVisual(cell, type, overridePrefab);
                }
            }
            else
            {
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        TileType type = boardSystem.GetTileType(cell);
                        GameObject overridePrefab = null;

                        if (cellPrefabOverrides != null)
                        {
                            cellPrefabOverrides.TryGetValue(cell, out overridePrefab);
                        }

                        tileVisuals[cell] = CreateVisual(cell, type, overridePrefab);
                    }
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
            Color[] scoreColors = GetHoverScoreColors(path, scorePreview);
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
                Vector3 cellWorld = CellToWorld(cell);
                float surfaceY = hoverPreviewAnchorToTileSurface ? GetTileSurfaceY(cell) : GetCellAnchorY(cell);
                float markerY = surfaceY + hoverPreviewYOffset;
                Vector3 markerPosition = new Vector3(cellWorld.x, markerY, cellWorld.z);
                Vector3 targetScale = Vector3.one * (cellSize * sizeFactor);

                if (!marker.activeSelf)
                {
                    marker.SetActive(true);
                    marker.transform.position = markerPosition;
                    marker.transform.localScale = targetScale * hoverPreviewSpawnScale;
                    // play sound
                    hoverPreviewAudioSource?.Play();
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
                        Vector3 labelPosition = new Vector3(cellWorld.x, markerY + hoverScorePreviewYOffset, cellWorld.z);

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
                hoverPreviewAudioSource?.Play();}

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

        public IEnumerator TweenAndRemoveCells(IReadOnlyList<Vector2Int> cells, float sinkDistance, float duration, Ease sinkEase)
        {
            if (cells == null || cells.Count == 0)
            {
                yield break;
            }

            ClearHoverPathPreview();

            float clampedDuration = Mathf.Max(0.01f, duration);
            float clampedDistance = Mathf.Max(0f, sinkDistance);
            var removeQueue = new List<Vector2Int>(cells.Count);

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];

                if (!tileVisuals.TryGetValue(cell, out TileVisual visual) || visual.transform == null)
                {
                    continue;
                }

                if (visual.scaleTween.isAlive)
                {
                    visual.scaleTween.Stop();
                }

                if (visual.rotateTween.isAlive)
                {
                    visual.rotateTween.Stop();
                }

                Vector3 targetPosition = visual.transform.position + Vector3.down * clampedDistance;
                Tween.Position(visual.transform, targetPosition, clampedDuration, sinkEase);
                Tween.Scale(visual.transform, Vector3.zero, clampedDuration, Ease.InBack);
                removeQueue.Add(cell);
            }

            if (removeQueue.Count == 0)
            {
                yield break;
            }

            yield return new WaitForSeconds(clampedDuration);

            for (int i = 0; i < removeQueue.Count; i++)
            {
                Vector2Int cell = removeQueue[i];

                if (!tileVisuals.TryGetValue(cell, out TileVisual visual))
                {
                    continue;
                }

                if (visual.transform != null)
                {
                    Destroy(visual.transform.gameObject);
                }

                tileVisuals.Remove(cell);
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
                    UpdateTrashBlockedIcon(visual, null);
                    UpdateWorstSanitationMarker(visual, null);
                    continue;
                }

                UpdateFarmlandPrefabSprites(visual, tile);
                UpdateSanitationTurnLabel(visual, tile);
                UpdateTrashBlockedIcon(visual, tile);
                UpdateWorstSanitationMarker(visual, tile);
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

        public void PlayFloatingText(Vector2Int cell, string message, Color color)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Vector3 cellWorld = CellToWorld(cell);
            float y = GetCellAnchorY(cell) + floatingEventTextYOffset;
            Vector3 start = new Vector3(cellWorld.x, y, cellWorld.z);
            PlayFloatingTextAtWorld(start, message, color);
        }

        public void PlayFloatingTextAtWorld(Vector3 worldPosition, string message, Color color)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            EnsureGroundParent();

            GameObject labelObject = new GameObject("FloatingEventText");
            labelObject.transform.SetParent(groundParent, true);
            Vector3 end = worldPosition + new Vector3(0f, Mathf.Max(0f, floatingEventTextRiseDistance), 0f);

            labelObject.transform.position = worldPosition;
            labelObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            labelObject.transform.localScale = Vector3.zero;

            var text = labelObject.AddComponent<TextMeshPro>();
            text.text = message;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.horizontalAlignment = HorizontalAlignmentOptions.Center;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSize = floatingEventTextSize;

            if (floatingEventFont != null)
            {
                text.font = floatingEventFont;
            }

            if (text.TryGetComponent<MeshRenderer>(out MeshRenderer floatingRenderer))
            {
                floatingRenderer.shadowCastingMode = ShadowCastingMode.Off;
                floatingRenderer.receiveShadows = false;
                floatingRenderer.sortingOrder = floatingEventTextSortingOrder;
            }

            float duration = Mathf.Max(0.15f, floatingEventTextDuration);
            Tween.Scale(labelObject.transform, Vector3.one, Mathf.Min(0.18f, duration * 0.35f), Ease.OutBack);
            Tween.Position(labelObject.transform, end, duration, floatingEventTextEase);
            Tween.Delay(duration, () =>
            {
                if (labelObject != null)
                {
                    Destroy(labelObject);
                }
            });
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

            PlayTileSwapSfx();

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
                    ReplaceVisualModel(cell, newType, hideChildSpriteRenderers: true);

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

                    if (newType == TileType.Sanitation || newType == TileType.WorstSanitation)
                    {
                        int defaultTurns = newType == TileType.WorstSanitation ? 1 : 2;
                        int turns = forcedSanitationTurns > 0 ? forcedSanitationTurns : defaultTurns;
                        Color labelColor = newType == TileType.WorstSanitation
                            ? worstSanitationTurnLabelColor
                            : sanitationTurnLabelColor;
                        UpdateSanitationTurnLabelForTurns(replaced, turns, labelColor);
                    }
                    else
                    {
                        UpdateSanitationTurnLabel(replaced, null);
                    }

                    UpdateTrashBlockedIconForType(replaced, newType);
                    UpdateWorstSanitationMarkerForType(replaced, newType);
                    SetChildSpriteRendererVisibility(replaced.transform, true);

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
                cell = cell,
                transform = tileObject.transform,
                helper = EnsureTileHelper(tileObject, type),
                sanitationTurnLabel = CreateSanitationTurnLabel(tileObject.transform),
                trashBlockedIcon = CreateTrashBlockedIcon(tileObject.transform),
                worstSanitationMarker = CreateWorstSanitationMarker(tileObject.transform),
                baseScale = tileObject.transform.localScale,
                baseRotation = tileObject.transform.localEulerAngles
            };

            ConfigureChildSpriteShadows(tileObject.transform);
            CacheFarmlandPrefabSprites(visual);

            return visual;
        }

        private void ReplaceVisualModel(Vector2Int cell, TileType newType, bool hideChildSpriteRenderers = false)
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

            if (hideChildSpriteRenderers && tileVisuals.TryGetValue(cell, out TileVisual newVisual))
            {
                SetChildSpriteRendererVisibility(newVisual.transform, false);
            }
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

        private static void SetChildSpriteRendererVisibility(Transform root, bool visible)
        {
            if (root == null)
            {
                return;
            }

            SpriteRenderer[] childSprites = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

            for (int i = 0; i < childSprites.Length; i++)
            {
                SpriteRenderer sr = childSprites[i];

                if (sr == null || sr.transform == root)
                {
                    continue;
                }

                sr.enabled = visible;
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
            label.color = sanitationTurnLabelColor;
            label.fontSize = sanitationTurnLabelSize;
            label.text = string.Empty;

            if (sanitationTurnLabelFont != null)
            {
                label.font = sanitationTurnLabelFont;
            }

            labelObject.SetActive(false);
            return label;
        }

        private SpriteRenderer CreateTrashBlockedIcon(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject iconObject = new GameObject("TrashBlockedIcon");
            iconObject.transform.SetParent(parent, false);
            iconObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = trashBlockedIconSprite;
            icon.color = trashBlockedIconColor;
            icon.shadowCastingMode = ShadowCastingMode.Off;
            icon.receiveShadows = false;
            icon.sortingOrder = trashBlockedIconSortingOrder;

            iconObject.SetActive(false);
            return icon;
        }

        private SpriteRenderer CreateWorstSanitationMarker(Transform parent)
        {
            return null;
        }

        private void UpdateSanitationTurnLabel(TileVisual visual, TileData tile)
        {
            if (visual == null || visual.sanitationTurnLabel == null)
            {
                return;
            }

            bool show =
                tile != null &&
                (tile.Type == TileType.Sanitation || tile.Type == TileType.WorstSanitation);

            if (!show)
            {
                visual.sanitationTurnLabel.gameObject.SetActive(false);
                return;
            }

            bool isWorst = tile.Type == TileType.WorstSanitation;
            int turns = isWorst ? 1 : Mathf.Max(0, tile.SanitationTimer);
            Color labelColor = isWorst ? worstSanitationTurnLabelColor : sanitationTurnLabelColor;
            UpdateSanitationTurnLabelForTurns(visual, turns, labelColor);
        }

        private void UpdateSanitationTurnLabelForTurns(TileVisual visual, int turns, Color labelColor)
        {
            if (visual == null || visual.sanitationTurnLabel == null)
            {
                return;
            }

            int clampedTurns = Mathf.Max(0, turns);
            string unit = clampedTurns == 1 ? "turn" : "turns";
            visual.sanitationTurnLabel.text = $"({clampedTurns} {unit})";
            visual.sanitationTurnLabel.color = labelColor;
            visual.sanitationTurnLabel.fontSize = sanitationTurnLabelSize;

            if (sanitationTurnLabelFont != null)
            {
                visual.sanitationTurnLabel.font = sanitationTurnLabelFont;
            }

            Vector3 cellWorld = CellToWorld(visual.cell);
            Vector3 pos = new Vector3(cellWorld.x, GetCellAnchorY(visual.cell) + sanitationTurnLabelYOffset, cellWorld.z);
            visual.sanitationTurnLabel.transform.position = pos;
            visual.sanitationTurnLabel.gameObject.SetActive(true);
        }

        private void UpdateWorstSanitationMarker(TileVisual visual, TileData tile)
        {
            UpdateWorstSanitationMarkerForType(visual, tile != null ? tile.Type : TileType.Filler);
        }

        private void UpdateWorstSanitationMarkerForType(TileVisual visual, TileType type)
        {
            if (visual == null || visual.worstSanitationMarker == null)
            {
                return;
            }

            Sprite markerSprite = ResolveWorstSanitationMarkerSprite();

            bool show = type == TileType.WorstSanitation && markerSprite != null;

            if (!show)
            {
                visual.worstSanitationMarker.gameObject.SetActive(false);
                return;
            }

            visual.worstSanitationMarker.sprite = markerSprite;
            visual.worstSanitationMarker.color = worstSanitationMarkerColor;
            visual.worstSanitationMarker.sortingOrder = worstSanitationMarkerSortingOrder;
            visual.worstSanitationMarker.enabled = true;

            float size = Mathf.Max(0.01f, worstSanitationMarkerSize);
            visual.worstSanitationMarker.transform.localScale = new Vector3(size, size, 1f);

            Vector3 cellWorld = CellToWorld(visual.cell);
            float y = GetCellAnchorY(visual.cell) + worstSanitationMarkerYOffset;
            visual.worstSanitationMarker.transform.position = new Vector3(cellWorld.x, y, cellWorld.z);
            visual.worstSanitationMarker.gameObject.SetActive(true);
        }

        private void UpdateTrashBlockedIcon(TileVisual visual, TileData tile)
        {
            UpdateTrashBlockedIconForType(visual, tile != null ? tile.Type : TileType.Filler);
        }

        private void UpdateTrashBlockedIconForType(TileVisual visual, TileType type)
        {
            if (visual == null || visual.trashBlockedIcon == null)
            {
                return;
            }

            bool show = type == TileType.Trash && trashBlockedIconSprite != null;

            if (!show)
            {
                visual.trashBlockedIcon.gameObject.SetActive(false);
                return;
            }

            visual.trashBlockedIcon.sprite = trashBlockedIconSprite;
            visual.trashBlockedIcon.color = trashBlockedIconColor;
            visual.trashBlockedIcon.sortingOrder = trashBlockedIconSortingOrder;
            visual.trashBlockedIcon.enabled = true;

            float size = Mathf.Max(0.01f, trashBlockedIconSize);
            visual.trashBlockedIcon.transform.localScale = new Vector3(size, size, 1f);

            Vector3 cellWorld = CellToWorld(visual.cell);
            float y = GetCellAnchorY(visual.cell) + trashBlockedIconYOffset;
            visual.trashBlockedIcon.transform.position = new Vector3(cellWorld.x, y, cellWorld.z);
            visual.trashBlockedIcon.gameObject.SetActive(true);
        }

        private Sprite ResolveWorstSanitationMarkerSprite()
        {
            if (worstSanitationMarkerSprite != null)
            {
                return worstSanitationMarkerSprite;
            }

            // Fall back to the configured trash icon so WorstSanitation remains visually marked.
            return trashBlockedIconSprite;
        }


        private Sprite GetCropSpriteForVariant(int variantIndex)
        {
            TILE.FarmlandCropType cropType = GetCropTypeForVariant(variantIndex);

            if (farmlandCropSpriteEntries == null)
            {
                return null;
            }

            for (int i = 0; i < farmlandCropSpriteEntries.Count; i++)
            {
                FarmlandCropSpriteEntry entry = farmlandCropSpriteEntries[i];

                if (entry.crop == cropType && entry.sprite != null)
                {
                    return entry.sprite;
                }
            }

            return null;
        }

        private TILE.FarmlandCropType GetCropTypeForVariant(int variantIndex)
        {
            int count = SupportedFarmlandCrops.Length;
            int index = TileGridModel.NormalizeCropVariantIndex(variantIndex, count);
            index = Mathf.Clamp(index, 0, count - 1);
            return SupportedFarmlandCrops[index];
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

            if (text.TryGetComponent<MeshRenderer>(out MeshRenderer labelRenderer))
            {
                labelRenderer.shadowCastingMode = ShadowCastingMode.Off;
                labelRenderer.receiveShadows = false;
                labelRenderer.sortingOrder = hoverScorePreviewSortingOrder;
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
                    if (visual.farmlandCropVariantIndex >= 0)
                    {
                        effectiveCrop = TileGridModel.NormalizeCropVariantIndex(visual.farmlandCropVariantIndex, AvailableCropSpriteCount);
                    }
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

        private Color[] GetHoverScoreColors(IReadOnlyList<Vector2Int> path, int[] scorePreview)
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

            return ComputeHoverScoreColors(path, scorePreview);
        }

        private Color[] ComputeHoverScoreColors(IReadOnlyList<Vector2Int> path, int[] scorePreview)
        {
            int count = path != null ? path.Count : 0;
            var colors = new Color[count];

            if (count <= 0)
            {
                return colors;
            }

            int maxScore = 1;

            if (scorePreview != null)
            {
                for (int i = 1; i < scorePreview.Length; i++)
                {
                    maxScore = Mathf.Max(maxScore, scorePreview[i]);
                }
            }

            for (int i = 0; i < count; i++)
            {
                int currentScore = scorePreview != null && i < scorePreview.Length
                    ? Mathf.Max(0, scorePreview[i])
                    : 0;
                int previousScore = scorePreview != null && i > 0 && i - 1 < scorePreview.Length
                    ? Mathf.Max(0, scorePreview[i - 1])
                    : 0;

                colors[i] = ResolveHoverScoreColor(currentScore, previousScore, maxScore);
            }

            return colors;
        }

        private Color ResolveHoverScoreColor(int currentScore, int previousScore, int maxScore)
        {
            if (currentScore <= 0 || currentScore <= previousScore || maxScore <= 1)
            {
                return hoverScoreFlatColor;
            }

            float t = maxScore <= 1
                ? 1f
                : Mathf.InverseLerp(1f, maxScore, currentScore);

            return hoverScoreUpwardGradient != null
                ? hoverScoreUpwardGradient.Evaluate(Mathf.Clamp01(t))
                : hoverScoreFlatColor;
        }

        private void FreezeHoverScorePreview(IReadOnlyList<Vector2Int> path)
        {
            frozenHoverScorePreview.Clear();
            frozenHoverScoreColors.Clear();

            int[] preview = ComputeHoverScorePreview(path);
            Color[] previewColors = ComputeHoverScoreColors(path, preview);

            for (int i = 0; i < preview.Length; i++)
            {
                frozenHoverScorePreview.Add(preview[i]);
                frozenHoverScoreColors.Add(i < previewColors.Length ? previewColors[i] : hoverScoreFlatColor);
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
                renderer.sortingOrder = hoverPreviewSortingOrder;
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

        private float GetCellAnchorY(Vector2Int cell)
        {
            return CellToWorld(cell).y + GroundTopYOffset;
        }

        private float GetTileSurfaceY(Vector2Int cell)
        {
            float fallback = GetCellAnchorY(cell);

            if (!tileVisuals.TryGetValue(cell, out TileVisual visual) || visual == null || visual.transform == null)
            {
                return fallback;
            }

            if (!visual.transform.TryGetComponent<Renderer>(out Renderer rootRenderer) || rootRenderer == null)
            {
                return fallback;
            }

            return Mathf.Max(fallback, rootRenderer.bounds.max.y);
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

            if (type == TileType.WorstSanitation)
            {
                // If a dedicated WorstSanitation prefab is not assigned yet, reuse sanitation visuals.
                if (prefabByType.TryGetValue(TileType.Sanitation, out GameObject sanitationPrefab) && sanitationPrefab != null)
                {
                    return Instantiate(sanitationPrefab);
                }
            }

            if (fallbackGroundTilePrefab != null)
            {
                return Instantiate(fallbackGroundTilePrefab);
            }

            return GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        private void PlayTileSwapSfx()
        {
            TryAutoAssignTileSwapAudioSource();

            if (tileSwapAudioSource == null)
            {
                return;
            }

            // Multiple tiles can flip in the same frame; gate to one playback to avoid harsh stacking.
            if (lastTileSwapSfxFrame == Time.frameCount)
            {
                return;
            }

            lastTileSwapSfxFrame = Time.frameCount;
            PlayAudioSourceClipNoOverlap(tileSwapAudioSource);
        }

        private void TryAutoAssignTileSwapAudioSource()
        {
            if (tileSwapAudioSource != null)
            {
                return;
            }

            tileSwapAudioSource = FindSceneAudioSourceByNames(new[] { "Tileswap", "TileSwap" });
        }

        private static void PlayAudioSourceClipNoOverlap(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            if (source.clip != null)
            {
                if (!source.isPlaying)
                {
                    source.Play();
                }

                return;
            }

            source.Play();
        }

        private static AudioSource FindSceneAudioSourceByNames(IReadOnlyList<string> names)
        {
            AudioSource[] audioSources = Resources.FindObjectsOfTypeAll<AudioSource>();
            AudioSource fallback = null;

            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource candidate = audioSources[i];

                if (candidate == null || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!IsNameMatch(candidate.gameObject.name, names))
                {
                    continue;
                }

                if (candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }

                fallback = candidate;
            }

            return fallback;
        }

        private static bool IsNameMatch(string objectName, IReadOnlyList<string> expectedNames)
        {
            string normalizedObjectName = NormalizeObjectName(objectName);

            for (int i = 0; i < expectedNames.Count; i++)
            {
                if (normalizedObjectName == NormalizeObjectName(expectedNames[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            float halfSpan = (gridSize - 1) * 0.5f;
            float stride = cellSize + cellPadding;
            float worldX = (cell.x - halfSpan) * stride;
            float worldZ = (cell.y - halfSpan) * stride;
            return gridCenter + new Vector3(worldX, 0f, worldZ);
        }
    }
}
