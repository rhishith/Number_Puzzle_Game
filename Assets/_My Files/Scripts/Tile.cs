using UnityEngine;
using TMPro;

namespace SlideAndMatch
{
    /// <summary>
    /// Visual representation of a single tile on the board.
    /// Handles smooth Lerp movement and scale pop animations.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Tile : MonoBehaviour
    {
        // ── References (set via Inspector for prefab, or via Initialize for code-created) ──
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private TextMeshPro label;

        // ── Tunables ──
        [Header("Animation")]
        [SerializeField] private float moveSpeed = 18f;
        [SerializeField] private float spawnDuration = 0.12f;
        [SerializeField] private float mergePunchScale = 0.2f;

        // ── Movement ──
        private Vector3 targetPosition;
        private bool isMoving;
        private System.Action onMoveComplete;

        // ── Scale animation ──
        private Vector3 naturalScale = Vector3.zero;   // captured once
        private bool isScaling;
        private float scaleTimer;
        private float scaleDuration;
        private Vector3 scaleFrom;
        private Vector3 scaleTo;

        // ── Value ──
        private int tileValue;
        public int Value
        {
            get => tileValue;
            set { tileValue = value; UpdateVisuals(); }
        }

        public Vector2Int GridPosition { get; set; }
        public bool IsAnimating => isMoving || isScaling;

        // ───────────────────────────────────────────────────────
        // Init
        // ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by GameBoard when creating a tile from code (no prefab).
        /// </summary>
        public void Initialize(SpriteRenderer bg, TextMeshPro lbl, Vector3 scale)
        {
            background = bg;
            label = lbl;
            naturalScale = scale;
            transform.localScale = scale;
        }

        void Awake()
        {
            // Prefab path — grab serialized refs if not yet set
            if (background == null)
                background = GetComponent<SpriteRenderer>();
            if (label == null)
                label = GetComponentInChildren<TextMeshPro>();

            // Capture "resting" scale (skip if Initialize already set it)
            if (naturalScale == Vector3.zero)
                naturalScale = transform.localScale;
        }

        // ───────────────────────────────────────────────────────
        // Tick
        // ───────────────────────────────────────────────────────
        void Update()
        {
            // ── Lerp position ──
            if (isMoving)
            {
                transform.position = Vector3.Lerp(
                    transform.position, targetPosition, Time.deltaTime * moveSpeed);

                if (Vector3.SqrMagnitude(transform.position - targetPosition) < 0.0001f)
                {
                    transform.position = targetPosition;
                    isMoving = false;
                    var cb = onMoveComplete;
                    onMoveComplete = null;
                    cb?.Invoke();
                }
            }

            // ── Scale animation ──
            if (isScaling)
            {
                scaleTimer += Time.deltaTime;
                float t = Mathf.Clamp01(scaleTimer / scaleDuration);
                float ease = EaseOutBack(t);
                transform.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, ease);

                if (t >= 1f)
                {
                    transform.localScale = scaleTo;
                    isScaling = false;
                }
            }
        }

        // ───────────────────────────────────────────────────────
        // Public animation API
        // ───────────────────────────────────────────────────────

        public void AnimateMove(Vector3 target, System.Action onComplete = null)
        {
            targetPosition = target;
            isMoving = true;
            onMoveComplete = onComplete;
        }

        public void SetPositionImmediate(Vector3 pos)
        {
            transform.position = pos;
            targetPosition = pos;
            isMoving = false;
        }

        /// <summary>Scale from zero → natural (pop in).</summary>
        public void AnimateSpawn()
        {
            scaleFrom = Vector3.zero;
            scaleTo = naturalScale;
            scaleDuration = spawnDuration;
            scaleTimer = 0f;
            isScaling = true;
            transform.localScale = Vector3.zero;
        }

        /// <summary>Scale from slightly oversized → natural (punch pop).</summary>
        public void AnimateMerge()
        {
            scaleFrom = naturalScale * (1f + mergePunchScale);
            scaleTo = naturalScale;
            scaleDuration = spawnDuration;
            scaleTimer = 0f;
            isScaling = true;
            transform.localScale = scaleFrom;
        }

        // ───────────────────────────────────────────────────────
        // Visuals
        // ───────────────────────────────────────────────────────

        private void UpdateVisuals()
        {
            if (background != null)
                background.color = TileStyleLookup.GetTileColor(tileValue);

            if (label != null)
            {
                label.text = tileValue.ToString();
                label.color = TileStyleLookup.GetTextColor(tileValue);
            }
        }

        // ───────────────────────────────────────────────────────
        // Easing
        // ───────────────────────────────────────────────────────
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
