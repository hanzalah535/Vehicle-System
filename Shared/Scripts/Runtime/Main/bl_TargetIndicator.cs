using UnityEngine;
using UnityEngine.UI;

namespace MFPS.Runtime.Vehicles.Combat
{
    public class bl_TargetIndicator : MonoBehaviour
    {
        [Header("Indicator Settings")]
        public float screenMargin = 50f;
        public float scaleSpeed = 2f;
        public Color lockedColor = Color.red;
        public Color lockingColor = Color.yellow;
        public Color neutralColor = Color.white;

        [Header("References")]
        public Image indicatorImage;
        public Text distanceText;
        public CanvasGroup canvasGroup;

        private Transform target;
        private Camera vehicleCamera;
        private RectTransform rectTransform;
        private bool isVisible = true;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            vehicleCamera = bl_VehicleCamera.Instance?.cameraRef;

            if (indicatorImage != null)
            {
                indicatorImage.color = neutralColor;
            }
        }

        public void SetTarget(Transform targetTransform)
        {
            target = targetTransform;
            gameObject.SetActive(true);
        }

        public void UpdateIndicator()
        {
            Debug.Log("Indicating1");
            if (target == null || vehicleCamera == null)
            {
                Destroy(gameObject);
                return;
            }
            Debug.Log("Indicating2");
            UpdatePosition();
            UpdateAppearance();
        }

        private void UpdatePosition()
        {
            Vector3 screenPos = vehicleCamera.WorldToScreenPoint(target.position);

            // Check if target is behind camera
            if (screenPos.z < 0)
            {
                screenPos *= -1;
            }

            // Clamp to screen bounds with margin
            screenPos.x = Mathf.Clamp(screenPos.x, screenMargin, Screen.width - screenMargin);
            screenPos.y = Mathf.Clamp(screenPos.y, screenMargin, Screen.height - screenMargin);

            rectTransform.position = screenPos;
            Debug.Log(screenPos);
            // Update distance text
            if (distanceText != null)
            {
                float distance = Vector3.Distance(vehicleCamera.transform.position, target.position);
                distanceText.text = $"{distance:F0}m";
            }
        }

        private void UpdateAppearance()
        {
            // This will be enhanced in Phase 2 with proper lock state integration
            if (indicatorImage != null)
            {
                // Simple color change based on visibility
                Vector3 viewportPos = vehicleCamera.WorldToViewportPoint(target.position);
                bool isOnScreen = viewportPos.x >= 0 && viewportPos.x <= 1 &&
                                 viewportPos.y >= 0 && viewportPos.y <= 1 && viewportPos.z > 0;

                indicatorImage.color = isOnScreen ? neutralColor : lockingColor;
            }
        }

        public void SetLockState(bool isLocking, bool isLocked)
        {
            if (indicatorImage == null) return;

            if (isLocked)
            {
                indicatorImage.color = lockedColor;
            }
            else if (isLocking)
            {
                indicatorImage.color = lockingColor;
            }
            else
            {
                indicatorImage.color = neutralColor;
            }
        }

        public void Show(bool show)
        {
            isVisible = show;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = show ? 1f : 0f;
            }
            else
            {
                gameObject.SetActive(show);
            }
        }
    }
}