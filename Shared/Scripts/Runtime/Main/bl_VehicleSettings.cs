using UnityEngine;
using MFPS.Internal.Vehicles;

public class bl_VehicleSettings : ScriptableObject
{
    [LovattoToogle] public bool vehicleRespawn = true;
    [LovattoToogle] public bool forceFPModeForPassengers = false;
    public VehicleCameraFollowType cameraFollowType = VehicleCameraFollowType.Orbit;

#if UNITY_EDITOR
    public Mesh playerSitMesh;
#endif

    public static bool IsThirdPerson()
    {
#if MFPSTPV
        return bl_CameraViewSettings.IsThirdPerson();
#else
        return false;
#endif
    }

    private static bl_VehicleSettings _instance;
    public static bl_VehicleSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<bl_VehicleSettings>("VehicleSettings") as bl_VehicleSettings;
            }
            return _instance;
        }
    }
}