using System.Collections.Generic;
using UnityEngine;
using MFPSEditor;
using UnityEditor;

public class MFPSVehicleDoc : TutorialWizard
{
    //required//////////////////////////////////////////////////////
    private const string ImagesFolder = "mfps2/editor/vehicle/";
    private NetworkImages[] m_ServerImages = new NetworkImages[]
    {
        new NetworkImages{Name = "img-0.png", Image = null},
        new NetworkImages{Name = "img-1.png", Image = null},
        new NetworkImages{Name = "img-2.png", Image = null},
        new NetworkImages{Name = "img-3.png", Image = null},
        new NetworkImages{Name = "img-4.png", Image = null},
        new NetworkImages{Name = "img-5.png", Image = null},
        new NetworkImages{Name = "img-6.png", Image = null},
        new NetworkImages{Name = "img-7.png", Image = null},
    };
    private readonly GifData[] AnimatedImages = new GifData[]
    {
        new GifData{ Path = "none.gif" },

    };
    private Steps[] AllSteps = new Steps[] {
     new Steps { Name = "Get Started", StepsLenght = 0, DrawFunctionName = nameof(GetStartedDoc) },
     new Steps { Name = "Add Vehicle", StepsLenght = 3, DrawFunctionName = nameof(AddVehicleDoc) },
     new Steps { Name = "Seat", StepsLenght = 0, DrawFunctionName = nameof(SeatDoc) },
     new Steps { Name = "Driver", StepsLenght = 0, DrawFunctionName = nameof(DriverDoc) },
     new Steps { Name = "Passengers", StepsLenght = 0, DrawFunctionName = nameof(PassengerDoc) },
     new Steps { Name = "Colliders", StepsLenght = 0, DrawFunctionName = nameof(CollidersDoc) },
     new Steps { Name = "Doors", StepsLenght = 0, DrawFunctionName = nameof(DoorsDoc) },
    };

    public override void WindowArea(int window)
    {
        AutoDrawWindows();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        base.Initizalized(m_ServerImages, AllSteps, ImagesFolder, AnimatedImages);
        GUISkin gs = Resources.Load<GUISkin>("content/MFPSEditorSkin") as GUISkin;
        if (gs != null)
        {
            base.SetTextStyle(gs.customStyles[2]);
            base.m_GUISkin = gs;
        }
        FetchWebTutorials("mfps2/tutorials/vehicle/");
    }
    //final required////////////////////////////////////////////////

    void GetStartedDoc()
    {
        DrawText("<b>REQUIRE</b>\n\n■ MFPS 1.9++\n■ Unity 2019.4++\n\n<b>INTEGRATION</b>\n\n■ To integrate, follow the integration wizard <i>(click the button below)</i>"); Space(10);
        if (DrawButton("Open integration wizard"))
        {
            GetWindow<VehicleAddonIntegration>();
        }
    }

    void AddVehicleDoc()
    {
        if (subStep == 0)
        {
            DrawText("You may want to add multiple vehicle types/models in your game, that of course is possible to do, basically, all that you have to do is <b>use one of the current vehicle prefabs ➔ change the vehicle model it ➔ adjusts any of the vehicle property to your needs ➔ create a prefab of it ➔ done.</b>\n\nSo, here go, step 1:");
            DrawTitleText("Use a vehicle prefab");
            DrawText("<i><color=#76767694>For make things more clear, open a new empty scene in the editor.</color></i>\n\nIf this is the first time adding a car you can use the default Car prefab which is located in: <i>Assets➔Addons➔Vehicles➔Cars➔Prefabs➔<b>Car</b></i>");
            if (Buttons.FlowButton("Ping car prefab"))
            {
                var carPrefab = AssetDatabase.LoadAssetAtPath("Assets/Addons/Vehicles/Cars/Prefabs/Car.prefab", typeof(GameObject)) as GameObject;
                Selection.activeGameObject = carPrefab;
                EditorGUIUtility.PingObject(carPrefab);
            }
            DownArrow();
            DrawText("■ Drag the Car prefab and drop it in your scene hierarchy.\n\n■ Select the instanced car prefab in the hierarchy window ➔ right mouse click over it ➔ (Click) <b>Unpack prefab completely</b>.");
            Space(10);
        }
        else if (subStep == 1)
        {
            DrawTitleText("Change Model");
            DrawText("■ Drag the Car prefab and drop it in your scene hierarchy.\n\n■ Select the instanced car prefab in the hierarchy window ➔ right mouse click over it ➔ (Click) <b>Unpack prefab completely</b>.\n\n■ Then, drag you new car model into the car prefab that you just instance in the hierarchy, put the vehicle model under the <b>Model</b> child object of the Car prefab.\n\n■ Positioned your new model using the current car model as reference of position.");
            DrawServerImage(0);
            DrawText("■ Once you positioned the new model correctly you can <b>DISABLE</b> the old vehicle model for the moment.");
            DownArrow();
            DrawText("Now you need to set up the new vehicle wheels.\n \nInside the Car prefab instance, you will see an object called \"<b>Wheels</b>\", this object contains the <b>WheelColliders</b>. What you have to do with those is align the position with your new car wheel meshes manually, use their name to identify in which wheel you should align each one of them <i>(Left Front = Left Front Wheel)</i>.\n \nSo select these <b>WheelColliders</b> in the hierarchy and align the four of them manually.");
            DrawServerImage(1);
            DownArrow();
            DrawText("The next step is to list the new wheel meshes in the script<b> bl_CarController.cs ➔ Wheel Meshes</b> <i>(on the inspector)</i>, this script is attached to the root of the car prefab instance.\n \n<b>The order in which the wheel has to be assigned in the list matter</b>, this is the order in which you have to assign them:\n \nElement 0 = Left Front Wheel\nElement 1 = Right Front Wheel\nElement 2 = Left Back Wheel\nElement 3 = Right Back Wheel");
            DrawServerImage(2);
        }
        else if (subStep == 2)
        {
            DrawText("Finally, you simply need to modify the vehicle properties to your needs, like the speed, traction, engine power, etc...\n \nYou can do this by modifying the values of the scripts attached in the Car prefab instance <i>(bl_CarControl.cs, bl_VehicleManager.cs, etc...)</i>\n \nAlso, you can add, remove or modify the vehicle driver and passenger seats, for it checks the respective section in this document.\n\nOnce you finish with your modifications, simply create a new prefab of this vehicle so you can instance it in your game maps scenes.");
        }
    }

    void SeatDoc()
    {
        DrawText("The <b>Seats</b> in the vehicle system define a slot where a player can enter/exit the vehicle, you can have as many passenger seats as you want and 1 driver seat.\n \nYou can customize some properties like the seat position, rotation, view angle, define if the player should be visible inside of the seat , if the player can shoot, etc...\n \nTo add, remove or modify a seat, check the respective section in this document.");
        DrawText("Here you have a description of the properties of the Vehicle Seat Class:");

        Space(10);
        DrawPropertieInfo("Position", "Vector3", "The position of where the player will be placed when enter in this seat, the position is relative to the vehicle, you can preview it with the blue or green seated player gizmo.");
        DrawPropertieInfo("Rotation", "Vector3", "The rotation of the player when enter in this seat, the rotation is relative to the vehicle, you can preview it with the blue or green seated player gizmo.");
        DrawPropertieInfo("ExitPoint", "Vector3", "The position where the player will be moved to when exit the vehicle from this seat, you can preview it with the Red sphere gizmo that has a line starting from the seat position.");
        DrawPropertieInfo("Player Visible", "bool", "Define if the player model will be visible when enter in this seat.");
        DrawPropertieInfo("Player Can Shoot", "bool", "Define if the player can shoot when is in this seat <i>(not work on 3rd view)</i>");
        DrawPropertieInfo("Is Driver", "bool", "Define if this seat is for the Driver of the vehicle.");
        DrawPropertieInfo("FP View Clamp", "Vector2", "For passenger seats only, define the horizontal rotation where the player can look when is in this seat, you can preview the view angle with the white arc gizmo in the head of the seated player gizmo.");
    }

    void DriverDoc()
    {
        DrawText("The driver seat is configurable independently from all the other vehicle seats.\n \nTo modify the driver seat values like position, rotation, exit position, etc... you can do so in <b>bl_VehicleManager.cs ➔ Driver Seat ➔ *</b>\n \nThe script <b>bl_VehicleManager.cs</b> is attached in the root of the Car prefab instance.\n \nIn the Scene view, you can preview the driver seat position with the <b>Green seated player</b>.");
        DrawServerImage(3);
        DownArrow();
        DrawText("Additionally, for the driver seat, you also can modify the steer wheel position, that function as the IK goal for the player arms when the player is in the driver seat.\n \nYou can modify the position by moving the Steer Wheel game object which is located inside the car prefab instance <b>Car prefab ➔ Wheels ➔ Steer Wheel.</b>\n \nSelect this object and positioned in wherever the steering wheel is located in your vehicle model, you can also modify the space between the arms in the steering wheel in <b>bl_VehicleManager.cs ➔ Steer Wheel ➔ Steer Hand Space.</b>");
        DrawServerImage(4);
    }

    void PassengerDoc()
    {
        DrawText("The <b>Passenger</b> seats are slots that can be used by any driver-team mate to enter/exit from the vehicle, you can set up as many passenger seats as you want.\n \nFor Add, Remove, or Modify any passenger seat you simply need to Add, Remove or Modify the seat info in the <b>Seats</b> list of <b>bl_VehiclePassenger.cs</b>, this script is attached in the root of the vehicle prefab instance.\n \nSo if you wanna add a new seat ➔ add a new field in the list, if you wanna remove one ➔ remove it from the list, if you wanna modify it ➔ fold out the seat info and make the pertinent changes.\n \nYou can preview the passenger seats in the Scene View with the <b>blue seated player gizmos</b>.");
        DrawServerImage(5);
    }

    void CollidersDoc()
    {
        DrawText("You will need to manually adjust your vehicle colliders because <b>you can't use Mesh Collider</b> with it, so you have to create a collider structure with the shape of your vehicle with only Box, Capsule, and Sphere Colliders.\n \nSince the default vehicle prefabs do already have some colliders, when you add a new vehicle model you should first adjust those default colliders, you can find these colliders inside the vehicle prefab instance in <i>*Vehicle Instance* ➔ Colliders ➔ *</i>, simply select the colliders ➔ positioned/rotate them to align them as accurately as you want to the vehicle shape. If the default colliders are not enough or you don't need all of them, you are free to add/remove as many as you want.\n \nTo add a new collider, simply create an empty game object ➔ add the Box, Capsule, or Sphere Collider ➔ adjust it's shaped ➔ assign the <b>Vehicle</b> Layer to it.");
        DrawServerImage(6);
    }

    void DoorsDoc()
    {
        DrawText("In this vehicle system, the local player detects the doors/triggers to enter a vehicle by <b>ray detection</b>, you can add as many doors as you want in the vehicle for the same or different seats.\n \nA seat door <i><size=8><color=#76767694>(for driver or passenger)</color></size></i> is just a collider <i><b><size=8><color=#76767694>(non-trigger)</color></size></b></i> with the <color=#33B75AFF>bl_VehicleSeatTrigger.cs</color> script attached, the body of the collider define where the player can detect that door.\n \nThe default vehicle prefabs do already have some doors colliders placed, so if you have replaced the vehicle model, you should first modify these doors (add or remove), you can find the default vehicle door inside of the vehicle prefab instance in: *Vehicle Instance* ➔ Colliders ➔ Seats Triggers ➔ *, so, select them ➔ placed wherever you wanna the door to be detected. When you finish positioned it, go to the inspector of it ➔ bl_VehicleSeatTrigger ➔ Setup the properties:");

        DrawPropertieInfo("Seat Target", "enum", "Is this the door for a Passenger seat or for the Driver Seat?");
        DrawPropertieInfo("Seat ID", "int" , "In the case where this is for a passenger seat, this is the index of the seat that this door is for (index from the Seats list in bl_VehiclePassengers)");
        DrawPropertieInfo("Ray Detection", "bool", "Should detect this with the player camera ray?");

        DrawServerImage(7);
    }

    [MenuItem("MFPS/Tutorials/Vehicles/Car")]
    private static void Open()
    {
        EditorWindow.GetWindow(typeof(MFPSVehicleDoc));
    }

    [MenuItem("MFPS/Addons/Vehicle/Car/Documentation")]
    private static void Open2()
    {
        EditorWindow.GetWindow(typeof(MFPSVehicleDoc));
    }
}