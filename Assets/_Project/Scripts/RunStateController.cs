using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Torrent")]
        [SerializeField, Min(1f)] private float torrentSliderHopPulseScale = 1.06f;
        [SerializeField, Min(0.02f)] private float torrentSliderHopPulseDuration = 0.1f;
        [SerializeField] private Ease torrentSliderHopPulseEase = Ease.OutSine;
        [SerializeField, Min(0.02f)] private float torrentSliderValueTweenDuration = 0.12f;
        [SerializeField, Min(1)] private int torrentChargeTarget = 24;
        [SerializeField, Min(1)] private int torrentDurationTurns = 3;
        [SerializeField, Min(1)] private int torrentPathRange = 10;
        [SerializeField, Min(1)] private int basePathRange = 7;
        [SerializeField, Min(1)] private int torrentScoreMultiplier = 3;

        [Header("Debug")]
        [SerializeField] private bool logPathScoreDebug = true;

        [Header("UI")]
        [SerializeField] private Slider torrentFlowSlider;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text sanitationSpawnTrackerText;
        [SerializeField] private TMP_Text torrentModeLabel;

        [Header("Torrent Mode Label")]
        [SerializeField] private bool showTorrentModeLabel = true;
        [SerializeField] private string torrentModeLabelText = "TORRENT MODE";
        [SerializeField, Min(0f)] private float torrentModeLabelHiddenScale = 0f;
        [SerializeField, Min(0.05f)] private float torrentModeLabelShownScale = 1f;
        [SerializeField, Min(0.02f)] private float torrentModeLabelTweenDuration = 0.2f;
        [SerializeField] private Ease torrentModeLabelTweenEase = Ease.OutBack;
        [SerializeField] private Ease torrentModeLabelHideEase = Ease.InBack;
        [SerializeField, Min(1f)] private float torrentModeLabelPulseScale = 1.08f;
        [SerializeField, Min(0.05f)] private float torrentModeLabelPulseDuration = 0.55f;
        [SerializeField] private Ease torrentModeLabelPulseEase = Ease.InOutSine;

        [Header("Preload UI Intro")]
        [SerializeField] private bool enablePreloadHudIntro = true;
        [SerializeField] private float preloadSliderYOffset = 50f;
        [SerializeField] private float preloadScoreYOffset = -50f;
        [SerializeField, Min(0.05f)] private float preloadHudIntroDuration = 0.45f;

        public bool IsGameOver { get; private set; }
        public int CurrentWaterReserve { get; private set; }
        public int CurrentScore { get; private set; }
        public bool IsTorrentActive => torrentTurnsLeft > 0;
        public int TorrentTurnsLeft => torrentTurnsLeft;
        public int BasePathRange => Mathf.Max(1, basePathRange);
        public int TorrentPathRange => Mathf.Max(BasePathRange, torrentPathRange);

        private int torrentCharge;
        private int torrentTurnsLeft;
        private bool torrentActivatedThisResolution;
        private Tween torrentSliderPulseTween;
        private Vector3 torrentSliderBaseScale = Vector3.one;
        private bool hasCachedTorrentSliderScale;
        private Coroutine torrentSliderValueTweenRoutine;
        private RectTransform cachedSliderRect;
        private RectTransform cachedScoreRect;
        private Vector2 cachedSliderAnchoredPosition;
        private Vector2 cachedScoreAnchoredPosition;
        private bool hasCachedHudPositions;
        private Coroutine preloadHudIntroRoutine;
        private Tween torrentModeLabelTween;
        private Tween torrentModeLabelPulseTween;
        private Vector3 torrentModeLabelBaseScale = Vector3.one;
        private bool hasCachedTorrentModeLabelScale;
        private bool torrentModeLabelVisibleStateKnown;
        private bool torrentModeLabelVisible;

        private bool chargePreviewActive;
        private int previewCharge;
        private int? previewPreviousEffectiveCropVariant;
        private int previewStreakLength;

        public void Initialize()
        {
            startingDroplets = Mathf.Max(1, startingDroplets);
            maxDroplets = Mathf.Max(startingDroplets, maxDroplets);
            clickCost = Mathf.Max(0, clickCost);
            pathTileCost = Mathf.Max(0, pathTileCost);
            streakIncreaseRefund = Mathf.Max(0, streakIncreaseRefund);
            marineReward = Mathf.Max(0, marineReward);
            torrentChargeTarget = Mathf.Max(1, torrentChargeTarget);
            torrentDurationTurns = Mathf.Max(1, torrentDurationTurns);
            torrentPathRange = Mathf.Max(1, torrentPathRange);
            basePathRange = Mathf.Max(1, basePathRange);
            torrentScoreMultiplier = Mathf.Max(1, torrentScoreMultiplier);

            IsGameOver = false;
            CurrentWaterReserve = startingDroplets;
            CurrentScore = 0;
            torrentCharge = 0;
            torrentTurnsLeft = 0;
            torrentActivatedThisResolution = false;
            CacheTorrentSliderScale();
            TryAutoAssignTorrentModeLabel();
            CacheTorrentModeLabelScale();
            torrentModeLabelVisibleStateKnown = false;
            UpdateTorrentModeLabelState(IsTorrentActive, immediate: true);

            if (torrentFlowSlider != null)
            {
                torrentFlowSlider.transform.localScale = torrentSliderBaseScale;
            }

            RefreshHud();
        }

        public bool CanAffordPath(int tileSteps)
        {
            return true;
        }

        public bool ApplyPathClickCost()
        {
            return IsGameOver;
        }

        public bool ApplyPathResolution(IReadOnlyList<TileStepResult> stepResults, IReadOnlyList<string> collisionOrder = null)
        {
            if (IsGameOver || stepResults == null || stepResults.Count == 0)
            {
                return IsGameOver;
            }

            bool torrentWasActive = IsTorrentActive;
            torrentActivatedThisResolution = false;

            int baseScore = 0;
            int sanitationTouches = 0;
            int marineTouches = 0;
            int streakIncreaseEvents = 0;
            int? previousEffectiveCropVariant = null;
            int streakLength = 0;
            int streakChargeGain = 0;
            var debugParts = logPathScoreDebug ? new List<string>(stepResults.Count) : null;

            for (int i = 0; i < stepResults.Count; i++)
            {
                TileStepResult step = stepResults[i];
                int awarded = CalculateStepAward(step, ref previousEffectiveCropVariant, ref streakLength);

                if (step.EnteredType == TileType.Sanitation || step.EnteredType == TileType.WorstSanitation)
                {
                    sanitationTouches++;
                }

                if (step.EnteredType == TileType.Marine)
                {
                    marineTouches++;
                }
                string label = ResolveDebugLabel(step, collisionOrder, i);

                if (awarded > 1)
                {
                    streakIncreaseEvents++;
                }

                baseScore += awarded;

                streakChargeGain += awarded;

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

            if (torrentWasActive)
            {
                scoreDelta *= torrentScoreMultiplier;
            }

            if (debugParts != null)
            {
                string chain = string.Join(" -> ", debugParts);
                Debug.Log($"PathScoreDebug: {chain} | base={baseScore} | sanitation x{scoreMultiplier} | final={scoreDelta}");
            }

            bool gameOver = ApplyEconomyAndScore(0, scoreDelta);

            if (!gameOver)
            {
                if (torrentWasActive)
                {
                    AdvanceTorrentTurn();
                }

                AddTorrentCharge(streakChargeGain);
                RefreshHud();
            }

            return gameOver;
        }

        public IReadOnlyList<int> BuildStepScorePreview(IReadOnlyList<TileStepResult> stepResults)
        {
            var awards = new List<int>(stepResults != null ? stepResults.Count : 0);

            if (stepResults == null)
            {
                return awards;
            }

            int? previousEffectiveCropVariant = null;
            int streakLength = 0;

            for (int i = 0; i < stepResults.Count; i++)
            {
                awards.Add(CalculateStepAward(stepResults[i], ref previousEffectiveCropVariant, ref streakLength));
            }

            return awards;
        }

        public bool ApplyEconomyAndScore(int waterDelta, int scoreDelta, bool clampToReservoir = true)
        {
            if (IsGameOver)
            {
                return true;
            }

            CurrentScore += Mathf.Max(0, scoreDelta);
            RefreshHud();

            return IsGameOver;
        }

        public void TriggerSoftLockGameOver()
        {
            TriggerGameOver();
        }

        public bool ConsumeTorrentActivationFlag()
        {
            bool activated = torrentActivatedThisResolution;
            torrentActivatedThisResolution = false;
            return activated;
        }

        public void BeginPathChargePreview()
        {
            chargePreviewActive = true;
            previewCharge = torrentCharge;
            previewPreviousEffectiveCropVariant = null;
            previewStreakLength = 0;
            UpdateTorrentSliderValueWithTween(GetSliderNormalizedValueForCharge(previewCharge), immediate: true);
        }

        public void PrepareHudForPreload()
        {
            CacheHudIntroPositions();

            if (scoreText != null)
            {
                scoreText.text = "Score: 0";
            }

            if (torrentFlowSlider != null)
            {
                torrentFlowSlider.minValue = 0f;
                torrentFlowSlider.maxValue = 1f;
                torrentFlowSlider.value = 0f;
            }

            UpdateTorrentModeLabelState(false, immediate: true);

            if (!enablePreloadHudIntro)
            {
                return;
            }

            if (preloadHudIntroRoutine != null)
            {
                StopCoroutine(preloadHudIntroRoutine);
                preloadHudIntroRoutine = null;
            }

            if (cachedSliderRect != null)
            {
                cachedSliderRect.anchoredPosition = cachedSliderAnchoredPosition + new Vector2(0f, preloadSliderYOffset);
            }

            if (cachedScoreRect != null)
            {
                cachedScoreRect.anchoredPosition = cachedScoreAnchoredPosition + new Vector2(0f, preloadScoreYOffset);
            }
        }

        public void PlayHudIntroFromPreload()
        {
            CacheHudIntroPositions();

            if (!enablePreloadHudIntro)
            {
                if (cachedSliderRect != null)
                {
                    cachedSliderRect.anchoredPosition = cachedSliderAnchoredPosition;
                }

                if (cachedScoreRect != null)
                {
                    cachedScoreRect.anchoredPosition = cachedScoreAnchoredPosition;
                }

                return;
            }

            if (preloadHudIntroRoutine != null)
            {
                StopCoroutine(preloadHudIntroRoutine);
            }

            preloadHudIntroRoutine = StartCoroutine(TweenHudIntroRoutine());
        }

        public void PreviewPathStepCharge(TileStepResult step)
        {
            if (!chargePreviewActive || step == null)
            {
                return;
            }

            if (IsTorrentActive)
            {
                UpdateTorrentSliderValueWithTween(1f);
                return;
            }

            int awarded = CalculateStepAward(step, ref previewPreviousEffectiveCropVariant, ref previewStreakLength);
            previewCharge = Mathf.Clamp(previewCharge + Mathf.Max(0, awarded), 0, torrentChargeTarget);
            UpdateTorrentSliderValueWithTween(GetSliderNormalizedValueForCharge(previewCharge));
        }

        public void EndPathChargePreview()
        {
            chargePreviewActive = false;
            previewPreviousEffectiveCropVariant = null;
            previewStreakLength = 0;
            previewCharge = torrentCharge;
            UpdateTorrentSliderValueWithTween(GetSliderNormalizedValueForCharge(previewCharge));
        }

        public void PlayHopSliderFeedback()
        {
            if (torrentFlowSlider == null)
            {
                return;
            }

            CacheTorrentSliderScale();

            if (torrentSliderPulseTween.isAlive)
            {
                torrentSliderPulseTween.Stop();
            }

            Transform sliderTransform = torrentFlowSlider.transform;
            sliderTransform.localScale = torrentSliderBaseScale;
            float halfDuration = Mathf.Max(0.01f, torrentSliderHopPulseDuration * 0.5f);
            float scaleMultiplier = Mathf.Max(1f, torrentSliderHopPulseScale);
            Vector3 pulseScale = torrentSliderBaseScale * scaleMultiplier;

            torrentSliderPulseTween = Tween.Scale(
                sliderTransform,
                pulseScale,
                halfDuration,
                torrentSliderHopPulseEase,
                cycles: 2,
                cycleMode: CycleMode.Yoyo);
        }

        public void SetNextSanitationSpawnIn(int turns)
        {
            if (sanitationSpawnTrackerText == null)
            {
                return;
            }

            sanitationSpawnTrackerText.text = $"Next Guck In: {Mathf.Max(0, turns)}";
        }

        private void AddTorrentCharge(int amount)
        {
            if (amount <= 0 || IsTorrentActive)
            {
                return;
            }

            torrentCharge = Mathf.Clamp(torrentCharge + amount, 0, torrentChargeTarget);

            if (torrentCharge >= torrentChargeTarget)
            {
                torrentTurnsLeft = torrentDurationTurns;
                torrentCharge = 0;
                torrentActivatedThisResolution = true;
            }
        }

        private void AdvanceTorrentTurn()
        {
            if (!IsTorrentActive)
            {
                return;
            }

            torrentTurnsLeft = Mathf.Max(0, torrentTurnsLeft - 1);
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

        private static int CalculateStepAward(TileStepResult step, ref int? previousEffectiveCropVariant, ref int streakLength)
        {
            int? effectiveCropVariant = ResolveEffectiveCropVariant(step, previousEffectiveCropVariant);

            if (!effectiveCropVariant.HasValue)
            {
                previousEffectiveCropVariant = null;
                streakLength = 0;
                return 1;
            }

            int awarded;

            if (previousEffectiveCropVariant.HasValue && effectiveCropVariant.Value == previousEffectiveCropVariant.Value)
            {
                streakLength = Mathf.Max(1, streakLength + 1);
                awarded = streakLength;
            }
            else
            {
                streakLength = 1;
                awarded = 1;
            }

            previousEffectiveCropVariant = effectiveCropVariant.Value;
            return Mathf.Max(1, awarded);
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
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (scoreText != null)
            {
                scoreText.text = IsGameOver
                    ? $"Score: {CurrentScore} - GAME OVER"
                    : $"Score: {CurrentScore}";
            }

            if (torrentFlowSlider != null)
            {
                torrentFlowSlider.minValue = 0f;
                torrentFlowSlider.maxValue = 1f;

                float normalized = IsTorrentActive
                    ? 1f
                    : (float)torrentCharge / Mathf.Max(1, torrentChargeTarget);

                torrentFlowSlider.value = Mathf.Clamp01(normalized);
            }

            UpdateTorrentModeLabelState(IsTorrentActive);
        }

        private float GetSliderNormalizedValueForCharge(int charge)
        {
            return IsTorrentActive
                ? 1f
                : Mathf.Clamp01((float)Mathf.Clamp(charge, 0, torrentChargeTarget) / Mathf.Max(1, torrentChargeTarget));
        }

        private void UpdateTorrentSliderValueWithTween(float normalizedTarget, bool immediate = false)
        {
            if (torrentFlowSlider == null)
            {
                return;
            }

            torrentFlowSlider.minValue = 0f;
            torrentFlowSlider.maxValue = 1f;
            float clampedTarget = Mathf.Clamp01(normalizedTarget);

            if (immediate)
            {
                if (torrentSliderValueTweenRoutine != null)
                {
                    StopCoroutine(torrentSliderValueTweenRoutine);
                    torrentSliderValueTweenRoutine = null;
                }

                torrentFlowSlider.value = clampedTarget;
                return;
            }

            float duration = Mathf.Max(0.02f, torrentSliderValueTweenDuration);

            if (torrentSliderValueTweenRoutine != null)
            {
                StopCoroutine(torrentSliderValueTweenRoutine);
            }

            torrentSliderValueTweenRoutine = StartCoroutine(TweenTorrentSliderValueRoutine(clampedTarget, duration));
        }

        private IEnumerator TweenTorrentSliderValueRoutine(float target, float duration)
        {
            float start = torrentFlowSlider != null ? torrentFlowSlider.value : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (torrentFlowSlider == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                torrentFlowSlider.value = Mathf.Lerp(start, target, eased);
                yield return null;
            }

            if (torrentFlowSlider != null)
            {
                torrentFlowSlider.value = target;
            }

            torrentSliderValueTweenRoutine = null;
        }

        private void CacheHudIntroPositions()
        {
            if (hasCachedHudPositions)
            {
                return;
            }

            if (torrentFlowSlider != null)
            {
                cachedSliderRect = torrentFlowSlider.transform as RectTransform;

                if (cachedSliderRect != null)
                {
                    cachedSliderAnchoredPosition = cachedSliderRect.anchoredPosition;
                }
            }

            if (scoreText != null)
            {
                cachedScoreRect = scoreText.transform as RectTransform;

                if (cachedScoreRect != null)
                {
                    cachedScoreAnchoredPosition = cachedScoreRect.anchoredPosition;
                }
            }

            hasCachedHudPositions = cachedSliderRect != null || cachedScoreRect != null;
        }

        private IEnumerator TweenHudIntroRoutine()
        {
            Vector2 sliderStart = cachedSliderRect != null ? cachedSliderRect.anchoredPosition : Vector2.zero;
            Vector2 scoreStart = cachedScoreRect != null ? cachedScoreRect.anchoredPosition : Vector2.zero;
            float duration = Mathf.Max(0.05f, preloadHudIntroDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                if (cachedSliderRect != null)
                {
                    cachedSliderRect.anchoredPosition = Vector2.Lerp(sliderStart, cachedSliderAnchoredPosition, eased);
                }

                if (cachedScoreRect != null)
                {
                    cachedScoreRect.anchoredPosition = Vector2.Lerp(scoreStart, cachedScoreAnchoredPosition, eased);
                }

                yield return null;
            }

            if (cachedSliderRect != null)
            {
                cachedSliderRect.anchoredPosition = cachedSliderAnchoredPosition;
            }

            if (cachedScoreRect != null)
            {
                cachedScoreRect.anchoredPosition = cachedScoreAnchoredPosition;
            }

            preloadHudIntroRoutine = null;
        }

        private void CacheTorrentSliderScale()
        {
            if (torrentFlowSlider == null || hasCachedTorrentSliderScale)
            {
                return;
            }

            torrentSliderBaseScale = torrentFlowSlider.transform.localScale;
            hasCachedTorrentSliderScale = true;
        }

        private void TryAutoAssignTorrentModeLabel()
        {
            if (torrentModeLabel != null)
            {
                return;
            }

            TMP_Text[] labels = Resources.FindObjectsOfTypeAll<TMP_Text>();

            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text candidate = labels[i];

                if (candidate == null || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                string candidateName = candidate.gameObject.name;

                if (!string.IsNullOrEmpty(candidateName) && candidateName.ToLowerInvariant().Contains("torrent"))
                {
                    torrentModeLabel = candidate;
                    break;
                }
            }
        }

        private void CacheTorrentModeLabelScale()
        {
            if (torrentModeLabel == null || hasCachedTorrentModeLabelScale)
            {
                return;
            }

            torrentModeLabelBaseScale = torrentModeLabel.transform.localScale;
            hasCachedTorrentModeLabelScale = true;
        }

        private void UpdateTorrentModeLabelState(bool shouldShow, bool immediate = false)
        {
            TryAutoAssignTorrentModeLabel();

            if (torrentModeLabel == null)
            {
                return;
            }

            CacheTorrentModeLabelScale();

            if (!showTorrentModeLabel)
            {
                if (torrentModeLabelTween.isAlive)
                {
                    torrentModeLabelTween.Stop();
                }

                if (torrentModeLabelPulseTween.isAlive)
                {
                    torrentModeLabelPulseTween.Stop();
                }

                torrentModeLabel.gameObject.SetActive(false);
                torrentModeLabelVisible = false;
                torrentModeLabelVisibleStateKnown = true;
                return;
            }

            if (!torrentModeLabelVisibleStateKnown)
            {
                torrentModeLabelVisibleStateKnown = true;
                torrentModeLabelVisible = shouldShow;
                PlayTorrentModeLabelTransition(shouldShow, immediate: true);
                return;
            }

            if (!immediate && torrentModeLabelVisible == shouldShow)
            {
                if (shouldShow)
                {
                    StartTorrentModeLabelPulse();
                }

                return;
            }

            torrentModeLabelVisible = shouldShow;
            PlayTorrentModeLabelTransition(shouldShow, immediate);
        }

        private void PlayTorrentModeLabelTransition(bool shouldShow, bool immediate)
        {
            if (torrentModeLabel == null)
            {
                return;
            }

            if (torrentModeLabelTween.isAlive)
            {
                torrentModeLabelTween.Stop();
            }

            if (torrentModeLabelPulseTween.isAlive)
            {
                torrentModeLabelPulseTween.Stop();
            }

            Transform labelTransform = torrentModeLabel.transform;
            Vector3 hiddenScale = torrentModeLabelBaseScale * Mathf.Max(0f, torrentModeLabelHiddenScale);
            Vector3 shownScale = torrentModeLabelBaseScale * Mathf.Max(0.05f, torrentModeLabelShownScale);

            if (shouldShow)
            {
                if (!string.IsNullOrWhiteSpace(torrentModeLabelText))
                {
                    torrentModeLabel.text = torrentModeLabelText;
                }

                torrentModeLabel.gameObject.SetActive(true);

                if (immediate)
                {
                    labelTransform.localScale = shownScale;
                    StartTorrentModeLabelPulse();
                    return;
                }

                labelTransform.localScale = hiddenScale;
                float duration = Mathf.Max(0.02f, torrentModeLabelTweenDuration);
                torrentModeLabelTween = Tween.Scale(labelTransform, shownScale, duration, torrentModeLabelTweenEase)
                    .OnComplete(StartTorrentModeLabelPulse);
                return;
            }

            if (immediate)
            {
                labelTransform.localScale = hiddenScale;
                torrentModeLabel.gameObject.SetActive(false);
                return;
            }

            if (!torrentModeLabel.gameObject.activeSelf)
            {
                return;
            }

            float hideDuration = Mathf.Max(0.02f, torrentModeLabelTweenDuration);
            torrentModeLabelTween = Tween.Scale(labelTransform, hiddenScale, hideDuration, torrentModeLabelHideEase)
                .OnComplete(() =>
                {
                    if (torrentModeLabel != null)
                    {
                        torrentModeLabel.gameObject.SetActive(false);
                    }
                });
        }

        private void StartTorrentModeLabelPulse()
        {
            if (torrentModeLabel == null || !torrentModeLabel.gameObject.activeSelf)
            {
                return;
            }

            if (torrentModeLabelPulseTween.isAlive)
            {
                torrentModeLabelPulseTween.Stop();
            }

            Transform labelTransform = torrentModeLabel.transform;
            Vector3 shownScale = torrentModeLabelBaseScale * Mathf.Max(0.05f, torrentModeLabelShownScale);
            float pulseScaleMultiplier = Mathf.Max(1f, torrentModeLabelPulseScale);
            Vector3 pulseScale = shownScale * pulseScaleMultiplier;

            labelTransform.localScale = shownScale;
            torrentModeLabelPulseTween = Tween.Scale(
                labelTransform,
                pulseScale,
                Mathf.Max(0.05f, torrentModeLabelPulseDuration),
                torrentModeLabelPulseEase,
                cycles: -1,
                cycleMode: CycleMode.Yoyo);
        }
    }
}
