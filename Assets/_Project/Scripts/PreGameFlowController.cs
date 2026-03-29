using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;

namespace projectsplippy
{
    public class PreGameFlowController : MonoBehaviour
    {
        [Header("Special Tile Prefabs")]
        [SerializeField] private GameObject settingsTilePrefab;
        [SerializeField] private GameObject creditsTilePrefab;
        [SerializeField] private GameObject startGameTilePrefab;

        [Header("Start Sequence")]
        [SerializeField] private float bloomRingStepDelay = 0.18f;
        [SerializeField] private int countdownStart = 3;

        public MainMenuAnimator mainMenuAnimator;
        private GameManager gameManager;
        private Vector2Int settingsCell;
        private Vector2Int creditsCell;
        private Vector2Int startCell;
        private bool startSequenceRunning;

        public void Begin(GameManager manager)
        {
            gameManager = manager;
            startSequenceRunning = false;
            BuildLobbyBoard();
            mainMenuAnimator.OnEntry();
        }

        public void HandleLobbyLanding(Vector2Int cell)
        {
            if (cell == settingsCell)
            {
                Debug.Log("[Lobby] Settings tile stepped. Open settings menu.");
                return;
            }

            if (cell == creditsCell)
            {
                Debug.Log("[Lobby] Credits tile stepped. Show credits.");
                return;
            }

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

            settingsCell = new Vector2Int(0, 0);
            creditsCell = new Vector2Int(gridSize - 1, 0);
            startCell = new Vector2Int(centerX, centerY);

            var walkable = new HashSet<Vector2Int>();

            for (int x = 0; x < gridSize; x++)
            {
                walkable.Add(new Vector2Int(x, 0));
            }

            for (int y = 0; y <= centerY; y++)
            {
                walkable.Add(new Vector2Int(centerX, y));
            }

            var overrides = new Dictionary<Vector2Int, GameObject>();

            if (settingsTilePrefab != null)
            {
                overrides[settingsCell] = settingsTilePrefab;
            }

            if (creditsTilePrefab != null)
            {
                overrides[creditsCell] = creditsTilePrefab;
            }

            if (startGameTilePrefab != null)
            {
                overrides[startCell] = startGameTilePrefab;
            }

            gameManager.ConfigureLobby(walkable, overrides, gameManager.BottomCenterCell);
            Debug.Log("[Lobby] T-shape ready. Step center tile to start game.");
        }

        private IEnumerator RunStartSequence()
        {
            startSequenceRunning = true;
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
    }
}
