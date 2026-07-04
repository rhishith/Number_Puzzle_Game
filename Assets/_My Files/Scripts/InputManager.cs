using UnityEngine;
using UnityEngine.InputSystem;

namespace SlideAndMatch
{
    /// <summary>
    /// Detects keyboard (Arrow / WASD) and touch-swipe input using
    /// Unity's new Input System, then forwards the direction to
    /// GameBoard.HandleSwipe(). Auto-finds GameBoard if no reference is assigned.
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

        // ── Keyboard (New Input System) ───────────────────
        private void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Right);
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Up);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                gameBoard.HandleSwipe(Direction.Down);
        }

        // ── Touch + Mouse (New Input System) ───────────────
        private void HandlePointerInput()
        {
            // Prefer touch over mouse (mobile devices)
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                var touch = touchscreen.touches[0];
                var phase = touch.phase.ReadValue();
                var pos = touch.position.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (gameBoard != null && gameBoard.IsPositionInsideBoard(pos))
                    {
                        pointerStart = pos;
                        isSwiping = true;
                    }
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended && isSwiping)
                {
                    isSwiping = false;
                    ProcessSwipe(pointerStart, pos);
                }
                return;
            }

            // Mouse fallback (editor / desktop)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var mousePos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (gameBoard != null && gameBoard.IsPositionInsideBoard(mousePos))
                    {
                        pointerStart = mousePos;
                        isSwiping = true;
                    }
                }
                else if (mouse.leftButton.wasReleasedThisFrame && isSwiping)
                {
                    isSwiping = false;
                    ProcessSwipe(pointerStart, mousePos);
                }
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
