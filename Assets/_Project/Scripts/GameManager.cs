using System.Collections.Generic;
using System.Collections;
using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

namespace projectsplippy
{
    public class GameManager : MonoBehaviour
    {
        public enum GamePhase
        {
            Lobby,
            Revealing,
            Countdown,
            Gameplay
        }

        [Header("References")]
        [SerializeField] private TileBoardView boardView;
        [SerializeField] private RunStateController runState;
        [SerializeField] private PreGameFlowController preGameFlow;

        [Header("Grid")]
        [SerializeField] private int gridSize = 7;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string playerMapName = "Player";
        [SerializeField] private string moveTowardsActionName = "MoveTowards";

        [Header("Path Authoring")]
        [SerializeField, Min(1)] private int maxGameplayPathRange = 8;

        [Header("Tile Rules")]
        [SerializeField] private TileRules tileRules = default;

        [Header("Tile Frequency (%)")]
        [SerializeField, Range(0, 100)] private int farmlandPercent = 72;
        [SerializeField, Range(0, 100)] private int ecosystemPercent = 4;
        [SerializeField, Range(0, 100)] private int sanitationPercent = 8;
        [SerializeField, Range(0, 100)] private int marinePercent = 16;

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

        private Camera mainCamera;
        private InputAction moveTowardsAction;
        private TileBoardSystem tileBoardSystem;
        private readonly List<TileStepResult> deferredPathStepResults = new List<TileStepResult>();
        private readonly List<Vector2Int> authoredGameplayPath = new List<Vector2Int>();

        private Vector2Int currentCell;
        private bool isMoving;
        private bool suppressGameplayClick;
        private Vector3 splippyBaseScale = Vector3.one;
        private GamePhase currentPhase = GamePhase.Gameplay;

        public int GridSize => gridSize;
        public float CellSize => cellSize;
        public Vector2Int CurrentCell => currentCell;
        public Vector2Int CenterCell => new Vector2Int(gridSize / 2, gridSize / 2);
        public Vector2Int BottomCenterCell => new Vector2Int(gridSize / 2, 0);
        public Vector3 GridCenterWorld => GetGridCenterWorld();
        public bool IsMoving => isMoving;
        public GamePhase CurrentPhase => currentPhase;

        private void Awake()
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                enabled = false;
                return;
            }

            if (boardView == null)
            {
                boardView = GetComponent<TileBoardView>();
            }

            if (runState == null)
            {
                runState = GetComponent<RunStateController>();
            }

            if (preGameFlow == null)
            {
                preGameFlow = GetComponent<PreGameFlowController>();
            }

            if (boardView == null || runState == null)
            {
                Debug.LogError("GameManager: Assign TileBoardView and RunStateController in inspector.");
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
            tileRules = ResolveTileRules(tileRules);
            currentCell = new Vector2Int(gridSize / 2, gridSize / 2);
            ResetGameplayPath();

            tileBoardSystem = new TileBoardSystem(gridSize, tileRules, BuildSpawnWeights());
            SetupInput();

            if (preGameFlow != null)
            {
                preGameFlow.Begin(this);
            }
            else
            {
                StartGameplayImmediate();
            }
        }

        private void OnEnable()
        {
            moveTowardsAction?.Enable();
        }

        private void OnDisable()
        {
            moveTowardsAction?.Disable();
            boardView?.ClearHoverPathPreview();
        }

        private void Update()
        {
            boardView.UpdateBillboardInteractor(splippy.position);

            bool movePressedThisFrame = moveTowardsAction != null && moveTowardsAction.WasPressedThisFrame();

            if (isMoving)
            {
                if (movePressedThisFrame)
                {
                    suppressGameplayClick = true;
                }
                return;
            }

            if (currentPhase != GamePhase.Lobby && currentPhase != GamePhase.Gameplay)
            {
                return;
            }

            if (currentPhase == GamePhase.Gameplay && runState != null && runState.IsGameOver)
            {
                return;
            }

            bool hasHoveredCell = false;
            Vector2Int hoveredCell = default;

            if (TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
            {
                hasHoveredCell = TryGetClickedCell(pointerScreenPosition, out hoveredCell);
            }

            if (currentPhase == GamePhase.Gameplay && hasHoveredCell)
            {
                UpdateGameplayAuthoredPath(hoveredCell);
            }

            UpdateHoverPathPreview(hasHoveredCell, hoveredCell);

            if (moveTowardsAction == null || !movePressedThisFrame)
            {
                return;
            }

            if (!hasHoveredCell)
            {
                return;
            }

            boardView.PlayTileTapFeedback(hoveredCell);

            if (currentPhase == GamePhase.Gameplay)
            {
                if (suppressGameplayClick)
                {
                    suppressGameplayClick = false;
                    return;
                }

                if (hoveredCell == currentCell)
                {
                    ResetGameplayPath();
                    return;
                }

                TryExecuteAuthoredGameplayPath();
                return;
            }

            if (hoveredCell == currentCell)
            {
                return;
            }

            MoveToCell(hoveredCell);
        }

        private void UpdateGameplayAuthoredPath(Vector2Int hoveredCell)
        {
            if (authoredGameplayPath.Count == 0 || authoredGameplayPath[0] != currentCell)
            {
                ResetGameplayPath();
            }

            if (hoveredCell == currentCell)
            {
                ResetGameplayPath();
                return;
            }

            if (!IsCellWalkable(hoveredCell))
            {
                return;
            }

            if (authoredGameplayPath.Count > 1 && hoveredCell == authoredGameplayPath[authoredGameplayPath.Count - 2])
            {
                authoredGameplayPath.RemoveAt(authoredGameplayPath.Count - 1);
                return;
            }

            Vector2Int tail = authoredGameplayPath[authoredGameplayPath.Count - 1];

            int existingSteps = authoredGameplayPath.Count - 1;
            int rangeRemaining = Mathf.Max(0, Mathf.Max(1, maxGameplayPathRange) - existingSteps);
            int stepBudget = rangeRemaining;

            if (stepBudget <= 0)
            {
                return;
            }

            if (!IsCardinalAdjacent(tail, hoveredCell))
            {
                if (!GridPathfinder.TryFindPathBfs(gridSize, tail, hoveredCell, IsCellWalkable, out List<Vector2Int> autoPath) || autoPath.Count <= 1)
                {
                    return;
                }

                int appendCount = Mathf.Min(stepBudget, autoPath.Count - 1);

                for (int i = 1; i <= appendCount; i++)
                {
                    Vector2Int next = autoPath[i];
                    int nextExistingIndex = authoredGameplayPath.IndexOf(next);

                    if (nextExistingIndex >= 0)
                    {
                        int removeCount = authoredGameplayPath.Count - (nextExistingIndex + 1);

                        if (removeCount > 0)
                        {
                            authoredGameplayPath.RemoveRange(nextExistingIndex + 1, removeCount);
                        }

                        continue;
                    }

                    authoredGameplayPath.Add(next);
                }

                return;
            }

            int existingIndex = authoredGameplayPath.IndexOf(hoveredCell);

            if (existingIndex >= 0)
            {
                int removeCount = authoredGameplayPath.Count - (existingIndex + 1);

                if (removeCount > 0)
                {
                    authoredGameplayPath.RemoveRange(existingIndex + 1, removeCount);
                }

                return;
            }

            authoredGameplayPath.Add(hoveredCell);
        }

        private void UpdateHoverPathPreview(bool hasHoveredCell, Vector2Int hoveredCell)
        {
            if (currentPhase == GamePhase.Gameplay)
            {
                if (authoredGameplayPath.Count <= 1)
                {
                    boardView.ClearHoverPathPreview();
                    return;
                }

                boardView.ShowHoverPathPreview(authoredGameplayPath);
                return;
            }

            if (currentPhase != GamePhase.Lobby)
            {
                boardView.ClearHoverPathPreview();
                return;
            }

            if (!hasHoveredCell)
            {
                boardView.ClearHoverPathPreview();
                return;
            }

            if (hoveredCell == currentCell)
            {
                boardView.ClearHoverPathPreview();
                return;
            }

            if (!GridPathfinder.TryFindPathBfs(gridSize, currentCell, hoveredCell, IsCellWalkable, out List<Vector2Int> previewPath) || previewPath.Count <= 1)
            {
                boardView.ClearHoverPathPreview();
                return;
            }

            boardView.ShowHoverPathPreview(previewPath);
        }

        private bool TryExecuteAuthoredGameplayPath()
        {
            if (currentPhase != GamePhase.Gameplay || isMoving)
            {
                return false;
            }

            if (runState != null && runState.IsGameOver)
            {
                return false;
            }

            if (authoredGameplayPath.Count <= 1)
            {
                return false;
            }

            if (runState != null)
            {
                if (!runState.CanAffordPath(authoredGameplayPath.Count - 1))
                {
                    return false;
                }

                bool clickCostGameOver = runState.ApplyPathClickCost();

                if (clickCostGameOver)
                {
                    return false;
                }
            }

            var path = new List<Vector2Int>(authoredGameplayPath);
            deferredPathStepResults.Clear();
            isMoving = true;

            // Keep preview visible and consume it step-by-step while moving.
            boardView.ShowHoverPathPreviewImmediateFrozen(path);
            MoveAlongPath(path, 1, path.Count - 1);
            return true;
        }

        private void ResetGameplayPath()
        {
            authoredGameplayPath.Clear();
            authoredGameplayPath.Add(currentCell);
        }

        private static bool IsCardinalAdjacent(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) == 1;
        }

        public void ConfigureLobby(
            HashSet<Vector2Int> walkableCells,
            Dictionary<Vector2Int, GameObject> specialPrefabs,
            Vector2Int playerStartCell)
        {
            currentPhase = GamePhase.Lobby;
            currentCell = playerStartCell;
            ResetGameplayPath();
            suppressGameplayClick = false;

            tileBoardSystem.ApplyLobbyMask(walkableCells);
            boardView.BuildBoard(gridSize, cellSize, GetGridCenterWorld(), tileBoardSystem, walkableCells, specialPrefabs);
            boardView.RefreshProgressVisuals(tileBoardSystem);
            splippy.position = CellToWorldForSplippy(currentCell);
            boardView.UpdateBillboardInteractor(splippy.position);
        }

        public IEnumerator StartGameplayBloomReveal(float ringStepDelay)
        {
            currentPhase = GamePhase.Revealing;

            tileBoardSystem.InitializeBoard(currentCell);
            Vector2Int center = CenterCell;
            int maxDistance = Mathf.Abs(center.x - 0) + Mathf.Abs(center.y - 0);
            maxDistance = Mathf.Max(maxDistance, Mathf.Abs(center.x - (gridSize - 1)) + Mathf.Abs(center.y - (gridSize - 1)));
            maxDistance = Mathf.Max(maxDistance, Mathf.Abs(center.x - 0) + Mathf.Abs(center.y - (gridSize - 1)));
            maxDistance = Mathf.Max(maxDistance, Mathf.Abs(center.x - (gridSize - 1)) + Mathf.Abs(center.y - 0));

            float delay = Mathf.Max(0.01f, ringStepDelay);

            for (int dist = 0; dist <= maxDistance; dist++)
            {
                var ringCells = new List<Vector2Int>();

                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        int manhattan = Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y);

                        if (manhattan == dist)
                        {
                            ringCells.Add(cell);
                        }
                    }
                }

                // Randomize within each ring so the bloom feels organic.
                for (int i = ringCells.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (ringCells[i], ringCells[j]) = (ringCells[j], ringCells[i]);
                }

                for (int i = 0; i < ringCells.Count; i++)
                {
                    Vector2Int cell = ringCells[i];
                    TileType type = tileBoardSystem.GetTileType(cell);
                    int farmlandVariantIndex = -1;
                    int sanitationTurns = -1;

                    if (tileBoardSystem.TryGetTile(cell, out TileData tile))
                    {
                        if (type == TileType.Farmland)
                        {
                            farmlandVariantIndex = tile.CropVariantIndex;
                        }
                        else if (type == TileType.Sanitation)
                        {
                            sanitationTurns = tile.SanitationTimer;
                        }
                    }

                    boardView.PlayTileReplacementFlip(
                        cell,
                        type,
                        forcedFarmlandCropVariantIndex: farmlandVariantIndex,
                        forcedSanitationTurns: sanitationTurns);
                }

                if (dist < maxDistance)
                {
                    yield return new WaitForSeconds(delay);
                }
            }

            boardView.RefreshProgressVisuals(tileBoardSystem);
        }

        public void BeginCountdownPhase()
        {
            currentPhase = GamePhase.Countdown;
        }

        public void BeginGameplayPhase()
        {
            currentPhase = GamePhase.Gameplay;
            runState.Initialize();
            boardView.RefreshProgressVisuals(tileBoardSystem);
            ResetGameplayPath();
            suppressGameplayClick = false;
        }

        private void StartGameplayImmediate()
        {
            tileBoardSystem.InitializeBoard(currentCell);
            boardView.BuildBoard(gridSize, cellSize, GetGridCenterWorld(), tileBoardSystem);
            runState.Initialize();
            splippy.position = CellToWorldForSplippy(currentCell);
            boardView.UpdateBillboardInteractor(splippy.position);
            currentPhase = GamePhase.Gameplay;
            ResetGameplayPath();
            suppressGameplayClick = false;
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

            deferredPathStepResults.Clear();
            isMoving = true;
            boardView.ShowHoverPathPreviewImmediateFrozen(path);
            MoveAlongPath(path, 1, path.Count - 1);
        }

        private void MoveAlongPath(List<Vector2Int> path, int index, int finalIndex)
        {
            if (index >= path.Count)
            {
                if (currentPhase == GamePhase.Gameplay)
                {
                    ResolvePostMoveEvents();
                    ResetGameplayPath();
                    boardView.ClearHoverPathPreview();
                }

                suppressGameplayClick = false;
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
                            UpdateRemainingMovementPreview(path, index);

                            if (currentPhase == GamePhase.Lobby)
                            {
                                boardView.PlayTileLandingFeedback(nextCell);

                                if (index == finalIndex)
                                {
                                    preGameFlow?.HandleLobbyLanding(nextCell);
                                }

                                Tween.Scale(splippy, splippyBaseScale, landingSettleDuration, landingSettleEase);
                                MoveAlongPath(path, index + 1, finalIndex);
                                return;
                            }

                            if (tileBoardSystem != null)
                            {
                                TileStepResult stepResult = tileBoardSystem.ProcessStep(nextCell);
                                deferredPathStepResults.Add(stepResult);
                                boardView.PlayTileLandingFeedback(nextCell);
                            }

                            if (runState != null && runState.IsGameOver)
                            {
                                isMoving = false;
                                return;
                            }

                            Tween.Scale(splippy, splippyBaseScale, landingSettleDuration, landingSettleEase);
                            MoveAlongPath(path, index + 1, finalIndex);
                        });
                });
        }

        private void ResolvePostMoveEvents()
        {
            if (tileBoardSystem == null)
            {
                return;
            }

            ResolveDeferredPathResults();
        }

        private void UpdateRemainingMovementPreview(List<Vector2Int> fullPath, int landedIndex)
        {
            if (fullPath == null)
            {
                return;
            }

            int remainingCount = fullPath.Count - landedIndex;

            if (remainingCount <= 1)
            {
                boardView.ClearHoverPathPreview();
                return;
            }

            var remainingPath = fullPath.GetRange(landedIndex, remainingCount);
            boardView.ConsumeHoverPreviewStep(remainingPath);
        }

        private int GetMaxAffordableAdditionalSteps(int existingSteps)
        {
            if (runState == null)
            {
                return Mathf.Max(1, maxGameplayPathRange);
            }

            int additional = 0;
            int safetyCap = Mathf.Max(1, Mathf.Max(1, maxGameplayPathRange) - Mathf.Max(0, existingSteps));

            while (additional < safetyCap)
            {
                int totalStepsAfterAppend = existingSteps + additional + 1;

                if (!runState.CanAffordPath(totalStepsAfterAppend))
                {
                    break;
                }

                additional++;
            }

            return additional;
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
            return basePos + new Vector3(0f, boardView.GroundTopYOffset + splippyHeightOffset, 0f);
        }

        private Vector3 GetGridCenterWorld()
        {
            return transform.position + gridOrigin;
        }

        private TileRules ResolveTileRules(TileRules rules)
        {
            TileRules fallback = TileRules.Default;

            if (rules.sanitationTimeoutTurns <= 0)
            {
                rules.sanitationTimeoutTurns = fallback.sanitationTimeoutTurns;
            }

            rules.sanitationTimeoutTurns = Mathf.Max(2, rules.sanitationTimeoutTurns);

            int cropVariantCount = boardView != null ? boardView.AvailableCropSpriteCount : fallback.farmlandCropVariantCount;
            rules.farmlandCropVariantCount = Mathf.Max(1, cropVariantCount);

            return rules;
        }

        private TileSpawnWeights BuildSpawnWeights()
        {
            farmlandPercent = Mathf.Clamp(farmlandPercent, 0, 100);
            ecosystemPercent = Mathf.Clamp(ecosystemPercent, 0, 100);
            sanitationPercent = Mathf.Clamp(sanitationPercent, 0, 100);
            marinePercent = Mathf.Clamp(marinePercent, 0, 100);

            int total = farmlandPercent + ecosystemPercent + sanitationPercent + marinePercent;

            if (total <= 0)
            {
                return TileSpawnWeights.Default;
            }

            float invTotal = 1f / total;

            return new TileSpawnWeights
            {
                farmlandWeight = farmlandPercent * invTotal,
                ecosystemWeight = ecosystemPercent * invTotal,
                sanitationWeight = sanitationPercent * invTotal,
                marineWeight = marinePercent * invTotal
            };
        }

        private void ResolveDeferredPathResults()
        {
            if (tileBoardSystem == null)
            {
                return;
            }

            if (deferredPathStepResults.Count > 0)
            {
                List<string> collisionOrder = BuildCollisionOrderDebug(deferredPathStepResults);
                runState?.ApplyPathResolution(deferredPathStepResults, collisionOrder);
                var traversedCells = new List<Vector2Int>(deferredPathStepResults.Count);

                for (int i = 0; i < deferredPathStepResults.Count; i++)
                {
                    TileStepResult step = deferredPathStepResults[i];
                    traversedCells.Add(step.Cell);

                    if (step.LandingResult != null)
                    {
                        for (int e = 0; e < step.LandingResult.ExpiredToTrashCells.Count; e++)
                        {
                            Vector2Int expiredCell = step.LandingResult.ExpiredToTrashCells[e];
                            boardView.PlayTileReplacementFlip(expiredCell, TileType.Trash, pulseAfterReplace: true);
                        }
                    }
                }

                Dictionary<Vector2Int, TileType> traversedReplacements = tileBoardSystem.ReplaceTraversedTiles(traversedCells, currentCell);

                foreach (KeyValuePair<Vector2Int, TileType> replacement in traversedReplacements)
                {
                    int farmlandVariantIndex = -1;
                    int sanitationTurns = -1;

                    if (tileBoardSystem.TryGetTile(replacement.Key, out TileData tile))
                    {
                        if (replacement.Value == TileType.Farmland)
                        {
                            farmlandVariantIndex = tile.CropVariantIndex;
                        }
                        else if (replacement.Value == TileType.Sanitation)
                        {
                            sanitationTurns = tile.SanitationTimer;
                        }
                    }

                    boardView.PlayTileReplacementFlip(
                        replacement.Key,
                        replacement.Value,
                        pulseAfterReplace: true,
                        forcedFarmlandCropVariantIndex: farmlandVariantIndex,
                        forcedSanitationTurns: sanitationTurns);
                }
            }

            boardView.RefreshProgressVisuals(tileBoardSystem);
            deferredPathStepResults.Clear();
        }

        private List<string> BuildCollisionOrderDebug(IReadOnlyList<TileStepResult> steps)
        {
            var labels = new List<string>(steps != null ? steps.Count : 0);

            if (steps == null)
            {
                return labels;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                TileStepResult step = steps[i];

                if (step.EnteredType == TileType.Farmland)
                {
                    labels.Add(boardView != null ? boardView.GetCropVariantLabel(step.EnteredCropVariantIndex) : $"Crop[{step.EnteredCropVariantIndex}]" );
                }
                else
                {
                    labels.Add(step.EnteredType.ToString());
                }
            }

            return labels;
        }

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
