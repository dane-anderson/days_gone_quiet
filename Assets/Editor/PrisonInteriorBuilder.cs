#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Adds a real cellblock to the existing yard without rebuilding the yard or its agents.</summary>
public static class PrisonInteriorBuilder
{
    public const string RootName = "Prison Interior Extension";
    private const string Art = "Assets/Art/Environment/PrisonInterior";
    private static Material concrete, floor, green, iron, rust, wood, cloth, paper, black, glow, warm;
    private static readonly List<Renderer> cutaway = new List<Renderer>();

    [MenuItem("Days Gone Quiet/Build Connected Prison Interior", priority = 20)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before building the prison interior.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/PrisonYard.unity")
            throw new InvalidOperationException("Open PrisonYard before adding its interior.");
        GameObject environment = GameObject.Find("Prison Yard Environment");
        RickAgent rick = Object.FindAnyObjectByType<RickAgent>();
        WalkerChase walker = Object.FindAnyObjectByType<WalkerChase>();
        Camera camera = Camera.main;
        if (environment == null || rick == null || walker == null || camera == null)
            throw new InvalidOperationException("The existing yard, Rick, Walker, and camera are required.");

        // A rebuild changes only this extension and explicitly named pieces of the old solid facade.
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Connected Prison Interior");
        GameObject previous = GameObject.Find(RootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        Folder(Art); Folder(Art + "/Materials");
        Materials(); cutaway.Clear();
        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create prison interior");

        ConvertOriginalFacade(environment.transform);
        Transform shell = Group("Cellblock Architecture", root.transform);
        Shell(shell);
        Transform cells = Group("Twelve Cells - Two Levels", root.transform);
        for (int level = 0; level < 2; level++)
            for (int bay = 0; bay < 6; bay++)
                Cell(cells, bay, level);
        Galleries(Group("Upper Walkway and Stairs", root.transform));
        Checkpoint(Group("Yard Entrance and Guard Checkpoint", root.transform));
        CommonRoom(Group("Survivor Common Room", root.transform));
        DangerWing(Group("Chained West Wing", root.transform));
        Dress(Group("Pipes Signs and Wear", root.transform));
        RestrainedWalkersBuilder.Build(root.transform);

        PrisonYardCameraViews views = camera.GetComponent<PrisonYardCameraViews>();
        if (views == null) views = Undo.AddComponent<PrisonYardCameraViews>(camera.gameObject);
        Transform spawn = Group("Tour Start - Yard Entrance", root.transform);
        spawn.position = new Vector3(0f, 1.1f, 21.5f);
        PrisonInteriorTour tour = root.AddComponent<PrisonInteriorTour>();
        tour.rick = rick; tour.walker = walker; tour.views = views;
        tour.exteriorRenderers = cutaway.Where(r => r != null).Distinct().ToArray();
        tour.interiorColliders = root.GetComponentsInChildren<Collider>(true);
        tour.tourSpawn = spawn;
        Transform yardBounds = Group("Tour Only Yard Boundaries", root.transform);
        InvisibleWall("West Fence Collision", V(-24.9f, 2.5f, 0), V(.2f, 5, 50), yardBounds);
        InvisibleWall("East Fence Collision", V(24.9f, 2.5f, 0), V(.2f, 5, 50), yardBounds);
        InvisibleWall("South Fence Collision", V(0, 2.5f, -24.9f), V(50, 5, .2f), yardBounds);
        InvisibleWall("North West Fence Collision", V(-14.9f, 2.5f, 24.9f), V(20, 5, .2f), yardBounds);
        InvisibleWall("North East Fence Collision", V(14.9f, 2.5f, 24.9f), V(20, 5, .2f), yardBounds);
        tour.tourBoundaryColliders = yardBounds.GetComponentsInChildren<Collider>();
        foreach (Collider boundary in tour.tourBoundaryColliders) boundary.enabled = false;
        Undo.RecordObjects(new Object[] { views, camera, camera.transform }, "Show cellblock");
        PrisonRoofUpgrade.ApplyTo(tour);
        views.ShowCellblock();
        EditorUtility.SetDirty(tour);
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("PRISON INTERIOR BUILT: 12 cells, upper gallery, walkable stairs, checkpoint, survivor room, sealed wing, and 4 restrained walkers. Press 6 for the cellblock, 7 for the chained walkers, and T in Play mode for a manual tour. Rick's saved learning settings are unchanged.");
    }

    private static void ConvertOriginalFacade(Transform environment)
    {
        Transform block = environment.Find("Grayhaven Cell Block");
        string[] replaced = { "Main Cell Block", "Roof Slab", "Entrance Projection", "Left Steel Door", "Right Steel Door", "Concrete Barrier 04" };
        foreach (string name in replaced)
        {
            Transform item = block != null ? block.Find(name) : null;
            if (item == null) continue;
            Undo.RecordObject(item.gameObject, "Open prison shell");
            item.gameObject.SetActive(false);
        }
        // Open two central fence bays into a fenced pedestrian approach. Existing meshes stay recoverable.
        Transform fence = environment.Find("Perimeter Fence/North Fence");
        if (fence != null)
            foreach (Transform item in fence)
            {
                bool centerBay = Mathf.Abs(item.position.x) < 4.9f;
                if (!centerBay) continue;
                Undo.RecordObject(item.gameObject, "Open north entrance");
                item.gameObject.SetActive(false);
            }
        // These decorative facade elements would otherwise cover the cutaway view.
        foreach (Renderer renderer in environment.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.gameObject.activeInHierarchy) continue;
            Vector3 p = renderer.transform.position;
            if (p.z >= 25.3f && p.z <= 35.1f && Mathf.Abs(p.x) < 18.2f)
                cutaway.Add(renderer);
        }
    }

    private static void Shell(Transform p)
    {
        Box("Cellblock Concrete Floor", V(0, -.17f, 38), V(34, .34f, 20), floor, p, true);
        Box("Entrance Apron", V(0, -.1f, 26.5f), V(10, .2f, 5), floor, p, true);
        Box("West Wall", V(-16.9f, 4.5f, 38), V(.4f, 9, 20), concrete, p, true);
        Box("East Wall", V(16.9f, 4.5f, 38), V(.4f, 9, 20), concrete, p, true);
        Box("Rear Wall", V(0, 4.5f, 47.8f), V(34, 9, .4f), concrete, p, true);
        Box("West Painted Dado", V(-16.67f, 1, 38), V(.025f, 2, 19.5f), green, p);
        Box("East Painted Dado", V(16.67f, 1, 38), V(.025f, 2, 19.5f), green, p);
        Box("Rear Painted Dado", V(0, 1, 47.55f), V(33.4f, 2, .025f), green, p);
        Exterior(Box("Front West Wall", V(-10, 4.5f, 28.55f), V(14, 9, .4f), concrete, p, true));
        Exterior(Box("Front East Wall", V(10, 4.5f, 28.55f), V(14, 9, .4f), concrete, p, true));
        Exterior(Box("Front Door Lintel", V(0, 6.4f, 28.55f), V(6, 5.2f, .4f), concrete, p, true));
        Exterior(Box("Deep Roof Slab", V(0, 9.16f, 38), V(35, .3f, 20.5f), concrete, p, true));
        GameObject shadowRoof = Box("Ceiling Shadow Caster", V(0, 9.16f, 38), V(35, .3f, 20.5f), concrete, p);
        shadowRoof.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        // High windows sit above the cells and carry daylight into the cutaway.
        for (int i = 0; i < 6; i++)
        {
            float x = -12.5f + i * 5;
            Box("High Window Recess", V(x, 8.15f, 47.53f), V(2.8f, 1.15f, .05f), black, p);
            Box("Clouded Window Glass", V(x, 8.15f, 47.49f), V(2.62f, 1, .025f), glow, p);
            for (int bar = -2; bar <= 2; bar++)
                Box("High Window Bar", V(x + bar * .48f, 8.15f, 47.44f), V(.04f, 1.1f, .06f), iron, p);
        }
        for (int i = 0; i < 4; i++)
        {
            float x = -12 + i * 8;
            Box("Overhead Cross Beam", V(x, 8.6f, 38), V(.25f, .38f, 19), iron, p);
            Fixture("Cold Fluorescent", V(x, 7.9f, 37), p, new Color(.68f, .83f, .78f), 13, 5f);
        }
        LightFill("Cellblock Daylight", V(0, 7.5f, 42), new Color(.64f, .76f, .82f), 4f, 24, p);
    }

    private static void Cell(Transform parent, int bay, int level)
    {
        float x = -12.5f + bay * 5;
        float y = level * 4.2f;
        string number = (level == 0 ? "1" : "2") + (bay + 1).ToString("00");
        Transform p = Group("Cell " + number, parent);
        if (level == 1) Box("Cell Floor", V(x, y - .12f, 44.6f), V(5, .24f, 5.9f), floor, p, true);
        if (bay == 0) Box("West Partition", V(-15, y + 1.85f, 44.6f), V(.18f, 3.7f, 5.9f), concrete, p, true);
        Box("East Partition", V(x + 2.5f, y + 1.85f, 44.6f), V(.18f, 3.7f, 5.9f), concrete, p, true);
        Box("Painted Rear Cell Wall", V(x, y + 1, 47.52f), V(4.8f, 2, .025f), green, p);
        Box("Barred Front Header", V(x, y + 3.45f, 41.65f), V(4.9f, .22f, .18f), iron, p);
        // Each cell has a 1.4m open door at its right; fixed bars occupy only the left portion.
        for (int b = 0; b < 11; b++)
            Box("Fixed Vertical Bar", V(x - 2.3f + b * .28f, y + 1.7f, 41.65f), V(.045f, 3.35f, .055f), iron, p);
        Box("Front Cross Rail", V(x - .9f, y + 1.1f, 41.65f), V(2.95f, .075f, .08f), iron, p);
        InvisibleWall("Fixed Bar Collision", V(x - .9f, y + 1.7f, 41.65f), V(2.98f, 3.4f, .14f), p);
        Transform door = Group("Open Cell Door", p);
        door.position = V(x + .68f, y, 41.65f);
        door.rotation = Quaternion.Euler(0, -76, 0);
        for (int b = 0; b < 5; b++)
            LocalBox("Door Bar", V(.12f + b * .28f, 1.65f, 0), V(.045f, 3.25f, .06f), iron, door);
        LocalBox("Door Top", V(.7f, 3.3f, 0), V(1.5f, .1f, .09f), iron, door);
        LocalBox("Door Bottom", V(.7f, .09f, 0), V(1.5f, .12f, .09f), iron, door);
        LocalBox("Door Lock", V(1.28f, 1.5f, 0), V(.18f, .26f, .1f), rust, door);
        Text("Cell Number", number, V(x, y + 3.72f, 41.47f), .24f, paper.color, p);
        for (int bunk = 0; bunk < 2; bunk++)
        {
            float bedY = y + .55f + bunk * 1.25f;
            Box("Bunk Frame", V(x - 1.45f, bedY, 45.4f), V(1.25f, .14f, 2.9f), iron, p, true);
            Box("Faded Mattress", V(x - 1.45f, bedY + .16f, 45.4f), V(1.15f, .23f, 2.7f), cloth, p);
            Box("Pillow", V(x - 1.45f, bedY + .32f, 46.28f), V(.85f, .15f, .46f), paper, p);
            Box("Folded Blanket", V(x - 1.45f, bedY + .29f, 44.75f), V(1.17f, .08f, 1.05f), green, p);
        }
        foreach (float dx in new[] { -2.05f, -.85f })
            foreach (float dz in new[] { 44.05f, 46.75f })
                Box("Bunk Upright", V(x + dx, y + 1.1f, dz), V(.07f, 2.2f, .07f), iron, p);
        Box("Steel Wall Desk", V(x + 1.5f, y + .83f, 46.9f), V(1.45f, .13f, .7f), iron, p, true);
        Box("Old Book", V(x + 1.5f, y + .95f, 46.8f), V(.3f, .06f, .4f), paper, p);
        Cylinder("Toilet Base", V(x + 1.8f, y + .3f, 44.9f), .23f, .6f, concrete, p);
        Box("Toilet Cistern", V(x + 2.21f, y + .65f, 44.9f), V(.24f, .7f, .6f), concrete, p);
        Text("Wall Tally", "||||  ||||", V(x + .55f, y + 1.6f, 47.47f), .12f, new Color(.2f, .22f, .19f), p);
    }

    private static void Galleries(Transform p)
    {
        Box("Rear Upper Walkway", V(0, 4.08f, 40.3f), V(30.6f, .24f, 2.6f), floor, p, true);
        Railing("Upper Walkway Rail", V(-15.2f, 4.2f, 39), V(11.4f, 4.2f, 39), p);
        Railing("Short Landing Rail", V(14.6f, 4.2f, 39), V(15.2f, 4.2f, 39), p);
        // A broad stair on the east side rises away from the entrance. The invisible ramp is continuous.
        const float x = 13, start = 31, run = 8, rise = 4.2f;
        for (int i = 0; i < 21; i++)
            Box("Stair Tread " + (i + 1), V(x, (i + 1) * .2f - .07f, start + (i + .5f) * run / 21), V(2.8f, .14f, run / 21 + .015f), iron, p);
        GameObject ramp = Box("Smooth Stair Collision", V(x, rise / 2 - .15f, start + run / 2), V(2.8f, .22f, Mathf.Sqrt(run * run + rise * rise)), iron, p, true);
        ramp.transform.rotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0, 0);
        ramp.GetComponent<Renderer>().enabled = false;
        foreach (float side in new[] { 11.5f, 14.5f })
        {
            Rod("Stair Handrail", V(side, 1.1f, start), V(side, 5.3f, start + run), .045f, iron, p);
            for (int i = 0; i <= 7; i++)
            {
                float t = i / 7f;
                Box("Stair Rail Post", V(side, rise * t + .55f, start + run * t), V(.06f, 1.1f, .06f), iron, p);
            }
            InvisibleWall("Stair Side Guard", V(side, 2.8f, 35), V(.14f, 6, 8.5f), p);
        }
        for (int i = 0; i < 6; i++)
            Box("Walkway Support", V(-12.5f + i * 5, 1.95f, 40), V(.13f, 3.9f, .13f), iron, p, true);
    }

    private static void Checkpoint(Transform p)
    {
        // Open heavy doors retain a clear central route from the yard.
        foreach (int side in new[] { -1, 1 })
        {
            Box("Entrance Concrete Jamb", V(side * 3.25f, 1.9f, 27.6f), V(.5f, 3.8f, 1.6f), concrete, p, true);
            GameObject door = Box("Open Heavy Steel Door", V(side * 3.05f, 1.7f, 29.5f), V(.16f, 3.4f, 2.5f), green, p, true);
            Box("Door Inspection Slot", V(side * 2.95f, 2.2f, 29.5f), V(.04f, .35f, .8f), black, p);
            Box("Entry Fence Return", V(side * 4.7f, 1.35f, 26.5f), V(.10f, 2.7f, 3), iron, p, true);
            for (int i = 0; i < 10; i++)
                Box("Approach Vertical Bar", V(side * 4.7f, 1.35f, 25 + i * .3f), V(.065f, 2.7f, .065f), rust, p);
        }
        Exterior(Box("Entrance Beam", V(0, 4.05f, 27.6f), V(7, .5f, 1.6f), concrete, p));
        Exterior(Text("Exterior Entrance Label", "GRAYHAVEN  /  CELLBLOCK C", V(0, 4.55f, 27.05f), .27f, paper.color, p));
        Box("Welcome Floor Stripe", V(0, .012f, 30), V(5.5f, .014f, .12f), paper, p);
        Box("Guard Desk", V(-5.6f, 1.05f, 31.4f), V(3.4f, .2f, 1.3f), wood, p, true);
        foreach (float dx in new[] { -1.35f, 1.35f })
            Box("Guard Desk Leg", V(-5.6f + dx, .5f, 31.4f), V(.13f, 1, .95f), iron, p);
        Box("Radio", V(-6.5f, 1.37f, 31.35f), V(.55f, .42f, .35f), black, p);
        Rod("Radio Aerial", V(-6.65f, 1.55f, 31.4f), V(-6.6f, 2.02f, 31.4f), .012f, iron, p);
        Box("Clipboard", V(-5.4f, 1.17f, 31.35f), V(.43f, .025f, .63f), paper, p);
        for (int i = 0; i < 4; i++)
        {
            Box("Guard Locker", V(-10.4f + i * .72f, 1.15f, 29.6f), V(.65f, 2.3f, .65f), green, p, true);
            Box("Locker Vent", V(-10.4f + i * .72f, 1.9f, 29.24f), V(.42f, .19f, .025f), black, p);
        }
        Text("Checkpoint Sign", "CHECKPOINT  /  KEEP GATE CLEAR", V(-6.2f, 3.1f, 30), .19f, paper.color, p);
        Fixture("Entry Light", V(0, 3.6f, 29.6f), p, new Color(.78f, .9f, .79f), 7, 1.8f);
    }

    private static void CommonRoom(Transform p)
    {
        Box("Common Room Signboard", V(0, 5.9f, 41.52f), V(8.4f, .7f, .1f), green, p);
        Text("Common Room Sign", "C BLOCK  /  COMMON ROOM", V(0, 5.9f, 41.45f), .38f, paper.color, p);
        for (int i = 0; i < 2; i++)
        {
            float x = -6 + i * 7;
            Box("Shared Meal Table", V(x, .86f, 35.5f), V(3.7f, .16f, 1.7f), wood, p, true);
            foreach (float side in new[] { -1.2f, 1.2f })
            {
                Box("Bench Seat", V(x, .47f, 35.5f + side), V(3.7f, .12f, .45f), wood, p, true);
                foreach (float dx in new[] { -1.35f, 1.35f })
                    Box("Bench Support", V(x + dx, .23f, 35.5f + side), V(.12f, .46f, .36f), iron, p);
            }
            foreach (float dx in new[] { -1.35f, 1.35f })
                Box("Table Support", V(x + dx, .42f, 35.5f), V(.14f, .84f, 1.3f), iron, p);
            Box("Spread Map", V(x, .956f, 35.45f), V(.8f, .018f, .55f), paper, p);
            Cylinder("Food Tin", V(x + .65f, 1.09f, 35.6f), .085f, .25f, green, p);
        }
        // A warm, visibly occupied corner beside the institutional cellblock.
        for (int i = 0; i < 3; i++)
        {
            float z = 32.4f + i * 2.6f;
            Box("Survivor Cot", V(-12.6f, .31f, z), V(2.3f, .22f, 1.2f), cloth, p, true);
            Box("Cot Pillow", V(-13.35f, .48f, z), V(.5f, .15f, .85f), paper, p);
            Box("Cot Blanket", V(-12, .46f, z), V(.9f, .08f, 1.17f), green, p);
        }
        for (int shelf = 0; shelf < 3; shelf++)
        {
            Box("Supply Shelf", V(8.3f, .35f + shelf * .7f, 30), V(3.4f, .09f, .8f), wood, p);
            for (int box = 0; box < 4; box++)
                Box("Stored Supplies", V(7.1f + box * .8f, .62f + shelf * .7f, 30), V(.58f, .45f, .55f), box % 2 == 0 ? green : cloth, p);
        }
        Box("Supply Shelf Left", V(6.6f, 1.2f, 30), V(.1f, 2.4f, .8f), iron, p, true);
        Box("Supply Shelf Right", V(10, 1.2f, 30), V(.1f, 2.4f, .8f), iron, p, true);
        Text("Supplies Label", "WATER  /  FOOD  /  MEDICAL", V(8.3f, 2.65f, 29.5f), .17f, paper.color, p);
        Lantern(V(-10.7f, .9f, 35), p);
        Lantern(V(2, 1.12f, 35.6f), p);
        LightFill("Warm Survivor Corner", V(-11, 2.4f, 35), new Color(1, .57f, .24f), 2.4f, 8, p);
    }

    private static void DangerWing(Transform p)
    {
        Box("Dark Wing Recess", V(-15.9f, 1.7f, 39.9f), V(1.1f, 3.4f, .2f), black, p);
        for (int i = 0; i < 5; i++)
            Box("Sealed Wing Bar", V(-16.45f + i * .28f, 1.7f, 39.72f), V(.07f, 3.4f, .08f), rust, p);
        Rod("Chained Gate A", V(-16.55f, 1, 39.59f), V(-15.22f, 2, 39.59f), .035f, iron, p);
        Rod("Chained Gate B", V(-16.55f, 2, 39.59f), V(-15.22f, 1, 39.59f), .035f, iron, p);
        Box("Gate Padlock", V(-15.86f, 1.5f, 39.5f), V(.17f, .24f, .12f), rust, p);
        Text("Sealed Wing Warning", "D WING\nDO NOT OPEN", V(-15.8f, 2.8f, 39.4f), .14f, new Color(.85f, .63f, .3f), p);
        InvisibleWall("Sealed Wing Barrier", V(-15.85f, 1.7f, 39.72f), V(1.5f, 3.4f, .22f), p);
    }

    private static void Dress(Transform p)
    {
        foreach (float y in new[] { 3.5f, 7.5f })
        {
            Rod("Exposed Rear Pipe", V(-16.5f, y, 47.3f), V(16.5f, y, 47.3f), .075f, rust, p);
            Rod("West Service Pipe", V(-16.5f, y, 29), V(-16.5f, y, 47.3f), .065f, rust, p);
        }
        Rod("Water Riser", V(16.45f, .3f, 46.8f), V(16.45f, 8.6f, 46.8f), .09f, rust, p);
        for (int i = 0; i < 11; i++)
        {
            float x = -14 + i * 2.7f;
            GameObject crack = Box("Floor Crack", V(x, .009f, 30.5f + i % 4 * 2.5f), V(1.2f + i % 3 * .4f, .012f, .024f), black, p);
            crack.transform.rotation = Quaternion.Euler(0, i * 47, 0);
        }
        for (int i = 0; i < 9; i++)
            Box("Peeling Dado Paint", V(-16.648f, .3f + i % 3 * .4f, 30 + i * 1.8f), V(.025f, .17f + i % 2 * .12f, .3f + i % 3 * .2f), concrete, p);
        Text("Exit Direction", "YARD  /  EXIT", V(0, 3.1f, 29), .22f, new Color(.63f, .78f, .64f), p, 180);
        Text("Gallery Label", "UPPER CELLS  201-206", V(13, 2.8f, 30.8f), .18f, paper.color, p);
        Text("Upper Block Label", "GRAYHAVEN  /  C", V(0, 8.9f, 47.4f), .39f, paper.color, p);
    }

    private static void Materials()
    {
        concrete = Mat("Aged Concrete", new Color(.65f, .66f, .60f), 0, .13f, "PrisonWallConcreteV2.png", new Vector2(3, 2));
        floor = Mat("Worn Interior Floor", new Color(.63f, .64f, .61f), 0, .14f, "YardConcreteV2.png", new Vector2(4, 4));
        green = Mat("Institutional Green", new Color(.24f, .34f, .29f), .1f, .18f);
        iron = Mat("Blackened Steel", new Color(.13f, .17f, .17f), .65f, .28f);
        rust = Mat("Old Rust", new Color(.31f, .21f, .14f), .5f, .2f);
        wood = Mat("Weathered Table Wood", new Color(.35f, .27f, .17f), 0, .1f);
        cloth = Mat("Worn Bedding", new Color(.35f, .38f, .31f), 0, .03f);
        paper = Mat("Faded Canvas", new Color(.73f, .70f, .56f), 0, .1f);
        black = Mat("Shadow Recess", new Color(.045f, .06f, .057f), 0, .1f);
        glow = Mat("Cold Glass", new Color(.43f, .61f, .61f), .1f, .4f);
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.3f, .47f, .45f) * 1.3f);
        warm = Mat("Lantern Glass", new Color(1, .55f, .18f), .1f, .25f);
        warm.EnableKeyword("_EMISSION"); warm.SetColor("_EmissionColor", new Color(1, .38f, .08f) * 2);
    }

    private static Material Mat(string name, Color color, float metallic, float smoothness, string texture = null, Vector2? tiling = null)
    {
        string path = Art + "/Materials/" + name.Replace(" ", "") + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
        if (texture != null)
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/PrisonYard/Textures/" + texture));
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        }
        EditorUtility.SetDirty(m); return m;
    }

    private static void Railing(string name, Vector3 from, Vector3 to, Transform p)
    {
        Rod(name, from + Vector3.up * 1.05f, to + Vector3.up * 1.05f, .045f, iron, p);
        Rod(name + " Lower", from + Vector3.up * .5f, to + Vector3.up * .5f, .032f, iron, p);
        int posts = Mathf.CeilToInt(Vector3.Distance(from, to) / 1.2f);
        for (int i = 0; i <= posts; i++)
            Box("Gallery Rail Post", Vector3.Lerp(from, to, (float)i / posts) + Vector3.up * .53f, V(.055f, 1.06f, .055f), iron, p);
        InvisibleWall("Gallery Guard Collision", (from + to) * .5f + Vector3.up * .6f, V(Mathf.Max(.1f, Mathf.Abs(to.x - from.x)), 1.2f, .12f), p);
    }

    private static void Lantern(Vector3 pos, Transform p)
    {
        Cylinder("Lantern Base", pos, .14f, .1f, iron, p);
        Cylinder("Lantern Glass", pos + Vector3.up * .21f, .1f, .33f, warm, p);
        Cylinder("Lantern Cap", pos + Vector3.up * .4f, .15f, .08f, iron, p);
    }
    private static void Fixture(string name, Vector3 pos, Transform p, Color color, float range, float intensity)
    {
        Box(name + " Housing", pos, V(2.2f, .13f, .4f), iron, p);
        Box(name + " Tube", pos + V(0, -.09f, 0), V(1.9f, .07f, .22f), glow, p);
        LightFill(name, pos + Vector3.down * .25f, color, intensity, range, p);
    }
    private static void LightFill(string name, Vector3 pos, Color color, float intensity, float range, Transform p)
    {
        Transform t = Group(name, p); t.position = pos;
        Light light = t.gameObject.AddComponent<Light>(); light.type = LightType.Point;
        light.color = color; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
    }
    private static void Exterior(GameObject g) { cutaway.AddRange(g.GetComponentsInChildren<Renderer>()); }
    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    private static Transform Group(string name, Transform p)
    { var g = new GameObject(name); g.transform.SetParent(p, false); return g.transform; }
    private static GameObject Box(string name, Vector3 pos, Vector3 size, Material m, Transform p, bool solid = false)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false);
        g.transform.position = pos; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m;
        if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    private static GameObject LocalBox(string name, Vector3 pos, Vector3 size, Material m, Transform p)
    { GameObject g = Box(name, Vector3.zero, size, m, p); g.transform.localPosition = pos; g.transform.localRotation = Quaternion.identity; return g; }
    private static GameObject Cylinder(string name, Vector3 pos, float radius, float height, Material m, Transform p)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(p, false);
        g.transform.position = pos; g.transform.localScale = V(radius * 2, height / 2, radius * 2);
        g.GetComponent<Renderer>().sharedMaterial = m; Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
    }
    private static void Rod(string name, Vector3 from, Vector3 to, float radius, Material m, Transform p)
    { GameObject g = Cylinder(name, (from + to) / 2, radius, Vector3.Distance(from, to), m, p); g.transform.up = (to - from).normalized; }
    private static void InvisibleWall(string name, Vector3 pos, Vector3 size, Transform p)
    { Transform t = Group(name, p); t.position = pos; t.gameObject.AddComponent<BoxCollider>().size = size; }
    private static GameObject Text(string name, string value, Vector3 pos, float size, Color color, Transform p, float yaw = 0)
    {
        Transform t = Group(name, p); t.position = pos; t.rotation = Quaternion.Euler(0, yaw, 0);
        TextMesh tm = t.gameObject.AddComponent<TextMesh>(); tm.text = value; tm.fontSize = 64; tm.characterSize = .1f;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
        MeshRenderer renderer = t.GetComponent<MeshRenderer>(); renderer.sharedMaterial = WorldTextMaterial(tm.font);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        float height = renderer.bounds.size.y;
        if (height > .001f) t.localScale = Vector3.one * (size * value.Split('\n').Length / height);
        return t.gameObject;
    }
    public static Material WorldTextMaterial(Font font)
    {
        string path = Art + "/Materials/WorldText.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("DaysGoneQuiet/World Text")) { name = "Depth Tested World Text" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_MainTex", font.material.mainTexture);
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }
    private static void Folder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
}
#endif
