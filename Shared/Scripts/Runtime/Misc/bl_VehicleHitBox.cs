using UnityEngine;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleHitBox : MonoBehaviour, IMFPSDamageable
    {
        public float damageMultiplier = 1;
        public float explosionMultiplier = 5;

        /// <summary>
        /// 
        /// </summary>
        public void ReceiveDamage(DamageData damageData)
        {
            if (VehicleHealth == null) return;

            damageData.Damage = (int)(damageData.Damage * damageMultiplier);
            if(damageData.Cause == DamageCause.Explosion)
            {
                damageData.Damage = (int)(damageData.Damage * explosionMultiplier);
            }
            VehicleHealth.ReceiveDamage(damageData);
        }

        private bl_VehicleHealth m_vehicleDamage;
        public bl_VehicleHealth VehicleHealth
        {
            get
            {
                if (m_vehicleDamage == null) m_vehicleDamage = GetComponentInParent<bl_VehicleHealth>();
                return m_vehicleDamage;
            }
        }
    }
}