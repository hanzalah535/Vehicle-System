using System;
using System.Collections.Generic;
using UnityEngine;

namespace MFPS.Internal.Vehicles
{
    [Serializable]
    public class VehicleSteerWheel
    {
        public Transform SteerWheel;
        [Range(0.01f, 1)] public float SteeringHandSpace = 0.1f;
        [Range(10, 180)] public float SteeringWheelMaxAngle = 90;

        private Vector3 rotation;

        public Vector3 GetLeftHandPosition() => -(SteerWheel.right * SteeringHandSpace);
        public Vector3 GetRightHandPosition() => (SteerWheel.right * SteeringHandSpace);

        public bool isSpaceShip;

        public void Update(float angle)
        {
            if (SteerWheel == null || isSpaceShip) return;

            rotation = SteerWheel.localEulerAngles;
            rotation.z = -(angle * SteeringWheelMaxAngle);
            SteerWheel.localRotation = Quaternion.Slerp(SteerWheel.localRotation, Quaternion.Euler(rotation), Time.deltaTime * 4);
        }

        public void DrawGizmos()
        {
            if (SteerWheel == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(SteerWheel.position, 0.07f);
            Gizmos.color = Color.yellow;
            Vector3 center = SteerWheel.position;
            Gizmos.DrawSphere(center + (SteerWheel.right * SteeringHandSpace), 0.05f);
            Gizmos.DrawSphere(center - (SteerWheel.right * SteeringHandSpace), 0.05f);
        }
    }
}