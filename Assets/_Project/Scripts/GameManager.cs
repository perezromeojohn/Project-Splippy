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
        [SerializeField] private bool autoResolveHoverPathAfterMove = true;
        [SerializeField, Min(1)] private int firstStepWaterCost = 5;
        [SerializeField, Min(1)] private int secondStepWaterCost = 3;
        [SerializeField, Min(1)] private int sustainedStepWaterCost = 1;

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

        [Header("Drought")]
        [SerializeField, Min(1)] private int droughtEveryTurns = 5;
        [SerializeField, Min(0)] private int droughtHydrationLoss = 1;
        [SerializeField, Min(0)] private int droughtNewTilesCount = 3;

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
        private readonly List<Vector2Int> clearedSanitationSources = new List<Vector2Int>();
        private readonly List<Vector2Int> authoredGameplayPath = new List<Vector2Int>();

        private Vector2Int currentCell;
        private bool isMoving;
        private bool resolveHoverPathOnIdle;
        private bool suppressGameplayClick;
        private Vector3 splippyBaseScale = Vector3.one;
        private int completedTurns;
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

            tileBoardSystem = new TileBoardSystem(gridSize, tileRules, BuildBoardTurnRules(), BuildSpawnWeights());
            completedTurns = 0;
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

                boardView.ClearHoverPathPreview();
                return;
            }

            if (currentPhase != GamePhase.Lobby && currentPhase != GamePhase.Gameplay)
            {
                boardView.ClearHoverPathPreview();
                return;
            }

            if (currentPhase == GamePhase.Gameplay && runState != null && runState.IsGameOver)
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

            if (currentPhase == GamePhase.Gameplay && resolveHoverPathOnIdle)
            {
                resolveHoverPathOnIdle = false;
            }

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
                    boardView.ClearHoverPathPreview();
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
            int waterRemainingSteps = GetMaxAffordableAdditionalSteps(existingSteps);
            int stepBudget = Mathf.Min(rangeRemaining, waterRemainingSteps);

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

            var path = new List<Vector2Int>(authoredGameplayPath);
            boardView.ClearHoverPathPreview();
            clearedSanitationSources.Clear();
            isMoving = true;
            resolveHoverPathOnIdle = false;
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
            resolveHoverPathOnIdle = false;
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
            completedTurns = 0;

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
                    boardView.PlayTileReplacementFlip(cell, type);
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
            resolveHoverPathOnIdle = false;
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
            resolveHoverPathOnIdle = false;
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
            boardView.ClearHoverPathPreview();

            if (!GridPathfinder.TryFindPathBfs(gridSize, currentCell, targetCell, IsCellWalkable, out List<Vector2Int> path))
            {
                return;
            }

            if (path.Count <= 1)
            {
                return;
            }

            clearedSanitationSources.Clear();
            isMoving = true;
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
                    resolveHoverPathOnIdle = autoResolveHoverPathAfterMove;
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
                            bool hopGameOver = currentPhase == GamePhase.Gameplay && runState != null && runState.ApplyHopCost(GetStepWaterCost(index), evaluateGameOver: false);

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

                            if (hopGameOver)
                            {
                                isMoving = false;
                                return;
                            }

                            if (tileBoardSystem != null)
                            {
                                TileStepResult stepResult = tileBoardSystem.ProcessStep(nextCell);
                                bool stepGameOver = runState != null && runState.ApplyStepOutcome(
                                    stepResult.EnteredType,
                                    stepResult.LandingResult,
                                    stepResult.MarineConsumed,
                                    stepResult.ConnectedClusterSize);

                                if (stepResult.EnteredType == TileType.Sanitation && stepResult.LandingResult.LandedCellBloomed)
                                {
                                    clearedSanitationSources.Add(nextCell);
                                }

                                if (stepResult.LandingResult.LandedCellBloomed)
                                {
                                    boardView.PlayTileTapFeedback(nextCell);
                                }

                                boardView.PlayTileLandingFeedback(nextCell);

                                for (int i = 0; i < stepResult.LandingResult.DecayedCells.Count; i++)
                                {
                                    boardView.PlayTileLandingFeedback(stepResult.LandingResult.DecayedCells[i]);
                                }

                                for (int i = 0; i < stepResult.LandingResult.PollutedCells.Count; i++)
                                {
                                    boardView.PlayTileLandingFeedback(stepResult.LandingResult.PollutedCells[i]);
                                }

                                if (stepResult.CurrentType != stepResult.EnteredType)
                                {
                                    boardView.PlayTileReplacementFlip(nextCell, stepResult.CurrentType);
                                }

                                boardView.RefreshProgressVisuals(tileBoardSystem);

                                if (stepGameOver)
                                {
                                    isMoving = false;
                                    return;
                                }
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

            List<Vector2Int> spawnedSanitation = tileBoardSystem.SpreadSanitationToAdjacent(clearedSanitationSources, currentCell);
            clearedSanitationSources.Clear();

            for (int i = 0; i < spawnedSanitation.Count; i++)
            {
                boardView.PlayTileReplacementFlip(spawnedSanitation[i], TileType.Sanitation, pulseAfterReplace: true);
            }

            if (spawnedSanitation.Count > 0)
            {
                boardView.RefreshProgressVisuals(tileBoardSystem);
            }

            completedTurns++;

            if (droughtEveryTurns > 0 && completedTurns % droughtEveryTurns == 0)
            {
                DroughtResult drought = tileBoardSystem.ApplyDrought(currentCell, droughtHydrationLoss, droughtNewTilesCount);

                for (int i = 0; i < drought.DehydratedCells.Count; i++)
                {
                    boardView.PlayTileLandingFeedback(drought.DehydratedCells[i]);
                }

                foreach (KeyValuePair<Vector2Int, TileType> replacement in drought.ReplacedTiles)
                {
                    boardView.PlayTileReplacementFlip(replacement.Key, replacement.Value);
                }

                if (drought.DehydratedCells.Count > 0 || drought.ReplacedTiles.Count > 0)
                {
                    boardView.RefreshProgressVisuals(tileBoardSystem);
                }
            }
        }

        private int GetStepWaterCost(int stepIndexInPath)
        {
            if (stepIndexInPath <= 1)
            {
                return Mathf.Max(1, firstStepWaterCost);
            }

            if (stepIndexInPath == 2)
            {
                return Mathf.Max(1, secondStepWaterCost);
            }

            return Mathf.Max(1, sustainedStepWaterCost);
        }

        private int GetMaxAffordableAdditionalSteps(int existingSteps)
        {
            if (runState == null)
            {
                return Mathf.Max(1, maxGameplayPathRange);
            }

            int water = Mathf.Max(0, runState.CurrentWaterReserve);
            int additional = 0;
            int safetyCap = Mathf.Max(1, maxGameplayPathRange);

            while (additional < safetyCap)
            {
                int stepIndex = existingSteps + additional + 1;
                int cost = GetStepWaterCost(stepIndex);

                if (water < cost)
                {
                    break;
                }

                water -= cost;
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
