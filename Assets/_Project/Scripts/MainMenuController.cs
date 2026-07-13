using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace projectsplippy
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private CanvasGroup startCanvasGroup;

        [Header("Logo")]
        [SerializeField] private Transform logoSplippy;
        [SerializeField] private float logoBobHeight = 12f;
        [SerializeField] private float logoBobSpeed = 2.5f;
        [SerializeField] private float logoRotationAngle = 4f;
        [SerializeField] private float logoRotationSpeed = 1.8f;

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.5f;

        private GameManager gameManager;
        private bool playPressed;
        private Vector3 logoStartLocalPosition;

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            playButton.onClick.AddListener(OnPlayPressed);
            quitButton.onClick.AddListener(OnQuitPressed);
        }

        public void Show()
        {
            startPanel.SetActive(true);
            startCanvasGroup.alpha = 1f;
            playPressed = false;
            playButton.interactable = true;

            if (logoSplippy != null)
            {
                logoStartLocalPosition = logoSplippy.localPosition;
            }
        }

        private void Update()
        {
            if (!startPanel.activeSelf || playPressed || logoSplippy == null)
            {
                return;
            }

            float time = Time.time;
            float bob = Mathf.Sin(time * logoBobSpeed) * logoBobHeight;
            float rotation = Mathf.Sin(time * logoRotationSpeed) * logoRotationAngle;
            logoSplippy.localPosition = logoStartLocalPosition + new Vector3(0f, bob, 0f);
            logoSplippy.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void OnPlayPressed()
        {
            if (playPressed)
            {
                return;
            }

            playPressed = true;
            playButton.interactable = false;

            gameManager.BeginGameplayFromMenu();

            Tween.Alpha(startCanvasGroup, 0f, fadeDuration)
                .OnComplete(() =>
                {
                    startPanel.SetActive(false);
                });
        }

        private void OnQuitPressed()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayPressed);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitPressed);
            }
        }
    }
}
