using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFPS.Runtime.Vehicles
{
    public abstract class bl_Vehicle : bl_MonoBehaviour
    {
        public int mobileButtonLayer = 1;

        /// <summary>
        /// This should be override in the inheriting class and return the input values
        /// </summary>
        public abstract void GetVehicleInput(ref float acceleration, ref float steering, ref Vector3 velocity);

        /// <summary>
        /// This should be override in the inheriting class and apply the input received from the server
        /// </summary>
        public abstract void SetVehicleInput(float acceleration, float steering, Vector3 velocity);

        /// <summary>
        /// This should be override in the inheriting class and apply the input received from the server
        /// </summary>
        public virtual void SetVehicleInput(float acceleration, float steering, bool isRemote = true) { }

        /// <summary>
        /// 
        /// </summary>
        public virtual void OnEnterVehicle() { }

        /// <summary>
        /// 
        /// </summary>
        public virtual void OnExitVehicle() { }
    }
}