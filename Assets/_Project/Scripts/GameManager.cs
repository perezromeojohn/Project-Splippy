using UnityEngine;
using PrimeTween;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

namespace projectsplippy
{
    public class GameManager : MonoBehaviour
    {
        [System.Serializable]
        private struct TileTypeMaterial
        {
            public TileType type;
            public Material material;
        }

        [Header("Grid")]
        [SerializeField] private int gridSize = 7;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string playerMapName = "Player";
        [SerializeField] private string moveTowardsActionName = "MoveTowards";

        [Header("Ground")]
        [SerializeField] private bool buildGroundOnAwake = true;
        [SerializeField] private Transform groundParent;
        [SerializeField] private GameObject groundTilePrefab;
        [SerializeField] private GameObject[] rockTopPrefabs;
        [SerializeField] private float tileHeight = 0.2f;
        [SerializeField] private float tileYOffset = -0.1f;
        [SerializeField] private float rockTopYOffset = 0.06f;
        [SerializeField] private Vector3 rockTopScale = Vector3.one;
        [SerializeField] private TileTypeMaterial[] groundMaterials;

        [Header("Tile Rules")]
        [SerializeField] private TileRules tileRules = default;

        [Header("Board Turn")]
        [SerializeField, Min(0)] private int replacementsPerTurn = 2;
        [SerializeField, Min(0)] private int farmlandLockTurns = 2;
        [SerializeField, Range(0, 100)] private int rockChancePercent = 12;

        [Header("Early Board Mix (%)")]
        [SerializeField, Range(0, 100)] private int fillerPercent = 60;
        [SerializeField, Range(0, 100)] private int farmlandPercent = 32;
        [SerializeField, Range(0, 100)] private int marinePercent = 8;

        [Header("Run")]
        [SerializeField] private int maxWaterReserve = 10;
        [SerializeField] private int scorePerLanding = 1;

        [Header("UI")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text waterText;

        [Header("Player")]
        [SerializeField] private Transform splippy;
        [SerializeField] private float moveDurationPerCell = 0.12f;
        [SerializeField] private float splippyHeightOffset = 0.6f;
        [SerializeField] private float hopHeight = 0.3f;
        [SerializeField] private Ease hopUpEase = Ease.OutQuad;
        [SerializeField] private Ease hopDownEase = Ease.InQuad;
        [SerializeField] private float splippyStretchAmount = 0.12f;
        [SerializeField] private float splippySquashAmount = 0.14f;
        [SerializeField] private float landingSettleDuration = 0.06f;
        [SerializeField] private Ease landingSettleEase = Ease.OutBack;

        [Header("Feedback")]
        [SerializeField] private float tileTapScaleMultiplier = 1.08f;
        [SerializeField] private float tileTapDuration = 0.1f;
        [SerializeField] private float tileLandingScaleMultiplier = 0.9f;
        [SerializeField] private float tileLandingDuration = 0.08f;
        [SerializeField] private float tileReplaceFlipDuration = 0.22f;
        [SerializeField] private Ease tileReplaceFlipInEase = Ease.InBack;
        [SerializeField] private Ease tileReplaceFlipOutEase = Ease.OutBack;

        private Camera mainCamera;
        private InputAction moveTowardsAction;
        private Vector2Int currentCell;
        private bool isMoving;
        private TileBoardSystem tileBoardSystem;
        private readonly Dictionary<TileType, Material> materialByType = new Dictionary<TileType, Material>();
        private readonly Dictionary<Vector2Int, Transform> tileByCell = new Dictionary<Vector2Int, Transform>();
        private readonly Dictionary<Vector2Int, Renderer> tileRendererByCell = new Dictionary<Vector2Int, Renderer>();
        private readonly Dictionary<Vector2Int, GameObject> rockTopByCell = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, Vector3> tileBaseScaleByCell = new Dictionary<Vector2Int, Vector3>();
        private readonly Dictionary<Vector2Int, Vector3> tileBaseRotationByCell = new Dictionary<Vector2Int, Vector3>();
        private readonly Dictionary<Vector2Int, Tween> tileScaleTweenByCell = new Dictionary<Vector2Int, Tween>();
        private readonly Dictionary<Vector2Int, Tween> tileRotateTweenByCell = new Dictionary<Vector2Int, Tween>();
        private Vector3 splippyBaseScale = Vector3.one;
        private int currentScore;
        private int currentWaterReserve;
        private bool isGameOver;

        private void Awake()
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                enabled = false;
                return;
            }

            if (splippy == null)
            {
                splippy = transform;
            }

            splippyBaseScale = splippy.localScale;

            gridSize = Mathf.Max(1, gridSize);
            cellSize = Mathf.Max(0.1f, cellSize);
            tileHeight = Mathf.Max(0.05f, tileHeight);
            tileRules = ResolveTileRules(tileRules);
            currentCell = new Vector2Int(gridSize / 2, gridSize / 2);
            TileSpawnWeights spawnWeights = BuildSpawnWeights();
            BoardTurnRules boardTurnRules = BuildBoardTurnRules();
            tileBoardSystem = new TileBoardSystem(gridSize, tileRules, boardTurnRules, spawnWeights);
            tileBoardSystem.InitializeBoard(currentCell);
            maxWaterReserve = Mathf.Max(1, maxWaterReserve);
            scorePerLanding = Mathf.Max(1, scorePerLanding);
            currentWaterReserve = maxWaterReserve;
            currentScore = 0;
            isGameOver = false;

            CacheGroundMaterials();
            SetupInput();
            RefreshHud();

            splippy.position = CellToWorldForSplippy(currentCell);

            if (buildGroundOnAwake)
            {
                BuildGroundGrid();
            }
        }

        private void OnEnable()
        {
            moveTowardsAction?.Enable();
        }

        private void OnDisable()
        {
            moveTowardsAction?.Disable();
        }

        private void Update()
        {
            if (isMoving)
            {
                return;
            }

            if (isGameOver)
            {
                return;
            }

            if (moveTowardsAction == null || !moveTowardsAction.WasPressedThisFrame())
            {
                return;
            }

            if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
            {
                return;
            }

            if (!TryGetClickedCell(pointerScreenPosition, out Vector2Int clickedCell))
            {
                return;
            }

            PlayTileTapFeedback(clickedCell);

            if (clickedCell == currentCell)
            {
                return;
            }

            MoveToCell(clickedCell);
        }

        private void SetupInput()
        {
            if (inputActions == null)
            {
                return;
            }

            InputActionMap map = inputActions.FindActionMap(playerMapName, throwIfNotFound: false);

            if (map == null)
            {
                return;
            }

            moveTowardsAction = map.FindAction(moveTowardsActionName, throwIfNotFound: false);

            if (moveTowardsAction == null)
            {
                return;
            }

            moveTowardsAction.Enable();
        }

        private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        private bool TryGetClickedCell(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = default;

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            Vector3 gridCenter = GetGridCenterWorld();
            Plane gridPlane = new Plane(Vector3.up, gridCenter);

            if (!gridPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 worldHit = ray.GetPoint(enter);
            Vector3 local = worldHit - gridCenter;
            float halfSpan = (gridSize - 1) * 0.5f;

            int x = Mathf.RoundToInt((local.x / cellSize) + halfSpan);
            int y = Mathf.RoundToInt((local.z / cellSize) + halfSpan);

            if (!IsInBounds(x, y))
            {
                return false;
            }

            cell = new Vector2Int(x, y);
            return true;
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

        public void BuildGroundGrid()
        {
            EnsureGroundParent();
            ClearExistingGround();

            tileByCell.Clear();
            tileRendererByCell.Clear();
            rockTopByCell.Clear();
            tileBaseScaleByCell.Clear();
            tileBaseRotationByCell.Clear();
            tileScaleTweenByCell.Clear();
            tileRotateTweenByCell.Clear();

            tileBoardSystem.InitializeBoard(currentCell);

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    TileType type = tileBoardSystem.GetTileType(cell);
                    Material material = GetMaterialForType(type);

                    GameObject tile = CreateGroundTile();
                    tile.name = $"Tile_{x}_{y}_{type}";
                    tile.transform.SetParent(groundParent, true);

                    Vector3 tilePosition = CellToWorld(cell) + new Vector3(0f, tileYOffset, 0f);
                    tile.transform.position = tilePosition;
                    tile.transform.localScale = new Vector3(cellSize, tileHeight, cellSize);
                    tile.transform.localEulerAngles = Vector3.zero;
                    tileByCell[cell] = tile.transform;
                    tileBaseScaleByCell[cell] = tile.transform.localScale;
                    tileBaseRotationByCell[cell] = tile.transform.localEulerAngles;

                    Renderer renderer = tile.GetComponent<Renderer>();
                    tileRendererByCell[cell] = renderer;

                    if (renderer != null && material != null)
                    {
                        renderer.sharedMaterial = material;
                    }

                    SyncRockTopVisual(cell, type);
                }
            }
        }

        private void EnsureGroundParent()
        {
            if (groundParent != null)
            {
                return;
            }

            var root = new GameObject("GroundRoot");
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

        private Material GetMaterialForType(TileType type)
        {
            materialByType.TryGetValue(type, out Material material);
            return material;
        }

        private void MoveToCell(Vector2Int targetCell)
        {
            if (!GridPathfinder.TryFindPathBfs(gridSize, currentCell, targetCell, IsCellWalkable, out List<Vector2Int> path))
            {
                return;
            }

            if (path.Count <= 1)
            {
                return;
            }

            isMoving = true;
            MoveAlongPath(path, 1, path.Count - 1);
        }

        private void MoveAlongPath(List<Vector2Int> path, int index, int finalIndex)
        {
            if (index >= path.Count)
            {
                isMoving = false;
                return;
            }

            Vector2Int nextCell = path[index];
            Vector3 start = splippy.position;
            Vector3 end = CellToWorldForSplippy(nextCell);
            Vector3 apex = Vector3.Lerp(start, end, 0.5f);
            apex.y = Mathf.Max(start.y, end.y) + hopHeight;

            float halfDuration = Mathf.Max(0.01f, moveDurationPerCell * 0.5f);
            Vector3 stretchScale = new Vector3(
                splippyBaseScale.x * (1f - splippyStretchAmount * 0.5f),
                splippyBaseScale.y * (1f + splippyStretchAmount),
                splippyBaseScale.z * (1f - splippyStretchAmount * 0.5f));
            Vector3 squashScale = new Vector3(
                splippyBaseScale.x * (1f + splippySquashAmount),
                splippyBaseScale.y * (1f - splippySquashAmount),
                splippyBaseScale.z * (1f + splippySquashAmount));

            Tween.Scale(splippy, stretchScale, halfDuration, hopUpEase);
            Tween.Position(splippy, apex, halfDuration, hopUpEase)
                .OnComplete(() =>
                {
                    Tween.Scale(splippy, squashScale, halfDuration, hopDownEase);
                    Tween.Position(splippy, end, halfDuration, hopDownEase)
                        .OnComplete(() =>
                        {
                            currentCell = nextCell;

                            if (index == finalIndex)
                            {
                                ResolveTurnAtDestination(nextCell);
                            }
                            else
                            {
                                PlayTileLandingFeedback(nextCell);
                            }

                            if (isGameOver)
                            {
                                isMoving = false;
                                return;
                            }

                            Tween.Scale(splippy, splippyBaseScale, landingSettleDuration, landingSettleEase);
                            MoveAlongPath(path, index + 1, finalIndex);
                        });
                });
        }

        private void ResolveTurnAtDestination(Vector2Int landedCell)
        {
            TileType landedTypeBeforeTurn = tileBoardSystem != null
                ? tileBoardSystem.GetTileType(landedCell)
                : TileType.Filler;

            ApplyWaterAndScore(landedTypeBeforeTurn);

            if (isGameOver || tileBoardSystem == null)
            {
                return;
            }

            BoardTurnResult turnResult = tileBoardSystem.ResolveEndTurn(landedCell);

            if (turnResult.LandingResult.LandedCellBloomed)
            {
                PlayTileTapFeedback(landedCell);
            }

            PlayTileLandingFeedback(landedCell);

            for (int i = 0; i < turnResult.LandingResult.DecayedCells.Count; i++)
            {
                PlayTileLandingFeedback(turnResult.LandingResult.DecayedCells[i]);
            }

            for (int i = 0; i < turnResult.LandingResult.PollutedCells.Count; i++)
            {
                PlayTileLandingFeedback(turnResult.LandingResult.PollutedCells[i]);
            }

            foreach (KeyValuePair<Vector2Int, TileType> replacement in turnResult.ReplacedTiles)
            {
                PlayTileReplacementFlip(replacement.Key, replacement.Value);
            }
        }

        private void ApplyWaterAndScore(TileType landedType)
        {
            currentWaterReserve = Mathf.Max(0, currentWaterReserve - 1);

            if (landedType == TileType.Marine)
            {
                currentWaterReserve = maxWaterReserve;
            }

            currentScore += scorePerLanding;
            RefreshHud();

            if (currentWaterReserve <= 0)
            {
                TriggerGameOver();
            }
        }

        private TileSpawnWeights BuildSpawnWeights()
        {
            fillerPercent = Mathf.Clamp(fillerPercent, 0, 100);
            farmlandPercent = Mathf.Clamp(farmlandPercent, 0, 100);
            marinePercent = Mathf.Clamp(marinePercent, 0, 100);

            int total = fillerPercent + farmlandPercent + marinePercent;

            if (total <= 0)
            {
                return TileSpawnWeights.EarlyGameDefault;
            }

            float invTotal = 1f / total;

            return new TileSpawnWeights
            {
                fillerWeight = fillerPercent * invTotal,
                farmlandWeight = farmlandPercent * invTotal,
                marineWeight = marinePercent * invTotal
            };
        }

        private BoardTurnRules BuildBoardTurnRules()
        {
            replacementsPerTurn = Mathf.Max(0, replacementsPerTurn);
            farmlandLockTurns = Mathf.Max(0, farmlandLockTurns);
            rockChancePercent = Mathf.Clamp(rockChancePercent, 0, 100);

            return new BoardTurnRules
            {
                replacementsPerTurn = replacementsPerTurn,
                farmlandReplaceLockTurns = farmlandLockTurns,
                rockChanceFromFiller = rockChancePercent / 100f
            };
        }

        private void TriggerGameOver()
        {
            if (isGameOver)
            {
                return;
            }

            isGameOver = true;
            isMoving = false;

            if (waterText != null)
            {
                waterText.text = $"Water: {currentWaterReserve}/{maxWaterReserve} - GAME OVER";
            }
        }

        private void RefreshHud()
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {currentScore}";
            }

            if (waterText != null)
            {
                waterText.text = $"Water: {currentWaterReserve}/{maxWaterReserve}";
            }
        }

        private TileRules ResolveTileRules(TileRules rules)
        {
            TileRules fallback = TileRules.Default;

            if (rules.farmlandMaxProgress <= 0)
            {
                rules.farmlandMaxProgress = fallback.farmlandMaxProgress;
            }

            if (rules.ecosystemMaxProgress <= 0)
            {
                rules.ecosystemMaxProgress = fallback.ecosystemMaxProgress;
            }

            if (rules.marineMaxProgress <= 0)
            {
                rules.marineMaxProgress = fallback.marineMaxProgress;
            }

            if (rules.ecosystemDecayTurns <= 0)
            {
                rules.ecosystemDecayTurns = fallback.ecosystemDecayTurns;
            }

            if (rules.sanitationTimeoutTurns <= 0)
            {
                rules.sanitationTimeoutTurns = fallback.sanitationTimeoutTurns;
            }

            return rules;
        }

        private void PlayTileTapFeedback(Vector2Int cell)
        {
            PlayTileScaleFeedback(cell, tileTapScaleMultiplier, tileTapDuration);
        }

        private void PlayTileLandingFeedback(Vector2Int cell)
        {
            PlayTileScaleFeedback(cell, tileLandingScaleMultiplier, tileLandingDuration);
        }

        private void PlayTileScaleFeedback(Vector2Int cell, float scaleMultiplier, float duration)
        {
            if (!tileByCell.TryGetValue(cell, out Transform tileTransform))
            {
                return;
            }

            if (!tileBaseScaleByCell.TryGetValue(cell, out Vector3 baseScale))
            {
                baseScale = tileTransform.localScale;
                tileBaseScaleByCell[cell] = baseScale;
            }

            if (tileScaleTweenByCell.TryGetValue(cell, out Tween activeTween) && activeTween.isAlive)
            {
                activeTween.Stop();
            }

            tileTransform.localScale = baseScale;

            float clampedDuration = Mathf.Max(0.01f, duration * 0.5f);
            Vector3 targetScale = baseScale * scaleMultiplier;

            Tween scaleTween = Tween.Scale(tileTransform, targetScale, clampedDuration, cycles: 2, cycleMode: CycleMode.Yoyo)
                .OnComplete(() =>
                {
                    tileTransform.localScale = baseScale;
                });

            tileScaleTweenByCell[cell] = scaleTween;
        }

        private void PlayTileReplacementFlip(Vector2Int cell, TileType newType)
        {
            if (!tileByCell.TryGetValue(cell, out Transform tileTransform))
            {
                return;
            }

            if (!tileBaseRotationByCell.TryGetValue(cell, out Vector3 baseRotation))
            {
                baseRotation = tileTransform.localEulerAngles;
                tileBaseRotationByCell[cell] = baseRotation;
            }

            if (tileRotateTweenByCell.TryGetValue(cell, out Tween activeTween) && activeTween.isAlive)
            {
                activeTween.Stop();
            }

            float halfDuration = Mathf.Max(0.05f, tileReplaceFlipDuration * 0.5f);
            Vector3 foldIn = baseRotation + new Vector3(90f, 0f, 0f);
            Vector3 foldOutStart = baseRotation + new Vector3(-90f, 0f, 0f);

            Tween firstHalf = Tween.LocalEulerAngles(tileTransform, baseRotation, foldIn, halfDuration, tileReplaceFlipInEase)
                .OnComplete(() =>
                {
                    ApplyTileMaterial(cell, newType);
                    tileTransform.localEulerAngles = foldOutStart;

                    Tween secondHalf = Tween.LocalEulerAngles(tileTransform, foldOutStart, baseRotation, halfDuration, tileReplaceFlipOutEase)
                        .OnComplete(() =>
                        {
                            tileTransform.localEulerAngles = baseRotation;
                        });

                    tileRotateTweenByCell[cell] = secondHalf;
                });

            tileRotateTweenByCell[cell] = firstHalf;
        }

        private void ApplyTileMaterial(Vector2Int cell, TileType type)
        {
            if (!tileRendererByCell.TryGetValue(cell, out Renderer renderer) || renderer == null)
            {
                return;
            }

            Material material = GetMaterialForType(type);

            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            SyncRockTopVisual(cell, type);
        }

        private void SyncRockTopVisual(Vector2Int cell, TileType type)
        {
            if (type != TileType.Rock)
            {
                if (rockTopByCell.TryGetValue(cell, out GameObject existingRock) && existingRock != null)
                {
                    Destroy(existingRock);
                }

                rockTopByCell.Remove(cell);
                return;
            }

            if (!tileByCell.TryGetValue(cell, out Transform tileTransform))
            {
                return;
            }

            if (rockTopByCell.TryGetValue(cell, out GameObject existing) && existing != null)
            {
                return;
            }

            GameObject rockPrefab = PickRockTopPrefab();

            if (rockPrefab == null)
            {
                return;
            }

            GameObject rockVisual = Instantiate(rockPrefab, tileTransform);
            float topY = (tileHeight * 0.5f) + rockTopYOffset;
            rockVisual.transform.localPosition = new Vector3(0f, topY, 0f);
            rockVisual.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            rockVisual.transform.localScale = rockTopScale;
            rockTopByCell[cell] = rockVisual;
        }

        private GameObject PickRockTopPrefab()
        {
            if (rockTopPrefabs == null || rockTopPrefabs.Length == 0)
            {
                return null;
            }

            var valid = new List<GameObject>();

            for (int i = 0; i < rockTopPrefabs.Length; i++)
            {
                if (rockTopPrefabs[i] != null)
                {
                    valid.Add(rockTopPrefabs[i]);
                }
            }

            if (valid.Count == 0)
            {
                return null;
            }

            int index = Random.Range(0, valid.Count);
            return valid[index];
        }

        private bool IsCellWalkable(Vector2Int cell)
        {
            return IsInBounds(cell.x, cell.y) && tileBoardSystem != null && tileBoardSystem.IsWalkable(cell);
        }

        private bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < gridSize && y < gridSize;
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            float halfSpan = (gridSize - 1) * 0.5f;
            float worldX = (cell.x - halfSpan) * cellSize;
            float worldZ = (cell.y - halfSpan) * cellSize;
            return GetGridCenterWorld() + new Vector3(worldX, 0f, worldZ);
        }

        private Vector3 CellToWorldForSplippy(Vector2Int cell)
        {
            Vector3 basePos = CellToWorld(cell);
            return basePos + new Vector3(0f, tileYOffset + (tileHeight * 0.5f) + splippyHeightOffset, 0f);
        }

        private Vector3 GetGridCenterWorld()
        {
            return transform.position + gridOrigin;
        }

        // just for visuals so I can see stuff
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            float halfSpan = (gridSize - 1) * 0.5f;
            Vector3 gridCenter = GetGridCenterWorld();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    float worldX = (x - halfSpan) * cellSize;
                    float worldZ = (y - halfSpan) * cellSize;
                    Vector3 center = gridCenter + new Vector3(worldX, 0f, worldZ);
                    Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.02f, cellSize));
                }
            }
        }
    }
}
