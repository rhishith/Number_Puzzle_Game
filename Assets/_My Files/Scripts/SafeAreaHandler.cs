using UnityEngine;

namespace SlideAndMatch
{
    /// <summary>
    /// Attach to any Canvas child RectTransform to make it respect
    /// Screen.safeArea (handles notches, rounded corners, etc.).
    /// Re-evaluates every frame to handle orientation changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHandler : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private int lastWidth = 0;
        private int lastHeight = 0;

        void Update()
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea != lastSafeArea || Screen.width != lastWidth || Screen.height != lastHeight)
            {
                ApplySafeArea(safeArea);
                lastSafeArea = safeArea;
                lastWidth = Screen.width;
                lastHeight = Screen.height;
            }
        }

        private void ApplySafeArea(Rect safeArea)
        {
            if (Screen.width <= 0 || Screen.height <= 0 || safeArea.width <= 0 || safeArea.height <= 0)
                return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Clamp anchors to valid range [0, 1]
            anchorMin.x = Mathf.Clamp01(anchorMin.x);
            anchorMin.y = Mathf.Clamp01(anchorMin.y);
            anchorMax.x = Mathf.Clamp01(anchorMax.x);
            anchorMax.y = Mathf.Clamp01(anchorMax.y);

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
