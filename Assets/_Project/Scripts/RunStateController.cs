using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace projectsplippy
{
    public class RunStateController : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private int startingDroplets = 100;
        [SerializeField] private int maxDroplets = 100;

        [Header("Economy")]
        [SerializeField, Min(0)] private int clickCost = 15;
        [SerializeField, Min(0)] private int pathTileCost = 1;
        [SerializeField, Min(0)] private int streakIncreaseRefund = 2;
        [SerializeField, Min(0)] private int marineReward = 20;

        [Header("Debug")]
        [SerializeField] private bool logPathScoreDebug = true;

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
            clickCost = Mathf.Max(0, clickCost);
            pathTileCost = Mathf.Max(0, pathTileCost);
            streakIncreaseRefund = Mathf.Max(0, streakIncreaseRefund);
            marineReward = Mathf.Max(0, marineReward);

            IsGameOver = false;
            CurrentWaterReserve = startingDroplets;
            CurrentScore = 0;
            RefreshHud();
        }

        public bool CanAffordPath(int tileSteps)
        {
            int steps = Mathf.Max(0, tileSteps);
            int totalCost = clickCost + (steps * pathTileCost);
            return CurrentWaterReserve >= totalCost;
        }

        public bool ApplyPathClickCost()
        {
            return ApplyEconomyAndScore(-clickCost, 0);
        }

        public bool ApplyPathResolution(IReadOnlyList<TileStepResult> stepResults, IReadOnlyList<string> collisionOrder = null)
        {
            if (IsGameOver || stepResults == null || stepResults.Count == 0)
            {
                return IsGameOver;
            }

            int baseScore = 0;
            int sanitationTouches = 0;
            int marineTouches = 0;
            int streakIncreaseEvents = 0;
            int? previousEffectiveCropVariant = null;
            int streakLength = 0;
            var debugParts = logPathScoreDebug ? new List<string>(stepResults.Count) : null;

            for (int i = 0; i < stepResults.Count; i++)
            {
                TileStepResult step = stepResults[i];
                int awarded = 0;

                if (step.EnteredType == TileType.Sanitation)
                {
                    sanitationTouches++;
                }

                if (step.EnteredType == TileType.Marine)
                {
                    marineTouches++;
                }

                int? effectiveCropVariant = ResolveEffectiveCropVariant(step, previousEffectiveCropVariant);
                string label = ResolveDebugLabel(step, collisionOrder, i);

                if (!effectiveCropVariant.HasValue)
                {
                    awarded = 1;
                    baseScore += awarded;
                    previousEffectiveCropVariant = null;
                    streakLength = 0;

                    if (debugParts != null)
                    {
                        debugParts.Add($"+{awarded} {label}");
                    }

                    continue;
                }

                if (previousEffectiveCropVariant.HasValue && effectiveCropVariant.Value == previousEffectiveCropVariant.Value)
                {
                    streakLength = Mathf.Max(1, streakLength + 1);
                    awarded = streakLength;
                    baseScore += awarded;
                    streakIncreaseEvents++;
                }
                else
                {
                    streakLength = 1;
                    awarded = 1;
                    baseScore += awarded;
                }

                previousEffectiveCropVariant = effectiveCropVariant.Value;

                if (debugParts != null)
                {
                    debugParts.Add($"+{awarded} {label}");
                }
            }

            int scoreMultiplier = 1;

            for (int i = 0; i < sanitationTouches; i++)
            {
                scoreMultiplier *= 2;
            }

            int scoreDelta = baseScore * scoreMultiplier;
            int traversalCost = stepResults.Count * pathTileCost;
            int waterDelta = (streakIncreaseEvents * streakIncreaseRefund) + (marineTouches * marineReward) - traversalCost;

            if (debugParts != null)
            {
                string chain = string.Join(" -> ", debugParts);
                Debug.Log($"PathScoreDebug: {chain} | base={baseScore} | sanitation x{scoreMultiplier} | final={scoreDelta} | waterDelta={waterDelta}");
            }

            return ApplyEconomyAndScore(waterDelta, scoreDelta);
        }

        public bool ApplyEconomyAndScore(int waterDelta, int scoreDelta, bool clampToReservoir = true)
        {
            if (IsGameOver)
            {
                return true;
            }

            CurrentWaterReserve += waterDelta;

            if (clampToReservoir)
            {
                CurrentWaterReserve = Mathf.Clamp(CurrentWaterReserve, 0, maxDroplets);
            }

            CurrentScore += Mathf.Max(0, scoreDelta);
            RefreshHud();

            if (CurrentWaterReserve == 0)
            {
                TriggerGameOver();
            }

            return IsGameOver;
        }

        private static int? ResolveEffectiveCropVariant(TileStepResult step, int? previousEffectiveCropVariant)
        {
            if (step.EnteredType == TileType.Farmland)
            {
                return step.EnteredCropVariantIndex < 0 ? (int?)null : step.EnteredCropVariantIndex;
            }

            if (step.EnteredType == TileType.Ecosystem)
            {
                return previousEffectiveCropVariant;
            }

            return null;
        }

        private static string ResolveDebugLabel(TileStepResult step, IReadOnlyList<string> collisionOrder, int index)
        {
            if (collisionOrder != null && index >= 0 && index < collisionOrder.Count && !string.IsNullOrWhiteSpace(collisionOrder[index]))
            {
                return collisionOrder[index];
            }

            return step.EnteredType.ToString();
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
