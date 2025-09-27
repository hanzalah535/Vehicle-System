using MFPS.Runtime.Vehicles;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class bl_VehicleEvents 
{
    public static Action<bl_VehicleManager> onLocalEnterInVehicle;
    public static void DispatchLocalEnterInVehicle(bl_VehicleManager vehicle) => onLocalEnterInVehicle?.Invoke(vehicle);

    public static Action<bl_VehicleManager> onLocalExitInVehicle;
    public static void DispatchLocalExitVehicle(bl_VehicleManager vehicle) => onLocalExitInVehicle?.Invoke(vehicle);
}