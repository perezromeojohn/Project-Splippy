using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace projectsplippy
{
    public class PreGameFlowController : MonoBehaviour
    {
        [Header("Center Tile Prefab")]
        [SerializeField] private GameObject startGameTilePrefab;
        [SerializeField, Min(0)] private int lobbyExtraTilesOutsideLeft = 2;

        [Header("Outside Tile Exit")]
        [SerializeField] private float outerLobbyTileSinkDistance = 2f;
        [SerializeField] private float outerLobbyTileSinkDuration = 0.45f;
        [SerializeField] private Ease outerLobbyTileSinkEase = Ease.InCubic;

        [Header("Camera Tween On Center Trigger")]
        [SerializeField] private bool tweenCameraOnCenterTrigger = true;
        [SerializeField] private Vector3 centerTriggerCameraPosition = new Vector3(0f, 7f, -5f);
        [SerializeField] private Vector3 centerTriggerCameraEuler = new Vector3(60f, 0f, 0f);
        [SerializeField] private Ease cameraTweenEase = Ease.InOutSine;

        [Header("Start Sequence")]
        [SerializeField] private float bloomRingStepDelay = 0.18f;
        [SerializeField] private int countdownStart = 3;

        public MainMenuAnimator mainMenuAnimator;
        public bool CanAcceptLobbyMoveInput => mainMenuAnimator == null || mainMenuAnimator.AllowSplippyTapMove || lobbyMoveAllowed;
        private GameManager gameManager;
        private Vector2Int lineStartCell;
        private Vector2Int startCell;
        private bool startSequenceRunning;
        private bool lobbyMoveAllowed;
        private readonly List<Vector2Int> lobbyOuterCells = new List<Vector2Int>();

        public void Begin(GameManager manager)
        {
            gameManager = manager;
            startSequenceRunning = false;
            BuildLobbyBoard();
            mainMenuAnimator.OnEntry();
        }

        public void BeginLobby(GameManager manager)
        {
            gameManager = manager;
            startSequenceRunning = false;
            lobbyMoveAllowed = true;
            BuildLobbyBoard();
        }

        public void HandleLobbyLanding(Vector2Int cell)
        {
            if (cell == startCell && !startSequenceRunning)
            {
                StartCoroutine(RunStartSequence());
            }
        }

        private void BuildLobbyBoard()
        {
            int gridSize = gameManager.GridSize;
            int centerX = gridSize / 2;
            int centerY = gridSize / 2;

            int outsideLeft = Mathf.Max(0, lobbyExtraTilesOutsideLeft);
            int lineStartX = -outsideLeft;
            int lineEndX = centerX;

            lineStartCell = new Vector2Int(lineStartX, centerY);
            startCell = new Vector2Int(centerX, centerY);
            lobbyOuterCells.Clear();

            var walkable = new HashSet<Vector2Int>();

            for (int x = lineStartX; x <= lineEndX; x++)
            {
                Vector2Int cell = new Vector2Int(x, centerY);
                walkable.Add(cell);

                if (x < 0 || x >= gridSize)
                {
                    lobbyOuterCells.Add(cell);
                }
            }

            var overrides = new Dictionary<Vector2Int, GameObject>();

            if (startGameTilePrefab != null)
            {
                overrides[startCell] = startGameTilePrefab;
            }

            gameManager.ConfigureLobby(walkable, overrides, lineStartCell);
            Debug.Log($"[Lobby] Line lobby ready (range={lineStartX}-{lineEndX}, outer={lobbyOuterCells.Count}). Step center tile to start game.");
        }

        private IEnumerator RunStartSequence()
        {
            startSequenceRunning = true;

            if (lobbyOuterCells.Count > 0)
            {
                yield return StartCoroutine(gameManager.TweenAndRemoveLobbyCells(
                    lobbyOuterCells,
                    outerLobbyTileSinkDistance,
                    outerLobbyTileSinkDuration,
                    outerLobbyTileSinkEase));
            }

            if (tweenCameraOnCenterTrigger)
            {
                StartCameraTweenToCenterTriggerView();
            }

            Debug.Log("[Lobby] Start tile stepped. Bloom reveal started...");

            yield return StartCoroutine(gameManager.StartGameplayBloomReveal(bloomRingStepDelay));

            gameManager.BeginCountdownPhase();

            int start = Mathf.Max(1, countdownStart);

            for (int i = start; i >= 1; i--)
            {
                Debug.Log($"[Countdown] {i}");
                yield return new WaitForSeconds(1f);
            }

            Debug.Log("[Countdown] GO");
            gameManager.BeginGameplayPhase();
            startSequenceRunning = false;
        }

        private void StartCameraTweenToCenterTriggerView()
        {
            Camera activeCamera = Camera.main;

            if (activeCamera == null)
            {
                return;
            }

            float duration = gameManager != null ? gameManager.GetBloomRevealDuration(bloomRingStepDelay) : 0f;
            duration = Mathf.Max(0f, duration);
            Quaternion targetRotation = Quaternion.Euler(centerTriggerCameraEuler);

            if (duration <= 0f)
            {
                activeCamera.transform.SetPositionAndRotation(centerTriggerCameraPosition, targetRotation);
                return;
            }

            Tween.Position(activeCamera.transform, centerTriggerCameraPosition, duration, cameraTweenEase);
            Tween.Rotation(activeCamera.transform, targetRotation, duration, cameraTweenEase);
        }
    }
}
