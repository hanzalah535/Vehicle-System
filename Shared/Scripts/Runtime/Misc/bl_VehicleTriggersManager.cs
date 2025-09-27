using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleTriggersManager : MonoBehaviour
    {
        public bl_VehicleSeatTrigger areaTrigger;
        public bl_VehicleManager vehicleManager;

        private bl_VehicleSeatTrigger[] m_triggers;


        /// <summary>
        /// 
        /// </summary>
        public void SetActive(bool active, bool fromArea = false)
        {
            if (m_triggers == null) m_triggers = transform.GetComponentInParent<bl_VehicleManager>().GetComponentsInChildren<bl_VehicleSeatTrigger>();

            for (int i = 0; i < m_triggers.Length; i++)
            {
                if (m_triggers[i] == areaTrigger) continue;

                m_triggers[i].SetActive(active, fromArea);
            }
        }
    }
}