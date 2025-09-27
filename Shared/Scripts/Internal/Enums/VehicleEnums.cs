using System;
using System.Collections.Generic;
using UnityEngine;

namespace MFPS.Internal.Vehicles
{
    [Serializable]
    public enum LocalVehicleState
    {
        Idle = 0,
        PlayerInTrigger,
        PlayerIn,
        PlayerInPassenger,
    }

    [Serializable]
    public enum VehicleState
    {
        Idle = 0,
        UseByLocal,
        UseByRemote,
    }

    public enum VehicleCall
    {
        DriverState = 0,
        VehicleInit = 1,
        SeatChange = 2,
        VehicleDamage = 3,
    }

    [Serializable]
    public enum VehicleCameraFollowType
    {
        Follow,
        Orbit,
        Look,
        Custom,
    }
}