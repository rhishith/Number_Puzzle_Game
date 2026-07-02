using UnityEngine;
using UnityEngine.InputSystem;

namespace SlideAndMatch
{
    /// <summary>
    /// Detects keyboard (Arrow / WASD) and touch-swipe input using
    /// Unity's new Input System, then forwards the direction to
    /// GameBoard.HandleSwipe().  Auto-finds GameBoard if no reference is assigned.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        [SerializeField] private GameBoard gameBoard;

        [Header("Swipe Settings")]
        [SerializeField] private float swipeThreshold = 50f;

        private Vector2 pointerStart;
        private bool isSwiping;

        void Start()
        {
            if (gameBoard == null)
                gameBoard = FindAnyObjectByType<GameBoard>();
        }

        void Update()
        {
            if (gameBoard == null || gameBoard.IsAnimating || UIManager.IsAdActive) return;

            HandleKeyboard();
            HandlePointerInput();
        }

        // ── Keyboard (new Input System) ───────────────────────
        private void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Left);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Right);
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Up);
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Down);
        }

        // ── Touch + Mouse (new Input System) ──────────────────
        private void HandlePointerInput()
        {
            // Prefer touch over mouse
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var primary = touchscreen.primaryTouch;

                if (primary.press.wasPressedThisFrame)
                {
                    pointerStart = primary.position.ReadValue();
                    isSwiping = true;
                }
                else if (primary.press.wasReleasedThisFrame && isSwiping)
                {
                    isSwiping = false;
                    ProcessSwipe(pointerStart, primary.position.ReadValue());
                }
                return;
            }

            // Mouse fallback (editor / desktop)
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pointerStart = mouse.position.ReadValue();
                isSwiping = true;
            }
            else if (mouse.leftButton.wasReleasedThisFrame && isSwiping)
            {
                isSwiping = false;
                ProcessSwipe(pointerStart, mouse.position.ReadValue());
            }
        }

        private void ProcessSwipe(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            if (delta.magnitude < swipeThreshold) return;

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                gameBoard.HandleSwipe(delta.x > 0 ? Direction.Right : Direction.Left);
            else
                gameBoard.HandleSwipe(delta.y > 0 ? Direction.Up : Direction.Down);
        }
    }
}
