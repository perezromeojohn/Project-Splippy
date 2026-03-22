using TMPro;
using UnityEngine;

namespace projectsplippy
{
    public class RunStateController : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private int maxWaterReserve = 10;
        [SerializeField] private int scorePerLanding = 1;

        [Header("UI")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text waterText;

        public bool IsGameOver { get; private set; }
        public int CurrentScore { get; private set; }
        public int CurrentWaterReserve { get; private set; }

        public void Initialize()
        {
            maxWaterReserve = Mathf.Max(1, maxWaterReserve);
            scorePerLanding = Mathf.Max(1, scorePerLanding);

            IsGameOver = false;
            CurrentScore = 0;
            CurrentWaterReserve = maxWaterReserve;
            RefreshHud();
        }

        public bool ApplyLanding(TileType landedType, int adjacencyBonusScore)
        {
            if (IsGameOver)
            {
                return true;
            }

            CurrentWaterReserve = Mathf.Max(0, CurrentWaterReserve - 1);

            if (landedType == TileType.Marine)
            {
                CurrentWaterReserve = maxWaterReserve;
            }

            CurrentScore += scorePerLanding + Mathf.Max(0, adjacencyBonusScore);
            RefreshHud();

            if (CurrentWaterReserve <= 0)
            {
                TriggerGameOver();
            }

            return IsGameOver;
        }

        private void TriggerGameOver()
        {
            if (IsGameOver)
            {
                return;
            }

            IsGameOver = true;

            if (waterText != null)
            {
                waterText.text = $"Water: {CurrentWaterReserve}/{maxWaterReserve} - GAME OVER";
            }
        }

        private void RefreshHud()
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {CurrentScore}";
            }

            if (waterText != null)
            {
                waterText.text = $"Water: {CurrentWaterReserve}/{maxWaterReserve}";
            }
        }
    }
}
