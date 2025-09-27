using MFPS.Runtime.Vehicles.Combat;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleUI : MonoBehaviour
    {
        [Header("Vehicle Stats")]
        public GameObject vehicleStatsUI;
        public GameObject exitButton;
        public TMP_Text vehicleHealth;

        public Slider vehicleHealthSlider;
        public Color HealthAbove60 = Color.cyan;
        public Color HealthAbove30 = Color.yellow;
        public Color HealthAbove0 = Color.red;

        public Slider vehicleBoostSlider;
        public Color BoostActive = Color.red;
        public Color BoostInActive = Color.yellow;

        [Header("Target Locking System")]
        public Image LockTargetCenter;
        public Image LockTargetLeft;
        public Image LockTargetRight;
        public Color TargetLocked = Color.red;
        public Color TargetLocking = Color.yellow;
        public Color TargetOpen = Color.cyan;
        public Color MissileLock = Color.magenta;

        public Slider lockProgressSlider;
        public TMP_Text lockDistanceText;
        public TMP_Text lockStatusText;
        public TMP_Text targetCountText;
        public TMP_Text flareCountText;
        public GameObject lockAcquiredPanel;
        public GameObject missileLockPanel;
        public AudioClip lockAcquiredSound;
        public AudioClip lockLostSound;
        public AudioClip missileLockSound;

        [Header("Radar Display")]
        public GameObject radarDisplay;
        public RectTransform radarScope;
        public Image radarBlipPrefab;
        public Color radarFriendColor = Color.green;
        public Color radarEnemyColor = Color.red;
        public Color radarLockedColor = Color.magenta;
        public float radarDisplayRange = 500f;

        [Header("Countermeasures Display")]
        public GameObject countermeasuresPanel;
        public TMP_Text flaresReadyText;
        public Image flareCooldownFill;

        [Header("Lock Indicator Prefabs")]
        public GameObject targetIndicatorPrefab;
        public Transform indicatorsParent;

        // Private variables
        private bl_VehicleManager activeVehicleTrigger;
        private bl_VehicleManager activeLocalVehicle;
        private bl_TargetLockSystem activeLockSystem;
        private int currentSeatID = 0;
        private int activeSeatID = 0;
        private AudioSource audioSource;
        private bool wasLocked = false;
        private bool wasMissileArmed = false;
        private Dictionary<int, Image> radarBlips = new Dictionary<int, Image>();
        private List<bl_TargetLockSystem.RadarContact> radarContacts = new List<bl_TargetLockSystem.RadarContact>();

        /// <summary>
        /// 
        /// </summary>
        private void Start()
        {
            ShowEnterUI(false);
            vehicleStatsUI.SetActive(false);
            InitializeSliders();
            InitializeLockUI();
            InitializeRadar();
            InitializeCountermeasures();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        /// <summary>
        /// Initialize vehicle stat sliders
        /// </summary>
        private void InitializeSliders()
        {
            if (vehicleHealthSlider != null)
            {
                vehicleHealthSlider.gameObject.SetActive(false);
                vehicleHealthSlider.minValue = 0;
                vehicleHealthSlider.maxValue = 100;
                vehicleHealthSlider.value = 100;
            }

            if (vehicleBoostSlider != null)
            {
                vehicleBoostSlider.gameObject.SetActive(false);
                vehicleBoostSlider.minValue = 0;
                vehicleBoostSlider.maxValue = 1;
                vehicleBoostSlider.value = 1;
            }

            if (lockProgressSlider != null)
            {
                lockProgressSlider.gameObject.SetActive(false);
                lockProgressSlider.minValue = 0;
                lockProgressSlider.maxValue = 1;
                lockProgressSlider.value = 0;
            }
        }

        /// <summary>
        /// Initialize target locking UI elements
        /// </summary>
        private void InitializeLockUI()
        {
            if (LockTargetCenter != null) LockTargetCenter.gameObject.SetActive(false);
            if (LockTargetLeft != null) LockTargetLeft.gameObject.SetActive(false);
            if (LockTargetRight != null) LockTargetRight.gameObject.SetActive(false);
            if (lockAcquiredPanel != null) lockAcquiredPanel.SetActive(false);
            if (missileLockPanel != null) missileLockPanel.SetActive(false);
            if (lockDistanceText != null) lockDistanceText.gameObject.SetActive(false);
            if (lockStatusText != null) lockStatusText.gameObject.SetActive(false);
            if (targetCountText != null) targetCountText.gameObject.SetActive(false);
            if (flareCountText != null) flareCountText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Initialize radar display
        /// </summary>
        private void InitializeRadar()
        {
            if (radarDisplay != null) radarDisplay.SetActive(false);
            if (radarScope != null) radarBlips.Clear();
        }
        /// <summary>
        /// Initialize countermeasures display
        /// </summary>
        private void InitializeCountermeasures()
        {
            if (countermeasuresPanel != null) countermeasuresPanel.SetActive(false);
            if (flareCooldownFill != null) flareCooldownFill.fillAmount = 1f;
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnEnable()
        {
            bl_VehicleEvents.onLocalEnterInVehicle += OnLocalEnterInVehicle;
            bl_VehicleEvents.onLocalExitInVehicle += OnLocalExitVehicle;
            bl_EventHandler.onLocalPlayerDeath += OnLocalPlayerDeath;
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnDisable()
        {
            bl_VehicleEvents.onLocalEnterInVehicle -= OnLocalEnterInVehicle;
            bl_VehicleEvents.onLocalExitInVehicle -= OnLocalExitVehicle;
            bl_EventHandler.onLocalPlayerDeath -= OnLocalPlayerDeath;

            // Clean up lock system reference
            activeLockSystem = null;
            radarContacts.Clear();
            ClearRadarBlips();
        }

        /// <summary>
        /// Main update loop for vehicle UI systems
        /// </summary>
        private void Update()
        {
            if (activeLocalVehicle != null)
            {
                UpdateHealthUI(activeLocalVehicle);

                // Update boost UI if it's a spaceship
                var carController = activeLocalVehicle.GetComponent<bl_CarController>();
                if (carController != null && carController.isSpaceShip)
                {
                    UpdateBoostUI(carController);
                }

                // Update target locking UI
                UpdateTargetLockUI();

                // Update radar display
                UpdateRadarDisplay();

                // Update countermeasures display
                UpdateCountermeasuresDisplay();
            }
        }

        /// <summary>
        /// Update target locking UI elements
        /// </summary>
        private void UpdateTargetLockUI()
        {
            if (activeLockSystem == null) return;

            bool isLocking = activeLockSystem.IsLocking;
            bool hasLock = activeLockSystem.HasLock;
            bool missileArmed = activeLockSystem.MissileArmed;
            float lockProgress = activeLockSystem.LockProgress;
            bl_VehicleManager primaryTarget = activeLockSystem.PrimaryTarget;
            int lockedTargetCount = activeLockSystem.LockedTargets.Count;
            int currentFlares = activeLockSystem.CurrentFlares;
            int maxFlares = activeLockSystem.MaxFlares;

            // Update lock progress slider
            if (lockProgressSlider != null)
            {
                lockProgressSlider.gameObject.SetActive(isLocking || hasLock);
                lockProgressSlider.value = lockProgress;

                var fillImage = lockProgressSlider.fillRect?.GetComponent<Image>();
                if (fillImage != null)
                {
                    if (missileArmed) fillImage.color = MissileLock;
                    else if (hasLock) fillImage.color = TargetLocked;
                    else if (isLocking) fillImage.color = TargetLocking;
                    else fillImage.color = TargetOpen;
                }
            }

            // Update lock status text
            if (lockStatusText != null)
            {
                lockStatusText.gameObject.SetActive(isLocking || hasLock);
                if (missileArmed) lockStatusText.text = "MISSILE LOCK";
                else if (hasLock) lockStatusText.text = "TARGET LOCKED";
                else if (isLocking) lockStatusText.text = "ACQUIRING LOCK...";
                else lockStatusText.text = "SEARCHING";

                lockStatusText.color = missileArmed ? MissileLock : (hasLock ? TargetLocked : (isLocking ? TargetLocking : TargetOpen));
            }

            // Update target count text
            if (targetCountText != null)
            {
                targetCountText.gameObject.SetActive(lockedTargetCount > 0);
                targetCountText.text = $"TARGETS: {lockedTargetCount}";
            }

            // Update flare count text
            if (flareCountText != null)
            {
                flareCountText.gameObject.SetActive(true);
                flareCountText.text = $"FLARES: {currentFlares}/{maxFlares}";
                flareCountText.color = currentFlares > 0 ? Color.green : Color.red;
            }

            // Update distance text
            if (lockDistanceText != null && primaryTarget != null)
            {
                lockDistanceText.gameObject.SetActive(true);
                float distance = Vector3.Distance(activeLocalVehicle.transform.position, primaryTarget.transform.position);
                lockDistanceText.text = $"{distance:F0}m";
            }
            else if (lockDistanceText != null)
            {
                lockDistanceText.gameObject.SetActive(false);
            }

            // Update lock indicator positions
            UpdateLockIndicators(primaryTarget, isLocking, hasLock, missileArmed);

            // Play lock acquired/lost sounds
            if (hasLock && !wasLocked)
            {
                OnLockAcquired();
            }
            else if (!hasLock && wasLocked)
            {
                OnLockLost();
            }

            // Play missile lock sounds
            if (missileArmed && !wasMissileArmed)
            {
                OnMissileLockAcquired();
            }
            else if (!missileArmed && wasMissileArmed)
            {
                OnMissileLockLost();
            }

            wasLocked = hasLock;
            wasMissileArmed = missileArmed;
        }

        /// <summary>
        /// Update the lock indicator positions on screen
        /// </summary>
        private void UpdateLockIndicators(bl_VehicleManager target, bool isLocking, bool hasLock, bool missileArmed)
        {
            if (target == null)
            {
                SetLockIndicatorVisibility(false);
                return;
            }

            Camera vehicleCamera = bl_VehicleCamera.Instance?.cameraRef;
            if (vehicleCamera == null) return;

            Vector3 screenPos = vehicleCamera.WorldToScreenPoint(target.transform.position);
            bool isOnScreen = screenPos.z > 0 &&
                             screenPos.x >= 0 && screenPos.x <= Screen.width &&
                             screenPos.y >= 0 && screenPos.y <= Screen.height;

            if (isOnScreen)
            {
                UpdateCenterIndicator(screenPos, isLocking, hasLock, missileArmed);
            }
            else
            {
                UpdateEdgeIndicators(screenPos, isLocking, hasLock, missileArmed);
            }
        }

        /// <summary>
        /// Update center lock indicator when target is on screen
        /// </summary>
        private void UpdateCenterIndicator(Vector3 screenPos, bool isLocking, bool hasLock, bool missileArmed)
        {
            if (LockTargetCenter != null)
            {
                LockTargetCenter.gameObject.SetActive(true);
                LockTargetCenter.transform.position = screenPos;

                // Pulse effect based on lock state
                float pulseSpeed = missileArmed ? 3f : (hasLock ? 2f : 1f);
                float pulseScale = 1f + Mathf.PingPong(Time.time * pulseSpeed, 0.3f);
                LockTargetCenter.transform.localScale = Vector3.one * pulseScale;

                LockTargetCenter.color = missileArmed ? MissileLock : (hasLock ? TargetLocked : (isLocking ? TargetLocking : TargetOpen));
            }

            if (LockTargetLeft != null) LockTargetLeft.gameObject.SetActive(false);
            if (LockTargetRight != null) LockTargetRight.gameObject.SetActive(false);
        }

        /// <summary>
        /// Update edge indicators when target is off screen
        /// </summary>
        private void UpdateEdgeIndicators(Vector3 screenPos, bool isLocking, bool hasLock, bool missileArmed)
        {
            if (LockTargetCenter != null) LockTargetCenter.gameObject.SetActive(false);

            Vector3 viewportPos = Camera.main.ScreenToViewportPoint(screenPos);
            bool isLeft = viewportPos.x < 0.5f;
            Color indicatorColor = missileArmed ? MissileLock : (hasLock ? TargetLocked : (isLocking ? TargetLocking : TargetOpen));

            if (isLeft && LockTargetLeft != null)
            {
                LockTargetLeft.gameObject.SetActive(true);
                LockTargetLeft.color = indicatorColor;

                Vector3 edgePos = new Vector3(50, screenPos.y, 0);
                LockTargetLeft.transform.position = edgePos;

                Vector3 direction = (screenPos - edgePos).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                LockTargetLeft.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            else if (!isLeft && LockTargetRight != null)
            {
                LockTargetRight.gameObject.SetActive(true);
                LockTargetRight.color = indicatorColor;

                Vector3 edgePos = new Vector3(Screen.width - 50, screenPos.y, 0);
                LockTargetRight.transform.position = edgePos;

                Vector3 direction = (screenPos - edgePos).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                LockTargetRight.transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            if (LockTargetLeft != null) LockTargetLeft.gameObject.SetActive(isLeft);
            if (LockTargetRight != null) LockTargetRight.gameObject.SetActive(!isLeft);
        }

        /// <summary>
        /// Update radar display with current contacts
        /// </summary>
        private void UpdateRadarDisplay()
        {
            if (radarDisplay == null || activeLockSystem == null) return;

            radarDisplay.SetActive(true);
            radarContacts = activeLockSystem.GetRadarContacts();

            // Clear old blips
            ClearRadarBlips();

            // Create new blips for each contact
            foreach (var contact in radarContacts)
            {
                if (contact.vehicle == null) continue;

                Vector3 relativePos = activeLocalVehicle.transform.InverseTransformPoint(contact.position);
                Vector2 radarPos = new Vector2(relativePos.x, relativePos.z) / radarDisplayRange;

                // Clamp to radar scope
                radarPos = Vector2.ClampMagnitude(radarPos, 0.95f);

                CreateRadarBlip(contact, radarPos);
            }
        }

        /// <summary>
        /// Create a radar blip for a contact
        /// </summary>
        private void CreateRadarBlip(bl_TargetLockSystem.RadarContact contact, Vector2 position)
        {
            if (radarBlipPrefab == null || radarScope == null) return;

            Image blip = Instantiate(radarBlipPrefab, radarScope);
            RectTransform blipRect = blip.GetComponent<RectTransform>();

            // Position in radar scope (normalized to -1 to 1 range)
            blipRect.anchoredPosition = position * (radarScope.rect.width * 0.5f);

            // Color based on threat level and lock status
            if (contact.isLocked)
            {
                blip.color = radarLockedColor;
            }
            else
            {
                switch (contact.threatLevel)
                {
                    case bl_TargetLockSystem.ThreatLevel.Critical:
                    case bl_TargetLockSystem.ThreatLevel.High:
                        blip.color = radarEnemyColor;
                        break;
                    default:
                        blip.color = radarFriendColor;
                        break;
                }
            }

            // Size based on distance (closer = larger)
            float size = Mathf.Lerp(0.3f, 1f, 1f - (contact.distance / radarDisplayRange));
            blipRect.sizeDelta = Vector2.one * (20f * size);

            radarBlips[contact.vehicle.photonView.ViewID] = blip;
        }

        /// <summary>
        /// Update countermeasures display
        /// </summary>
        private void UpdateCountermeasuresDisplay()
        {
            if (countermeasuresPanel == null || activeLockSystem == null) return;

            countermeasuresPanel.SetActive(true);

            if (flaresReadyText != null)
            {
                flaresReadyText.text = $"{activeLockSystem.CurrentFlares}";
                flaresReadyText.color = activeLockSystem.CurrentFlares > 0 ? Color.green : Color.red;
            }

            if (flareCooldownFill != null)
            {
                // Simple cooldown indicator - you can enhance this with actual cooldown timing
                flareCooldownFill.fillAmount = (float)activeLockSystem.CurrentFlares / activeLockSystem.MaxFlares;
            }
        }

        /// <summary>
        /// Clear all radar blips
        /// </summary>
        private void ClearRadarBlips()
        {
            foreach (var blip in radarBlips.Values)
            {
                if (blip != null) Destroy(blip.gameObject);
            }
            radarBlips.Clear();
        }

        /// <summary>
        /// Set visibility of all lock indicators
        /// </summary>
        private void SetLockIndicatorVisibility(bool visible)
        {
            if (LockTargetCenter != null) LockTargetCenter.gameObject.SetActive(visible);
            if (LockTargetLeft != null) LockTargetLeft.gameObject.SetActive(visible);
            if (LockTargetRight != null) LockTargetRight.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Called when lock is acquired
        /// </summary>
        private void OnLockAcquired()
        {
            if (lockAcquiredPanel != null)
            {
                lockAcquiredPanel.SetActive(true);
                StartCoroutine(HidePanelAfterDelay(lockAcquiredPanel, 2f));
            }

            if (lockAcquiredSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(lockAcquiredSound);
            }

#if UNITY_ANDROID || UNITY_IOS
            if (bl_UtilityHelper.isMobile) Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Called when missile lock is acquired
        /// </summary>
        private void OnMissileLockAcquired()
        {
            if (missileLockPanel != null)
            {
                missileLockPanel.SetActive(true);
                StartCoroutine(HidePanelAfterDelay(missileLockPanel, 2f));
            }

            if (missileLockSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(missileLockSound);
            }

#if UNITY_ANDROID || UNITY_IOS
            if (bl_UtilityHelper.isMobile) Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Called when lock is lost
        /// </summary>
        private void OnLockLost()
        {
            if (lockLostSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(lockLostSound);
            }
        }

        /// <summary>
        /// Called when missile lock is lost
        /// </summary>
        private void OnMissileLockLost()
        {
            // Optional: Add specific missile lock lost feedback
        }

        /// <summary>
        /// Hide a panel after a delay
        /// </summary>
        private IEnumerator HidePanelAfterDelay(GameObject panel, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>
        /// Update the lock indicator positions on screen
        /// </summary>
        private void UpdateLockIndicators(bl_VehicleManager target, bool isLocking, bool hasLock)
        {
            if (target == null)
            {
                // No target - hide indicators
                SetLockIndicatorVisibility(false);
                return;
            }

            Camera vehicleCamera = bl_VehicleCamera.Instance?.cameraRef;
            if (vehicleCamera == null) return;

            Vector3 screenPos = vehicleCamera.WorldToScreenPoint(target.transform.position);
            bool isOnScreen = screenPos.z > 0 &&
                             screenPos.x >= 0 && screenPos.x <= Screen.width &&
                             screenPos.y >= 0 && screenPos.y <= Screen.height;

            if (isOnScreen)
            {
                // Target is on screen - use center indicator
                UpdateCenterIndicator(screenPos, isLocking, hasLock);
            }
            else
            {
                // Target is off screen - use edge indicators
                UpdateEdgeIndicators(screenPos, isLocking, hasLock);
            }
        }

        /// <summary>
        /// Update center lock indicator when target is on screen
        /// </summary>
        private void UpdateCenterIndicator(Vector3 screenPos, bool isLocking, bool hasLock)
        {
            if (LockTargetCenter != null)
            {
                LockTargetCenter.gameObject.SetActive(true);
                LockTargetCenter.transform.position = screenPos;

                // Pulse effect when locking
                float pulseScale = hasLock ? 1f : (1f + Mathf.PingPong(Time.time * 2f, 0.3f));
                LockTargetCenter.transform.localScale = Vector3.one * pulseScale;

                LockTargetCenter.color = hasLock ? TargetLocked : (isLocking ? TargetLocking : TargetOpen);
            }

            // Hide edge indicators
            if (LockTargetLeft != null) LockTargetLeft.gameObject.SetActive(false);
            if (LockTargetRight != null) LockTargetRight.gameObject.SetActive(false);
        }

        /// <summary>
        /// Update edge indicators when target is off screen
        /// </summary>
        private void UpdateEdgeIndicators(Vector3 screenPos, bool isLocking, bool hasLock)
        {
            if (LockTargetCenter != null) LockTargetCenter.gameObject.SetActive(false);

            Vector3 viewportPos = Camera.main.ScreenToViewportPoint(screenPos);
            bool isLeft = viewportPos.x < 0.5f;

            if (isLeft && LockTargetLeft != null)
            {
                LockTargetLeft.gameObject.SetActive(true);
                LockTargetLeft.color = hasLock ? TargetLocked : (isLocking ? TargetLocking : TargetOpen);

                // Position at left edge, pointing toward target
                Vector3 edgePos = new Vector3(50, screenPos.y, 0);
                LockTargetLeft.transform.position = edgePos;

                // Rotate to point toward target
                Vector3 direction = (screenPos - edgePos).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                LockTargetLeft.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            else if (!isLeft && LockTargetRight != null)
            {
                LockTargetRight.gameObject.SetActive(true);
                LockTargetRight.color = hasLock ? TargetLocked : (isLocking ? TargetLocking : TargetOpen);

                // Position at right edge, pointing toward target
                Vector3 edgePos = new Vector3(Screen.width - 50, screenPos.y, 0);
                LockTargetRight.transform.position = edgePos;

                // Rotate to point toward target
                Vector3 direction = (screenPos - edgePos).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                LockTargetRight.transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // Hide the indicator on the opposite side
            if (LockTargetLeft != null) LockTargetLeft.gameObject.SetActive(isLeft);
            if (LockTargetRight != null) LockTargetRight.gameObject.SetActive(!isLeft);
        }

        

        /// <summary>
        /// Hide the lock acquired panel after a delay
        /// </summary>
        private IEnumerator HideLockAcquiredPanel()
        {
            yield return new WaitForSeconds(2f);
            if (lockAcquiredPanel != null)
            {
                lockAcquiredPanel.SetActive(false);
            }
        }

        // Existing methods with target locking integration...

        /// <summary>
        /// 
        /// </summary>
        void OnLocalEnterInVehicle(bl_VehicleManager vehicle)
        {
            ShowEnterUI(false);
            vehicleStatsUI.SetActive(true);
            activeLocalVehicle = vehicle;

            // Get the target lock system if it exists
            activeLockSystem = vehicle.GetComponent<bl_TargetLockSystem>();

            // Initialize health slider for the vehicle
            UpdateHealthUI(vehicle);

            if (exitButton != null) exitButton.SetActive(bl_UtilityHelper.isMobile);
#if MFPSM
            var mobileLayers = Mobile.bl_MobileButtonLayers.Instance;
            if (mobileLayers != null)
            {
                mobileLayers.SetActiveButtonGroup(vehicle.vehicle.mobileButtonLayer);
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        void OnLocalExitVehicle(bl_VehicleManager vehicle)
        {
            vehicleStatsUI.SetActive(false);
            activeLocalVehicle = null;
            activeLockSystem = null;
            currentSeatID = 0;

            // Hide all UI elements
            if (vehicleHealthSlider != null) vehicleHealthSlider.gameObject.SetActive(false);
            if (vehicleBoostSlider != null) vehicleBoostSlider.gameObject.SetActive(false);
            SetLockIndicatorVisibility(false);
            if (lockProgressSlider != null) lockProgressSlider.gameObject.SetActive(false);
            if (lockDistanceText != null) lockDistanceText.gameObject.SetActive(false);
            if (lockStatusText != null) lockStatusText.gameObject.SetActive(false);
            if (lockAcquiredPanel != null) lockAcquiredPanel.SetActive(false);

#if MFPSM
            var mobileLayers = MFPS.Mobile.bl_MobileButtonLayers.Instance;
            if (mobileLayers != null)
            {
                mobileLayers.SetActiveButtonGroup(0);
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        void OnLocalPlayerDeath()
        {
            ShowEnterUI(false);
            vehicleStatsUI.SetActive(false);
            activeLocalVehicle = null;
            activeLockSystem = null;
            currentSeatID = -1;
            activeSeatID = -1;

            // Hide all UI elements
            if (vehicleHealthSlider != null) vehicleHealthSlider.gameObject.SetActive(false);
            if (vehicleBoostSlider != null) vehicleBoostSlider.gameObject.SetActive(false);
            SetLockIndicatorVisibility(false);
            if (lockProgressSlider != null) lockProgressSlider.gameObject.SetActive(false);
            if (lockDistanceText != null) lockDistanceText.gameObject.SetActive(false);
            if (lockStatusText != null) lockStatusText.gameObject.SetActive(false);
            if (lockAcquiredPanel != null) lockAcquiredPanel.SetActive(false);
        }

        // ... Rest of the existing methods remain the same ...

        /// <summary>
        /// Update vehicle health UI
        /// </summary>
        private void UpdateHealthUI(bl_VehicleManager vehicle)
        {
            if (vehicle.VehicleHealth == null) return;

            // Update health text
            if (vehicleHealth != null)
            {
                vehicleHealth.text = ((int)vehicle.VehicleHealth.CurrentHealth).ToString();
            }

            // Update health slider
            if (vehicleHealthSlider != null)
            {
                vehicleHealthSlider.gameObject.SetActive(true);
                vehicleHealthSlider.value = vehicle.VehicleHealth.CurrentHealth;

                // Optional: Change color based on health percentage
                var fillImage = vehicleHealthSlider.targetGraphic?.GetComponent<Image>();
                if (fillImage != null)
                {
                    float healthPercent = vehicle.VehicleHealth.CurrentHealth / 100;
                    if (healthPercent > 0.6f)
                        fillImage.color = HealthAbove60;
                    else if (healthPercent > 0.3f)
                        fillImage.color = HealthAbove30;
                    else
                        fillImage.color = HealthAbove0;
                }
            }
        }

        /// <summary>
        /// Update boost UI for spaceship
        /// </summary>
        private void UpdateBoostUI(bl_CarController spaceship)
        {
            if (vehicleBoostSlider != null)
            {
                vehicleBoostSlider.gameObject.SetActive(true);
                vehicleBoostSlider.value = spaceship.GetBoostPercentage();

                // Optional: Change color when boosting
                var fillImage = vehicleBoostSlider.targetGraphic?.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = spaceship.IsBoosting() ? BoostActive : BoostInActive;
                }
            }
        }

        /// <summary>
        /// Public API for updating lock UI from external systems
        /// </summary>
        public void UpdateLockUI(bool isLocking, float lockProgress, bool isLocked)
        {
            // This method is called by the target lock system
            // The main UpdateTargetLockUI() method handles the actual updates
        }

        // ... Rest of the existing methods ...

        private static bl_VehicleUI _instance;
        public static bl_VehicleUI Instance
        {
            get
            {
                if (_instance == null) { _instance = FindObjectOfType<bl_VehicleUI>(); }
                return _instance;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnEnterVehicleClick()
        {
            if (activeVehicleTrigger == null) return;

            if (currentSeatID < 0)
            {
                activeVehicleTrigger.IntentToEnterInVehicle();
            }
            else
            {
                activeSeatID = currentSeatID;
                activeVehicleTrigger.VehicleSeats.IntentToEnterInSeat();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnExitVehicleClick()
        {
            if (activeLocalVehicle == null)
            {
                Debug.LogWarning("Intent to exit a vehicle when is not registered as inside a vehicle.");
                return;
            }

            if (activeSeatID < 0)
                activeLocalVehicle.IntentToExitVehicle();
            else
            {
                activeLocalVehicle.VehicleSeats?.IntentToExitSeat();
                activeSeatID = -1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vehicle"></param>
        public void UpdateStats(bl_VehicleManager vehicle)
        {
            UpdateHealthUI(vehicle);

            // Update boost UI if it's a spaceship
            var carController = vehicle.GetComponent<bl_CarController>();
            if (carController != null && carController.isSpaceShip)
            {
                UpdateBoostUI(carController);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        public void ShowEnterUI(bool active, bl_VehicleManager vehicle = null, int seatID = -1)
        {
            activeVehicleTrigger = active ? vehicle : null;
            currentSeatID = seatID;
            if (active)
            {
                string inputName = bl_UtilityHelper.isMobile ? "TOUCH" : bl_Input.GetButtonName("Interact");
                bl_InputInteractionIndicator.ShowIndication(inputName, "TO ENTER IN VEHICLE", () =>
                {
                    OnEnterVehicleClick();
                });
            }
            else
                bl_InputInteractionIndicator.SetActive(false);
        }


        
    }
}