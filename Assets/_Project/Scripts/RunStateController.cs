using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace projectsplippy
{
    public class RunStateController : MonoBehaviour
    {
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
        [SerializeField] private CanvasGroup gameplayHudCanvasGroup;
        [SerializeField] private float gameplayHudFadeDuration = 0.4f;
        [SerializeField] private Slider torrentFlowSlider;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text sanitationSpawnTrackerText;
        [SerializeField] private TMP_Text torrentModeLabel;

        [Header("Game Over UI")]
        [SerializeField] private bool enableGameOverPresentation = true;
        [SerializeField] private TMP_Text gameOverHeaderText;
        [SerializeField] private TMP_Text gameOverReasonText;
        [SerializeField] private TMP_Text gameOverFinalScoreText;
        [SerializeField] private TMP_Text gameOverClickAnywhereText;
        [SerializeField] private Graphic gameOverRetryBlockerGraphic;
        [SerializeField] private Color gameOverRetryBlockerColor = Color.white;
        [SerializeField, Min(0.02f)] private float gameOverUiTweenDuration = 3f;
        [SerializeField, Min(1f)] private float gameOverClickPulseScale = 1.08f;
        [SerializeField, Min(0.02f)] private float gameOverClickPulseDuration = 3f;
        [SerializeField] private Ease gameOverClickPulseEase = Ease.InOutSine;
        [SerializeField] private string finalScorePrefix = "Final Score: ";

        [Header("Audio")]
        [SerializeField] private AudioSource runStateAudioSource;
        [SerializeField] private AudioClip gameOverSfx;
        [SerializeField, Range(0f, 1f)] private float gameOverSfxVolume = 1f;
        [SerializeField] private AudioClip torrentModeSfx;
        [SerializeField, Range(0f, 1f)] private float torrentModeSfxVolume = 1f;

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

        public bool IsGameOver { get; private set; }
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
        private Tween torrentModeLabelTween;
        private Tween torrentModeLabelPulseTween;
        private Vector3 torrentModeLabelBaseScale = Vector3.one;
        private bool hasCachedTorrentModeLabelScale;
        private bool torrentModeLabelVisibleStateKnown;
        private bool torrentModeLabelVisible;
        private Tween gameOverHeaderFadeTween;
        private Tween gameOverReasonFadeTween;
        private Tween gameOverFinalScoreFadeTween;
        private Tween gameOverClickFadeTween;
        private Tween gameOverClickPulseTween;
        private Tween gameOverBlockerFadeTween;
        private Coroutine gameOverPresentationRoutine;
        private Coroutine gameOverRestartRoutine;
        private bool gameOverAwaitingRestartClick;
        private bool gameOverRestartSequenceStarted;
        private Vector3 gameOverClickTextBaseScale = Vector3.one;
        private bool hasCachedGameOverClickTextScale;
        private Graphic runtimeGameOverRetryBlockerGraphic;
        private Tween gameplayHudFadeTween;

        private bool chargePreviewActive;
        private int previewCharge;
        private int? previewPreviousEffectiveCropVariant;
        private int previewStreakLength;

        private void Update()
        {
            if (!IsGameOver || !gameOverAwaitingRestartClick || gameOverRestartSequenceStarted)
            {
                return;
            }

            if (IsAnyPointerPressedThisFrame())
            {
                BeginGameOverRestartSequence();
            }
        }

        private void OnDisable()
        {
            StopGameOverPresentationTweens();

            if (gameplayHudFadeTween.isAlive)
            {
                gameplayHudFadeTween.Stop();
            }
        }

        public void Initialize()
        {
            torrentChargeTarget = Mathf.Max(1, torrentChargeTarget);
            torrentDurationTurns = Mathf.Max(1, torrentDurationTurns);
            torrentPathRange = Mathf.Max(1, torrentPathRange);
            basePathRange = Mathf.Max(1, basePathRange);
            torrentScoreMultiplier = Mathf.Max(1, torrentScoreMultiplier);

            IsGameOver = false;
            CurrentScore = 0;
            torrentCharge = 0;
            torrentTurnsLeft = 0;
            torrentActivatedThisResolution = false;
            TryResolveRunStateAudioSource();
            CacheTorrentSliderScale();
            TryAutoAssignTorrentModeLabel();
            CacheTorrentModeLabelScale();
            torrentModeLabelVisibleStateKnown = false;
            ResetGameOverPresentationUi();
            UpdateTorrentModeLabelState(IsTorrentActive, immediate: true);

            if (torrentFlowSlider != null)
            {
                torrentFlowSlider.transform.localScale = torrentSliderBaseScale;
            }

            RefreshHud();
        }

        public void PlayGameplayHudFadeIn()
        {
            if (gameplayHudCanvasGroup == null)
            {
                return;
            }

            gameplayHudCanvasGroup.gameObject.SetActive(true);
            gameplayHudCanvasGroup.alpha = 0f;

            if (gameplayHudFadeTween.isAlive)
            {
                gameplayHudFadeTween.Stop();
            }

            gameplayHudFadeTween = Tween.Alpha(gameplayHudCanvasGroup, 1f, gameplayHudFadeDuration);
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
                string label = ResolveDebugLabel(step, collisionOrder, i);

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

        public void PlayTorrentModeSfx()
        {
            PlayRunStateSfx(torrentModeSfx, torrentModeSfxVolume);
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
                PlayTorrentModeSfx();
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
            PlayRunStateSfx(gameOverSfx, gameOverSfxVolume);
            RefreshHud();

            if (enableGameOverPresentation)
            {
                StartGameOverPresentation();
            }
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

        private void CacheTorrentSliderScale()
        {
            if (torrentFlowSlider == null || hasCachedTorrentSliderScale)
            {
                return;
            }

            torrentSliderBaseScale = torrentFlowSlider.transform.localScale;
            hasCachedTorrentSliderScale = true;
        }

        private void TryResolveRunStateAudioSource()
        {
            if (runStateAudioSource != null)
            {
                return;
            }

            runStateAudioSource = GetComponent<AudioSource>();
        }

        private void PlayRunStateSfx(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            TryResolveRunStateAudioSource();
            float clampedVolume = Mathf.Clamp01(volume);

            if (runStateAudioSource != null)
            {
                runStateAudioSource.PlayOneShot(clip, clampedVolume);
                return;
            }

            Camera cam = Camera.main;
            Vector3 position = cam != null ? cam.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(clip, position, clampedVolume);
        }

        private void ResetGameOverPresentationUi()
        {
            StopGameOverPresentationTweens();
            gameOverAwaitingRestartClick = false;
            gameOverRestartSequenceStarted = false;

            if (!enableGameOverPresentation)
            {
                if (scoreText != null)
                {
                    scoreText.gameObject.SetActive(true);
                }

                return;
            }

            TryAutoAssignGameOverPresentationRefs();

            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(true);
            }

            HideGameOverText(gameOverHeaderText);
            HideGameOverText(gameOverReasonText);
            HideGameOverText(gameOverFinalScoreText);
            HideGameOverText(gameOverClickAnywhereText);

            if (gameOverRetryBlockerGraphic != null)
            {
                SetRetryBlockerColorAndAlpha(gameOverRetryBlockerGraphic, 1f);
                gameOverRetryBlockerGraphic.gameObject.SetActive(false);
            }

            if (runtimeGameOverRetryBlockerGraphic != null)
            {
                SetRetryBlockerColorAndAlpha(runtimeGameOverRetryBlockerGraphic, 1f);
                runtimeGameOverRetryBlockerGraphic.gameObject.SetActive(false);
            }
        }

        private void StartGameOverPresentation()
        {
            StopGameOverPresentationTweens();
            gameOverAwaitingRestartClick = false;
            gameOverRestartSequenceStarted = false;

            TryAutoAssignGameOverPresentationRefs();

            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(false);
            }

            PrepareGameOverText(gameOverHeaderText);
            PrepareGameOverText(gameOverReasonText);
            PrepareGameOverText(gameOverFinalScoreText);
            PrepareGameOverText(gameOverClickAnywhereText);

            if (gameOverFinalScoreText != null)
            {
                string prefix = string.IsNullOrWhiteSpace(finalScorePrefix) ? "Score: " : finalScorePrefix;
                gameOverFinalScoreText.text = $"{prefix}{CurrentScore}";
            }

            if (gameOverRetryBlockerGraphic != null)
            {
                SetRetryBlockerColorAndAlpha(gameOverRetryBlockerGraphic, 1f);
                gameOverRetryBlockerGraphic.gameObject.SetActive(false);
            }

            if (runtimeGameOverRetryBlockerGraphic != null)
            {
                SetRetryBlockerColorAndAlpha(runtimeGameOverRetryBlockerGraphic, 1f);
                runtimeGameOverRetryBlockerGraphic.gameObject.SetActive(false);
            }

            gameOverPresentationRoutine = StartCoroutine(GameOverPresentationRoutine());
        }

        private IEnumerator GameOverPresentationRoutine()
        {
            float duration = Mathf.Max(0.02f, gameOverUiTweenDuration);
            gameOverHeaderFadeTween = FadeTextAlpha(gameOverHeaderText, 1f, duration);
            gameOverReasonFadeTween = FadeTextAlpha(gameOverReasonText, 1f, duration);
            gameOverFinalScoreFadeTween = FadeTextAlpha(gameOverFinalScoreText, 1f, duration);
            gameOverClickFadeTween = FadeTextAlpha(gameOverClickAnywhereText, 1f, duration);

            yield return new WaitForSeconds(duration);

            StartGameOverClickPulse();
            gameOverAwaitingRestartClick = true;
            gameOverPresentationRoutine = null;
        }

        private void BeginGameOverRestartSequence()
        {
            if (gameOverRestartSequenceStarted)
            {
                return;
            }

            gameOverAwaitingRestartClick = false;
            gameOverRestartSequenceStarted = true;

            if (gameOverClickPulseTween.isAlive)
            {
                gameOverClickPulseTween.Stop();
            }

            if (gameOverClickAnywhereText != null && hasCachedGameOverClickTextScale)
            {
                gameOverClickAnywhereText.transform.localScale = gameOverClickTextBaseScale;
            }

            gameOverRestartRoutine = StartCoroutine(GameOverRestartRoutine());
        }

        private IEnumerator GameOverRestartRoutine()
        {
            float duration = Mathf.Max(0.02f, gameOverUiTweenDuration);
            Graphic blockerGraphic = ResolveRetryBlockerGraphicForPresentation();

            if (blockerGraphic != null)
            {
                blockerGraphic.gameObject.SetActive(true);
                SetRetryBlockerColorAndAlpha(blockerGraphic, 0f);

                gameOverBlockerFadeTween = Tween.Custom(
                    0f,
                    1f,
                    duration: duration,
                    onValueChange: alpha => SetRetryBlockerColorAndAlpha(blockerGraphic, alpha));
            }

            yield return new WaitForSeconds(duration);
            ReloadCurrentScene();
        }

        private void StartGameOverClickPulse()
        {
            if (gameOverClickAnywhereText == null || !gameOverClickAnywhereText.gameObject.activeSelf)
            {
                return;
            }

            if (!hasCachedGameOverClickTextScale)
            {
                gameOverClickTextBaseScale = gameOverClickAnywhereText.transform.localScale;
                hasCachedGameOverClickTextScale = true;
            }

            if (gameOverClickPulseTween.isAlive)
            {
                gameOverClickPulseTween.Stop();
            }

            Transform clickTransform = gameOverClickAnywhereText.transform;
            clickTransform.localScale = gameOverClickTextBaseScale;
            Vector3 targetScale = gameOverClickTextBaseScale * Mathf.Max(1f, gameOverClickPulseScale);
            gameOverClickPulseTween = Tween.Scale(
                clickTransform,
                targetScale,
                Mathf.Max(0.02f, gameOverClickPulseDuration),
                gameOverClickPulseEase,
                cycles: -1,
                cycleMode: CycleMode.Yoyo);
        }

        private void StopGameOverPresentationTweens()
        {
            if (gameOverPresentationRoutine != null)
            {
                StopCoroutine(gameOverPresentationRoutine);
                gameOverPresentationRoutine = null;
            }

            if (gameOverRestartRoutine != null)
            {
                StopCoroutine(gameOverRestartRoutine);
                gameOverRestartRoutine = null;
            }

            if (gameOverHeaderFadeTween.isAlive)
            {
                gameOverHeaderFadeTween.Stop();
            }

            if (gameOverReasonFadeTween.isAlive)
            {
                gameOverReasonFadeTween.Stop();
            }

            if (gameOverFinalScoreFadeTween.isAlive)
            {
                gameOverFinalScoreFadeTween.Stop();
            }

            if (gameOverClickFadeTween.isAlive)
            {
                gameOverClickFadeTween.Stop();
            }

            if (gameOverClickPulseTween.isAlive)
            {
                gameOverClickPulseTween.Stop();
            }

            if (gameOverBlockerFadeTween.isAlive)
            {
                gameOverBlockerFadeTween.Stop();
            }

            if (gameOverClickAnywhereText != null && hasCachedGameOverClickTextScale)
            {
                gameOverClickAnywhereText.transform.localScale = gameOverClickTextBaseScale;
            }
        }

        private Tween FadeTextAlpha(TMP_Text text, float targetAlpha, float duration)
        {
            if (text == null)
            {
                return default;
            }

            float startAlpha = text.color.a;

            return Tween.Custom(
                startAlpha,
                Mathf.Clamp01(targetAlpha),
                duration: Mathf.Max(0.02f, duration),
                onValueChange: alpha => SetTextAlpha(text, alpha));
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }

        private void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        private void SetRetryBlockerColorAndAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color blockerColor = gameOverRetryBlockerColor;
            blockerColor.a = Mathf.Clamp01(alpha);
            graphic.color = blockerColor;
        }

        private static void PrepareGameOverText(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.gameObject.SetActive(true);
            SetTextAlpha(text, 0f);
        }

        private static void HideGameOverText(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            SetTextAlpha(text, 1f);
            text.gameObject.SetActive(false);
        }

        private void TryAutoAssignGameOverPresentationRefs()
        {
            if (gameOverHeaderText == null)
            {
                gameOverHeaderText = FindSceneTmpTextByNames(new[] { "Header" });
            }

            if (gameOverReasonText == null)
            {
                gameOverReasonText = FindSceneTmpTextByNames(new[] { "GameOverReason" });
            }

            if (gameOverFinalScoreText == null)
            {
                gameOverFinalScoreText = FindSceneTmpTextByNames(new[] { "FinalScore" });
            }

            if (gameOverClickAnywhereText == null)
            {
                gameOverClickAnywhereText = FindSceneTmpTextByNames(new[] { "ClickTheScreen", "Click the Screen" });
            }

            if (gameOverRetryBlockerGraphic == null)
            {
                gameOverRetryBlockerGraphic = FindSceneGraphicByNames(new[] { "BLOCKERSRETRY", "BLOCKERS" });
            }
        }

        private Graphic ResolveRetryBlockerGraphicForPresentation()
        {
            if (gameOverRetryBlockerGraphic != null)
            {
                gameOverRetryBlockerGraphic.gameObject.SetActive(true);

                if (gameOverRetryBlockerGraphic.gameObject.activeInHierarchy)
                {
                    return gameOverRetryBlockerGraphic;
                }

                gameOverRetryBlockerGraphic.gameObject.SetActive(false);
            }

            if (runtimeGameOverRetryBlockerGraphic == null)
            {
                runtimeGameOverRetryBlockerGraphic = CreateRuntimeRetryBlockerGraphic();
            }

            return runtimeGameOverRetryBlockerGraphic;
        }

        private Graphic CreateRuntimeRetryBlockerGraphic()
        {
            Canvas canvas = null;

            if (scoreText != null)
            {
                canvas = scoreText.canvas;
            }

            if (canvas == null)
            {
                Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();

                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas candidate = canvases[i];

                    if (candidate != null && candidate.gameObject.scene.IsValid())
                    {
                        canvas = candidate;
                        break;
                    }
                }
            }

            if (canvas == null)
            {
                return null;
            }

            var blockerObject = new GameObject("BLOCKERSRETRY_Runtime");
            blockerObject.layer = canvas.gameObject.layer;
            RectTransform rect = blockerObject.AddComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsLastSibling();

            Image image = blockerObject.AddComponent<Image>();
            image.color = new Color(
                gameOverRetryBlockerColor.r,
                gameOverRetryBlockerColor.g,
                gameOverRetryBlockerColor.b,
                0f);
            image.raycastTarget = true;
            blockerObject.SetActive(false);
            return image;
        }

        private static TMP_Text FindSceneTmpTextByNames(IReadOnlyList<string> names)
        {
            TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            TMP_Text fallback = null;

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];

                if (candidate == null || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!IsNameMatch(candidate.gameObject.name, names))
                {
                    continue;
                }

                if (IsDirectChildOfCanvas(candidate.transform))
                {
                    return candidate;
                }

                fallback = candidate;
            }

            return fallback;
        }

        private static Graphic FindSceneGraphicByNames(IReadOnlyList<string> names)
        {
            Graphic[] graphics = Resources.FindObjectsOfTypeAll<Graphic>();
            Graphic fallback = null;

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic candidate = graphics[i];

                if (candidate == null || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!IsNameMatch(candidate.gameObject.name, names))
                {
                    continue;
                }

                if (IsDirectChildOfCanvas(candidate.transform))
                {
                    return candidate;
                }

                fallback = candidate;
            }

            return fallback;
        }

        private static bool IsDirectChildOfCanvas(Transform transform)
        {
            return transform != null && transform.parent != null && transform.parent.GetComponent<Canvas>() != null;
        }

        private static bool IsNameMatch(string objectName, IReadOnlyList<string> expectedNames)
        {
            string normalizedObjectName = NormalizeObjectName(objectName);

            for (int i = 0; i < expectedNames.Count; i++)
            {
                if (normalizedObjectName == NormalizeObjectName(expectedNames[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static bool IsAnyPointerPressedThisFrame()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private static void ReloadCurrentScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
