using UnityEngine;
using System.Collections;
using MFPSEditor;
using MFPS.Internal.Vehicles;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleCamera : bl_MonoBehaviour
    {
        /// <summary>
        /// 
        /// </summary>
        public struct ZoomTransition
        {
            public float TargetZoom;
            public float TransitionDuration;
            public AnimationCurve Curve;
        }

        #region Public members
        public AnimationCurve TransitionCurve;
        public float TransitionDuration = 0.5f;
        public float smoothing = 6f;
        public float SteerIntensity = 1f;
        public LayerMask collisionMask;
        public Transform cameraTransform;
        public Transform headLook;
        public Camera cameraRef;
        [SerializeField, ScriptableDrawer] private bl_VehicleCameraSettings defaultCameraSettings = null;
        #endregion

        #region Private members
        private Quaternion _pitch;
        private Quaternion _yaw;
        private Quaternion _targetRotation;
        private bool Hold = false;
        private bool Interrup = false;
        private float CameraTargetDistance = 1;
        private bl_VehicleCameraSettings overrideCameraSettings;
        private float defaultFov = 50;
        private Camera m_currentCamera;
        private bl_CarController m_CarController;
        private Quaternion _roll;
        #endregion



        #region Public properties
        /// <summary>
        /// The current active vehicle camera
        /// </summary>
        public Camera CurrentVehicleCamera
        {
            get => m_currentCamera;
            set
            {
                if (value == null) m_currentCamera = cameraRef;
                else m_currentCamera = value;
            }
        }

        public float YawLimit
        {
            get
            {
                return GetCameraSettings().YawLimit;
            }
        }

        public float PitchLimit { get { return GetCameraSettings().PitchLimit; } }
        public float Yaw
        {
            get { return _yaw.eulerAngles.y; }
            private set { _yaw = Quaternion.Euler(0, value, 0); }
        }

        public float Pitch
        {
            get { return _pitch.eulerAngles.x; }
            private set { _pitch = Quaternion.Euler(value, 0, 0); }
        }

        public VehicleCameraFollowType CameraFollowType 
        { 
            get; 
            set; 
        }

        public bl_VehicleCameraRig CurrentRigTarget
        {
            get;
            set;
        } = null;
        public float Roll
        {
            get { return _roll.eulerAngles.z; }
            private set { _roll = Quaternion.Euler(0, 0, value); }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            CameraFollowType = bl_VehicleSettings.Instance.cameraFollowType;
            //cache singleton
            Instance.Disable();
            defaultFov = cameraRef.fieldOfView;
            CurrentVehicleCamera = cameraRef;
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            bl_EventHandler.onLocalPlayerDeath += OnLocalDeath;
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            bl_EventHandler.onLocalPlayerDeath -= OnLocalDeath;
        }

        /// <summary>
        /// 
        /// </summary>
        void OnLocalDeath()
        {
            Disable();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnFixedUpdate()
        {
            if (CameraFollowType != VehicleCameraFollowType.Follow) return;
            if (CurrentRigTarget == null || CurrentRigTarget.LookAtTarget == null || Hold)
                return;

            UpdateFollowCamera();
            DetectCollision();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnUpdate()
        {
            if (CurrentRigTarget == null || CurrentRigTarget.LookAtTarget == null || Hold)
                return;

            UpdateOrbitCamera();
            UpdateLookCamera();
        }

        private void UpdateOrbitCamera()
        {
            if (CameraFollowType != VehicleCameraFollowType.Orbit) return;

            if (!bl_UtilityHelper.isMobile)
                Move(bl_GameInput.MouseX, -bl_GameInput.MouseY, bl_GameInput.Horizontal);
            else
            {
#if MFPSM
        Move(bl_TouchPad.Instance.Horizontal, -bl_TouchPad.Instance.Vertical);
#endif
            }


            // calculate target positions
            _targetRotation = _yaw * _pitch * _roll; // Include roll in the rotation

            // apply movement damping
            Quaternion targetRot = Quaternion.Lerp(this.cameraTransform.rotation, _targetRotation, Mathf.Clamp01(Time.smoothDeltaTime * GetCameraSettings().Damping));

            SetCameraRotation(targetRot);

            // offset the camera at distance from the target position.
            Vector3 offset = this.cameraTransform.rotation * (-Vector3.forward * GetCameraSettings().Distance);
            SetCameraPosition(CurrentRigTarget.LookAtTarget.position + offset);
        }

        /// <summary>
        /// Follow target movement
        /// </summary>
        private void UpdateFollowCamera()
        {
            if (Interrup)
                return;

            SetCameraPosition(Vector3.Lerp(cameraTransform.position, CurrentRigTarget.TargetPosition(), Time.deltaTime * smoothing));

            // Get the base look rotation
            Quaternion lookRotation = Quaternion.LookRotation(CurrentRigTarget.LookAtTarget.position - cameraTransform.position);


            cameraTransform.rotation = lookRotation;


        }

        /// <summary>
        /// Look movement
        /// </summary>
        private void UpdateLookCamera()
        {
            if (CameraFollowType != VehicleCameraFollowType.Look) return;

            _targetRotation.y += bl_GameInput.MouseX * GetCameraSettings().GetSensitivity();
            _targetRotation.x += -bl_GameInput.MouseY * GetCameraSettings().GetSensitivity();
            _targetRotation.x = Mathf.Clamp(_targetRotation.x, -GetCameraSettings().PitchLimit, GetCameraSettings().PitchLimit);
            _targetRotation.y = Mathf.Clamp(_targetRotation.y, -GetCameraSettings().YawLimit, GetCameraSettings().YawLimit);

            Quaternion targetRot = Quaternion.Euler(_targetRotation.x * smoothing, _targetRotation.y, 0);


            SetCameraRotation(targetRot);

        }


        /// <summary>
        /// 
        /// </summary>
        void DetectCollision()
        {
            //if there a obstacle detected
            Interrup = Physics.CheckSphere(cameraTransform.position, 0.5f, collisionMask, QueryTriggerInteraction.Ignore);
            if (Interrup)
            {
                cameraTransform.LookAt(CurrentRigTarget.LookAtTarget);
                if (Vector3.Distance(cameraTransform.position, CurrentRigTarget.LookAtTarget.position) >= CameraTargetDistance) { Interrup = false; }
            }
        }


        public void Move(float yawDelta, float pitchDelta, float rollInput)
        {
            yawDelta *= GetCameraSettings().GetSensitivity();
            pitchDelta *= GetCameraSettings().GetSensitivity();
            rollInput *= SteerIntensity;
            _yaw = _yaw * Quaternion.Euler(0, yawDelta, 0);
            _pitch = _pitch * Quaternion.Euler(pitchDelta, 0, 0);
            _roll = _roll * Quaternion.Euler(0, 0, rollInput);

            ApplyConstraints();
        }

        /// <summary>
        /// 
        /// </summary>

        private void ApplyConstraints()
        {
            Quaternion targetYaw = Quaternion.Euler(0, CurrentRigTarget.LookAtTarget.eulerAngles.y, 0);
            Quaternion targetPitch = Quaternion.Euler(CurrentRigTarget.LookAtTarget.eulerAngles.x, 0, 0);

            float yawDifference = Quaternion.Angle(_yaw, targetYaw);
            float pitchDifference = Quaternion.Angle(_pitch, targetPitch);

            float yawOverflow = yawDifference - YawLimit;
            float pitchOverflow = pitchDifference - PitchLimit;

            // We'll simply use lerp to move a bit towards the focus target's orientation. Just enough to get back within the constraints.
            if (yawOverflow > 0) { _yaw = Quaternion.Slerp(_yaw, targetYaw, yawOverflow / yawDifference); }
            if (pitchOverflow > 0) { _pitch = Quaternion.Slerp(_pitch, targetPitch, pitchOverflow / pitchDifference); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newZoom"></param>
        public void SetZoom(ZoomTransition zoom)
        {
            StopCoroutine(nameof(DoZoomTransition));
            StartCoroutine(nameof(DoZoomTransition), zoom);
        }

        /// <summary>
        /// Set the camera field of view to it is original value
        /// </summary>
        public void ResetZoom(float duration = 0.5f)
        {
            SetZoom(new ZoomTransition()
            {
                TargetZoom = defaultFov,
                TransitionDuration = duration,
                Curve = AnimationCurve.EaseInOut(0, 0, 1, 1)
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newZoom"></param>
        /// <returns></returns>
        IEnumerator DoZoomTransition(ZoomTransition zoom)
        {
            float origin = CurrentVehicleCamera.fieldOfView;

            float d = 0;
            float c;
            while(d <= 1)
            {
                d += Time.deltaTime / zoom.TransitionDuration;
                c = zoom.Curve.Evaluate(d);
                CurrentVehicleCamera.fieldOfView = Mathf.Lerp(origin, zoom.TargetZoom, c);

                yield return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetCarTarget(Transform car)
        {
            GetRig(car);
            cameraTransform.gameObject.SetActive(true);
            _pitch = Quaternion.Euler(this.cameraTransform.rotation.eulerAngles.x, 0, 0);
            _yaw = Quaternion.Euler(0, this.cameraTransform.rotation.eulerAngles.y, 0);
            _roll = Quaternion.identity; 
            cameraRef.fieldOfView = defaultFov = GetCameraSettings().FieldOfView;

            // Get the car controller reference
            m_CarController = car.GetComponent<bl_CarController>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetCarTarget(Transform car, Transform from)
        {

            Hold = true;
            bool rigFound = GetRig(car);

            // Get the car controller reference
            m_CarController = car.GetComponent<bl_CarController>();

            if (rigFound)
            {
                CameraTargetDistance = CurrentRigTarget.GetDistanceBetweenCameraAndTarget();
                _pitch = Quaternion.Euler(CurrentRigTarget.CameraPosition.rotation.eulerAngles.x, 0, 0);
                _yaw = Quaternion.Euler(0, CurrentRigTarget.CameraPosition.rotation.eulerAngles.y, 0);
                _roll = Quaternion.identity; 
            }
            else
            {
                _pitch = Quaternion.Euler(this.cameraTransform.rotation.eulerAngles.x, 0, 0);
                _yaw = Quaternion.Euler(0, this.cameraTransform.rotation.eulerAngles.y, 0);
                _roll = Quaternion.identity; 
            }
            cameraRef.fieldOfView = defaultFov = GetCameraSettings().FieldOfView;
            CurrentVehicleCamera = cameraRef;
            cameraTransform.gameObject.SetActive(true);
            if (rigFound)
            {
                StartCoroutine(Transition(from));
            }
            else
            {
                Hold = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="From"></param>
        /// <returns></returns>
        IEnumerator Transition(Transform From)
        {
            float d = 0;
            Vector3 po = From.position;
            Quaternion ro = From.rotation;
            cameraTransform.position = po;
            cameraTransform.rotation = ro;
            float curve;

            while (d < 1)
            {
                d += Time.deltaTime / TransitionDuration;
                curve = TransitionCurve.Evaluate(d);
                cameraTransform.position = Vector3.Lerp(po, CurrentRigTarget.TargetPosition(), curve);
                cameraTransform.rotation = Quaternion.Slerp(ro, CurrentRigTarget.TargetRotation(), curve);
                yield return null;
            }
            Hold = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rotation"></param>
        public void SetCameraRotation(Quaternion rotation)
        {
            cameraTransform.rotation = rotation;
            if(CurrentRigTarget != null && CurrentRigTarget.CameraPosition != null)
            {
                CurrentRigTarget.CameraPosition.rotation = rotation;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rotation"></param>
        public void SetCameraPosition(Vector3 position)
        {
            cameraTransform.position = position;
            if (CurrentRigTarget != null && CurrentRigTarget.CameraPosition != null)
            {
                CurrentRigTarget.CameraPosition.position = position;
            }
        }
        /// <summary>
        /// Check if the current vehicle is a spaceship
        /// </summary>
        private bool IsSpaceship()
        {
            return m_CarController != null && m_CarController.isSpaceShip;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cameraSettings"></param>
        public void SetCameraSettings(bl_VehicleCameraSettings cameraSettings)
        {
            overrideCameraSettings = cameraSettings;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Disable()
        {
            StopAllCoroutines();
            if (CurrentRigTarget != null && CurrentRigTarget.CameraPosition != null)
            {
                CurrentRigTarget.CameraPosition.position = cameraTransform.position;
                CurrentRigTarget.CameraPosition.rotation = cameraTransform.rotation;
            }
            CurrentRigTarget = null;
            m_CarController = null; // Clear the reference
            overrideCameraSettings = null;
            cameraTransform.gameObject.SetActive(false);
            Hold = false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetActive(bool active)
        {
            cameraTransform.gameObject.SetActive(active);
        }

        /// <summary>
        /// 
        /// </summary>
        public void BlockMovement()
        {
            Hold = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ReleaseMovement()
        {
            Hold = false;
        }

        /// <summary>
        /// 
        /// </summary>
        private bool GetRig(Transform car)
        {
            var vr = car.GetComponentInChildren<bl_VehicleCameraRig>();
            if (vr == null) { Debug.LogWarning("Can't found camera rig references on child's."); return false; }

            CurrentRigTarget = vr;
            if (vr.customCameraSettings != null)
            {
                overrideCameraSettings = vr.customCameraSettings;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bl_VehicleCameraSettings GetCameraSettings()
        {
            if (overrideCameraSettings == null) return defaultCameraSettings;
            return overrideCameraSettings;
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(cameraTransform.position, 0.5f);
        }

        private static bl_VehicleCamera _instance;
        public static bl_VehicleCamera Instance
        {
            get
            {
                if (_instance == null) { _instance = FindObjectOfType<bl_VehicleCamera>(); }
                return _instance;
            }
        }

    }
}