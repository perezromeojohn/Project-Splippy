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
        [SerializeField] private MainMenuController mainMenuController;
        [SerializeField] private GameplayPressureController gameplayPressureController;
        [SerializeField] private SanitationSpawnController sanitationSpawnController;
        [SerializeField] private PathResolutionController pathResolutionController;
        [SerializeField] private bool skipPregameSequence = true;

        [Header("Grid")]
        [SerializeField] private int gridSize = 7;
        [SerializeField] private float cellSize = 1f;
        [SerializeField, Min(0f)] private float tilePadding = 0.08f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string playerMapName = "Player";
        [SerializeField] private string moveTowardsActionName = "MoveTowards";

        [Header("Path Authoring")]
        [SerializeField, Min(1)] private int maxGameplayPathRange = 7;

        [Header("Tile Rules")]
        [SerializeField] private TileRules tileRules = default;
        [SerializeField, Min(1)] private int sanitationTurnsToTrash = 2;

        [Header("Tile Frequency (%)")]
        [SerializeField, Range(0, 100)] private int farmlandPercent = 72;
        [SerializeField, Range(0, 100)] private int ecosystemPercent = 4;
        [SerializeField, Range(0, 100)] private int sanitationPercent = 8;
        [SerializeField, Range(0, 100)] private int worstSanitationPercent = 3;
        [SerializeField, Range(0, 100)] private int marinePercent = 14;
        [SerializeField, Range(0, 100)] private int splashPercent = 2;

        [Header("Player")]
        [SerializeField] private Transform splippy;
        [SerializeField] private GameObject splippyHopAudioSource;
        [SerializeField] private float moveDurationPerCell = 0.12f;
        [SerializeField] private float splippyHeightOffset = 0.6f;
        [SerializeField] private float hopHeight = 0.3f;
        [SerializeField] private Ease hopUpEase = Ease.OutQuad;
        [SerializeField] private Ease hopDownEase = Ease.InQuad;
        [SerializeField] private float splippyStretchAmount = 0.12f;
        [SerializeField] private float splippySquashAmount = 0.14f;
        [SerializeField] private float landingSettleDuration = 0.06f;
        [SerializeField] private Ease landingSettleEase = Ease.OutBack;
        [SerializeField] private bool faceSplippyToMovement = true;
        [SerializeField] private float splippyTurnDuration = 0.08f;
        [SerializeField] private Ease splippyTurnEase = Ease.OutSine;
        [SerializeField] private bool enableSplippyIdleSquash = true;
        [SerializeField, Range(0f, 0.45f)] private float splippyIdleSquashAmount = 0.14f;
        [SerializeField] private float splippyIdleSquashHalfDuration = 0.32f;
        [SerializeField] private Ease splippyIdleSquashEase = Ease.InOutSine;
        [SerializeField, Min(0f)] private float marineFinalDestinationEndTurnDelay = 0.5f;
        [SerializeField, Min(0f)] private float splashFinalDestinationEndTurnDelay = 0.3f;

        private Camera mainCamera;
        private InputAction moveTowardsAction;
        private TileBoardSystem tileBoardSystem;
        private readonly List<TileStepResult> deferredPathStepResults = new List<TileStepResult>();
        private readonly List<Vector2Int> authoredGameplayPath = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> lobbyWalkableCells = new HashSet<Vector2Int>();

        private Vector2Int currentCell;
        private bool isMoving;
        private bool suppressGameplayClick;
        private Vector3 splippyBaseScale = Vector3.one;
        private Tween splippyRotationTween;
        private Tween splippyIdleSquashTween;
        private bool splippyIdleSquashLoopActive;
        private bool awaitingMainMenuPlay;
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

            if (mainMenuController == null)
            {
                mainMenuController = GetComponent<MainMenuController>();
            }

            if (gameplayPressureController == null)
            {
                gameplayPressureController = GetComponent<GameplayPressureController>();
            }

            if (sanitationSpawnController == null)
            {
                sanitationSpawnController = GetComponent<SanitationSpawnController>();
            }

            if (pathResolutionController == null)
            {
                pathResolutionController = GetComponent<PathResolutionController>();
            }

            if (boardView == null || runState == null)
            {
                Debug.LogError("GameManager: Assign TileBoardView and RunStateController in inspector.");
                enabled = false;
                return;
            }

            if (pathResolutionController == null)
            {
                Debug.LogError("GameManager: Assign PathResolutionController in inspector.");
                enabled = false;
                return;
            }

            if (gameplayPressureController == null)
            {
                Debug.LogWarning("GameManager: GameplayPressureController is not assigned; low-move vignette/soft-lock evaluation is disabled.");
            }

            if (sanitationSpawnController == null)
            {
                Debug.LogWarning("GameManager: SanitationSpawnController is not assigned; spawn cadence tracking is disabled.");
            }

            if (splippy == null)
            {
                splippy = transform;
            }

            splippyBaseScale = splippy.localScale;

            gridSize = Mathf.Max(1, gridSize);
            cellSize = Mathf.Max(0.1f, cellSize);
            tilePadding = Mathf.Max(0f, tilePadding);
            tileRules = ResolveTileRules(tileRules);
            currentCell = new Vector2Int(gridSize / 2, gridSize / 2);
            ResetGameplayPath();

            tileBoardSystem = new TileBoardSystem(gridSize, tileRules, BuildSpawnWeights());
            SetupInput();
            gameplayPressureController?.Initialize();
            runState?.PrepareHudForPreload();

            awaitingMainMenuPlay = mainMenuController != null && skipPregameSequence;

            if (awaitingMainMenuPlay)
            {
                mainMenuController.Show();
            }
            else if (preGameFlow != null)
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
            StopSplippyIdleSquash(resetScale: false);
            gameplayPressureController?.SetInactiveImmediate();

            if (splippyRotationTween.isAlive)
            {
                splippyRotationTween.Stop();
            }
        }

        private void Update()
        {
            if (awaitingMainMenuPlay)
            {
                return;
            }

            boardView.UpdateBillboardInteractor(splippy.position);
            gameplayPressureController?.SyncRunStateVisuals(currentPhase, runState);
            gameplayPressureController?.UpdateVignette(Time.deltaTime);

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

            if (currentPhase == GamePhase.Lobby && preGameFlow != null && !preGameFlow.CanAcceptLobbyMoveInput)
            {
                boardView.ClearHoverPathPreview();
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
            int rangeRemaining = Mathf.Max(0, GetCurrentPathRangeLimit() - existingSteps);
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

            if (!TryFindMovementPath(currentCell, hoveredCell, out List<Vector2Int> previewPath) || previewPath.Count <= 1)
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
            runState?.BeginPathChargePreview();
            StopSplippyIdleSquash(resetScale: true);
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
            lobbyWalkableCells.Clear();

            if (walkableCells != null)
            {
                foreach (Vector2Int cell in walkableCells)
                {
                    lobbyWalkableCells.Add(cell);
                }
            }

            tileBoardSystem.ApplyLobbyMask(walkableCells);
            boardView.BuildBoard(gridSize, cellSize, GetGridCenterWorld(), tileBoardSystem, walkableCells, specialPrefabs, tilePadding);
            boardView.RefreshProgressVisuals(tileBoardSystem);
            splippy.position = CellToWorldForSplippy(currentCell);
            boardView.UpdateBillboardInteractor(splippy.position);
            StartSplippyIdleSquashIfReady();
        }

        public IEnumerator TweenAndRemoveLobbyCells(IReadOnlyList<Vector2Int> cells, float sinkDistance, float duration, Ease sinkEase)
        {
            if (boardView == null || cells == null || cells.Count == 0)
            {
                yield break;
            }

            yield return StartCoroutine(boardView.TweenAndRemoveCells(cells, sinkDistance, duration, sinkEase));
        }

        public IEnumerator StartGameplayBloomReveal(float ringStepDelay)
        {
            currentPhase = GamePhase.Revealing;
            runState?.PlayHudIntroFromPreload();

            tileBoardSystem.InitializeBoard(currentCell);
            Vector2Int center = CenterCell;
            int maxDistance = GetBloomRevealMaxDistance();

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
                        else if (type == TileType.Sanitation || type == TileType.WorstSanitation)
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

        public float GetBloomRevealDuration(float ringStepDelay)
        {
            int maxDistance = GetBloomRevealMaxDistance();

            if (maxDistance <= 0)
            {
                return 0f;
            }

            return Mathf.Max(0.01f, ringStepDelay) * maxDistance;
        }

        private int GetBloomRevealMaxDistance()
        {
            Vector2Int center = CenterCell;
            int maxDistance = Mathf.Abs(center.x - 0) + Mathf.Abs(center.y - 0);
            maxDistance = Mathf.Max(maxDistance, Mathf.Abs(center.x - (gridSize - 1)) + Mathf.Abs(center.y - (gridSize - 1)));
            maxDistance = Mathf.Max(maxDistance, Mathf.Abs(center.x - 0) + Mathf.Abs(center.y - (gridSize - 1)));
            maxDistance = Mathf.Max(maxDistance, Mathf.Abs(center.x - (gridSize - 1)) + Mathf.Abs(center.y - 0));
            return maxDistance;
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
            sanitationSpawnController?.ResetTracker(runState);
            gameplayPressureController?.Evaluate(currentPhase, tileBoardSystem, runState, currentCell, gridSize);
            StartSplippyIdleSquashIfReady();
        }

        public void BeginGameplayFromMenu()
        {
            awaitingMainMenuPlay = false;

            if (preGameFlow != null)
            {
                preGameFlow.BeginLobby(this);
            }
            else
            {
                StartGameplayImmediate();
            }
        }

        private void StartGameplayImmediate()
        {
            tileBoardSystem.InitializeBoard(currentCell);
            boardView.BuildBoard(gridSize, cellSize, GetGridCenterWorld(), tileBoardSystem, tilePadding);
            runState?.PlayHudIntroFromPreload();
            runState.Initialize();
            splippy.position = CellToWorldForSplippy(currentCell);
            boardView.UpdateBillboardInteractor(splippy.position);
            currentPhase = GamePhase.Gameplay;
            ResetGameplayPath();
            suppressGameplayClick = false;
            sanitationSpawnController?.ResetTracker(runState);
            gameplayPressureController?.Evaluate(currentPhase, tileBoardSystem, runState, currentCell, gridSize);
            StartSplippyIdleSquashIfReady();
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
            float stride = cellSize + tilePadding;

            int x = Mathf.RoundToInt((local.x / stride) + halfSpan);
            int y = Mathf.RoundToInt((local.z / stride) + halfSpan);

            cell = new Vector2Int(x, y);

            if (currentPhase == GamePhase.Lobby && lobbyWalkableCells.Count > 0)
            {
                return lobbyWalkableCells.Contains(cell);
            }

            if (!IsInBounds(x, y))
            {
                return false;
            }

            return true;
        }

        private void MoveToCell(Vector2Int targetCell)
        {
            if (!TryFindMovementPath(currentCell, targetCell, out List<Vector2Int> path))
            {
                return;
            }

            if (path.Count <= 1)
            {
                return;
            }

            deferredPathStepResults.Clear();
            StopSplippyIdleSquash(resetScale: true);
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
                    StartCoroutine(FinalizeGameplayMoveRoutine());
                    return;
                }

                suppressGameplayClick = false;
                isMoving = false;
                StartSplippyIdleSquashIfReady();
                return;
            }

            Vector2Int nextCell = path[index];
            Vector3 start = splippy.position;
            Vector3 end = CellToWorldForSplippy(nextCell);
            FaceSplippyTowards(end - start);
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
                            if (splippyHopAudioSource != null)
                            {
                                GameObject hopSfx = Instantiate(splippyHopAudioSource, splippy.position, Quaternion.identity);
                                AudioSource hopAudioSource = hopSfx.GetComponent<AudioSource>();
                                hopAudioSource.pitch = 1f + ((authoredGameplayPath.Count - 1) * 0.06f);
                                hopAudioSource.Play();
                                Destroy(hopSfx, hopAudioSource.clip.length / hopAudioSource.pitch);
                            }

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
                                TileType nextTileType = tileBoardSystem.GetTileType(nextCell);
                                bool marineInterrupt = nextTileType == TileType.Marine;
                                bool splashInterrupt = nextTileType == TileType.Splash;
                                bool activatorInterrupt = marineInterrupt || splashInterrupt;
                                bool advanceTurnDecay = index == finalIndex || activatorInterrupt;
                                IReadOnlyList<Vector2Int> touchedCellsThisTurn = null;

                                if (advanceTurnDecay)
                                {
                                    touchedCellsThisTurn = path.GetRange(1, index);
                                }

                                TileStepResult stepResult = tileBoardSystem.ProcessStep(nextCell, advanceTurnDecay, touchedCellsThisTurn);
                                deferredPathStepResults.Add(stepResult);
                                runState?.PreviewPathStepCharge(stepResult);
                                boardView.PlayTileLandingFeedback(nextCell);
                                runState?.PlayHopSliderFeedback();

                                if (activatorInterrupt)
                                {
                                    Tween.Scale(splippy, splippyBaseScale, landingSettleDuration, landingSettleEase);

                                    if (index < finalIndex)
                                    {
                                        StartCoroutine(ResolveActivatorInterruptAndContinueRoutine(path, index + 1, finalIndex));
                                    }
                                    else
                                    {
                                        float delay = splashInterrupt
                                            ? splashFinalDestinationEndTurnDelay
                                            : marineFinalDestinationEndTurnDelay;
                                        StartCoroutine(FinalizeGameplayMoveRoutine(delay));
                                    }

                                    return;
                                }
                            }

                            if (runState != null && runState.IsGameOver)
                            {
                                runState.EndPathChargePreview();
                                isMoving = false;
                                StartSplippyIdleSquashIfReady();
                                return;
                            }

                            Tween.Scale(splippy, splippyBaseScale, landingSettleDuration, landingSettleEase);
                            MoveAlongPath(path, index + 1, finalIndex);
                        });
                });
        }

        private IEnumerator FinalizeGameplayMoveRoutine(float delayBeforeEndTurnSystems = 0f)
        {
            yield return StartCoroutine(ResolvePostMoveEventsRoutine(
                applyEndOfTurnSystems: true,
                delayBeforeEndTurnSystems: delayBeforeEndTurnSystems));
            CompleteGameplayMove();
        }

        private IEnumerator ResolveActivatorInterruptAndContinueRoutine(List<Vector2Int> path, int nextIndex, int finalIndex)
        {
            yield return StartCoroutine(ResolvePostMoveEventsRoutine(applyEndOfTurnSystems: false));

            if (runState != null && runState.IsGameOver)
            {
                CompleteGameplayMove();
                yield break;
            }

            if (path == null || nextIndex >= path.Count)
            {
                StartCoroutine(FinalizeGameplayMoveRoutine());
                yield break;
            }

            MoveAlongPath(path, nextIndex, finalIndex);
        }

        private void CompleteGameplayMove()
        {
            runState?.EndPathChargePreview();
            ResetGameplayPath();
            boardView.ClearHoverPathPreview();
            suppressGameplayClick = false;
            isMoving = false;
            StartSplippyIdleSquashIfReady();
        }

        private IEnumerator ResolvePostMoveEventsRoutine(bool applyEndOfTurnSystems, float delayBeforeEndTurnSystems = 0f)
        {
            if (tileBoardSystem == null)
            {
                yield break;
            }

            if (pathResolutionController != null)
            {
                yield return StartCoroutine(pathResolutionController.ResolvePath(
                    tileBoardSystem,
                    boardView,
                    runState,
                    deferredPathStepResults,
                    currentCell,
                    splippy != null ? splippy.position : CellToWorldForSplippy(currentCell)));
            }
            else
            {
                boardView.RefreshProgressVisuals(tileBoardSystem);
                deferredPathStepResults.Clear();
            }

            if (
                applyEndOfTurnSystems &&
                delayBeforeEndTurnSystems > 0f &&
                currentPhase == GamePhase.Gameplay &&
                runState != null &&
                !runState.IsGameOver)
            {
                yield return new WaitForSeconds(delayBeforeEndTurnSystems);
            }

            if (
                applyEndOfTurnSystems &&
                currentPhase == GamePhase.Gameplay &&
                runState != null &&
                !runState.IsGameOver)
            {
                sanitationSpawnController?.HandleResolvedTurn(tileBoardSystem, boardView, runState, currentCell);
                gameplayPressureController?.Evaluate(currentPhase, tileBoardSystem, runState, currentCell, gridSize);
            }
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
                return GetCurrentPathRangeLimit();
            }

            int additional = 0;
            int safetyCap = Mathf.Max(1, GetCurrentPathRangeLimit() - Mathf.Max(0, existingSteps));

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
            if (currentPhase == GamePhase.Lobby && lobbyWalkableCells.Count > 0)
            {
                return lobbyWalkableCells.Contains(cell);
            }

            if (!IsInBounds(cell.x, cell.y) || tileBoardSystem == null)
            {
                return false;
            }

            if (tileBoardSystem.IsWalkable(cell))
            {
                return true;
            }

            return false;
        }

        private int GetCurrentPathRangeLimit()
        {
            int baseRange = Mathf.Max(1, maxGameplayPathRange);

            if (runState == null || !runState.IsTorrentActive)
            {
                return baseRange;
            }

            return Mathf.Max(baseRange, runState.TorrentPathRange);
        }

        private bool TryFindMovementPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
        {
            if (currentPhase == GamePhase.Lobby && lobbyWalkableCells.Count > 0)
            {
                return TryFindLobbyPathBfs(start, goal, out path);
            }

            return GridPathfinder.TryFindPathBfs(gridSize, start, goal, IsCellWalkable, out path);
        }

        private bool TryFindLobbyPathBfs(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
        {
            path = null;

            if (!lobbyWalkableCells.Contains(start) || !lobbyWalkableCells.Contains(goal))
            {
                return false;
            }

            if (start == goal)
            {
                path = new List<Vector2Int> { start };
                return true;
            }

            var visited = new HashSet<Vector2Int> { start };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);

            Vector2Int[] directions =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            bool found = false;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();

                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int next = current + directions[i];

                    if (!lobbyWalkableCells.Contains(next) || !visited.Add(next))
                    {
                        continue;
                    }

                    cameFrom[next] = current;

                    if (next == goal)
                    {
                        found = true;
                        frontier.Clear();
                        break;
                    }

                    frontier.Enqueue(next);
                }
            }

            if (!found)
            {
                return false;
            }

            path = new List<Vector2Int>();
            Vector2Int step = goal;
            path.Add(step);

            while (step != start)
            {
                step = cameFrom[step];
                path.Add(step);
            }

            path.Reverse();
            return true;
        }

        private bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < gridSize && y < gridSize;
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            float halfSpan = (gridSize - 1) * 0.5f;
            float stride = cellSize + tilePadding;
            float worldX = (cell.x - halfSpan) * stride;
            float worldZ = (cell.y - halfSpan) * stride;
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
            sanitationTurnsToTrash = Mathf.Max(1, sanitationTurnsToTrash);
            rules.sanitationTimeoutTurns = sanitationTurnsToTrash > 0 ? sanitationTurnsToTrash : fallback.sanitationTimeoutTurns;

            int cropVariantCount = boardView != null ? boardView.AvailableCropSpriteCount : fallback.farmlandCropVariantCount;
            rules.farmlandCropVariantCount = Mathf.Max(1, cropVariantCount);

            return rules;
        }

        private TileSpawnWeights BuildSpawnWeights()
        {
            farmlandPercent = Mathf.Clamp(farmlandPercent, 0, 100);
            ecosystemPercent = Mathf.Clamp(ecosystemPercent, 0, 100);
            sanitationPercent = Mathf.Clamp(sanitationPercent, 0, 100);
            worstSanitationPercent = Mathf.Clamp(worstSanitationPercent, 0, 100);
            marinePercent = Mathf.Clamp(marinePercent, 0, 100);
            splashPercent = Mathf.Clamp(splashPercent, 0, 100);

            int total = farmlandPercent + ecosystemPercent + sanitationPercent + worstSanitationPercent + marinePercent + splashPercent;

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
                worstSanitationWeight = worstSanitationPercent * invTotal,
                marineWeight = marinePercent * invTotal,
                splashWeight = splashPercent * invTotal
            };
        }

        private void FaceSplippyTowards(Vector3 worldDirection)
        {
            if (!faceSplippyToMovement || splippy == null)
            {
                return;
            }

            worldDirection.y = 0f;

            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float targetYaw = GetCardinalYawFromWorldDirection(worldDirection);
            Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);

            if (splippyRotationTween.isAlive)
            {
                splippyRotationTween.Stop();
            }

            float duration = Mathf.Max(0f, splippyTurnDuration);

            if (duration <= 0f)
            {
                splippy.rotation = targetRotation;
                return;
            }

            splippyRotationTween = Tween.Rotation(splippy, targetRotation, duration, splippyTurnEase);
        }

        private static float GetCardinalYawFromWorldDirection(Vector3 worldDirection)
        {
            if (Mathf.Abs(worldDirection.x) >= Mathf.Abs(worldDirection.z))
            {
                return worldDirection.x >= 0f ? 0f : 180f;
            }

            // Grid-up (positive Z) maps to top and grid-down (negative Z) maps to down.
            return worldDirection.z < 0f ? 90f : -90f;
        }

        private void StartSplippyIdleSquashIfReady()
        {
            if (!enableSplippyIdleSquash || splippy == null || isMoving || !isActiveAndEnabled)
            {
                return;
            }

            if (splippyIdleSquashLoopActive)
            {
                return;
            }

            splippyIdleSquashLoopActive = true;
            PlaySplippyIdleSquashCycle();
        }

        private void PlaySplippyIdleSquashCycle()
        {
            if (!splippyIdleSquashLoopActive || splippy == null || isMoving)
            {
                return;
            }

            if (splippyIdleSquashTween.isAlive)
            {
                splippyIdleSquashTween.Stop();
            }

            float amount = Mathf.Clamp(splippyIdleSquashAmount, 0f, 0.45f);
            float halfDuration = Mathf.Max(0.05f, splippyIdleSquashHalfDuration);
            Vector3 targetScale = new Vector3(
                splippyBaseScale.x * (1f + amount),
                splippyBaseScale.y * (1f - amount),
                splippyBaseScale.z * (1f + amount));

            splippyIdleSquashTween = Tween.Scale(splippy, targetScale, halfDuration, splippyIdleSquashEase, cycles: 2, cycleMode: CycleMode.Yoyo)
                .OnComplete(() =>
                {
                    if (splippyIdleSquashLoopActive && !isMoving && splippy != null && isActiveAndEnabled)
                    {
                        PlaySplippyIdleSquashCycle();
                    }
                    else if (splippy != null)
                    {
                        splippy.localScale = splippyBaseScale;
                    }
                });
        }

        private void StopSplippyIdleSquash(bool resetScale)
        {
            splippyIdleSquashLoopActive = false;

            if (splippyIdleSquashTween.isAlive)
            {
                splippyIdleSquashTween.Stop();
            }

            if (resetScale && splippy != null)
            {
                splippy.localScale = splippyBaseScale;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            float halfSpan = (gridSize - 1) * 0.5f;
            float stride = cellSize + tilePadding;
            Vector3 gridCenter = GetGridCenterWorld();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    float worldX = (x - halfSpan) * stride;
                    float worldZ = (y - halfSpan) * stride;
                    Vector3 center = gridCenter + new Vector3(worldX, 0f, worldZ);
                    Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.02f, cellSize));
                }
            }
        }
    }
}
