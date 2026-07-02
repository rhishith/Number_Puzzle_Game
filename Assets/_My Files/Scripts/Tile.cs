using System.Collections;
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
        [SerializeField] private float moveDuration = 0.15f;
        [SerializeField] private float spawnDuration = 0.12f;
        [SerializeField] private float mergePunchScale = 0.2f;
        [SerializeField] private float squashAmount = 0.15f;

        // ── Movement ──
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float moveTimer;
        private bool isMoving;
        private System.Action onMoveComplete;

        // ── Scale animation ──
        private Vector3 naturalScale = Vector3.zero;   // captured once
        private bool isScaling;
        private float scaleTimer;
        private float scaleDuration;
        private Vector3 scaleFrom;
        private Vector3 scaleTo;

        // ── Rotation animation ──
        private bool isRotating;
        private float rotateTimer;
        private float rotateDuration;
        private float rotateFromZ;
        private float rotateToZ;

        // ── Shake animation ──
        private Coroutine shakeCoroutine;

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
            // ── Lerp position with Squash & Stretch ──
            if (isMoving)
            {
                moveTimer += Time.deltaTime;
                float t = Mathf.Clamp01(moveTimer / moveDuration);
                float ease = EaseOutCubic(t);

                transform.position = Vector3.Lerp(startPosition, targetPosition, ease);

                // Apply Squash & Stretch along the movement axis
                if (t < 1f && startPosition != targetPosition)
                {
                    Vector3 moveDir = (targetPosition - startPosition).normalized;
                    float currentSquash = Mathf.Sin(t * Mathf.PI) * squashAmount;

                    Vector3 scale = naturalScale;
                    if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
                    {
                        // Moving horizontally: stretch X, squash Y
                        scale.x *= (1f + currentSquash);
                        scale.y *= (1f - currentSquash);
                    }
                    else
                    {
                        // Moving vertically: stretch Y, squash X
                        scale.y *= (1f + currentSquash);
                        scale.x *= (1f - currentSquash);
                    }
                    transform.localScale = scale;
                }

                if (t >= 1f)
                {
                    transform.position = targetPosition;
                    transform.localScale = naturalScale; // Reset scale
                    isMoving = false;

                    // Trigger impact shake if the tile actually moved
                    if (startPosition != targetPosition)
                    {
                        Vector3 moveDir = (targetPosition - startPosition).normalized;
                        TriggerImpactShake(moveDir);
                    }

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

            // ── Rotate animation ──
            if (isRotating)
            {
                rotateTimer += Time.deltaTime;
                float t = Mathf.Clamp01(rotateTimer / rotateDuration);
                float ease = EaseOutBack(t);
                float currentZ = Mathf.LerpUnclamped(rotateFromZ, rotateToZ, ease);
                transform.localRotation = Quaternion.Euler(0f, 0f, currentZ);

                if (t >= 1f)
                {
                    transform.localRotation = Quaternion.identity;
                    isRotating = false;
                }
            }
        }

        // ───────────────────────────────────────────────────────
        // Public animation API
        // ───────────────────────────────────────────────────────

        public void AnimateMove(Vector3 target, System.Action onComplete = null)
        {
            startPosition = transform.position;
            targetPosition = target;
            moveTimer = 0f;
            isMoving = true;
            onMoveComplete = onComplete;
        }

        public void SetPositionImmediate(Vector3 pos)
        {
            transform.position = pos;
            targetPosition = pos;
            startPosition = pos;
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

        /// <summary>Scale from slightly oversized → natural (punch pop) and rotate wiggle.</summary>
        public void AnimateMerge()
        {
            scaleFrom = naturalScale * (1f + mergePunchScale);
            scaleTo = naturalScale;
            scaleDuration = spawnDuration;
            scaleTimer = 0f;
            isScaling = true;
            transform.localScale = scaleFrom;

            // Wiggle rotation punch: random direction, back to 0
            rotateFromZ = Random.value < 0.5f ? -12f : 12f;
            rotateToZ = 0f;
            rotateDuration = spawnDuration * 1.5f; // slightly longer wiggle
            rotateTimer = 0f;
            isRotating = true;
            transform.localRotation = Quaternion.Euler(0f, 0f, rotateFromZ);
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
        // ───────────────────────────────────────────────────────
        // Shake
        // ───────────────────────────────────────────────────────
        public void TriggerImpactShake(Vector3 direction)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeTile(direction));
        }

        private IEnumerator ShakeTile(Vector3 direction)
        {
            float duration = 0.12f;
            float elapsed = 0f;
            float maxOffset = 0.08f; // subtle offset

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;
                float decay = 1f - percent;
                float offset = Mathf.Sin(percent * Mathf.PI * 4f) * maxOffset * decay;

                transform.position = targetPosition + direction * offset;
                yield return null;
            }

            transform.position = targetPosition;
            shakeCoroutine = null;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }
}
