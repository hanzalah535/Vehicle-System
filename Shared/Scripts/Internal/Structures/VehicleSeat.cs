using System;
using System.Collections.Generic;
using UnityEngine;
using MFPS.Runtime.Vehicles;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MFPS.Internal.Vehicles
{
    [Serializable]
    public class VehicleSeat
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 ExitPoint;
        [LovattoToogle] public bool PlayerVisible = true;
        [LovattoToogle] public bool PlayerCanShoot = true;
        [LovattoToogle] public bool IsDriver = false;
        public Vector2 FPViewClamp = new Vector2(-60, 80);
        [NonSerialized] public bool isUtilized = false;
        [NonSerialized] public bl_PlayerVehicle PlayerInSeat;
        public int PlayerInSeatActorNumber { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public void SitPlayer(bl_PlayerVehicle player, bool remote = false)
        {
            PlayerInSeat = player;
            if (PlayerInSeat != null) PlayerInSeatActorNumber = player.PlayerReferences.ActorNumber;

            if (PlayerCanShoot && !remote && !IsDriver && !bl_VehicleSettings.IsThirdPerson())
                player.transform.localPosition = Position + new Vector3(Rotation.y > 160 ? 0.165f : -0.165f, -0.34f, Rotation.y > 160 ? 0.145f : -0.145f);
            else
                player.transform.localPosition = Position;

            player.transform.localEulerAngles = Rotation;
            isUtilized = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetPlayerOut(Transform player)
        {
            if (player != null)
            {
                player.position = GetExitPoint(player.parent);
                player.parent = null;
            }
            PlayerInSeat = null;
            PlayerInSeatActorNumber = -1;
            isUtilized = false;
        }

        /// <summary>
        /// 
        /// </summary>
        public Vector3 GetExitPoint(Transform reference)
        {
            if (reference == null) return ExitPoint;
            return reference.TransformPoint(ExitPoint);
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsPlayerInSeat(int actorID)
        {
            if (actorID == -1) return false;
            return PlayerInSeatActorNumber == actorID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsSeatOccuped()
        {
            return PlayerInSeat != null;
        }

        public void DrawGizmo(Transform vehicle, Color seatColor)
        {
#if UNITY_EDITOR
            Gizmos.color = seatColor;
            Vector3 offset = new Vector3(Rotation.y > 160 ? 0.16f : -0.16f, 0.57f, Rotation.y > 160 ? 0.18f : -0.18f);
            Vector3 pos = Position + offset;
            pos = vehicle.TransformPoint(pos);

            var relativeRotation = Quaternion.Inverse(Quaternion.Euler(Rotation)) * vehicle.rotation;
            var rotOffset = Quaternion.Euler(new Vector3(-90, 0, 0));
            relativeRotation *= rotOffset;
            Gizmos.DrawMesh(bl_VehicleSettings.Instance.playerSitMesh, pos, relativeRotation);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, vehicle.TransformPoint(ExitPoint));
            Gizmos.DrawSphere(vehicle.TransformPoint(ExitPoint), 0.1f);
            Gizmos.color = Color.white;
#endif
        }

        public void DrawViewArc(Transform vehicle)
        {
#if UNITY_EDITOR
            if (IsDriver) return;

            Vector3 offset = new Vector3(Rotation.y > 160 ? 0.16f : -0.16f, 0.57f, Rotation.y > 160 ? 0.18f : -0.18f); Vector3 pos = Position + offset;
            pos = vehicle.TransformPoint(pos);
            Handles.color = new Color(1, 1, 1, 0.3f);

            var rot = Rotation;
            rot.y = -FPViewClamp.x;
            var startRot = Quaternion.Inverse(Quaternion.Euler(rot)) * vehicle.rotation;
            var fwd = startRot * Vector3.forward;

            float phi = Mathf.Abs(FPViewClamp.y - FPViewClamp.x) % 360;       // This is either the distance or 360 - distance
            float angle = phi > 180 ? 360 - phi : phi;

            Handles.DrawSolidArc(pos + new Vector3(0, 0.83f, 0), vehicle.up, fwd, angle, 0.5f);
#endif
        }
    }
}