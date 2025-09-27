using UnityEngine;
using UnityEngine.UI;

namespace MFPS.Runtime.Vehicles.Combat
{
    public class bl_LockIndicatorAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float pulseSpeed = 2f;
        public float pulseAmount = 0.3f;
        public float rotationSpeed = 45f;

        [Header("References")]
        public Image indicatorImage;
        public RectTransform rectTransform;

        private Vector3 originalScale;
        private bool isPulsing = false;
        private bool isRotating = false;

        void Awake()
        {
            if (rectTransform != null)
            {
                originalScale = rectTransform.localScale;
            }
        }

        void Update()
        {
            if (isPulsing)
            {
                // Pulse animation
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                rectTransform.localScale = originalScale * pulse;
            }

            if (isRotating && rectTransform != null)
            {
                // Rotation animation
                rectTransform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            }
        }

        public void SetPulsing(bool pulse)
        {
            isPulsing = pulse;
            if (!pulse)
            {
                rectTransform.localScale = originalScale;
            }
        }

        public void SetRotating(bool rotate)
        {
            isRotating = rotate;
        }

        public void SetColor(Color color)
        {
            if (indicatorImage != null)
            {
                indicatorImage.color = color;
            }
        }
    }
}