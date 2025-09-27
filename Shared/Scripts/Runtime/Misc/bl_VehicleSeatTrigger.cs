using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MFPS.Runtime.Vehicles
{
    public class bl_VehicleSeatTrigger : MonoBehaviour
    {
        public TriggerTarget seatTarget = TriggerTarget.Passenger;
        public int seatID = 0;
        [LovattoToogle] public bool rayDetection = false;
        public bl_VehicleTriggersManager triggerManager;
        private byte objectID = 0;
        private bool isCameraOver = false;
        private bool nameSetup = false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        private void OnTriggerEnter(Collider other)
        {
            if (!other.isLocalPlayerCollider()) return;

            if(seatTarget == TriggerTarget.Area)
            {
                triggerManager.SetActive(true, true);
                return;
            }
            SetTrigger(true);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnTriggerExit(Collider other)
        {
            if (!other.isLocalPlayerCollider()) return;

            if (seatTarget == TriggerTarget.Area)
            {
                triggerManager.SetActive(false, true);
                return;
            }
            SetTrigger(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="enable"></param>
        void SetTrigger(bool enable)
        {
            if (seatTarget == TriggerTarget.Driver)
            {
                VehicleManager?.OnPlayerEnterInTrigger(enable, this);
            }
            else
            {
                VehicleManager.VehicleSeats?.OnLocalTrigger(enable, seatID, this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ActiveRayDetection(bool active)
        {
            if (!nameSetup)
            {
                // since the ray detection is cached by the collider name
                // we have to make sure the collider have an unique name, if you have multiple vehicles in the map
                // the child colliders will all have the same name which will cause the ray to only work with 1 vehicle
                // to mitigate this we are going to rename this collider with the vehicle root name WHICH SHOULD BE UNIQUE IN THE HIERARCHY
                if (seatTarget == TriggerTarget.Passenger)
                {
                    gameObject.name = $"{transform.root.name} ({VehicleManager.TriggerIDCounter})";
                }
                else
                {
                    gameObject.name = $"{transform.root.name}";
                }
                nameSetup = true;
            }

            if (active)
            {
                objectID = bl_MFPS.LocalPlayer.AddCameraRayDetection(new bl_CameraRayBase.DetecableInfo()
                {
                    Name = gameObject.name,
                    ID = objectID,
                    Callback = OnCameraRay
                });
            }
            else
            {
                bl_MFPS.LocalPlayer.RemoveCameraDetection(new bl_CameraRayBase.DetecableInfo()
                {
                    Name = gameObject.name,
                    ID = objectID,
                });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void OnCameraRay(bool enter)
        {
            isCameraOver = enter;
            SetTrigger(enter);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetActive(bool active, bool fromArea)
        {
            if (rayDetection)
            {
                ActiveRayDetection(active);
            }
            if(fromArea && !active && isCameraOver)
            {
                SetTrigger(false);
                isCameraOver = false;
            }
            gameObject.SetActive(active);
        }

        private bl_VehicleManager m_vehicleManager;
        private bl_VehicleManager VehicleManager
        {
            get
            {
                if (m_vehicleManager == null) m_vehicleManager = GetComponentInParent<bl_VehicleManager>();
                return m_vehicleManager;
            }
        }

        [Serializable]
        public enum TriggerTarget
        {
            Driver,
            Passenger,
            Area,
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(bl_VehicleSeatTrigger))]
    public class bl_VehicleSeatTriggerEditor : Editor
    {
        private bl_VehicleSeatTrigger script;
        bl_VehicleManager manager;

        private void OnEnable()
        {
            script = (bl_VehicleSeatTrigger)target;
            manager = script.transform.GetComponentInParent<bl_VehicleManager>();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            script.seatTarget = (bl_VehicleSeatTrigger.TriggerTarget)EditorGUILayout.EnumPopup("Seat Target", script.seatTarget);
            if(script.seatTarget == bl_VehicleSeatTrigger.TriggerTarget.Passenger)
            {
                script.seatID = EditorGUILayout.IntField("Seat ID", script.seatID);
            }
            if(script.seatTarget != bl_VehicleSeatTrigger.TriggerTarget.Area)
            {
                script.rayDetection = EditorGUILayout.ToggleLeft("Detect By Ray", script.rayDetection, EditorStyles.toolbarButton);
            }
            else
            {
                script.triggerManager = (bl_VehicleTriggersManager)EditorGUILayout.ObjectField("Trigger Manager", script.triggerManager, typeof(bl_VehicleTriggersManager), true);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

        }
    }
#endif
}