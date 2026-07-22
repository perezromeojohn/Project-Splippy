using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace projectsplippy
{
    public class GameOverController : MonoBehaviour
    {
        [Header("End Panel")]
        [SerializeField] private CanvasGroup endCanvasGroup;
        [SerializeField, Min(0.1f)] private float endFadeInDuration = 0.5f;
        [SerializeField] private TMP_Text gameOverReasonText;
        [SerializeField] private TMP_Text finalScoreText;

        [Header("Camera Tween")]
        [SerializeField] private Camera cameraToTween;
        [SerializeField] private float cameraUpDistance = 3f;
        [SerializeField, Min(0.1f)] private float cameraTweenDuration = 1.5f;
        [SerializeField] private Ease cameraTweenEase = Ease.OutQuad;

        [Header("Delayed Reveal")]
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private GameObject highscoreInputPanel;
        [SerializeField] private RectTransform playAgainRect;
        [SerializeField, Min(0.1f)] private float highscoreInputDelay = 1f;
        [SerializeField] private float playAgainOffScreenOffset = 2000f;

        [Header("Highscore Input")]
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button submitButton;
        [SerializeField] private CanvasGroup highscoreInputCanvasGroup;
        [SerializeField, Range(0f, 1f)] private float inputFadeDuration = 0.3f;
        [SerializeField, Min(1)] private int minNameLength = 2;

        [Header("Leaderboard")]
        [SerializeField] private TMP_Text leaderboardEntryPrefab;
        [SerializeField] private Transform leaderboardContainer;
        [SerializeField, Min(1)] private int maxEntries = 8;
        [SerializeField] private string emptyEntryPlaceholder = "-------------------";

        [Header("Play Again")]
        [SerializeField] private Button playAgainButton;
        [SerializeField, Min(0.1f)] private float playAgainRiseDuration = 0.6f;
        [SerializeField] private Ease playAgainRiseEase = Ease.OutBack;
        [SerializeField, Min(0.1f)] private float restartFadeDuration = 0.4f;
        [SerializeField] private Image restartOverlay;

        [Header("Leaderboard Colors")]
        [SerializeField] private Gradient leaderboardColorGradient;
        [SerializeField, Min(0.1f)] private float entryBounceDuration = 0.5f;
        [SerializeField] private float entryBounceScale = 1.3f;
        [SerializeField] private Ease entryBounceEase = Ease.OutBack;

        [Header("Game Over Reasons")]
        [SerializeField] private string[] gameOverReasons = new string[]
        {
            "Island too polluted!",
            "Slippy is surrounded with pollution!",
            "The pollution overwhelmed Slippy!",
            "Slippy couldn't clean fast enough!",
            "The island has been consumed by waste!"
        };

        private const string HighScoreKey = "SplippyHighScores";
        private int finalScore;
        private string submittedPlayerName;
        private Vector2 playAgainOriginalPosition;
        private bool hasCachedPlayAgainPosition;
        private Tween fadeTween;
        private Tween riseTween;
        private Tween entryBounceTween;
        private Tween cameraTween;
        private readonly List<TMP_Text> instantiatedEntries = new List<TMP_Text>();
        private Vector3 cameraOriginalPosition;
        private bool hasCachedCameraPosition;

        private void Awake()
        {
            if (leaderboardColorGradient == null || leaderboardColorGradient.colorKeys.Length == 0)
            {
                leaderboardColorGradient = new Gradient();
                leaderboardColorGradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color32(0x6A, 0xD6, 0xFF, 0xFF), 0f),
                        new GradientColorKey(new Color32(0xFF, 0xD6, 0x3A, 0xFF), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });
            }

            if (restartOverlay != null)
            {
                restartOverlay.gameObject.SetActive(false);
                restartOverlay.color = new Color(1f, 1f, 1f, 0f);
            }

            nameInputField.characterLimit = 20;
        }

        public void Show(int score, string reason)
        {
            finalScore = score;

            // Use a random themed reason if none provided or default fallback
            string displayReason = reason;
            if (string.IsNullOrWhiteSpace(displayReason) || displayReason == "No more moves!")
            {
                displayReason = PickRandomReason();
            }

            if (gameOverReasonText != null)
            {
                gameOverReasonText.text = displayReason;
            }

            if (finalScoreText != null)
            {
                finalScoreText.text = $"Final Score: {score}";
            }

            CachePlayAgainPosition();
            HideDelayedChildren();

            // Leaderboard is visible and populated from the start
            PopulateLeaderboard();

            // Tween the camera upward
            TweenCameraUp();

            endCanvasGroup.alpha = 0f;
            endCanvasGroup.gameObject.SetActive(true);
            fadeTween = Tween.Alpha(endCanvasGroup, 1f, endFadeInDuration)
                .OnComplete(() => StartCoroutine(ShowHighscoreInputAfterDelay()));
        }

        private string PickRandomReason()
        {
            if (gameOverReasons == null || gameOverReasons.Length == 0)
            {
                return "No more moves!";
            }

            return gameOverReasons[Random.Range(0, gameOverReasons.Length)];
        }

        private void TweenCameraUp()
        {
            if (cameraToTween == null)
            {
                return;
            }

            if (!hasCachedCameraPosition)
            {
                cameraOriginalPosition = cameraToTween.transform.position;
                hasCachedCameraPosition = true;
            }

            Vector3 target = cameraOriginalPosition + new Vector3(0f, cameraUpDistance, 0f);
            cameraTween = Tween.Position(cameraToTween.transform, target, cameraTweenDuration, cameraTweenEase);
        }

        private void HideDelayedChildren()
        {
            // Leaderboard stays visible — only hide the input and push play again off screen
            if (highscoreInputPanel != null)
            {
                highscoreInputPanel.SetActive(false);
            }

            if (highscoreInputCanvasGroup != null)
            {
                highscoreInputCanvasGroup.alpha = 0f;
            }

            PushPlayAgainOffScreen();
        }

        private System.Collections.IEnumerator ShowHighscoreInputAfterDelay()
        {
            yield return new WaitForSeconds(highscoreInputDelay);
            ShowHighscoreInput();
        }

        private void ShowHighscoreInput()
        {
            if (highscoreInputPanel == null)
            {
                return;
            }

            highscoreInputPanel.SetActive(true);

            if (nameInputField != null)
            {
                nameInputField.text = string.Empty;
                nameInputField.onValueChanged.RemoveListener(OnNameInputChanged);
                nameInputField.onValueChanged.AddListener(OnNameInputChanged);
                nameInputField.ActivateInputField();
            }

            if (submitButton != null)
            {
                submitButton.interactable = false;
                submitButton.onClick.RemoveListener(OnSubmitScore);
                submitButton.onClick.AddListener(OnSubmitScore);
            }

            if (highscoreInputCanvasGroup != null)
            {
                highscoreInputCanvasGroup.alpha = 0f;
                Tween.Alpha(highscoreInputCanvasGroup, 1f, inputFadeDuration);
            }
        }

        private void OnNameInputChanged(string text)
        {
            if (submitButton == null)
            {
                return;
            }

            string trimmed = (text ?? string.Empty).Trim();
            bool valid = trimmed.Length >= minNameLength && trimmed != emptyEntryPlaceholder;
            submitButton.interactable = valid;
        }

        public void OnSubmitScore()
        {
            if (submitButton != null)
            {
                submitButton.interactable = false;
                submitButton.onClick.RemoveListener(OnSubmitScore);
            }

            if (nameInputField != null)
            {
                nameInputField.onValueChanged.RemoveListener(OnNameInputChanged);
                string playerName = nameInputField.text.Trim();
                submittedPlayerName = playerName;
                SaveHighScore(playerName, finalScore);
                nameInputField.interactable = false;
            }
            else
            {
                submittedPlayerName = string.Empty;
            }

            PopulateLeaderboard();
            BouncePlayerEntry();

            // Fade input out so player can see the leaderboard
            if (highscoreInputCanvasGroup != null)
            {
                Tween.Alpha(highscoreInputCanvasGroup, 0f, inputFadeDuration);
            }

            RisePlayAgain();
        }

        public void OnPlayAgainPressed()
        {
            if (playAgainButton != null)
            {
                playAgainButton.interactable = false;
                playAgainButton.onClick.RemoveListener(OnPlayAgainPressed);
            }

            if (restartOverlay != null)
            {
                restartOverlay.gameObject.SetActive(true);
                restartOverlay.color = new Color(1f, 1f, 1f, 0f);
                Tween.Alpha(restartOverlay, 1f, restartFadeDuration)
                    .OnComplete(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void SaveHighScore(string playerName, int score)
        {
            var entries = LoadHighScores();
            entries.Add(new HighScoreEntry { name = playerName, score = score });
            entries.Sort((a, b) => b.score.CompareTo(a.score));

            while (entries.Count > maxEntries)
            {
                entries.RemoveAt(entries.Count - 1);
            }

            string json = JsonUtility.ToJson(new HighScoreList { entries = entries });
            PlayerPrefs.SetString(HighScoreKey, json);
            PlayerPrefs.Save();
        }

        private void PopulateLeaderboard()
        {
            instantiatedEntries.Clear();

            if (leaderboardPanel == null || leaderboardContainer == null || leaderboardEntryPrefab == null)
            {
                return;
            }

            for (int i = leaderboardContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(leaderboardContainer.GetChild(i).gameObject);
            }

            leaderboardPanel.SetActive(true);

            var entries = LoadHighScores();

            for (int i = 0; i < maxEntries; i++)
            {
                TMP_Text entry = Instantiate(leaderboardEntryPrefab, leaderboardContainer);
                instantiatedEntries.Add(entry);

                if (i < entries.Count)
                {
                    entry.text = $"{i + 1}. {entries[i].name} - {entries[i].score}";
                }
                else
                {
                    entry.text = $"{i + 1}. {emptyEntryPlaceholder}";
                }

                // Color from bottom (blue) to top (gold)
                int filledCount = Mathf.Min(entries.Count, maxEntries);
                if (filledCount > 1 && i < entries.Count)
                {
                    float t = (float)(filledCount - 1 - i) / (filledCount - 1);
                    entry.color = leaderboardColorGradient.Evaluate(t);
                }
                else
                {
                    entry.color = Color.white;
                }
            }
        }

        private void BouncePlayerEntry()
        {
            if (string.IsNullOrWhiteSpace(submittedPlayerName))
            {
                return;
            }

            for (int i = 0; i < instantiatedEntries.Count; i++)
            {
                TMP_Text entry = instantiatedEntries[i];
                if (entry == null)
                {
                    continue;
                }

                string expectedText = $"{i + 1}. {submittedPlayerName} - {finalScore}";
                if (entry.text == expectedText)
                {
                    if (entryBounceTween.isAlive)
                    {
                        entryBounceTween.Stop();
                    }

                    Transform entryTransform = entry.transform;
                    entryTransform.localScale = Vector3.one;
                    entryBounceTween = Tween.Scale(
                        entryTransform,
                        Vector3.one * entryBounceScale,
                        entryBounceDuration,
                        entryBounceEase,
                        cycles: -1,
                        cycleMode: CycleMode.Yoyo);
                    return;
                }
            }
        }

        private void RisePlayAgain()
        {
            if (playAgainButton != null)
            {
                playAgainButton.onClick.AddListener(OnPlayAgainPressed);
            }

            if (playAgainRect == null)
            {
                return;
            }

            CachePlayAgainPosition();
            riseTween = Tween.UIAnchoredPosition(playAgainRect, playAgainOriginalPosition, playAgainRiseDuration, playAgainRiseEase);
        }

        private void PushPlayAgainOffScreen()
        {
            if (playAgainRect == null)
            {
                return;
            }

            CachePlayAgainPosition();
            Vector2 offScreen = playAgainOriginalPosition + new Vector2(0f, -playAgainOffScreenOffset);
            playAgainRect.anchoredPosition = offScreen;
        }

        private void CachePlayAgainPosition()
        {
            if (hasCachedPlayAgainPosition || playAgainRect == null)
            {
                return;
            }

            playAgainOriginalPosition = playAgainRect.anchoredPosition;
            hasCachedPlayAgainPosition = true;
        }

        private List<HighScoreEntry> LoadHighScores()
        {
            if (!PlayerPrefs.HasKey(HighScoreKey))
            {
                return new List<HighScoreEntry>();
            }

            string json = PlayerPrefs.GetString(HighScoreKey);
            HighScoreList list = JsonUtility.FromJson<HighScoreList>(json);
            return list?.entries ?? new List<HighScoreEntry>();
        }

        private void OnDisable()
        {
            if (fadeTween.isAlive)
            {
                fadeTween.Stop();
            }

            if (riseTween.isAlive)
            {
                riseTween.Stop();
            }

            if (entryBounceTween.isAlive)
            {
                entryBounceTween.Stop();
            }

            if (cameraTween.isAlive)
            {
                cameraTween.Stop();
            }
        }

        [System.Serializable]
        private struct HighScoreEntry
        {
            public string name;
            public int score;
        }

        [System.Serializable]
        private class HighScoreList
        {
            public List<HighScoreEntry> entries;
        }
    }
}
