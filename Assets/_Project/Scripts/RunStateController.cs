using TMPro;
using UnityEngine;

namespace projectsplippy
{
    public class RunStateController : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private int startingDroplets = 100;
        [SerializeField] private int maxDroplets = 100;

        [Header("Droplet Costs")]
        [SerializeField] private int hopCost = 1;
        [SerializeField] private int sanitationInfectCost = 10;

        [Header("Droplet Rewards")]
        [SerializeField] private int farmlandBloomReward = 5;
        [SerializeField] private int ecosystemBloomReward = 10;
        [SerializeField] private int sanitationBloomReward = 5;
        [SerializeField] private int marineStepReward = 20;

        [Header("Score")]
        [SerializeField] private int clearAnyTileScore = 5;
        [SerializeField] private int pollutedSanitationClearScore = 5;
        [SerializeField] private int marineClearScore = 20;
        [SerializeField] private int chain2Score = 15;
        [SerializeField] private int chain3Score = 25;
        [SerializeField] private int chain4PlusScore = 40;

        [Header("UI")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text waterText;

        public bool IsGameOver { get; private set; }
        public int CurrentWaterReserve { get; private set; }
        public int CurrentScore { get; private set; }

        public void Initialize()
        {
            startingDroplets = Mathf.Max(1, startingDroplets);
            maxDroplets = Mathf.Max(startingDroplets, maxDroplets);
            hopCost = Mathf.Max(0, hopCost);
            sanitationInfectCost = Mathf.Max(0, sanitationInfectCost);
            farmlandBloomReward = Mathf.Max(0, farmlandBloomReward);
            ecosystemBloomReward = Mathf.Max(0, ecosystemBloomReward);
            sanitationBloomReward = Mathf.Max(0, sanitationBloomReward);
            marineStepReward = Mathf.Max(0, marineStepReward);
            clearAnyTileScore = Mathf.Max(0, clearAnyTileScore);
            pollutedSanitationClearScore = Mathf.Max(0, pollutedSanitationClearScore);
            marineClearScore = Mathf.Max(0, marineClearScore);
            chain2Score = Mathf.Max(0, chain2Score);
            chain3Score = Mathf.Max(0, chain3Score);
            chain4PlusScore = Mathf.Max(0, chain4PlusScore);

            IsGameOver = false;
            CurrentWaterReserve = startingDroplets;
            CurrentScore = 0;
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
            RefreshHud();

            if (evaluateGameOver && CurrentWaterReserve <= 0)
            {
                TriggerGameOver();
            }

            return IsGameOver;
        }

        public bool ApplyStepOutcome(TileType enteredType, TileLandingResult landingResult, bool marineConsumed, int connectedClusterSize)
        {
            if (IsGameOver)
            {
                return true;
            }

            int delta = 0;
            int scoreDelta = 0;
            bool isFarmOrEcoStep = enteredType == TileType.Farmland || enteredType == TileType.Ecosystem;
            bool clearedThisStep = marineConsumed;
            bool canScore = enteredType != TileType.Filler && enteredType != TileType.Rock;

            if (landingResult != null)
            {
                if (!isFarmOrEcoStep)
                {
                    delta -= Mathf.Max(0, landingResult.PollutedCells.Count) * sanitationInfectCost;
                }

                if (landingResult.LandedCellBloomed)
                {
                    clearedThisStep = true;

                    switch (enteredType)
                    {
                        case TileType.Farmland:
                            delta += farmlandBloomReward;
                            break;
                        case TileType.Ecosystem:
                            delta += ecosystemBloomReward;
                            break;
                        case TileType.Sanitation:
                            delta += sanitationBloomReward;
                            break;
                    }

                    if (canScore)
                    {
                        scoreDelta += clearAnyTileScore;
                    }

                    if (enteredType == TileType.Sanitation && landingResult.LandedCellWasPolluted)
                    {
                        scoreDelta += pollutedSanitationClearScore;
                    }
                }
            }

            if (marineConsumed)
            {
                delta += marineStepReward;

                if (canScore)
                {
                    scoreDelta += marineClearScore;
                }
            }

            if (clearedThisStep && canScore)
            {
                scoreDelta += GetChainScore(connectedClusterSize);
            }

            CurrentWaterReserve = Mathf.Clamp(CurrentWaterReserve + delta, 0, maxDroplets);
            CurrentScore += Mathf.Max(0, scoreDelta);
            RefreshHud();

            if (CurrentWaterReserve <= 0)
            {
                TriggerGameOver();
            }

            return IsGameOver;
        }

        private int GetChainScore(int clusterSize)
        {
            if (clusterSize >= 4)
            {
                return chain4PlusScore;
            }

            if (clusterSize == 3)
            {
                return chain3Score;
            }

            if (clusterSize == 2)
            {
                return chain2Score;
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
                scoreText.text = $"Score: {CurrentScore}";
            }

            if (waterText != null)
            {
                waterText.text = $"Droplets: {CurrentWaterReserve}";
            }
        }
    }
}
