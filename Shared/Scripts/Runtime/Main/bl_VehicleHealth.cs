#define MFPS_19
using UnityEngine;
using MFPS.Internal.Vehicles;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Pun;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleHealth : bl_MonoBehaviour, IMFPSDamageable
    {
        public int vehicleHealth = 100;
        public int maxBulletDamage = 5;
        public int respawnTime = 10;
        public int explosionDamage = 100;
        [LovattoToogle] public bool showDamageIndicator = false;

        [Header("Events")]
        public bl_EventHandler.UEvent onDestroyed;
        public bl_EventHandler.UEvent onRespawn;

        [Header("References")]
        public GameObject vehicleModel;
        public GameObject explosionPrefab;
        public ParticleSystem damageParticle;

        public float CurrentHealth { get; set; } = 100;

        /// <summary>
        /// 
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            CurrentHealth = vehicleHealth;
            if (damageParticle != null) damageParticle.gameObject.SetActive(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public void ReceiveDamage(DamageData damageData)
        {
            if (!VehicleManager.Utilizable) return;

            var data = damageData.GetAsHashtable();
            photonView.RPC(bl_VehicleManager.VEHICLE_RPC_NAME, RpcTarget.All, VehicleCall.VehicleDamage, data); // bl_VehicleManager -> VehicleRpc
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnReceiveDamage(Hashtable data)
        {
            var damageData = new DamageData(data);
            float damage = (float)maxBulletDamage * ((float)damageData.Damage / 100f);
            damage = Mathf.Max(damage, 1);

            CurrentHealth -= damage;

            if (bl_MFPS.Utility.IsLocalPlayer(damageData.ActorViewID))
            {
                bl_CrosshairBase.Instance.OnHit();
            }

            if (damageParticle != null)
            {
                damageParticle.gameObject.SetActive(true);
                float percentage = CurrentHealth / vehicleHealth;
                var emission = damageParticle.emission;
                emission.rateOverTimeMultiplier = percentage <= 0.5f ? 1 - percentage : 0;

                if (!damageParticle.isPlaying)
                    damageParticle.Play();
            }

            if (VehicleManager.IsLocalPlayerInside())
            {
                bl_VehicleUI.Instance.UpdateStats(VehicleManager);
                if(showDamageIndicator && bl_GameData.Instance.showDamageIndicator)
                {
                    bl_DamageIndicatorBase.Instance.SetHit(damageData.Direction);
                }
            }

            if (CurrentHealth < 1)
            {
                OnVehicleDestroyed(damageData);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void OnVehicleDestroyed(DamageData damageData)
        {
            KillPlayer(damageData);
            if (!bl_VehicleSettings.Instance.vehicleRespawn)
            {
                if (bl_PhotonNetwork.IsMasterClient) PhotonNetwork.Destroy(gameObject);
                return;
            }
            if (VehicleManager.localVehicleState == LocalVehicleState.PlayerInTrigger) bl_VehicleUI.Instance.ShowEnterUI(false);

#if MFPS_19
            bl_UtilityHelper.DetachChildDecals(gameObject);
#endif

            VehicleManager.localVehicleState = LocalVehicleState.Idle;
            VehicleManager.vehicleState = VehicleState.Idle;
            VehicleManager.Utilizable = false;
            //vehicleModel.SetActive(false);
            //VehicleManager.SetCollidersEnable(false);
            VehicleManager.TriggerManager.SetActive(false);
            var explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity).GetComponent<bl_ExplosionDamage>();
            explosion.transform.parent = null;
            if (damageData.MFPSActor != null && bl_MFPS.LocalPlayer.ViewID == damageData.ActorViewID)
            {
                BulletData bd = new BulletData(damageData);
                bd.Damage = explosionDamage;
                bd.isNetwork = !(bl_MFPS.LocalPlayer.ViewID == damageData.ActorViewID);
                explosion.InitExplosion(bd, damageData.MFPSActor);
            }

            if (damageParticle != null)
            {
                damageParticle.Stop();
            }

            onDestroyed?.Invoke();
            this.InvokeAfter(respawnTime, RespawnVehicle);
        }

        /// <summary>
        /// 
        /// </summary>
        public void KillPlayer(DamageData damageData)
        {
            if (!VehicleManager.IsLocalPlayerInside()) return;

            if (damageData.Cause == DamageCause.Player || damageData.Cause == DamageCause.Bot || damageData.Cause == DamageCause.Explosion)
            {
                var localPlayerHealth = bl_MFPS.LocalPlayerReferences.playerHealthManager;
                if (localPlayerHealth == null) return;

                damageData.Damage = 1000;
                localPlayerHealth.DoDamage(damageData);
            }
            else
            {
                bl_MFPS.LocalPlayer.Suicide(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void RespawnVehicle()
        {
            CurrentHealth = vehicleHealth;
            VehicleManager.Utilizable = true;
            VehicleManager.SetVehicleToOrigin();
            if(TryGetComponent(out Rigidbody rigidbody))
            {
                rigidbody.velocity = Vector3.zero;
                rigidbody.Sleep();
            }
            vehicleModel.SetActive(true);
            VehicleManager.TriggerManager.SetActive(true);
            VehicleManager.SetCollidersEnable(true);
            if (damageParticle != null)
            {
                damageParticle.Clear();
                damageParticle.gameObject.SetActive(false);
            }
            onRespawn?.Invoke();
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
    }
}