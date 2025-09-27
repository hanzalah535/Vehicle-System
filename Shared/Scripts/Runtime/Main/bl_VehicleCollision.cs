using UnityEngine;
using MFPS.Internal.Vehicles;
using MFPS.Internal.Interfaces;

/// <summary>
/// This script handle the collisions with others colliders in the scene
/// this calculate the impact force of the collision and handle the damage given and gived to the local player and 
/// others players
/// </summary>
namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleCollision : MonoBehaviour
    {
        public float criticalImpact = 60;
        [LovattoToogle] public bool keepVelocityAfterCollision = true;
        [Header("Players Damage")]
        [Range(0.01f, 1)] public float playerThreshold = 0.4f;
        [Range(1, 100)] public float maxSelfDamage = 60;
        [Range(0f, 1)] public float minImpactForce = 1f;

        [Header("Vehicle Damage")]
        [Range(0.01f, 1)] public float vehicleThreshold = 0.4f;
        [Range(1, 100)] public float maxVehicleDamage = 30;

        [Header("Sound")]
        [Range(0.01f, 1)] public float audioThreshold = 0.1f;
        [SerializeField] private AudioClip CollisionAudio = null;

        public bool StopDetect { get; set; } = false;
        public float LastCollisionForce { get; set; } = 0;

        private float LastTime;
        private Rigidbody m_Rigidbody;

        /// <summary>
        /// 
        /// </summary>
        private void Awake()
        {
            LastTime = Time.time;
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (StopDetect) return;

            ImpactCalculation(collision);
            PlayerCollision(collision);
        }

        /// <summary>
        /// 
        /// </summary>
        void ImpactCalculation(Collision collision)
        {
            if (!collision.gameObject.CompareTag(bl_MFPS.HITBOX_TAG) && !collision.gameObject.CompareTag(bl_MFPS.AI_TAG))
            {
                if ((Time.time - LastTime) < 0.5f) return;
            }

            float collisionForce = collision.impulse.magnitude / Time.fixedDeltaTime;
            collisionForce /= m_Rigidbody.mass;
            LastCollisionForce = collisionForce;

            float dealthly = Mathf.Clamp01(collisionForce / criticalImpact);

            if (CollisionAudio != null && dealthly >= audioThreshold)
            {
                AudioSource.PlayClipAtPoint(CollisionAudio, transform.position, Mathf.Max(0.3f, dealthly));
            }

            if (dealthly >= playerThreshold && VehicleManager.vehicleState == VehicleState.UseByLocal && VehicleManager.DriverPlayer != null)
            {
                float playerDamage = maxSelfDamage * dealthly;
                VehicleManager.DriverPlayer.playerHealthManager.DoDamage(GetDamageData(playerDamage, collision.contacts[0].point));
            }

            if (dealthly >= vehicleThreshold && VehicleHealth != null)
            {
                float vehicleDamage = maxVehicleDamage * dealthly;
                VehicleHealth.ReceiveDamage(GetDamageData(vehicleDamage, collision.contacts[0].point));
            }

            LastTime = Time.time;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="col"></param>
        private void PlayerCollision(Collision col)
        {
            if (!VehicleManager.isMine) return;
            if (!col.gameObject.CompareTag(bl_MFPS.HITBOX_TAG) && !col.gameObject.CompareTag(bl_MFPS.AI_TAG))
                return;
            if (LastCollisionForce < minImpactForce)
            {
                if (keepVelocityAfterCollision) m_Rigidbody.velocity = VehicleManager.Velocity;
                return;
            }

            var playerRef = col.transform.GetComponentInParent<bl_PlayerReferencesCommon>();
            if (playerRef == null) return;

            var damageable = col.gameObject.GetComponent<IMFPSDamageable>();
            if (damageable == null)
            {
                return;
            }

            var damageData = GetDamageData(110, col.contacts[0].point);
            damageData.Cause = DamageCause.Vehicle;
            damageable.ReceiveDamage(damageData);

            playerRef.IgnoreColliders(VehicleManager.AllColliders, true);

            var agent = col.transform.GetComponentInParent<bl_AIShooter>();
            if (agent != null)
            {
                if (!agent.isTeamMate) agent.Agent.enabled = false;
            }

            if (keepVelocityAfterCollision) m_Rigidbody.velocity = VehicleManager.Velocity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        private DamageData GetDamageData(float damage, Vector3 point)
        {
            DamageData info = new DamageData();
            info.Actor = bl_PhotonNetwork.LocalPlayer;
            info.ActorViewID = bl_GameManager.LocalPlayerViewID;
            info.From = bl_PhotonNetwork.LocalPlayer.NickName;
            info.MFPSActor = bl_GameManager.Instance.LocalActor;
            info.Cause = DamageCause.FallDamage;
            info.Damage = (int)damage;
            info.Direction = point;
            info.GunID = -1;
            return info;
        }

        private bl_VehicleManager m_vehicleManager;
        private bl_VehicleManager VehicleManager
        {
            get
            {
                if (m_vehicleManager == null) m_vehicleManager = GetComponent<bl_VehicleManager>();
                return m_vehicleManager;
            }
        }

        private bl_VehicleHealth m_vehicleHealth;
        private bl_VehicleHealth VehicleHealth
        {
            get
            {
                if (m_vehicleHealth == null) m_vehicleHealth = GetComponent<bl_VehicleHealth>();
                return m_vehicleHealth;
            }
        }
    }
}