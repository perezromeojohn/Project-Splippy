using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace projectsplippy
{
    public class MainMenuAnimator : MonoBehaviour
    {
        [Header("References")]
        public Animator mainMenuAnimator;
        public GameObject mainMenuPanel;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string tapActionName = "MoveTowards";

        [Header("UI")]
        [SerializeField] private Button quitButton;

        [Header("State")]
        [SerializeField] private bool allowSplippyTapMove;

        private InputAction tapAction;
        private Coroutine enableQuitWhenReadyRoutine;
        private bool quitRequested;
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

        public bool AllowSplippyTapMove => allowSplippyTapMove;

        private static readonly int CanClickHash = Animator.StringToHash("canClick");
        private static readonly int HasClickedHash = Animator.StringToHash("hasClicked");
        private static readonly int RetryGameHash = Animator.StringToHash("retryGame");
        private static readonly int IsGameOverHash = Animator.StringToHash("isGameOver");
        private static readonly int CanClickGameOverHash = Animator.StringToHash("canClickGameOver");

        private void OnEnable()
        {
            BindTapAction();
            BindQuitButton();
        }

        private void OnDisable()
        {
            UnbindTapAction();
            UnbindQuitButton();
        }

        private void BindTapAction()
        {
            if (inputActions == null)
            {
                return;
            }

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);

            if (map == null)
            {
                return;
            }

            tapAction = map.FindAction(tapActionName, throwIfNotFound: false);

            if (tapAction == null)
            {
                return;
            }

            tapAction.performed += OnTapPerformed;
            tapAction.Enable();
        }

        private void UnbindTapAction()
        {
            if (tapAction == null)
            {
                return;
            }

            tapAction.performed -= OnTapPerformed;
            // Do not disable here: this action can be shared with gameplay movement.
            tapAction = null;
        }

        private void BindQuitButton()
        {
            TryAutoAssignQuitButton();

            if (quitButton == null)
            {
                return;
            }

            quitButton.onClick.RemoveListener(OnQuitPressed);
            quitButton.onClick.AddListener(OnQuitPressed);
            UpdateQuitInteractable(CanQuitNow() && !quitRequested);
        }

        private void UnbindQuitButton()
        {
            if (enableQuitWhenReadyRoutine != null)
            {
                StopCoroutine(enableQuitWhenReadyRoutine);
                enableQuitWhenReadyRoutine = null;
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitPressed);
            }
        }

        private void TryAutoAssignQuitButton()
        {
            if (quitButton != null)
            {
                return;
            }

            Transform quitTransform = transform.Find("Quit");

            if (quitTransform != null)
            {
                quitButton = quitTransform.GetComponent<Button>();
            }

            if (quitButton != null)
            {
                return;
            }

            Button[] buttons = GetComponentsInChildren<Button>(true);

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == "Quit")
                {
                    quitButton = buttons[i];
                    break;
                }
            }
        }

        private void OnTapPerformed(InputAction.CallbackContext _)
        {
            if (IsPressOverBlockingUi())
            {
                return;
            }

            if (mainMenuAnimator == null)
            {
                return;
            }

            if (!mainMenuAnimator.GetBool(CanClickHash))
            {
                return;
            }

            if (mainMenuAnimator.GetBool(HasClickedHash))
            {
                return;
            }

            HasClicked();
        }

        private bool IsPressOverBlockingUi()
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                return false;
            }

            if (!TryGetCurrentPointerScreenPosition(out Vector2 screenPosition))
            {
                return false;
            }

            PointerEventData pointerEventData = new PointerEventData(eventSystem) { position = screenPosition };
            uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, uiRaycastResults);

            for (int i = 0; i < uiRaycastResults.Count; i++)
            {
                GameObject raycastObject = uiRaycastResults[i].gameObject;

                if (raycastObject == null)
                {
                    continue;
                }

                Button pressedButton = raycastObject.GetComponentInParent<Button>();

                if (pressedButton == null || !pressedButton.IsActive() || !pressedButton.interactable)
                {
                    continue;
                }

                // Don't treat UI button clicks as a screen tap-to-start.
                return true;
            }

            return false;
        }

        private static bool TryGetCurrentPointerScreenPosition(out Vector2 position)
        {
            if (Mouse.current != null)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;

                if (touch.press.isPressed)
                {
                    position = touch.position.ReadValue();
                    return true;
                }
            }

            position = default;
            return false;
        }

        public void CanNowClick()
        {
            if (mainMenuAnimator == null)
            {
                return;
            }

            mainMenuAnimator.SetBool(CanClickHash, true);

            if (enableQuitWhenReadyRoutine != null)
            {
                StopCoroutine(enableQuitWhenReadyRoutine);
            }

            UpdateQuitInteractable(false);
            enableQuitWhenReadyRoutine = StartCoroutine(EnableQuitWhenReady());
        }

        public void OnExitFinished()
        {
            allowSplippyTapMove = true;

            if (enableQuitWhenReadyRoutine != null)
            {
                StopCoroutine(enableQuitWhenReadyRoutine);
                enableQuitWhenReadyRoutine = null;
            }

            UpdateQuitInteractable(false);

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }
        }

        public void HasClicked()
        {
            if (mainMenuAnimator == null)
            {
                return;
            }

            Debug.Log("[MainMenuAnimator] Click detected. Transitioning to exit animation.");
            UpdateQuitInteractable(false);

            mainMenuAnimator.SetBool(HasClickedHash, true);
        }

        public void OnEntry()
        {
            allowSplippyTapMove = false;
            quitRequested = false;

            if (enableQuitWhenReadyRoutine != null)
            {
                StopCoroutine(enableQuitWhenReadyRoutine);
                enableQuitWhenReadyRoutine = null;
            }

            UpdateQuitInteractable(false);

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }
        }

        public void OnQuitPressed()
        {
            if (quitRequested)
            {
                return;
            }

            if (!CanQuitNow())
            {
                Debug.Log("[MainMenuAnimator] Quit ignored because intro is not yet in idle state.");
                return;
            }

            quitRequested = true;
            UpdateQuitInteractable(false);

            Debug.Log("[MainMenuAnimator] Quit requested.");
            QuitApplication();
        }

        private bool CanQuitNow()
        {
            if (mainMenuAnimator == null)
            {
                return false;
            }

            if (!mainMenuAnimator.GetBool(CanClickHash))
            {
                return false;
            }

            if (mainMenuAnimator.IsInTransition(0))
            {
                return false;
            }

            AnimatorStateInfo stateInfo = mainMenuAnimator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName("Idle");
        }

        private IEnumerator EnableQuitWhenReady()
        {
            while (!CanQuitNow())
            {
                yield return null;
            }

            if (!quitRequested)
            {
                UpdateQuitInteractable(true);
            }

            enableQuitWhenReadyRoutine = null;
        }

        private void UpdateQuitInteractable(bool interactable)
        {
            if (quitButton == null)
            {
                return;
            }

            quitButton.interactable = interactable;
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // game over
        public void ReloadScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void IsGameOver()
        {
            mainMenuAnimator.SetBool(IsGameOverHash, true);
        }

        public void CanClickGameOver()
        {
            mainMenuAnimator.SetBool(CanClickGameOverHash, true);
        }

        // if clicked then set retryGame bool
        public void RetryGame()
        {
            mainMenuAnimator.SetBool(RetryGameHash, true);
        }

    }
}
