using UnityEngine;
using UnityEngine.InputSystem;

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

        private InputAction tapAction;

        private static readonly int CanClickHash = Animator.StringToHash("canClick");
        private static readonly int HasClickedHash = Animator.StringToHash("hasClicked");

        private void OnEnable()
        {
            BindTapAction();
        }

        private void OnDisable()
        {
            UnbindTapAction();
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

        private void OnTapPerformed(InputAction.CallbackContext _)
        {
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

        public void CanNowClick()
        {
            if (mainMenuAnimator == null)
            {
                return;
            }

            mainMenuAnimator.SetBool(CanClickHash, true);
        }

        public void OnExitFinished()
        {
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

            mainMenuAnimator.SetBool(HasClickedHash, true);
        }

        public void OnEntry()
        {
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }

            if (mainMenuAnimator != null)
            {
                mainMenuAnimator.SetBool(HasClickedHash, false);
            }
        }
    }
}
