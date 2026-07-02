using UnityEngine;

namespace SlideAndMatch
{
    /// <summary>
    /// Detects keyboard (Arrow / WASD) and touch-swipe input using
    /// Unity's legacy Input system, then forwards the direction to
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

        // ── Keyboard (legacy Input) ───────────────────────
        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                gameBoard.HandleSwipe(Direction.Left);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                gameBoard.HandleSwipe(Direction.Right);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                gameBoard.HandleSwipe(Direction.Up);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                gameBoard.HandleSwipe(Direction.Down);
        }

        // ── Touch + Mouse (legacy Input) ──────────────────
        private void HandlePointerInput()
        {
            // Prefer touch over mouse (mobile devices)
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    pointerStart = touch.position;
                    isSwiping = true;
                }
                else if (touch.phase == TouchPhase.Ended && isSwiping)
                {
                    isSwiping = false;
                    ProcessSwipe(pointerStart, touch.position);
                }
                return;
            }

            // Mouse fallback (editor / desktop)
            if (Input.GetMouseButtonDown(0))
            {
                pointerStart = Input.mousePosition;
                isSwiping = true;
            }
            else if (Input.GetMouseButtonUp(0) && isSwiping)
            {
                isSwiping = false;
                ProcessSwipe(pointerStart, Input.mousePosition);
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
