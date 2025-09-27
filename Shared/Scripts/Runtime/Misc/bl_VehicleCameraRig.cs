using MFPSEditor;
using UnityEngine;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleCameraRig : MonoBehaviour
    {
        public Transform LookAtTarget;
        public Transform CameraPosition;
        [ScriptableDrawer] public bl_VehicleCameraSettings customCameraSettings;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Vector3 TargetPosition() => CameraPosition.position;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Quaternion TargetRotation() => CameraPosition.rotation;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public float GetDistanceBetweenCameraAndTarget()
        {
            return bl_MathUtility.Distance(CameraPosition.position, LookAtTarget.position);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lookAt"></param>
        public void SetCameraLookAtTarget()
        {
            CameraPosition.LookAt(LookAtTarget);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (CameraPosition != null)
            {
                Matrix4x4 tempMat = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(CameraPosition.position, CameraPosition.rotation, Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, 50, 15, 0.5f, 1);
                Gizmos.matrix = tempMat;
            }

            if (LookAtTarget != null)
            {
                Gizmos.DrawSphere(LookAtTarget.position, 0.2f);
                Gizmos.DrawLine(CameraPosition.position, LookAtTarget.position);
            }
        }
    }
}