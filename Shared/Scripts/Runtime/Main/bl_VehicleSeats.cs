using MFPS.Internal.Vehicles;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleSeats : bl_MonoBehaviour
    {
        public List<VehicleSeat> seats = new List<VehicleSeat>();

        private int seatTrigger = -1;
        private bool wasThirdPerson = false;
        public int CurrentSeatID { get; set; }

        /// <summary>
        /// 
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            bl_EventHandler.onLocalPlayerDeath += OnLocalPlayerDie;
            bl_PhotonCallbacks.PlayerEnteredRoom += OnPhotonPlayerConnected;
            bl_PhotonCallbacks.PlayerLeftRoom += OnPhotonPlayerDisconnected;
            bl_EventHandler.onRemotePlayerDeath += OnRemotePlayerDie;
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            bl_EventHandler.onLocalPlayerDeath -= OnLocalPlayerDie;
            bl_PhotonCallbacks.PlayerEnteredRoom -= OnPhotonPlayerConnected;
            bl_PhotonCallbacks.PlayerLeftRoom -= OnPhotonPlayerDisconnected;
            bl_EventHandler.onRemotePlayerDeath -= OnRemotePlayerDie;

        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnUpdate()
        {
            InputControl();
        }

        /// <summary>
        /// 
        /// </summary>
        void InputControl()
        {
            if (seatTrigger != -1)
            {
                if (bl_GameInput.Interact())
                {
                    LocalEnterInSeat(seatTrigger);
                }
            }
            else if (VehicleManager.localVehicleState == LocalVehicleState.PlayerInPassenger)
            {
                if (bl_GameInput.Interact())
                {
                    LocalExitSeat();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void IntentToEnterInSeat()
        {
            if (seatTrigger != -1)
            {
               LocalEnterInSeat(seatTrigger);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void IntentToExitSeat()
        {
            if (VehicleManager.localVehicleState == LocalVehicleState.PlayerInPassenger)
            {
                LocalExitSeat();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void LocalEnterInSeat(int seatID)
        {
            if (VehicleManager.localVehicleState == LocalVehicleState.PlayerIn || VehicleManager.localVehicleState == LocalVehicleState.PlayerInPassenger)
                return;

            CurrentSeatID = seatID;
            CurrentSeat.isUtilized = true;
            seatTrigger = -1;
            VehicleManager.CurrentTrigger = null;
            VehicleManager.TriggerManager.SetActive(false);

            VehicleManager.localVehicleState = LocalVehicleState.PlayerInPassenger;

#if MFPSTPV
            // if the player in third person mode and force to first person mode for passengers is enabled
            if (bl_VehicleSettings.IsThirdPerson() && bl_VehicleSettings.Instance.forceFPModeForPassengers)
            {
                wasThirdPerson = true;
                var cameraView = bl_MFPS.LocalPlayerReferences.GetComponent<bl_PlayerCameraSwitcher>();
                cameraView.SetViewMode(ThirdPerson.MPlayerViewMode.FirstPerson);
            }
#endif

#if MFPS_VEHICLE
            bl_MFPS.LocalPlayerReferences.PlayerVehicle.LocalEnterInVehicle(VehicleManager, CurrentSeat);
#endif
            if (!CurrentSeat.PlayerCanShoot || bl_VehicleSettings.IsThirdPerson())
            {
                bl_VehicleCamera.Instance.SetCarTarget(transform, bl_MFPS.LocalPlayerReferences.PlayerCameraTransform);
            }

            bl_VehicleEvents.DispatchLocalEnterInVehicle(VehicleManager);
            SyncSeatState(true);
        }

        /// <summary>
        /// 
        /// </summary>
        public void LocalExitSeat()
        {
            VehicleManager.localVehicleState = LocalVehicleState.Idle;
#if MFPS_VEHICLE
            bl_MFPS.LocalPlayerReferences.PlayerVehicle.LocalExitVehicle(VehicleManager, CurrentSeat);
#endif

            if (wasThirdPerson && bl_MFPS.LocalPlayerReferences != null)
            {
#if MFPSTPV
                wasThirdPerson = false;
                var cameraView = bl_MFPS.LocalPlayerReferences.GetComponent<bl_PlayerCameraSwitcher>();
                cameraView.SetViewMode(ThirdPerson.MPlayerViewMode.ThirdPerson);
#endif
            }
            
            CurrentSeat.isUtilized = false;
            bl_VehicleCamera.Instance.Disable();
            bl_VehicleEvents.DispatchLocalExitVehicle(VehicleManager);
            SyncSeatState(false);
            VehicleManager.TriggerManager.SetActive(true);
            CurrentSeatID = -1;
        }

        #region Network
        void SyncSeatState(bool enter)
        {
            var data = bl_UtilityHelper.CreatePhotonHashTable();
            data.Add("seatID", CurrentSeatID);
            data.Add("enter", enter);
            data.Add("viewID", bl_MFPS.LocalPlayer.ViewID);
            photonView.RPC("VehicleRpc", RpcTarget.Others, VehicleCall.SeatChange, data);
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnSeatStateChange(Hashtable data)
        {
            var seat = seats[(int)data["seatID"]];
            seat.isUtilized = (bool)data["enter"];
            int viewID = (int)data["viewID"];

            //Find the client who send this call in our side.
            var remoteDriver = bl_GameManager.Instance.GetMFPSActor(viewID);
            if (remoteDriver == null) { Debug.LogWarning($"Actor {viewID} is not listed."); return; }
            if (remoteDriver.Actor == null) { Debug.LogWarning($"Actor {viewID} could not be found in the scene."); return; }

#if MFPS_VEHICLE
            //Manage the remote player 
            var playerRef = remoteDriver.Actor.GetComponent<bl_PlayerReferences>();
            if (seat.isUtilized) playerRef.PlayerVehicle.RemoteEnterInVehicle(VehicleManager, seat);
            else
            {
                playerRef.PlayerVehicle.RemoteExitVehicle(VehicleManager, seat);
            }
#endif
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void OnLocalTrigger(bool playerIn, int seatID, bl_VehicleSeatTrigger trigger)
        {
            if (playerIn)
            {
                if (seats[seatID].isUtilized || IsOnDriverTrigger()) return;

                seatTrigger = seatID;
                VehicleManager.CurrentTrigger = trigger;
                bl_VehicleUI.Instance.ShowEnterUI(true, VehicleManager, seatTrigger);
            }
            else
            {
                if (VehicleManager.CurrentTrigger != trigger) return;
                seatTrigger = -1;
                VehicleManager.CurrentTrigger = null;
                bl_VehicleUI.Instance.ShowEnterUI(false, null, seatTrigger);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void OnLocalPlayerDie()
        {
            if (VehicleManager.localVehicleState == LocalVehicleState.PlayerInPassenger)
            {              
                LocalExitSeat();
                VehicleManager.CurrentTrigger = null;
            }
            seatTrigger = -1;
        }

        /// <summary>
        /// 
        /// </summary>
        void OnRemotePlayerDie(MFPSPlayer player)
        {
            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                if (!seat.IsPlayerInSeat(player.ActorNumber)) continue;
                //free up the seat
                seat.GetPlayerOut(player.Actor);
                break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnPhotonPlayerDisconnected(Player otherPlayer)
        {
            //if the player how left was seat in one of the seats
            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                if (!seat.IsPlayerInSeat(otherPlayer.ActorNumber)) continue;
                //free up the seat
                seat.GetPlayerOut(null);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnPhotonPlayerConnected(Player newPlayer)
        {
            if (bl_PhotonNetwork.IsMasterClient)
            {
                for (int i = 0; i < seats.Count; i++)
                {
                    var seat = seats[i];
                    if (!seat.IsSeatOccuped()) continue;

                    var data = bl_UtilityHelper.CreatePhotonHashTable();
                    data.Add("seatID", i);
                    data.Add("enter", true);
                    data.Add("viewID", seat.PlayerInSeat.PlayerReferences.ViewID);
                    photonView.RPC("VehicleRpc", newPlayer, VehicleCall.SeatChange, data);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public VehicleSeat CurrentSeat
        {
            get
            {
                if (CurrentSeatID == -1) return null;
                return seats[CurrentSeatID];
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool IsOnDriverTrigger()
        {
            if (VehicleManager.CurrentTrigger == null) return false;
            return VehicleManager.CurrentTrigger.seatTarget == bl_VehicleSeatTrigger.TriggerTarget.Driver;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            for (int i = 0; i < seats.Count; i++)
            {
                seats[i].DrawGizmo(transform, new Color(0, 0, 1, 0.54f));
            }
        }

        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < seats.Count; i++)
            {
                seats[i].DrawViewArc(transform);
            }
        }
#endif

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