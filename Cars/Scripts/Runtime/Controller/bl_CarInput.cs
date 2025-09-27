using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFPS.Runtime.Vehicles
{
    public class bl_CarInput : bl_MonoBehaviour
    {
        private bl_CarController car;
        private float horizontal, vertical, handBrake = 0;
        private float floatUp = 0;
        private float floatDown = 0;
        private float boost = 0;

        /// <summary>
        /// 
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            car = GetComponent<bl_CarController>();
        }

#if MFPSM
        protected override void OnEnable()
        {
            base.OnEnable();
            bl_TouchHelper.OnVehicleDirection += OnMobileDirection;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            bl_TouchHelper.OnVehicleDirection -= OnMobileDirection;
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dir"></param>
        void OnMobileDirection(Vector2 dir)
        {
            vertical = dir.y;
            horizontal = dir.x;
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnUpdate()
        {
            if (!bl_UtilityHelper.isMobile)
            {
                // For spaceship, we want different input behavior
                if (car != null && car.isSpaceShip)
                {
                    // Spaceship controls: left/right for tilt, forward/backward for acceleration
                    horizontal = bl_GameInput.Horizontal;
                    vertical = bl_GameInput.Vertical;
                    handBrake = bl_GameInput.Jump(GameInputType.Hold) ? 1 : 0;
                    floatUp = bl_GameInput.LeanRight(GameInputType.Hold) ? 1 : 0;
                    floatDown = bl_GameInput.LeanLeft(GameInputType.Hold) ? 1 : 0;
                    boost = bl_GameInput.Run(GameInputType.Hold) ? 1 : 0;
                }
                else
                {
                    // Original car controls
                    vertical = bl_GameInput.Vertical;
                    horizontal = bl_GameInput.Horizontal;
                    handBrake = bl_GameInput.Jump(GameInputType.Hold) ? 1 : 0;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnFixedUpdate()
        {
            if (car != null)
            {
                if (car.isSpaceShip)
                {
                    // For spaceship, pass all movement parameters including vertical and boost
                    car.Move(horizontal, vertical, 0, handBrake, floatUp, floatDown, boost, false);
                }
                else
                {
                    // Original car movement
                    car.Move(horizontal, vertical, vertical, handBrake, 0, 0, 0, false);
                }
            }
        }
    }
}