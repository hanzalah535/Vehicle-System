using System.Collections.Generic;
using UnityEngine;
using MFPS.Internal.Vehicles;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using MFPS.Internal.Interfaces;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleManager : bl_MonoBehaviour, IPunObservable
    {
        #region Public Members
        public VehicleState vehicleState = VehicleState.Idle;
        public LocalVehicleState localVehicleState = LocalVehicleState.Idle;

        public bl_Vehicle vehicle;

        [Header("Driver Seat")]
        public VehicleSeat driverSeat;

        [Header("Steering Wheel")]
        public VehicleSteerWheel steerWheel;

        [Header("References")]
        public MonoBehaviour[] vehicleScripts;

        [Header("Events")]
        [SerializeField] public bl_EventHandler.UEvent onLocalEnter;
        [SerializeField] public bl_EventHandler.UEvent onLocalExit;
        #endregion

        #region Public Properties
        /// <summary>
        /// Player references of the current driver of this vehicle
        /// Null if there's no one driving the vehicle
        /// </summary>
        public bl_PlayerReferences DriverPlayer 
        { 
            get;
            set; 
        }

        /// <summary>
        /// Is the local player inside the vehicle trigger collider?
        /// </summary>
        public bool IsLocalInTrigger 
        { 
            get; 
            set; 
        } = false;

        /// <summary>
        /// Current vehicle acceleration
        /// </summary>
        public float Acceleration
        { 
            get; 
            set; 
        } = 0;

        /// <summary>
        /// Current vehicle steering angle
        /// </summary>
        public float Steering 
        { 
            get; 
            set; 
        } = 0;

        /// <summary>
        /// Vehicle velocity
        /// </summary>
        public Vector3 Velocity 
        { 
            get;
            set;
        } = Vector3.zero;

        /// <summary>
        /// Vehicle velocity calculated in a fixed update frame
        /// </summary>
        public Vector3 FixedVelocity 
        { 
            get; 
            set; 
        } = Vector3.zero;

        /// <summary>
        /// Is this vehicle utilizable at the moment?
        /// </summary>
        public bool Utilizable 
        { 
            get;
            set; 
        } = true;

        public List<bl_PlayerVehicle> UpdateList { get; set; } = new List<bl_PlayerVehicle>();

        /// <summary>
        /// The current active vehicle trigger
        /// The active trigger is the collider that the local player is looking at
        /// </summary>
        public bl_VehicleSeatTrigger CurrentTrigger 
        { 
            get; 
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public int TriggerIDCounter
        {
            get
            {
                internalTriggerCounter++;
                return internalTriggerCounter;
            }
        }

        public const string VEHICLE_RPC_NAME = "VehicleRpc";
        #endregion

        #region Private Members
        private PhotonView m_photonView;
        private float m_acceleration, m_steering = 0;
        private Vector3 spawnPosition, spawnRotation, m_velocity;
        private bool defaultValuesFetched = false;
        private List<IVehicleUpdate> updateVehicleComponents = new List<IVehicleUpdate>();
        private int internalTriggerCounter = 0;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_photonView = photonView;
            SetActiveVehicleObjects(false);
            bl_PhotonCallbacks.PlayerEnteredRoom += OnPhotonPlayerConnected;
            bl_PhotonCallbacks.PlayerLeftRoom += OnPhotonPlayerDisconnected;
            bl_PhotonCallbacks.PlayerPropertiesUpdate += OnPlayerPropertiesUpdate;
            bl_EventHandler.onLocalPlayerDeath += OnLocalPlayerDeath;
            bl_EventHandler.onLocalPlayerSpawn += OnLocalPlayerSpawm;
            if (bl_PhotonNetwork.IsMasterClient)
            {
                TakeOwnershipOfThisVehicle();
            }
            GetDefaults();
            if (!m_photonView.ObservedComponents.Contains(this)) m_photonView.ObservedComponents.Add(this);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            bl_PhotonCallbacks.PlayerEnteredRoom -= OnPhotonPlayerConnected;
            bl_PhotonCallbacks.PlayerLeftRoom -= OnPhotonPlayerDisconnected;
            bl_PhotonCallbacks.PlayerPropertiesUpdate -= OnPlayerPropertiesUpdate;
            bl_EventHandler.onLocalPlayerSpawn -= OnLocalPlayerSpawm;
            bl_EventHandler.onLocalPlayerDeath -= OnLocalPlayerDeath;
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnUpdate()
        {
            InputControl();
            if (localVehicleState == LocalVehicleState.PlayerIn) OnLocalInVehicle();
            if (vehicleState == VehicleState.UseByRemote) OnRemoteInVehicle();
            UpdatePassengers();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnFixedUpdate()
        {
            if (vehicleState == VehicleState.UseByRemote && !m_photonView.IsMine)
            {
                vehicle.SetVehicleInput(Acceleration, Steering, Velocity);
            }

            if (localVehicleState == LocalVehicleState.PlayerIn)
            {
                OnLocalInVehicleFixed();
                for (int i = 0; i < updateVehicleComponents.Count; i++)
                {
                    updateVehicleComponents[i].WhenInsideFixedUpdateVehicle();
                }
            }
        }

        /// <summary>
        /// Add a component to update on demand
        /// </summary>
        public void AddUpdateComponent(IVehicleUpdate component)
        {
            if (updateVehicleComponents.Contains(component)) return;

            updateVehicleComponents.Add(component);
        }

        /// <summary>
        /// Check the inputs for enter and exit the vehicle
        /// </summary>
        void InputControl()
        {
            if (localVehicleState == LocalVehicleState.PlayerInTrigger)
            {
                if (!IsVehicleUsed() && bl_GameInput.Interact())
                {
                    EnterInVehicle();
                }
            }
            else if (localVehicleState == LocalVehicleState.PlayerIn)
            {
                if (bl_GameInput.Interact())
                {
                    ExitVehicle();
                }
            }
        }

        /// <summary>
        /// Make the local player intent to enter in this vehicle
        /// </summary>
        public void IntentToEnterInVehicle()
        {
            if (localVehicleState == LocalVehicleState.PlayerInTrigger)
            {
                if (!IsVehicleUsed()) EnterInVehicle();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void IntentToExitVehicle()
        {
            if (localVehicleState == LocalVehicleState.PlayerIn) ExitVehicle();
        }

        /// <summary>
        /// Called each frame when the local player is driving this vehicle.
        /// </summary>
        void OnLocalInVehicle()
        {        
            steerWheel.Update(Steering);

            for (int i = 0; i < updateVehicleComponents.Count; i++)
            {
                updateVehicleComponents[i].WhenInsideUpdateVehicle();
            }
        }

        /// <summary>
        /// Called each fixed frame when the local player is driving this vehicle.
        /// </summary>
        void OnLocalInVehicleFixed()
        {
            if (vehicle != null)
            {
                vehicle.GetVehicleInput(ref m_acceleration, ref m_steering, ref m_velocity);
                Acceleration = m_acceleration;
                Steering = m_steering;
                Velocity = m_velocity;
            }
        }

        /// <summary>
        /// Called each frame when a remote player is in the vehicle
        /// </summary>
        void OnRemoteInVehicle()
        {
            steerWheel.Update(Steering);
        }

        /// <summary>
        /// Make the local player enter in this vehicle
        /// </summary>
        public void EnterInVehicle()
        {
            if (localVehicleState == LocalVehicleState.PlayerInPassenger || localVehicleState == LocalVehicleState.PlayerIn) return;

            vehicleState = VehicleState.UseByLocal;
            localVehicleState = LocalVehicleState.PlayerIn;
            DriverPlayer = bl_MFPS.LocalPlayerReferences;
            CurrentTrigger = null;
            TriggerManager.SetActive(false);

            TakeOwnershipOfThisVehicle();
            SetActiveVehicleObjects(true);
#if MFPS_VEHICLE
            bl_MFPS.LocalPlayerReferences.PlayerVehicle.LocalEnterInVehicle(this, driverSeat);
#endif
            bl_VehicleCamera.Instance.SetCarTarget(CachedTransform, bl_MFPS.LocalPlayerReferences.PlayerCameraTransform);
            bl_VehicleEvents.DispatchLocalEnterInVehicle(this);
            SyncDriverState(true);
            vehicle?.OnEnterVehicle();
            onLocalEnter?.Invoke();
            bl_VehicleUI.Instance.UpdateStats(this);
        }

        /// <summary>
        /// Make the local player exit this vehicle
        /// </summary>
        /// <param name="statesOnly">Reset only the vehicle parameters without sending the player events</param>
        public void ExitVehicle(bool statesOnly = false)
        {
            vehicleState = VehicleState.Idle;
            localVehicleState = LocalVehicleState.Idle;
            DriverPlayer = null;
            CurrentTrigger = null;
            TriggerManager.SetActive(true);
            SetActiveVehicleObjects(false);
            bl_VehicleCamera.Instance.Disable();
#if MFPS_VEHICLE
           if(!statesOnly) bl_MFPS.LocalPlayerReferences.PlayerVehicle.LocalExitVehicle(this, driverSeat);
#endif
            SyncDriverState(false);
            bl_VehicleEvents.DispatchLocalExitVehicle(this);
            Steering = Acceleration = 0;
            vehicle?.OnExitVehicle();
            onLocalExit?.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        void SyncDriverState(bool enter)
        {
            var data = bl_UtilityHelper.CreatePhotonHashTable();
            data.Add("enter", enter);
            data.Add("viewID", bl_MFPS.LocalPlayer.ViewID);
            m_photonView.RPC(nameof(VehicleRpc), RpcTarget.Others, VehicleCall.DriverState, data);
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetActiveVehicleObjects(bool active)
        {
            foreach (var script in vehicleScripts)
            {
                script.enabled = active;
            }
        }

        /// <summary>
        /// Called when the player enter or exit the trigger to enter in this vehicle.
        /// </summary>
        public void OnPlayerEnterInTrigger(bool enter, bl_VehicleSeatTrigger trigger)
        {
            if (enter && vehicleState != VehicleState.Idle) return;

            if (enter) CurrentTrigger = trigger;
            else if (CurrentTrigger == trigger) CurrentTrigger = null;
            localVehicleState = enter ? LocalVehicleState.PlayerInTrigger : LocalVehicleState.Idle;
            bl_VehicleUI.Instance.ShowEnterUI(enter, this);
        }

        /// <summary>
        /// 
        /// </summary>
        void OnLocalPlayerDeath()
        {
            if (DriverPlayer != null && DriverPlayer.photonView.Owner.ActorNumber != bl_PhotonNetwork.LocalPlayer.ActorNumber)
                return;
            if (localVehicleState != LocalVehicleState.PlayerIn) return;

            CurrentTrigger = null;
            ExitVehicle();
        }

        /// <summary>
        /// 
        /// </summary>
        void UpdatePassengers()
        {
            for (int i = UpdateList.Count - 1; i >= 0; i--)
            {
                if (UpdateList[i] == null)
                {
                    UpdateList.RemoveAt(i);
                    continue;
                }
                UpdateList[i].UpdatePlayerInside();
            }
        }

        #region Network
        /// <summary>
        /// 
        /// </summary>
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(Acceleration);
                stream.SendNext(Steering);
                stream.SendNext(Velocity);
            }
            else
            {
                Acceleration = (float)stream.ReceiveNext();
                Steering = (float)stream.ReceiveNext();
                Velocity = (Vector3)stream.ReceiveNext();
                GetDefaults();
            }
        }

        [PunRPC]
        void VehicleRpc(VehicleCall callType, Hashtable data, PhotonMessageInfo message)
        {
            switch (callType)
            {
                case VehicleCall.DriverState:
                    OnDriverStateChange(data);
                    break;
                case VehicleCall.VehicleInit:
                    OnVehicleRemoteInit(data);
                    break;
                case VehicleCall.SeatChange:
                    VehicleSeats.OnSeatStateChange(data);
                    break;
                case VehicleCall.VehicleDamage:
                    VehicleHealth.OnReceiveDamage(data);
                    break;
            }
        }

        /// <summary>
        /// Called when a player (not the driver) exit or enter in the driver seat.
        /// </summary>
        void OnDriverStateChange(Hashtable data)
        {
            bool enter = (bool)data["enter"];
            int viewID = (int)data["viewID"];
            vehicleState = enter ? VehicleState.UseByRemote : VehicleState.Idle;

            //Find the client who send this call in our side.
            var remoteDriver = bl_GameManager.Instance.GetMFPSActor(viewID);
            if (remoteDriver == null) { Debug.LogWarning($"Actor {viewID} is not listed."); return; }
            if (remoteDriver.Actor == null) { Debug.LogWarning($"Actor {viewID} could not be found in the scene."); return; }

#if MFPS_VEHICLE
            //Manage the remote player 
            var playerRef = remoteDriver.Actor.GetComponent<bl_PlayerReferences>();
            if (enter) playerRef.PlayerVehicle.RemoteEnterInVehicle(this, driverSeat);
            else
            {
                playerRef.PlayerVehicle.RemoteExitVehicle(this, driverSeat);
                vehicle.SetVehicleInput(0, 0, Vector3.zero);
            }
#endif
        }

        /// <summary>
        /// This is called on new players (that just joined in the room) from the master client
        /// The call contains the sync data of this vehicle.
        /// </summary>
        void OnVehicleRemoteInit(Hashtable data)
        {
            vehicleState = (VehicleState)data["state"];
            if (data.ContainsKey("driverID"))//means that someone is currently inside of the vehicle
            {
                var viewID = (int)data["driverID"];
                //Find the driver
                var remoteDriver = bl_GameManager.Instance.GetMFPSActor(viewID);
                if (remoteDriver == null) { Debug.LogWarning($"Actor {viewID} is not listed."); return; }
                if (remoteDriver.Actor == null) { Debug.LogWarning($"Actor {viewID} could not be found in the scene."); return; }

#if MFPS_VEHICLE
                var playerRef = remoteDriver.Actor.GetComponent<bl_PlayerReferences>();
                playerRef.PlayerVehicle.RemoteEnterInVehicle(this, driverSeat);
#endif
            }
            if (data.ContainsKey("health"))
            {
                VehicleHealth.CurrentHealth = (float)data["health"];
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void TakeOwnershipOfThisVehicle()
        {
            if (m_photonView.Owner == null || m_photonView.Owner.UserId != bl_PhotonNetwork.LocalPlayer.UserId)
            {
                m_photonView.RequestOwnership();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnPhotonPlayerDisconnected(Player otherPlayer)
        {
            //if the player who left was the owner of this vehicle
            if (DriverPlayer != null && DriverPlayer.photonView.OwnerActorNr == otherPlayer.ActorNumber)
            {
                vehicleState = VehicleState.Idle;
                localVehicleState = LocalVehicleState.Idle;
                DriverPlayer = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnPhotonPlayerConnected(Player newPlayer)
        {
            if (bl_PhotonNetwork.IsMasterClient)
            {
                var data = bl_UtilityHelper.CreatePhotonHashTable();
                data.Add("state", vehicleState);
                if (DriverPlayer != null) data.Add("driverID", DriverPlayer.photonView.ViewID);
                if (VehicleHealth != null) data.Add("health", VehicleHealth.CurrentHealth);
                m_photonView.RPC(nameof(VehicleRpc), newPlayer, VehicleCall.VehicleInit, data);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnLocalPlayerSpawm()
        {
            // if the local player was inside the vehicle.
            if (IsLocalPlayerInside() && DriverPlayer == null)
            {
                ExitVehicle(true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="player"></param>
        /// <param name="hashtable"></param>
        public void OnPlayerPropertiesUpdate(Player player, Hashtable hashtable)
        {
            if (player.IsLocal)
            {
                // if the local player update his team
                if (hashtable.ContainsKey(PropertiesKeys.TeamKey))
                {
                    // if the local player is inside the vehicle.
                    if (IsLocalPlayerInside())
                    {
                        Debug.LogWarning("Local player team changed and inside");
                        ExitVehicle(true);
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// Is this vehicle currently being used?
        /// </summary>
        /// <returns></returns>
        public bool IsVehicleUsed()
        {
            if (vehicleState == VehicleState.UseByLocal || vehicleState == VehicleState.UseByRemote)
                return true;

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsLocalPlayerInside()
        {
            return localVehicleState == LocalVehicleState.PlayerIn || localVehicleState == LocalVehicleState.PlayerInPassenger;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsDriverTrigger()
        {
            if (CurrentTrigger == null) return false;
            return CurrentTrigger.seatTarget == bl_VehicleSeatTrigger.TriggerTarget.Driver;
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetVehicleToOrigin()
        {
            CachedTransform.position = spawnPosition;
            CachedTransform.eulerAngles = spawnRotation;
        }

        /// <summary>
        /// 
        /// </summary>
        void GetDefaults()
        {
            if (defaultValuesFetched) return;

            spawnPosition = CachedTransform.position;
            spawnRotation = CachedTransform.eulerAngles;
            defaultValuesFetched = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="enabled"></param>
        public void SetCollidersEnable(bool enabled)
        {
            for (int i = 0; i < AllColliders.Length; i++)
            {
                if (AllColliders[i] == null) continue;

                AllColliders[i].enabled = enabled;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;

            driverSeat.DrawGizmo(transform, new Color(0, 1, 0, 0.4f));
            steerWheel.DrawGizmos();
        }
#endif
        #region Getters
        private Collider[] m_allColliders = null;
        public Collider[] AllColliders
        {
            get
            {
                if (m_allColliders == null) m_allColliders = GetComponentsInChildren<Collider>();
                return m_allColliders;
            }
        }

        private bl_VehicleHealth m_vehicleHealth;
        public bl_VehicleHealth VehicleHealth
        {
            get
            {
                if (m_vehicleHealth == null) m_vehicleHealth = GetComponent<bl_VehicleHealth>();
                return m_vehicleHealth;
            }
        }

        private bl_VehicleSeats m_vehicleSeats;
        public bl_VehicleSeats VehicleSeats
        {
            get
            {
                if (m_vehicleSeats == null) m_vehicleSeats = GetComponent<bl_VehicleSeats>();
                return m_vehicleSeats;
            }
        }

        private bl_VehicleTriggersManager m_vehicleTriggerManager;
        public bl_VehicleTriggersManager TriggerManager
        {
            get
            {
                if (m_vehicleTriggerManager == null) m_vehicleTriggerManager = GetComponentInChildren<bl_VehicleTriggersManager>();
                return m_vehicleTriggerManager;
            }
        }
        #endregion
    }
}