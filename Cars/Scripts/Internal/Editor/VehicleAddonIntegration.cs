#define CAR
using System.Collections.Generic;
using UnityEngine;
using MFPSEditor;
using MFPSEditor.Addons;
using UnityEditor;
using MFPS.Runtime.Vehicles;

public class VehicleAddonIntegration : AddonIntegrationWizard
{
    //REQUIRED
    private const string ADDON_NAME = "Vehicles";
    private const string ADDON_KEY = "MFPS_VEHICLE";

    string[] integrateGuids = new string[] { "c5d972e76543fcf45ae6270413916c18", "525620deaa3629c4b907cf0430ed9741" };
    private List<IntegrationScene> sceneList;

    /// <summary>
    /// 
    /// </summary>
    public override void OnEnable()
    {
        base.OnEnable();
        addonName = ADDON_NAME;
        addonKey = ADDON_KEY;
        allSteps = 4;

        MFPSAddonsInfo addonInfo = MFPSAddonsData.Instance.Addons.Find(x => x.KeyName == addonKey);
        Dictionary<string, string> info = new Dictionary<string, string>();
        if (addonInfo != null) { info = addonInfo.GetInfoInDictionary(); }
        Initializate(info);

#if MFPS_VEHICLE
        currentStep = 2;//skip the activation step.
#endif
    }
    //REQUIRED END

    public override void DrawWindow(int stepID)
    {
        if (stepID == 1)
        {
            DrawText("First, you have to <b>Enable</b> this addon, for it simply click on the button below:");
#if MFPS_VEHICLE
            DrawText("The addons is already enabled, continue with the next step.\n");
#else
            GUILayout.Space(10);
            using (new MFPSEditorStyles.CenteredScope())
            {
                DrawAddonActivationButton();
            }
#endif
        }
        else if (stepID == 2)
        {
            DrawText("<b>PLAYER SETUP</b>\n \nRun the player auto-setup, click on the button below to configure all the available player prefabs.");
            GUILayout.Space(10);
            using (new MFPSEditorStyles.CenteredScope())
            {
                if (DrawButton("Run player setup", GUILayout.Width(200), GUILayout.Height(20)))
                {
                    SetupAllPlayers();
                    NextStep();
                }
            }
        }
        else if (stepID == 3)
        {
            DrawText("Now you have to <b>integrate</b> the addon content in the required game scenes,\nClick on the button below to see the integration details of each scene:");
            GUILayout.Space(10);

            if (sceneList != null)
                DrawMFPSMaps(sceneList.ToArray(), true);

            GUILayout.Space(10);
            using (new MFPSEditorStyles.CenteredScope())
            {
                if (DrawButton("Refresh Scenes Status", GUILayout.Width(200), GUILayout.Height(20)))
                {
                    BuildScenes();
                }
            }
            GUILayout.Space(40);

            var canDoit = FindObjectOfType<bl_VehicleUI>() == null && bl_UIReferences.Instance != null;
            if (!canDoit)
            {
                DrawText("Can't integrate here, either is already integrated or this is not a map scene.");
            }
            using (new MFPSEditorStyles.CenteredScope())
            {
                GUI.enabled = canDoit;
                if (MFPSEditorStyles.ButtonOutline("INTEGRATE", BLUE_COLOR, GUILayout.Width(200), GUILayout.Height(30)))
                {
                    IntegrateInMap();
                    BuildScenes();
                }
                GUI.enabled = true;
            }
            GUILayout.FlexibleSpace();
            DrawText("Once you finish here, go to the next step.");
            GUILayout.Space(5);
            using (new MFPSEditorStyles.CenteredScope())
            {
                if (MFPSEditorStyles.ButtonOutline("Next Step", new Color(1,1,1,0.3f), GUILayout.Width(100), GUILayout.Height(20)))
                {
                    NextStep();
                }
            }
        }
        else if(stepID == 4)
        {
            DrawText("Now the integration is completed, there's just one step left.\n \nYou have to manually place the vehicles in your maps as you desire, you simply need to <b>drag and drop</b> the vehicle prefabs into your map scenes,\nbelow you will find the buttons to locate the available vehicle prefabs so you can drag them from the Project View to the Scene Hierarchy and that's!");
            GUILayout.Space(10);

#if CAR
            if (MFPSEditorStyles.ButtonOutline("Car", BLUE_COLOR, GUILayout.Width(150), GUILayout.Height(24)))
            {
                var carPrefab = AssetDatabase.LoadAssetAtPath("Assets/Addons/Vehicles/Cars/Prefabs/Car.prefab", typeof(GameObject)) as GameObject;
                Selection.activeGameObject = carPrefab;
                EditorGUIUtility.PingObject(carPrefab);
            }
#endif
        }
    }

    /// <summary>
    /// 
    /// </summary>
    void SetupAllPlayers()
    {
        SetupPlayer(bl_GameData.Instance.Player1.gameObject);
        SetupPlayer(bl_GameData.Instance.Player2.gameObject);
#if PSELECTOR
        foreach(var p in bl_PlayerSelector.Data.AllPlayers)
        {
            if (p.Prefab == null ) continue;
             SetupPlayer(p.Prefab);
        }
#endif
    }

    void SetupPlayer(GameObject player)
    {
        if (player == null) return;

        var script = player.GetComponent<bl_PlayerVehicle>();
        if (script != null) return;

        player.AddComponent<bl_PlayerVehicle>();
        EditorUtility.SetDirty(player);
    }


    /// <summary>
    /// 
    /// </summary>
    private void BuildScenes()
    {
        sceneList = new List<IntegrationScene>();

        var alls = bl_GameData.Instance.AllScenes;
        foreach (var mfpscene in alls)
        {
            var nscene = new IntegrationScene(mfpscene.m_Scene, mfpscene.ShowName);
            sceneList.Add(nscene);
            CheckIntegrationInScene(nscene, integrateGuids);
        }
        Repaint();
    }

    /// <summary>
    /// 
    /// </summary>
    private void IntegrateInMap()
    {
        if (bl_UIReferences.Instance == null) { Debug.Log("Can't integrate in this scene."); return; }

        var script = FindObjectOfType<bl_VehicleUI>();
        if (script != null) { Debug.Log($"{ADDON_NAME} has been already integrated in this scene!"); return; }


        var instance = InstancePrefab("Assets/Addons/Vehicles/Shared/Prefabs/Level/Vehicles.prefab");
        if (instance == null) return;

        EditorUtility.SetDirty(instance);
        ShowSuccessIntegrationLog(instance);
        MarkSceneDirty();
        Repaint();
    }

#if !MFPS_VEHICLE
    [MenuItem("MFPS/Addons/Vehicle/Car/Enable")]
    private static void Enable()
    {
        EditorUtils.SetEnabled(ADDON_KEY, true);
    }
#endif

#if MFPS_VEHICLE
    [MenuItem("MFPS/Addons/Vehicle/Car/Disable")]
    private static void Disable()
    {
        EditorUtils.SetEnabled(ADDON_KEY, false);
    }
#endif

    public MFPSSceneType GetMFPSSceneType()
    {
        if (bl_Lobby.Instance != null) return MFPSSceneType.Lobby;
        if (bl_GameManager.Instance != null) return MFPSSceneType.Map;
        return MFPSSceneType.Other;
    }

    public enum MFPSSceneType
    {
        Lobby,
        Map,
        Other,
    }

    [MenuItem("MFPS/Addons/Vehicle/Car/Integrate")]
    static void Open()
    {
        GetWindow<VehicleAddonIntegration>();
    }
}