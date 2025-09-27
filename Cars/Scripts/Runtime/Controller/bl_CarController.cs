using MFPS.Runtime.Vehicles.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MFPS.Runtime.Vehicles
{
    public class bl_CarController : bl_Vehicle
    {
        [Header("Spaceship Settings")]
        public bool isSpaceShip = false;
        [SerializeField] private float m_SpaceshipHoverHeight = 5f;
        [SerializeField] private float m_SpaceshipHoverForce = 100f;
        [SerializeField] private float m_SpaceshipLiftSpeed = 2f;
        [SerializeField] private float m_SpaceshipRotationSpeed = 2f;
        [SerializeField] private Transform m_SpaceshipLookAtTarget;
        [SerializeField] private float m_SpaceshipThrustForce = 100f;
        [SerializeField] private float m_SpaceshipGravityMultiplier = 0.1f;
        [Header("Spaceship Steering Settings")]
        [SerializeField] private float m_SpaceshipSteeringSpeed = 2f;
        [SerializeField] private SteerAxis m_SpaceshipSteeringAxis = SteerAxis.X;
        [SerializeField] private Transform m_SpaceshipSteerTarget;
        [SerializeField] private float m_SpaceshiSteerTargetRotateAmount = 10f;
        [Header("Spaceship Boost Settings")]
        [SerializeField] private float m_SpaceshipBoostMultiplier = 2f;
        [SerializeField] private int m_SpaceshipBoostCameraView = 70;
        [SerializeField] private float m_SpaceshipBoostAmount = 100f;
        [SerializeField] private float m_SpaceshipBoostDecreaseRate = 1f;
        [SerializeField] private float m_SpaceshipBoostRechargeRate = 0.5f;

        // Add these private variables
        private float m_CurrentBoostAmount;
        private bool m_IsBoosting = false;
        private float m_DefaultFOV = 60f;
        private Camera m_MainCamera;
        [Space(10)]
        [SerializeField] private Transform m_SpaceshipWeaponTarget;
        [SerializeField] private Transform m_SpaceshipMissileTarget;
        [Space(10)]
        [Header("Target Locking System")]
        public bl_TargetLockSystem lockSystem;
        public bool enableTargetLocking = true;

        [Header("Car Settings")]
        [SerializeField] private CarDriveType m_CarDriveType = CarDriveType.FourWheelDrive;
        public GameObject[] m_WheelMeshes = new GameObject[4];
        public bl_CarWheel[] carWheels = new bl_CarWheel[4];
        [SerializeField] private Vector3 m_CentreOfMassOffset = Vector3.zero;
        [SerializeField] private float m_MaximumSteerAngle = 30;
        [Range(0, 1)][SerializeField] private float m_SteerHelper = 0.5f;
        public float steerResponsiveness = 6;
        [Range(0, 1)][SerializeField] private float m_TractionControl = 0.1f;
        [SerializeField] private float m_FullTorqueOverAllWheels = 1;
        [SerializeField] private float m_ReverseTorque = 0;
        [SerializeField] private float m_MaxHandbrakeTorque = 0;
        [SerializeField] private float m_Downforce = 100f;
        [SerializeField] public SpeedType m_SpeedType;
        [SerializeField] private float m_Topspeed = 200;
        [Range(0, 1)] public float breakResponsiveness = 0.6f;
        [SerializeField] private static int NoOfGears = 5;
        [SerializeField] private float m_RevRangeBoundary = 1f;
        [SerializeField] private float m_SlipLimit = 15;
        [SerializeField] private float m_BrakeTorque = 0;

        private Quaternion[] m_WheelMeshLocalRotations;
        private float m_SteerAngle;
        private int m_GearNum;
        private float m_GearFactor;
        private float m_OldRotation;
        private float m_CurrentTorque;
        private Rigidbody m_Rigidbody;
        private Transform m_Transform;
        private bool m_IsPlayerInVehicle = false;
        private float m_CurrentHoverHeight;
        private float smootSteer = 0;

        // Public properties
        public bool Skidding { get; private set; }
        public float BrakeInput { get; private set; }
        public float CurrentSteerAngle { get { return m_SteerAngle; } }
        public float CurrentSpeed { get { return m_Rigidbody.velocity.magnitude * 2.23693629f; } }
        public SteerAxis CurrentSteerAxis { get { return m_SpaceshipSteeringAxis; } }
        public float VelocityMagnitude { get; set; }
        public Vector3 Velocity { get; set; }
        public float MaxSpeed { get { return m_Topspeed; } }
        public float Revs { get; private set; }
        public float AccelInput { get; private set; }
        public float CarVelocity { get; private set; }

        // Input cache
        public float m_brakeInput { get; private set; }
        public float m_steerin { get; private set; }
        public float m_accel { get; private set; }
        public float m_handbrake { get; private set; }
        private float FactorForce = 1;
        private Vector3 FactorDirection;
        private bool isFactorOn = false;


        /// <summary>
        /// 
        /// </summary>
        private void Start()
        {
            m_Transform = transform;
            m_WheelMeshLocalRotations = new Quaternion[4];
            for (int i = 0; i < 4; i++)
            {
                if (m_WheelMeshes[i] != null)
                {
                    carWheels[i].wheelModel = m_WheelMeshes[i];
                    m_WheelMeshLocalRotations[i] = m_WheelMeshes[i].transform.localRotation;
                }
            }

            if (m_WheelColliders(0) != null)
            {
                m_WheelColliders(0).attachedRigidbody.centerOfMass = m_CentreOfMassOffset;
            }

            m_MaxHandbrakeTorque = float.MaxValue;
            m_Rigidbody = GetComponent<Rigidbody>();
            m_CurrentTorque = m_FullTorqueOverAllWheels - (m_TractionControl * m_FullTorqueOverAllWheels);

            // Initialize hover height
            m_CurrentHoverHeight = isSpaceShip ? transform.position.y : 0f;

            if (m_SpaceshipLookAtTarget == null)
            {
                m_SpaceshipLookAtTarget = bl_VehicleCamera.Instance.headLook;
            }

            // Setup rigidbody for spaceship physics
            if (isSpaceShip)
            {
                m_Rigidbody.useGravity = false;
                m_Rigidbody.drag = 1;
                m_Rigidbody.angularDrag = 2;
                InitializeTargetLocking();
            }

            m_CurrentBoostAmount = m_SpaceshipBoostAmount;
            m_MainCamera = bl_VehicleCamera.Instance.cameraRef;
            if (m_MainCamera != null)
            {
                m_DefaultFOV = m_MainCamera.fieldOfView;
            }
        }

        public void LaunchMissile()
        {
            if (lockSystem != null)
            {
                lockSystem.LaunchMissile();
            }
        }

        public void DeployCountermeasures()
        {
            if (lockSystem != null)
            {
                lockSystem.DeployCountermeasures();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Move(float steering, float accel, float footbrake, float handbrake, float floatUp, float floatDown, float boostInput, bool remote)
        {
            m_steerin = steering;
            m_accel = accel;
            m_brakeInput = footbrake;
            m_handbrake = handbrake;

            if (isSpaceShip && m_IsPlayerInVehicle)
            {
                HandleSpaceshipMovement(steering, accel, floatUp, floatDown, boostInput, remote);
            }
            else
            {
                HandleCarMovement(steering, accel, footbrake, handbrake, remote);
            }
        }

        /// <summary>
        /// Handle spaceship specific movement - SIMPLIFIED
        /// </summary>
        private void HandleSpaceshipMovement(float steering, float accel, float floatUp, float floatDown, float boostInput, bool remote)
        {
            if (!remote)
            {
                // Apply modified gravity
                ApplySpaceshipGravity();

                // Apply hover force to maintain height
                ApplyHoverForce();

                // Smoothly look at target
                if (m_SpaceshipLookAtTarget != null)
                {
                    SmoothLookAtTarget();
                }

                // Apply forward/backward movement
                ApplySpaceshipThrust(accel);

                HandleSpaceshipBoost(boostInput);

                // Apply vertical movement
                ApplySpaceshipVerticalMovement(floatUp, floatDown);

                // Apply simple steering (rotation only, no tilting)
                ApplySpaceshipSteering(steering);
            }

            // Always disable wheel physics for spaceship
            DisableWheelPhysics();
        }

        /// <summary>
        /// Disable wheel physics when in spaceship mode
        /// </summary>
        private void DisableWheelPhysics()
        {
            for (int i = 0; i < 4; i++)
            {
                if (m_WheelColliders(i) != null)
                {
                    m_WheelColliders(i).motorTorque = 0;
                    m_WheelColliders(i).brakeTorque = 0;
                    m_WheelColliders(i).steerAngle = 0;
                }
            }
        }

        private void HandleSpaceshipBoost(float boostInput)
        {
            Camera mainCam = GetMainCamera();
            if (boostInput > 0.1f && m_CurrentBoostAmount > 0)
            {
                m_IsBoosting = true;
                m_CurrentBoostAmount = Mathf.Max(0, m_CurrentBoostAmount - (m_SpaceshipBoostDecreaseRate * Time.deltaTime));

                // Apply boost to velocity
                Vector3 boostDirection = m_Rigidbody.velocity.normalized;
                m_Rigidbody.velocity = boostDirection * (m_Rigidbody.velocity.magnitude * m_SpaceshipBoostMultiplier);

                // Apply camera FOV effect
                if (mainCam != null)
                {
                    mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, m_SpaceshipBoostCameraView, 5f * Time.deltaTime);
                }
            }
            else
            {
                m_IsBoosting = false;

                // Recharge boost when not boosting
                if (m_CurrentBoostAmount < m_SpaceshipBoostAmount)
                {
                    m_CurrentBoostAmount = Mathf.Min(m_SpaceshipBoostAmount, m_CurrentBoostAmount + (m_SpaceshipBoostRechargeRate * Time.deltaTime));
                }

                // Return camera to normal FOV
                if (mainCam != null)
                {
                    mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, m_DefaultFOV, 3f * Time.deltaTime);
                }
            }
        }
        public float GetBoostPercentage()
        {
            return m_CurrentBoostAmount / m_SpaceshipBoostAmount;
        }

        public bool IsBoosting()
        {
            return m_IsBoosting;
        }
        // Add this method to initialize the locking system:
        private void InitializeTargetLocking()
        {
            if (enableTargetLocking && isSpaceShip)
            {
                lockSystem = GetComponent<bl_TargetLockSystem>();
                if (lockSystem == null)
                {
                    lockSystem = gameObject.AddComponent<bl_TargetLockSystem>();
                }
            }
        }

        

        // Add these public methods for external access:
        public bool HasTargetLock()
        {
            return lockSystem != null && lockSystem.HasLock;
        }

        /*public bl_VehicleManager GetLockedTarget()
        {
            return lockSystem != null ? lockSystem.LockedTarget : null;
        }*/

        public void ForceLockOnTarget(bl_VehicleManager target)
        {
            if (lockSystem != null)
            {
                lockSystem.ForceLockOnTarget(target);
            }
        }

        // Add this method to safely get the camera
        private Camera GetMainCamera()
        {
            if (m_MainCamera == null || !m_MainCamera.isActiveAndEnabled)
            {
                m_MainCamera = bl_VehicleCamera.Instance.cameraRef;
                if (m_MainCamera != null)
                {
                    m_DefaultFOV = m_MainCamera.fieldOfView;
                }
            }
            return m_MainCamera;
        }

        /// <summary>
        /// Apply thrust for forward/backward movement
        /// </summary>
        private void ApplySpaceshipThrust(float accel)
        {
            if (Mathf.Abs(accel) > 0.1f)
            {
                float thrustForce = accel * m_SpaceshipThrustForce;
                m_Rigidbody.AddRelativeForce(Vector3.forward * thrustForce);
            }

            // Cap speed
            CapSpaceshipSpeed();
        }

        /// <summary>
        /// Apply vertical movement for spaceship
        /// </summary>
        private void ApplySpaceshipVerticalMovement(float floatUp, float floatDown)
        {
            float verticalForce = 0f;

            if (floatUp > 0.1f)
            {
                verticalForce = floatUp * m_SpaceshipThrustForce;
                m_Rigidbody.AddRelativeForce(Vector3.up * verticalForce);
            }

            if (floatDown > 0.1f)
            {
                verticalForce = floatDown * m_SpaceshipThrustForce;
                m_Rigidbody.AddRelativeForce(Vector3.down * verticalForce);
            }
        }

        /// <summary>
        /// Apply modified gravity for spaceship
        /// </summary>
        private void ApplySpaceshipGravity()
        {
            // Apply reduced gravity
            m_Rigidbody.AddForce(Physics.gravity * m_SpaceshipGravityMultiplier, ForceMode.Acceleration);
        }
        /// <summary>
        /// Apply simple steering (rotation only)
        /// </summary>
        private void ApplySpaceshipSteering(float steering)
        {
            /*if (steering > 0.1f)
            {
                m_Rigidbody.AddRelativeForce(Vector3.left * -m_SpaceshipSteeringSpeed);
            }
            else if (steering < -0.1f)
            {
                m_Rigidbody.AddRelativeForce(Vector3.left * m_SpaceshipSteeringSpeed);
            }*/

            
        }


        /// <summary>
        /// Cap spaceship speed
        /// </summary>
        private void CapSpaceshipSpeed()
        {
            float currentSpeed = m_Rigidbody.velocity.magnitude;
            float maxSpeed = m_Topspeed / 3.6f;

            // Apply boost multiplier if boosting
            if (m_IsBoosting)
            {
                maxSpeed *= m_SpaceshipBoostMultiplier;
            }

            if (currentSpeed > maxSpeed)
            {
                m_Rigidbody.velocity = m_Rigidbody.velocity.normalized * maxSpeed;
            }
        }

        /// <summary>
        /// Apply hover force to maintain spaceship height
        /// </summary>
        private void ApplyHoverForce()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, -Vector3.up, out hit, m_SpaceshipHoverHeight * 2f))
            {
                float hoverError = m_SpaceshipHoverHeight - hit.distance;
                if (hoverError > 0)
                {
                    float upwardForce = hoverError * m_SpaceshipHoverForce;
                    m_Rigidbody.AddForce(Vector3.up * upwardForce);
                }
            }
        }

        /// <summary>
        /// Smoothly look at target transform
        /// </summary>
        private void SmoothLookAtTarget()
        {
            Vector3 direction = m_SpaceshipLookAtTarget.position - transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Preserve the current Z rotation (roll)
            Vector3 euler = targetRotation.eulerAngles;
            euler.z = bl_VehicleCamera.Instance.cameraTransform.rotation.eulerAngles.z;
            targetRotation = Quaternion.Euler(euler);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_SpaceshipRotationSpeed * Time.fixedDeltaTime);
        }


        /// <summary>
        /// Handle car specific movement
        /// </summary>
        private void HandleCarMovement(float steering, float accel, float footbrake, float handbrake, bool remote)
        {
            //clamp input values
            steering = Mathf.Clamp(steering, -1, 1);
            AccelInput = accel = Mathf.Clamp(accel, 0, 1);
            BrakeInput = footbrake = -1 * Mathf.Clamp(footbrake, -1, 0);
            handbrake = Mathf.Clamp(handbrake, 0, 1);

            Velocity = m_Rigidbody.velocity;
            VelocityMagnitude = Velocity.magnitude;

            //Set the steer on the front wheels.
            m_SteerAngle = steering * m_MaximumSteerAngle;
            smootSteer = Mathf.Lerp(smootSteer, m_SteerAngle, Time.deltaTime * steerResponsiveness);
            m_WheelColliders(0).steerAngle = smootSteer;
            m_WheelColliders(1).steerAngle = smootSteer;

            if (BrakeInput >= 1 || handbrake >= 1) m_Rigidbody.drag = breakResponsiveness;
            else m_Rigidbody.drag = 0;

            if (!remote)
            {
                SteerHelper();
                CapSpeed();
                ApplyDrive(accel, footbrake);
            }

            //Set the handbrake.
            if (handbrake > 0f)
            {
                float hbTorque = handbrake * m_MaxHandbrakeTorque;
                m_WheelColliders(2).brakeTorque = hbTorque;
                m_WheelColliders(3).brakeTorque = hbTorque;
            }

            CalculateRevs();
            if (!remote)
            {
                GearChanging();
                AddDownForce();
            }
            CheckForWheelSpin();
            TractionControl();
        }

        /// <summary>
        /// 
        /// </summary>
        private void GearChanging()
        {
            float f = Mathf.Abs(CurrentSpeed / MaxSpeed);
            float upgearlimit = (1 / (float)NoOfGears) * (m_GearNum + 1);
            float downgearlimit = (1 / (float)NoOfGears) * m_GearNum;

            if (m_GearNum > 0 && f < downgearlimit)
            {
                m_GearNum--;
            }

            if (f > upgearlimit && (m_GearNum < (NoOfGears - 1)))
            {
                m_GearNum++;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void CalculateGearFactor()
        {
            float f = (1 / (float)NoOfGears);
            // gear factor is a normalized representation of the current speed within the current gear's range of speeds.
            // We smooth towards the 'target' gear factor, so that revs don't instantly snap up or down when changing gear.
            var targetGearFactor = Mathf.InverseLerp(f * m_GearNum, f * (m_GearNum + 1), Mathf.Abs(CurrentSpeed / MaxSpeed));
            m_GearFactor = Mathf.Lerp(m_GearFactor, targetGearFactor, Time.deltaTime * 5f);
        }

        /// <summary>
        /// 
        /// </summary>
        private void CalculateRevs()
        {
            // calculate engine revs (for display / sound)
            // (this is done in retrospect - revs are not used in force/power calculations)
            CalculateGearFactor();
            float gearNumFactor = m_GearNum / (float)NoOfGears;
            float revsRangeMin = ULerp(0f, m_RevRangeBoundary, CurveFactor(gearNumFactor));
            float revsRangeMax = ULerp(m_RevRangeBoundary, 1f, gearNumFactor);
            Revs = ULerp(revsRangeMin, revsRangeMax, m_GearFactor);
        }

        /// <summary>
        /// this is used to add more grip in relation to speed
        /// </summary>
        private void AddDownForce()
        {
            m_Rigidbody.AddForce(-m_Transform.up * m_Downforce * VelocityMagnitude);
        }

        // checks if the wheels are spinning and is so does three things
        // 1) emits particles
        // 2) plays tiure skidding sounds
        // 3) leaves skidmarks on the ground
        // these effects are controlled through the WheelEffects class
        private void CheckForWheelSpin()
        {
            // loop through all wheels
            for (int i = 0; i < 4; i++)
            {
                WheelHit wheelHit;
                m_WheelColliders(i).GetGroundHit(out wheelHit);

                // is the tire slipping above the given threshhold
                if (Mathf.Abs(wheelHit.forwardSlip) >= m_SlipLimit || Mathf.Abs(wheelHit.sidewaysSlip) >= m_SlipLimit)
                {
                    carWheels[i].EmitTyreSmoke();

                    // avoiding all four tires screeching at the same time
                    // if they do it can lead to some strange audio artefacts
                    if (!AnySkidSoundPlaying())
                    {
                        carWheels[i].PlayAudio();
                    }
                    continue;
                }

                // if it wasnt slipping stop all the audio
                if (carWheels[i].PlayingAudio)
                {
                    carWheels[i].StopAudio();
                }
                // end the trail generation
                carWheels[i].EndSkidTrail();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool AnySkidSoundPlaying()
        {
            for (int i = 0; i < 4; i++)
            {
                if (carWheels[i].PlayingAudio)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ApplyFactor(bool apply, Vector3 Direction, float Force)
        {
            FactorDirection = Direction;
            isFactorOn = apply;
            FactorForce = Force * 0.1f;
        }

        /// <summary>
        /// 
        /// </summary>
        private void ApplyDrive(float accel, float footbrake)
        {
            float thrustTorque;
            switch (m_CarDriveType)
            {
                case CarDriveType.FourWheelDrive:
                    thrustTorque = accel * (m_CurrentTorque / 4f);
                    for (int i = 0; i < 4; i++)
                    {
                        m_WheelColliders(i).motorTorque = thrustTorque;
                    }
                    break;
                case CarDriveType.FrontWheelDrive:
                    thrustTorque = accel * (m_CurrentTorque / 2f);
                    m_WheelColliders(0).motorTorque = m_WheelColliders(1).motorTorque = thrustTorque;
                    break;

                case CarDriveType.RearWheelDrive:
                    thrustTorque = accel * (m_CurrentTorque / 2f);
                    m_WheelColliders(2).motorTorque = m_WheelColliders(3).motorTorque = thrustTorque;
                    break;

            }

            for (int i = 0; i < 4; i++)
            {
                if (CurrentSpeed > 5 && Vector3.Angle(m_Transform.forward, Velocity) < 50f)
                {
                    m_WheelColliders(i).brakeTorque = m_BrakeTorque * footbrake;
                }
                else if (footbrake > 0)
                {
                    m_WheelColliders(i).brakeTorque = 0f;
                    m_WheelColliders(i).motorTorque = -m_ReverseTorque * footbrake;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SteerHelper()
        {
            for (int i = 0; i < 4; i++)
            {
                WheelHit wheelhit;
                m_WheelColliders(i).GetGroundHit(out wheelhit);
                if (wheelhit.normal == Vector3.zero)
                    return; // wheels arent on the ground so dont realign the rigidbody velocity
            }

            // this if is needed to avoid gimbal lock problems that will make the car suddenly shift direction
            if (Mathf.Abs(m_OldRotation - m_Transform.eulerAngles.y) < 10f)
            {
                var turnadjust = (m_Transform.eulerAngles.y - m_OldRotation) * m_SteerHelper;
                Quaternion velRotation = Quaternion.AngleAxis(turnadjust, Vector3.up);
                m_Rigidbody.velocity = velRotation * Velocity;
            }
            m_OldRotation = m_Transform.eulerAngles.y;
        }

        /// <summary>
        /// crude traction control that reduces the power to wheel if the car is wheel spinning too much
        /// </summary>
        private void TractionControl()
        {
            WheelHit wheelHit;
            switch (m_CarDriveType)
            {
                case CarDriveType.FourWheelDrive:
                    // loop through all wheels
                    for (int i = 0; i < 4; i++)
                    {
                        m_WheelColliders(i).GetGroundHit(out wheelHit);

                        AdjustTorque(wheelHit.forwardSlip);
                    }
                    break;

                case CarDriveType.RearWheelDrive:
                    m_WheelColliders(2).GetGroundHit(out wheelHit);
                    AdjustTorque(wheelHit.forwardSlip);

                    m_WheelColliders(3).GetGroundHit(out wheelHit);
                    AdjustTorque(wheelHit.forwardSlip);
                    break;

                case CarDriveType.FrontWheelDrive:
                    m_WheelColliders(0).GetGroundHit(out wheelHit);
                    AdjustTorque(wheelHit.forwardSlip);

                    m_WheelColliders(1).GetGroundHit(out wheelHit);
                    AdjustTorque(wheelHit.forwardSlip);
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void AdjustTorque(float forwardSlip)
        {
            if (forwardSlip >= m_SlipLimit && m_CurrentTorque >= 0)
            {
                m_CurrentTorque -= 10 * m_TractionControl;
            }
            else
            {
                m_CurrentTorque += 10 * m_TractionControl;
                if (m_CurrentTorque > m_FullTorqueOverAllWheels)
                {
                    m_CurrentTorque = m_FullTorqueOverAllWheels;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnFixedUpdate()
        {
            if (!isSpaceShip)
            {
                WheelPosition();
            }
            else
            {
                // For spaceship, hide wheel visuals
                HandleSpaceshipWheelVisuals();
            }
        }

        /// <summary>
        /// Handle wheel visuals for spaceship mode
        /// </summary>
        private void HandleSpaceshipWheelVisuals()
        {
            for (int i = 0; i < 4; i++)
            {
                if (m_WheelMeshes[i] != null)
                {
                    m_WheelMeshes[i].SetActive(false);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void WheelPosition()
        {
            for (int i = 0; i < 4; i++)
            {
                if (m_WheelMeshes[i] != null && m_WheelColliders(i) != null)
                {
                    Quaternion quat;
                    Vector3 position;
                    m_WheelColliders(i).GetWorldPose(out position, out quat);
                    m_WheelMeshes[i].transform.position = position;
                    m_WheelMeshes[i].transform.rotation = quat;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void GetVehicleInput(ref float acceleration, ref float steering, ref Vector3 velocity)
        {
            acceleration = m_accel;
            steering = m_steerin;
            velocity = m_Rigidbody.velocity;
        }

        /// <summary>
        /// 
        /// </summary>
        public override void SetVehicleInput(float acceleration, float steering, Vector3 velocity)
        {
            m_Rigidbody.velocity = velocity;
            Move(steering, acceleration, 0, 0, 0, 0, 0, true);
        }

        public override void SetVehicleInput(float acceleration, float steering, bool isRemote = true)
        {
            Move(steering, acceleration, 0, 0, 0, 0, 0, isRemote);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnEnterVehicle()
        {
            m_IsPlayerInVehicle = true;

            if (isSpaceShip)
            {
                // Start lifting the spaceship
                StartCoroutine(LiftSpaceship(true));
                // Hide wheels for spaceship mode
                HandleSpaceshipWheelVisuals();
            }
            else
            {
                // Show wheels for car mode
                for (int i = 0; i < 4; i++)
                {
                    if (m_WheelMeshes[i] != null)
                    {
                        m_WheelMeshes[i].SetActive(true);
                    }
                    if (m_WheelColliders(i) != null)
                    {
                        m_WheelColliders(i).brakeTorque = 0f;
                        m_WheelColliders(i).motorTorque = -m_ReverseTorque;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnExitVehicle()
        {
            m_IsPlayerInVehicle = false;

            if (isSpaceShip)
            {
                // Start lowering the spaceship
                StartCoroutine(LiftSpaceship(false));

                // Show wheels again
                for (int i = 0; i < 4; i++)
                {
                    if (m_WheelMeshes[i] != null)
                    {
                        m_WheelMeshes[i].SetActive(true);
                    }
                }
            }
            else
            {
                Move(0, 0, 1, 1, 0, 0, 0, false);
            }
        }

        /// <summary>
        /// Coroutine to smoothly lift or lower the spaceship
        /// </summary>
        private IEnumerator LiftSpaceship(bool liftUp)
        {
            float targetHeight = liftUp ? m_SpaceshipHoverHeight : 0f;
            float currentHeight = m_CurrentHoverHeight;
            float elapsedTime = 0f;

            while (elapsedTime < m_SpaceshipLiftSpeed)
            {
                m_CurrentHoverHeight = Mathf.Lerp(currentHeight, targetHeight, elapsedTime / m_SpaceshipLiftSpeed);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            m_CurrentHoverHeight = targetHeight;
        }

        /// <summary>
        /// 
        /// </summary>
        private void CapSpeed()
        {
            CarVelocity = VelocityMagnitude;
            switch (m_SpeedType)
            {
                case SpeedType.MPH:
                    CarVelocity *= 2.23693629f;
                    if (CarVelocity > m_Topspeed)
                    {
                        m_Rigidbody.velocity = (m_Topspeed / 2.23693629f) * Velocity.normalized;
                    }
                    break;

                case SpeedType.KPH:
                    CarVelocity *= 3.6f;
                    if (CarVelocity > m_Topspeed)
                    {
                        m_Rigidbody.velocity = (m_Topspeed / 3.6f) * Velocity.normalized;
                    }
                    break;
            }
            if (isFactorOn) { m_Rigidbody.velocity += FactorDirection * FactorForce; }
        }



        // simple function to add a curved bias towards 1 for a value in the 0-1 range
        private static float CurveFactor(float factor)
        {
            return 1 - (1 - factor) * (1 - factor);
        }

        // unclamped version of Lerp, to allow value to exceed the from-to range
        private static float ULerp(float from, float to, float value)
        {
            return (1.0f - value) * from + value * to;
        }

        public WheelCollider m_WheelColliders(int index)
        {
            if (index < carWheels.Length && carWheels[index] != null)
            {
                return carWheels[index].wheelCollider;
            }
            return null;
        }

        public Transform GetSpaceShipWeaponTarget()
        {
            return m_SpaceshipWeaponTarget;
        }
        public Transform GetSpaceShipMissileTarget()
        {
            return m_SpaceshipMissileTarget;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (m_WheelColliders(0) != null)
            {
                Gizmos.DrawWireSphere(m_WheelColliders(0).transform.TransformPoint(m_CentreOfMassOffset), 0.1f);
            }
            Gizmos.DrawSphere(transform.TransformPoint(m_CentreOfMassOffset), 0.2f);

            // Draw spaceship hover height
            if (isSpaceShip)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, m_SpaceshipHoverHeight);
                Gizmos.DrawLine(transform.position, transform.position - Vector3.up * m_SpaceshipHoverHeight);
            }
        }
#endif
    }

    internal enum CarDriveType
    {
        FrontWheelDrive,
        RearWheelDrive,
        FourWheelDrive
    }

    public enum SpeedType
    {
        MPH,
        KPH
    }
    public enum SteerAxis
    {
        X,
        Y,
        Z
    }
}