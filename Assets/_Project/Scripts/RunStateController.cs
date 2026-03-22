using TMPro;
using UnityEngine;

namespace projectsplippy
{
    public class RunStateController : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private int startingDroplets = 10;

        [Header("Droplet Costs")]
        [SerializeField] private int hopCost = 1;
        [SerializeField] private int marineClearCost = 10;
        [SerializeField] private int sanitationInfectCost = 10;

        [Header("Droplet Rewards")]
        [SerializeField] private int pollutedSanitationClearReward = 5;
        [SerializeField] private int marineClearReward = 20;
        [SerializeField] private int singleClearReward = 5;
        [SerializeField] private int chain2Reward = 15;
        [SerializeField] private int chain3Reward = 25;
        [SerializeField] private int chain4PlusReward = 40;

        [Header("UI")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text waterText;

        public bool IsGameOver { get; private set; }
        public int CurrentScore { get; private set; }
        public int CurrentWaterReserve { get; private set; }

        public void Initialize()
        {
            startingDroplets = Mathf.Max(1, startingDroplets);
            hopCost = Mathf.Max(0, hopCost);
            marineClearCost = Mathf.Max(0, marineClearCost);
            sanitationInfectCost = Mathf.Max(0, sanitationInfectCost);
            pollutedSanitationClearReward = Mathf.Max(0, pollutedSanitationClearReward);
            marineClearReward = Mathf.Max(0, marineClearReward);
            singleClearReward = Mathf.Max(0, singleClearReward);
            chain2Reward = Mathf.Max(0, chain2Reward);
            chain3Reward = Mathf.Max(0, chain3Reward);
            chain4PlusReward = Mathf.Max(0, chain4PlusReward);

            IsGameOver = false;
            CurrentScore = 0;
            CurrentWaterReserve = startingDroplets;
            RefreshHud();
        }

        public bool ApplyHopCost(int hops = 1, bool evaluateGameOver = true)
        {
            if (IsGameOver)
            {
                return true;
            }

            int hopCount = Mathf.Max(1, hops);
            CurrentWaterReserve -= hopCount * hopCost;
            CurrentScore -= hopCount * hopCost;
            RefreshHud();

            if (evaluateGameOver && CurrentWaterReserve <= 0)
            {
                TriggerGameOver();
            }

            return IsGameOver;
        }

        public bool ApplyLandingOutcome(TileType landedType, TileLandingResult landingResult, int chainSize)
        {
            if (IsGameOver)
            {
                return true;
            }

            int delta = 0;

            if (landingResult != null)
            {
                delta -= Mathf.Max(0, landingResult.PollutedCells.Count) * sanitationInfectCost;

                if (landingResult.LandedCellBloomed)
                {
                    delta += singleClearReward;

                    if (landedType == TileType.Sanitation && landingResult.LandedCellWasPolluted)
                    {
                        delta += pollutedSanitationClearReward;
                    }

                    if (landedType == TileType.Marine)
                    {
                        delta -= marineClearCost;
                        delta += marineClearReward;
                    }

                    delta += GetChainReward(chainSize);
                }
            }

            CurrentWaterReserve += delta;
            CurrentScore += delta;
            RefreshHud();

            if (CurrentWaterReserve <= 0)
            {
                TriggerGameOver();
            }

            return IsGameOver;
        }

        private int GetChainReward(int chainSize)
        {
            if (chainSize >= 4)
            {
                return chain4PlusReward;
            }

            if (chainSize == 3)
            {
                return chain3Reward;
            }

            if (chainSize == 2)
            {
                return chain2Reward;
            }

            return 0;
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
                waterText.text = $"Droplets: {CurrentWaterReserve} - GAME OVER";
            }
        }

        private void RefreshHud()
        {
            if (scoreText != null)
            {
                scoreText.text = $"Net: {CurrentScore}";
            }

            if (waterText != null)
            {
                waterText.text = $"Droplets: {CurrentWaterReserve}";
            }
        }
    }
}
