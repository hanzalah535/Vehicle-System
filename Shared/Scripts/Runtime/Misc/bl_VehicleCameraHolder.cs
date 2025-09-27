using UnityEngine;
using MFPS.Internal.Vehicles;
using MFPSEditor;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleCameraHolder : bl_MonoBehaviour
    {
        [ScriptableDrawer] public bl_VehicleCameraSettings cameraSettings;
        public VehicleCameraFollowType cameraFollowType = VehicleCameraFollowType.Orbit;
        [LovattoToogle] public bool moveToPosition = false;


        /// <summary>
        /// 
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            bl_VehicleCamera.Instance.SetCameraSettings(cameraSettings);
            bl_VehicleCamera.Instance.CameraFollowType = cameraFollowType;
            if (moveToPosition)
            {
                bl_VehicleCamera.Instance.cameraTransform.position = transform.position;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            if (bl_VehicleCamera.Instance == null) return;

            bl_VehicleCamera.Instance.SetCameraSettings(null);
            bl_VehicleCamera.Instance.CameraFollowType = bl_VehicleSettings.Instance.cameraFollowType;
        }
    }
}