using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using MFPS.Runtime.Vehicles;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace MFPS.Runtime.Vehicles.Combat
{
    public class bl_TargetLockSystem : bl_MonoBehaviour, IPunObservable
    {
        [Header("Lock Settings")]
        public float lockRange = 500f;
        public float lockAngle = 30f;
        public float lockTimeRequired = 2f;
        public float lockBreakDistance = 600f;
        public float lockBreakAngle = 45f;
        public float maxLockedTargets = 1;

        [Header("Target Priorities")]
        public LayerMask targetLayers = 1 << 0;
        public TargetPriority targetPriority = TargetPriority.Closest;

        [Header("Visual Feedback")]
        public AudioClip lockStartSound;
        public AudioClip lockAcquiredSound;
        public AudioClip lockLostSound;
        public AudioClip missileLockSound;
        public GameObject lockIndicatorPrefab;

        [Header("Missile Guidance")]
        public bool enableMissileGuidance = true;
        public float missileLockRange = 400f;
        public float missileLaunchDelay = 0.5f;
        public GameObject missilePrefab;
        public Transform missileLaunchPoint;

        [Header("Countermeasures")]
        public bool enableCountermeasures = true;
        public int flareCount = 10;
        public float flareCooldown = 5f;
        public GameObject flarePrefab;
        public Transform flareLauncher;

        [Header("Radar System")]
        public bool enableRadar = true;
        public float radarRange = 1000f;
        public float radarScanInterval = 0.5f;
        public RadarDisplayType radarDisplay = RadarDisplayType.Circular;

        [Header("Debug")]
        public bool showDebugRays = false;
        public bool showRadarDebug = false;

        // Private variables
        private bl_VehicleManager m_vehicle;
        private bl_CarController m_carController;
        private bl_VehicleUI m_vehicleUI;
        private AudioSource m_audioSource;
        private PhotonView m_photonView;

        // Lock state
        private List<bl_VehicleManager> lockedTargets = new List<bl_VehicleManager>();
        private bl_VehicleManager primaryTarget;
        private bl_VehicleManager currentTarget;
        private float currentLockTime = 0f;
        private bool isLocking = false;
        private bool hasLock = false;
        private Dictionary<int, bl_TargetIndicator> targetIndicators = new Dictionary<int, bl_TargetIndicator>();

        // Missile system
        private bool missileArmed = false;
        private bl_VehicleManager missileLockedTarget;
        private float lastMissileLaunchTime = 0f;

        // Countermeasures
        private int currentFlares;
        private float lastFlareTime = 0f;
        private bool countermeasuresActive = false;

        // Radar system
        private List<bl_VehicleManager> radarContacts = new List<bl_VehicleManager>();
        private float lastRadarScanTime = 0f;
        private Dictionary<int, RadarContact> radarContactData = new Dictionary<int, RadarContact>();

        // Network sync
        private int[] syncedLockedTargetIDs = new int[0];
        private float syncedLockProgress = 0f;
        private int syncedMissileTargetID = -1;
        private int syncedFlareCount = 0;

        #region Enums
        public enum TargetPriority
        {
            Closest,
            MostDangerous,
            HighestHealth,
            LowestHealth,
            Leader
        }

        public enum RadarDisplayType
        {
            Circular,
            Linear,
            BScope
        }

        public enum LockQuality
        {
            None,
            Partial,
            Solid,
            MissileLock
        }

        public enum ThreatLevel
        {
            Low,
            Medium,
            High,
            Critical
        }
        #endregion

        #region Data Structures
        [System.Serializable]
        public struct RadarContact
        {
            public bl_VehicleManager vehicle;
            public Vector3 position;
            public Vector3 velocity;
            public float distance;
            public ThreatLevel threatLevel;
            public float lastSeenTime;
            public bool isLocked;
        }

        [System.Serializable]
        public struct TargetData
        {
            public int viewID;
            public LockQuality lockQuality;
            public float lockProgress;
            public ThreatLevel threatLevel;
        }
        #endregion

        protected override void Awake()
        {
            base.Awake();
            m_vehicle = GetComponent<bl_VehicleManager>();
            m_carController = GetComponent<bl_CarController>();
            m_audioSource = GetComponent<AudioSource>();
            m_photonView = GetComponent<PhotonView>();

            if (m_audioSource == null)
                m_audioSource = gameObject.AddComponent<AudioSource>();

            currentFlares = flareCount;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_vehicleUI = bl_VehicleUI.Instance;
            //m_vehicle.AddUpdateComponent(this);

            // Initialize network sync arrays
            syncedLockedTargetIDs = new int[(int)maxLockedTargets];
            for (int i = 0; i < syncedLockedTargetIDs.Length; i++)
            {
                syncedLockedTargetIDs[i] = -1;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            /*if (m_vehicle != null)
                m_vehicle.AddUpdateComponent(this);*/
        }

        public override void OnUpdate()
        {
            if (!m_vehicle.IsLocalPlayerInside()) return;

            UpdateRadarSystem();
            ScanForTargets();
            UpdateLockState();
            UpdateMissileSystem();
            UpdateCountermeasures();
            UpdateUI();
            UpdateTargetIndicators();

            // Update network sync
            if (PhotonNetwork.IsConnected && m_photonView.IsMine)
            {
                UpdateNetworkSync();
            }
        }

        #region Radar System
        /// <summary>
        /// Update radar scanning and contact tracking
        /// </summary>
        private void UpdateRadarSystem()
        {
            if (!enableRadar) return;

            if (Time.time - lastRadarScanTime >= radarScanInterval)
            {
                ScanRadarContacts();
                lastRadarScanTime = Time.time;
            }

            UpdateRadarContacts();
        }

        /// <summary>
        /// Scan for vehicles within radar range
        /// </summary>
        private void ScanRadarContacts()
        {
            radarContacts.Clear();
            bl_VehicleManager[] allVehicles = FindObjectsOfType<bl_VehicleManager>();

            foreach (var vehicle in allVehicles)
            {
                if (vehicle == m_vehicle) continue;
                if (vehicle.VehicleHealth != null && vehicle.VehicleHealth.CurrentHealth <= 0) continue;

                float distance = Vector3.Distance(transform.position, vehicle.transform.position);
                if (distance <= radarRange)
                {
                    radarContacts.Add(vehicle);

                    // Update or create radar contact data
                    int vehicleID = vehicle.photonView.ViewID;
                    if (!radarContactData.ContainsKey(vehicleID))
                    {
                        radarContactData[vehicleID] = new RadarContact();
                    }

                    RadarContact contact = radarContactData[vehicleID];
                    contact.vehicle = vehicle;
                    contact.position = vehicle.transform.position;
                    contact.velocity = vehicle.Velocity;
                    contact.distance = distance;
                    contact.threatLevel = CalculateThreatLevel(vehicle, distance);
                    contact.lastSeenTime = Time.time;
                    contact.isLocked = lockedTargets.Contains(vehicle);
                    radarContactData[vehicleID] = contact;
                }
                else
                {
                    // Remove from radar contact data if out of range for too long
                    int vehicleID = vehicle.photonView.ViewID;
                    if (radarContactData.ContainsKey(vehicleID))
                    {
                        if (Time.time - radarContactData[vehicleID].lastSeenTime > 10f)
                        {
                            radarContactData.Remove(vehicleID);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update existing radar contacts
        /// </summary>
        private void UpdateRadarContacts()
        {
            List<int> toRemove = new List<int>();

            foreach (var kvp in radarContactData)
            {
                RadarContact contact = kvp.Value;
                if (contact.vehicle == null ||
                    Vector3.Distance(transform.position, contact.vehicle.transform.position) > radarRange)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Update contact position and velocity
                contact.position = contact.vehicle.transform.position;
                contact.velocity = contact.vehicle.Velocity;
                contact.distance = Vector3.Distance(transform.position, contact.position);
                contact.threatLevel = CalculateThreatLevel(contact.vehicle, contact.distance);
                contact.isLocked = lockedTargets.Contains(contact.vehicle);
                radarContactData[kvp.Key] = contact;
            }

            foreach (int key in toRemove)
            {
                radarContactData.Remove(key);
            }
        }

        /// <summary>
        /// Calculate threat level for radar contact
        /// </summary>
        private ThreatLevel CalculateThreatLevel(bl_VehicleManager target, float distance)
        {
            float threatScore = 0f;

            // Distance factor (closer = more threat)
            threatScore += (1f - Mathf.Clamp01(distance / radarRange)) * 40f;

            // Frontal aspect (coming toward you = more threat)
            Vector3 toTarget = (target.transform.position - transform.position).normalized;
            Vector3 targetVelocity = target.Velocity.normalized;
            float aspect = Vector3.Dot(transform.forward, toTarget);
            threatScore += Mathf.Clamp01(aspect) * 30f;

            // Speed factor (faster = more threat)
            threatScore += Mathf.Clamp01(target.Velocity.magnitude / 100f) * 20f;

            // Lock status (locked = more threat)
            if (lockedTargets.Contains(target)) threatScore += 10f;

            if (threatScore >= 80f) return ThreatLevel.Critical;
            if (threatScore >= 60f) return ThreatLevel.High;
            if (threatScore >= 40f) return ThreatLevel.Medium;
            return ThreatLevel.Low;
        }

        /// <summary>
        /// Get radar contacts sorted by threat level
        /// </summary>
        public List<RadarContact> GetRadarContactsByThreat()
        {
            List<RadarContact> contacts = new List<RadarContact>(radarContactData.Values);
            contacts.Sort((a, b) => b.threatLevel.CompareTo(a.threatLevel));
            return contacts;
        }

        /// <summary>
        /// Get radar contacts within a specific threat level
        /// </summary>
        public List<RadarContact> GetRadarContactsByThreatLevel(ThreatLevel level)
        {
            List<RadarContact> result = new List<RadarContact>();
            foreach (var contact in radarContactData.Values)
            {
                if (contact.threatLevel == level)
                    result.Add(contact);
            }
            return result;
        }
        #endregion

        #region Target Locking Core
        /// <summary>
        /// Scans for potential targets in range
        /// </summary>
        private void ScanForTargets()
        {
            // Check if we should maintain existing locks
            for (int i = lockedTargets.Count - 1; i >= 0; i--)
            {
                if (ShouldBreakLock(lockedTargets[i]))
                {
                    BreakLockOnTarget(lockedTargets[i]);
                }
            }

            if (lockedTargets.Count >= maxLockedTargets)
            {
                primaryTarget = lockedTargets[0];
                return;
            }

            // Find best new target
            bl_VehicleManager bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var vehicle in radarContacts)
            {
                if (lockedTargets.Contains(vehicle)) continue;
                if (!IsValidTarget(vehicle)) continue;

                float score = CalculateTargetScore(vehicle);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = vehicle;
                }
            }

            currentTarget = bestTarget;
        }

        /// <summary>
        /// Check if a vehicle is a valid target
        /// </summary>
        private bool IsValidTarget(bl_VehicleManager target)
        {
            if (target == null) return false;
            if (target.VehicleHealth != null && target.VehicleHealth.CurrentHealth <= 0) return false;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > lockRange) return false;

            Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle > lockAngle) return false;

            // Line of sight check
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToTarget, out hit, lockRange, targetLayers))
            {
                if (hit.collider.transform != target.transform &&
                    !hit.collider.transform.IsChildOf(target.transform))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Calculate target score based on priority system
        /// </summary>
        private float CalculateTargetScore(bl_VehicleManager target)
        {
            float score = 0f;
            float distance = Vector3.Distance(transform.position, target.transform.position);

            switch (targetPriority)
            {
                case TargetPriority.Closest:
                    score = 1f / (distance + 0.1f);
                    break;

                case TargetPriority.MostDangerous:
                    score = CalculateThreatScore(target, distance);
                    break;

                case TargetPriority.HighestHealth:
                    if (target.VehicleHealth != null)
                        score = target.VehicleHealth.CurrentHealth / (distance + 0.1f);
                    break;

                case TargetPriority.LowestHealth:
                    if (target.VehicleHealth != null)
                        score = (100f - target.VehicleHealth.CurrentHealth) / (distance + 0.1f);
                    break;

                case TargetPriority.Leader:
                    // Implement leader targeting logic here
                    score = 50f / (distance + 0.1f);
                    break;
            }

            Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            score *= 1f - (angle / lockAngle);

            return score;
        }

        /// <summary>
        /// Calculate threat-based target score
        /// </summary>
        private float CalculateThreatScore(bl_VehicleManager target, float distance)
        {
            float threatScore = 0f;

            // Base threat from radar system
            if (radarContactData.ContainsKey(target.photonView.ViewID))
            {
                ThreatLevel level = radarContactData[target.photonView.ViewID].threatLevel;
                threatScore += (int)level * 25f;
            }

            // Distance modifier
            threatScore *= 1f / (distance + 0.1f);

            // Weapon system modifier (if target has weapons)
            var targetLockSystem = target.GetComponent<bl_TargetLockSystem>();
            if (targetLockSystem != null && targetLockSystem.hasLock)
            {
                threatScore += 50f; // Bonus threat if target has us locked
            }

            return threatScore;
        }

        /// <summary>
        /// Update lock progression state
        /// </summary>
        private void UpdateLockState()
        {
            if (currentTarget != null && lockedTargets.Count < maxLockedTargets)
            {
                if (!isLocking)
                {
                    StartLocking(currentTarget);
                }

                currentLockTime += Time.deltaTime;
                syncedLockProgress = currentLockTime / lockTimeRequired;

                if (currentLockTime >= lockTimeRequired)
                {
                    AcquireLock(currentTarget);
                }
            }
            else if (isLocking && (currentTarget == null || lockedTargets.Count >= maxLockedTargets))
            {
                BreakLock();
            }

            // Update primary target (first in list)
            if (lockedTargets.Count > 0)
            {
                primaryTarget = lockedTargets[0];
                hasLock = true;
            }
            else
            {
                primaryTarget = null;
                hasLock = false;
            }
        }

        /// <summary>
        /// Start the locking process on a specific target
        /// </summary>
        private void StartLocking(bl_VehicleManager target)
        {
            isLocking = true;
            currentLockTime = 0f;
            currentTarget = target;

            if (lockStartSound != null)
                m_audioSource.PlayOneShot(lockStartSound);

            CreateTargetIndicator(target);

            // Network sync
            if (PhotonNetwork.IsConnected && m_photonView.IsMine)
            {
                m_photonView.RPC("RPC_StartLocking", RpcTarget.Others, target.photonView.ViewID);
            }
        }

        /// <summary>
        /// Acquire full lock on target
        /// </summary>
        private void AcquireLock(bl_VehicleManager target)
        {
            if (lockedTargets.Contains(target)) return;

            lockedTargets.Add(target);
            hasLock = true;
            isLocking = false;
            currentLockTime = 0f;

            if (lockAcquiredSound != null)
                m_audioSource.PlayOneShot(lockAcquiredSound);

            UpdateTargetIndicator(target, true);

            // Network sync
            if (PhotonNetwork.IsConnected && m_photonView.IsMine)
            {
                UpdateNetworkSync();
                m_photonView.RPC("RPC_AcquireLock", RpcTarget.Others, target.photonView.ViewID);
            }
        }

        /// <summary>
        /// Break lock on specific target
        /// </summary>
        public void BreakLockOnTarget(bl_VehicleManager target)
        {
            if (lockedTargets.Contains(target))
            {
                lockedTargets.Remove(target);
                UpdateTargetIndicator(target, false);

                if (lockedTargets.Count == 0)
                {
                    hasLock = false;
                    if (lockLostSound != null)
                        m_audioSource.PlayOneShot(lockLostSound);
                }

                // Network sync
                if (PhotonNetwork.IsConnected && m_photonView.IsMine)
                {
                    UpdateNetworkSync();
                    m_photonView.RPC("RPC_BreakLock", RpcTarget.Others, target.photonView.ViewID);
                }
            }
        }

        /// <summary>
        /// Break all locks
        /// </summary>
        public void BreakLock()
        {
            if (hasLock && lockLostSound != null)
                m_audioSource.PlayOneShot(lockLostSound);

            foreach (var target in lockedTargets)
            {
                UpdateTargetIndicator(target, false);
            }

            lockedTargets.Clear();
            currentTarget = null;
            primaryTarget = null;
            hasLock = false;
            isLocking = false;
            currentLockTime = 0f;
            syncedLockProgress = 0f;

            // Network sync
            if (PhotonNetwork.IsConnected && m_photonView.IsMine)
            {
                UpdateNetworkSync();
                m_photonView.RPC("RPC_BreakAllLocks", RpcTarget.Others);
            }
        }

        /// <summary>
        /// Check if lock should be broken
        /// </summary>
        private bool ShouldBreakLock(bl_VehicleManager target)
        {
            if (target == null) return true;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > lockBreakDistance) return true;

            Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle > lockBreakAngle) return true;

            if (target.VehicleHealth != null && target.VehicleHealth.CurrentHealth <= 0) return true;

            // Check if target is using countermeasures
            if (IsTargetUsingCountermeasures(target)) return true;

            return false;
        }

        /// <summary>
        /// Check if target is actively using countermeasures
        /// </summary>
        private bool IsTargetUsingCountermeasures(bl_VehicleManager target)
        {
            var targetLockSystem = target.GetComponent<bl_TargetLockSystem>();
            return targetLockSystem != null && targetLockSystem.countermeasuresActive;
        }
        #endregion

        #region Missile System
        /// <summary>
        /// Update missile locking and launch system
        /// </summary>
        private void UpdateMissileSystem()
        {
            if (!enableMissileGuidance) return;

            // Auto-acquire missile lock if we have a target lock
            if (primaryTarget != null && !missileArmed)
            {
                float distance = Vector3.Distance(transform.position, primaryTarget.transform.position);
                if (distance <= missileLockRange)
                {
                    AcquireMissileLock(primaryTarget);
                }
            }

            // Break missile lock if conditions are met
            if (missileArmed && ShouldBreakMissileLock())
            {
                BreakMissileLock();
            }
        }

        /// <summary>
        /// Acquire missile lock on target
        /// </summary>
        private void AcquireMissileLock(bl_VehicleManager target)
        {
            missileArmed = true;
            missileLockedTarget = target;

            if (missileLockSound != null)
                m_audioSource.PlayOneShot(missileLockSound);

            // Network sync
            if (PhotonNetwork.IsConnected && m_photonView.IsMine)
            {
                syncedMissileTargetID = target.photonView.ViewID;
                m_photonView.RPC("RPC_AcquireMissileLock", RpcTarget.Others, target.photonView.ViewID);
            }
        }

        /// <summary>
        /// Break missile lock
        /// </summary>
        private void BreakMissileLock()
        {
            missileArmed = false;
            missileLockedTarget = null;

            // Network sync
            if (PhotonNetwork.IsConnected && m_photonView.IsMine)
            {
                syncedMissileTargetID = -1;
                m_photonView.RPC("RPC_BreakMissileLock", RpcTarget.Others);
            }
        }

        /// <summary>
        /// Check if missile lock should be broken
        /// </summary>
        private bool ShouldBreakMissileLock()
        {
            if (missileLockedTarget == null) return true;

            float distance = Vector3.Distance(transform.position, missileLockedTarget.transform.position);
            if (distance > missileLockRange) return true;

            if (missileLockedTarget.VehicleHealth != null &&
                missileLockedTarget.VehicleHealth.CurrentHealth <= 0) return true;

            if (IsTargetUsingCountermeasures(missileLockedTarget)) return true;

            return false;
        }

        /// <summary>
        /// Launch missile at locked target
        /// </summary>
        public void LaunchMissile()
        {
            if (!missileArmed || missileLockedTarget == null) return;
            if (Time.time - lastMissileLaunchTime < missileLaunchDelay) return;

            if (missilePrefab != null && missileLaunchPoint != null)
            {
                // Create missile
                GameObject missileObj = PhotonNetwork.Instantiate(missilePrefab.name,
                    missileLaunchPoint.position, missileLaunchPoint.rotation);

                /*var missile = missileObj.GetComponent<bl_GuidedMissile>();
                if (missile != null)
                {
                    missile.SetTarget(missileLockedTarget.transform);
                    missile.Launch();
                }*/

                lastMissileLaunchTime = Time.time;
                missileArmed = false;

                // Network sync
                if (PhotonNetwork.IsConnected && m_photonView.IsMine)
                {
                    m_photonView.RPC("RPC_LaunchMissile", RpcTarget.Others,
                        missileLockedTarget.photonView.ViewID, missileLaunchPoint.position, missileLaunchPoint.rotation);
                }
            }
        }
        #endregion

        #region Countermeasures
        /// <summary>
        /// Update countermeasures system
        /// </summary>
        private void UpdateCountermeasures()
        {
            if (!enableCountermeasures) return;

            // Recharge flares over time
            if (currentFlares < flareCount && Time.time - lastFlareTime > flareCooldown)
            {
                currentFlares = Mathf.Min(flareCount, currentFlares + 1);
                lastFlareTime = Time.time;
                syncedFlareCount = currentFlares;
            }
        }

        /// <summary>
        /// Deploy countermeasures (flares)
        /// </summary>
        public void DeployCountermeasures()
        {
            if (currentFlares <= 0) return;
            if (Time.time - lastFlareTime < 0.5f) return; // Minimum delay between flares

            if (flarePrefab != null && flareLauncher != null)
            {
                // Create flare
                GameObject flareObj = PhotonNetwork.Instantiate(flarePrefab.name,
                    flareLauncher.position, flareLauncher.rotation);

                /*var flare = flareObj.GetComponent<bl_FlareCountermeasure>();
                if (flare != null)
                {
                    flare.Launch();
                }*/

                currentFlares--;
                lastFlareTime = Time.time;
                countermeasuresActive = true;
                syncedFlareCount = currentFlares;

                // Break any incoming missile locks
                BreakIncomingLocks();

                // Network sync
                if (PhotonNetwork.IsConnected && m_photonView.IsMine)
                {
                    m_photonView.RPC("RPC_DeployCountermeasures", RpcTarget.Others);
                }

                // Reset countermeasures active after delay
                StartCoroutine(ResetCountermeasuresActive());
            }
        }

        /// <summary>
        /// Break any incoming missile locks from enemies
        /// </summary>
        private void BreakIncomingLocks()
        {
            bl_VehicleManager[] allVehicles = FindObjectsOfType<bl_VehicleManager>();
            foreach (var vehicle in allVehicles)
            {
                if (vehicle == m_vehicle) continue;

                var lockSystem = vehicle.GetComponent<bl_TargetLockSystem>();
                if (lockSystem != null && lockSystem.missileLockedTarget == m_vehicle)
                {
                    lockSystem.BreakMissileLock();
                }
            }
        }

        /// <summary>
        /// Reset countermeasures active flag after delay
        /// </summary>
        private IEnumerator ResetCountermeasuresActive()
        {
            yield return new WaitForSeconds(3f);
            countermeasuresActive = false;
        }
        #endregion

        #region UI and Visuals
        /// <summary>
        /// Update UI elements
        /// </summary>
        private void UpdateUI()
        {
            if (m_vehicleUI == null) return;

            float lockProgress = hasLock ? 1f : syncedLockProgress;
            //m_vehicleUI.UpdateLockUI(isLocking, lockProgress, hasLock, missileArmed, currentFlares, flareCount);
        }

        /// <summary>
        /// Create visual indicator for target
        /// </summary>
        private void CreateTargetIndicator(bl_VehicleManager target)
        {
            if (lockIndicatorPrefab == null || target == null) return;
            if (targetIndicators.ContainsKey(target.photonView.ViewID)) return;

            GameObject indicatorObj = Instantiate(lockIndicatorPrefab);
            bl_TargetIndicator indicator = indicatorObj.GetComponent<bl_TargetIndicator>();

            if (indicator != null)
            {
                indicator.SetTarget(target.transform);
                targetIndicators[target.photonView.ViewID] = indicator;
            }
        }

        /// <summary>
        /// Update target indicator state
        /// </summary>
        private void UpdateTargetIndicator(bl_VehicleManager target, bool isLocked)
        {
            if (targetIndicators.ContainsKey(target.photonView.ViewID))
            {
                var indicator = targetIndicators[target.photonView.ViewID];
                if (indicator != null)
                {
                    indicator.SetLockState(isLocking, isLocked);
                }
            }
        }

        /// <summary>
        /// Update all target indicators
        /// </summary>
        private void UpdateTargetIndicators()
        {
            List<int> toRemove = new List<int>();

            foreach (var kvp in targetIndicators)
            {
                if (kvp.Value == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                kvp.Value.UpdateIndicator();

                // Remove indicator if target is destroyed
                PhotonView targetView = PhotonView.Find(kvp.Key);
                if (targetView == null)
                {
                    toRemove.Add(kvp.Key);
                    Destroy(kvp.Value.gameObject);
                }
            }

            foreach (int key in toRemove)
            {
                targetIndicators.Remove(key);
            }
        }

        /// <summary>
        /// Clear all target indicators
        /// </summary>
        private void ClearTargetIndicators()
        {
            foreach (var indicator in targetIndicators.Values)
            {
                if (indicator != null)
                {
                    Destroy(indicator.gameObject);
                }
            }
            targetIndicators.Clear();
        }
        #endregion

        #region Network Synchronization
        /// <summary>
        /// Update network synchronization data
        /// </summary>
        private void UpdateNetworkSync()
        {
            // Sync locked targets
            syncedLockedTargetIDs = new int[lockedTargets.Count];
            for (int i = 0; i < lockedTargets.Count; i++)
            {
                if (lockedTargets[i] != null)
                    syncedLockedTargetIDs[i] = lockedTargets[i].photonView.ViewID;
                else
                    syncedLockedTargetIDs[i] = -1;
            }

            // Sync missile target
            syncedMissileTargetID = missileLockedTarget != null ? missileLockedTarget.photonView.ViewID : -1;

            // Sync flare count
            syncedFlareCount = currentFlares;
        }

        /// <summary>
        /// Photon network synchronization
        /// </summary>
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(hasLock);
                stream.SendNext(isLocking);
                stream.SendNext(syncedLockProgress);
                stream.SendNext(syncedLockedTargetIDs);
                stream.SendNext(syncedMissileTargetID);
                stream.SendNext(missileArmed);
                stream.SendNext(syncedFlareCount);
                stream.SendNext(countermeasuresActive);
            }
            else
            {
                hasLock = (bool)stream.ReceiveNext();
                isLocking = (bool)stream.ReceiveNext();
                syncedLockProgress = (float)stream.ReceiveNext();
                syncedLockedTargetIDs = (int[])stream.ReceiveNext();
                syncedMissileTargetID = (int)stream.ReceiveNext();
                missileArmed = (bool)stream.ReceiveNext();
                syncedFlareCount = (int)stream.ReceiveNext();
                countermeasuresActive = (bool)stream.ReceiveNext();

                // Reconstruct locked targets list
                lockedTargets.Clear();
                foreach (int targetID in syncedLockedTargetIDs)
                {
                    if (targetID != -1)
                    {
                        PhotonView targetView = PhotonView.Find(targetID);
                        if (targetView != null)
                        {
                            var targetVehicle = targetView.GetComponent<bl_VehicleManager>();
                            if (targetVehicle != null)
                            {
                                lockedTargets.Add(targetVehicle);
                            }
                        }
                    }
                }

                // Reconstruct missile target
                if (syncedMissileTargetID != -1)
                {
                    PhotonView missileTargetView = PhotonView.Find(syncedMissileTargetID);
                    if (missileTargetView != null)
                    {
                        missileLockedTarget = missileTargetView.GetComponent<bl_VehicleManager>();
                    }
                }
                else
                {
                    missileLockedTarget = null;
                }

                // Update flare count
                currentFlares = syncedFlareCount;
            }
        }

        [PunRPC]
        private void RPC_StartLocking(int targetViewID)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null)
            {
                var target = targetView.GetComponent<bl_VehicleManager>();
                StartLocking(target);
            }
        }

        [PunRPC]
        private void RPC_AcquireLock(int targetViewID)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null)
            {
                var target = targetView.GetComponent<bl_VehicleManager>();
                AcquireLock(target);
            }
        }

        [PunRPC]
        private void RPC_BreakLock(int targetViewID)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null)
            {
                var target = targetView.GetComponent<bl_VehicleManager>();
                BreakLockOnTarget(target);
            }
        }

        [PunRPC]
        private void RPC_BreakAllLocks()
        {
            BreakLock();
        }

        [PunRPC]
        private void RPC_AcquireMissileLock(int targetViewID)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null)
            {
                var target = targetView.GetComponent<bl_VehicleManager>();
                AcquireMissileLock(target);
            }
        }

        [PunRPC]
        private void RPC_BreakMissileLock()
        {
            BreakMissileLock();
        }

        [PunRPC]
        private void RPC_LaunchMissile(int targetViewID, Vector3 position, Quaternion rotation)
        {
            // Remote missile launch visualization
            if (missilePrefab != null)
            {
                GameObject missileObj = Instantiate(missilePrefab, position, rotation);
                /*var missile = missileObj.GetComponent<bl_GuidedMissile>();
                if (missile != null)
                {
                    PhotonView targetView = PhotonView.Find(targetViewID);
                    if (targetView != null)
                    {
                        missile.SetTarget(targetView.transform);
                    }
                }*/
            }
        }

        [PunRPC]
        private void RPC_DeployCountermeasures()
        {
            // Remote countermeasures visualization
            if (flarePrefab != null && flareLauncher != null)
            {
                GameObject flareObj = Instantiate(flarePrefab, flareLauncher.position, flareLauncher.rotation);
                /*var flare = flareObj.GetComponent<bl_FlareCountermeasure>();
                if (flare != null)
                {
                    flare.Launch();
                }*/
            }
        }
        #endregion

        #region Public API
        public bool HasLock { get { return hasLock; } }
        public bool IsLocking { get { return isLocking; } }
        public float LockProgress { get { return syncedLockProgress; } }
        public bl_VehicleManager PrimaryTarget { get { return primaryTarget; } }
        public List<bl_VehicleManager> LockedTargets { get { return lockedTargets; } }
        public bool MissileArmed { get { return missileArmed; } }
        public int CurrentFlares { get { return currentFlares; } }
        public int MaxFlares { get { return flareCount; } }

        public void ForceLockOnTarget(bl_VehicleManager target)
        {
            if (target == null) return;

            currentTarget = target;
            currentLockTime = lockTimeRequired;
            AcquireLock(target);
        }

        public void CancelLock()
        {
            BreakLock();
        }

        public List<RadarContact> GetRadarContacts()
        {
            return new List<RadarContact>(radarContactData.Values);
        }

        public RadarContact GetRadarContact(int vehicleViewID)
        {
            return radarContactData.ContainsKey(vehicleViewID) ? radarContactData[vehicleViewID] : new RadarContact();
        }
        #endregion

        #region IVehicleUpdate Implementation
        public void WhenInsideUpdateVehicle()
        {
            OnUpdate();
        }

        public void WhenInsideFixedUpdateVehicle()
        {
            // Physics-based updates can go here if needed
        }
        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebugRays) return;

            // Draw lock range sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, lockRange);

            // Draw missile lock range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, missileLockRange);

            // Draw radar range
            if (showRadarDebug)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, radarRange);
            }
            // Draw lock angle cone
            Gizmos.color = Color.red;
            float halfAngle = lockAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 forward = transform.forward;
            Vector3 left = Quaternion.Euler(0, -lockAngle * 0.5f, 0) * forward * lockRange;
            Vector3 right = Quaternion.Euler(0, lockAngle * 0.5f, 0) * forward * lockRange;

            Gizmos.DrawRay(transform.position, left);
            Gizmos.DrawRay(transform.position, right);
            Gizmos.DrawRay(transform.position + left, right - left);

            // Draw lines to locked targets
            foreach (var target in lockedTargets)
            {
                if (target != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, target.transform.position);
                }
            }

            // Draw line to current target
            if (currentTarget != null)
            {
                Gizmos.color = isLocking ? Color.yellow : Color.white;
                Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            }

            // Draw line to missile target
            if (missileLockedTarget != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, missileLockedTarget.transform.position);
            }
            // Draw radar contacts
            Gizmos.color = Color.blue;
            foreach (var contact in radarContactData.Values)
            {
                Gizmos.DrawWireSphere(contact.position, 5f);
            }
        }
#endif
    }
}