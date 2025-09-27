using UnityEngine;

[CreateAssetMenu( fileName = "VehicleCameraSettings", menuName = "MFPS/Vehicle/Camera Settings")]
public class bl_VehicleCameraSettings : ScriptableObject
{
    public float Distance = 5;
    public float YawLimit = 45;
    public float PitchLimit = 15;
    public float Damping = 5;
    public float FieldOfView = 60;
    public float Sensitivity = 1;
    public float MobileSensitivity = 0.5f;
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public float GetSensitivity()
    {
        return bl_UtilityHelper.isMobile ? MobileSensitivity : Sensitivity;
    }
}