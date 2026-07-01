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

        void Update()
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea != lastSafeArea)
            {
                ApplySafeArea(safeArea);
                lastSafeArea = safeArea;
            }
        }

        private void ApplySafeArea(Rect safeArea)
        {
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
