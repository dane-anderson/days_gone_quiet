#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class SurvivalSceneBuilder
{
    private const string RootName = "Survival World";
    private const string MaterialPath = "Assets/Art/Environment/PrisonInterior/Materials/";
    private static Material iron, rust, wood, glow;

    [MenuItem("Days Gone Quiet/Set Up Prison Survival", priority = 22)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before updating survival.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        PrisonInteriorTour tour = Object.FindAnyObjectByType<PrisonInteriorTour>();
        if (scene.name != "PrisonYard" || tour == null)
            throw new InvalidOperationException("Open the completed PrisonYard scene first.");
        GameObject old = FindSceneObject(scene, RootName);
        SurvivalWorld previousWorld = old != null ? old.GetComponent<SurvivalWorld>() : null;
        GameObject rickObject = tour.survivalRick != null ? tour.survivalRick.gameObject : null;
        if (rickObject == null && tour.rick != null) rickObject = tour.rick.gameObject;
        if (rickObject == null && previousWorld != null && previousWorld.rick != null)
            rickObject = previousWorld.rick.gameObject;
        if (rickObject == null) rickObject = FindSceneObject(scene, "Rick");
        GameObject walkerObject = tour.walker != null ? tour.walker.gameObject : null;
        if (walkerObject == null) walkerObject = FindSceneObject(scene, "Walker");
        if (walkerObject == null && previousWorld != null && previousWorld.walkers != null)
            foreach (SurvivalWalker candidate in previousWorld.walkers)
                if (candidate != null) { walkerObject = candidate.gameObject; break; }
        if (rickObject == null || walkerObject == null)
            throw new InvalidOperationException("The scene needs its Rick and original Walker objects before survival can be set up.");
        RickAgent legacyRick = rickObject.GetComponent<RickAgent>();
        WalkerChase legacyWalker = walkerObject.GetComponent<WalkerChase>();

        EnsureFolder("Assets/Survival/Navigation"); EnsureFolder("Assets/Survival/Models");
        ConfigureSprite("Assets/Art/Characters/NeganSurvivor.png");
        iron = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "BlackenedSteel.mat");
        rust = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "OldRust.mat");
        wood = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "WeatheredTableWood.mat");
        glow = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "LanternGlass.mat");
        Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Prison Survival");
        // A previous interrupted setup may already have removed legacy components.
        // Resolve and preserve the surviving actors before replacing generated content.
        if (old != null && rickObject.transform.IsChildOf(old.transform))
            Undo.SetTransformParent(rickObject.transform, null, "Preserve Rick while rebuilding survival");
        if (old != null && walkerObject.transform.IsChildOf(old.transform))
            Undo.SetTransformParent(walkerObject.transform, null, "Preserve walker while rebuilding survival");
        if (old != null) Undo.DestroyObjectImmediate(old);
        Transform root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add survival world");
        SurvivalWorld world = root.gameObject.AddComponent<SurvivalWorld>();

        // Preserve the original scripts/model for SampleScene and comparison, but retire the
        // old two-position policy in this connected map. New policies have their own names.
        foreach (DecisionRequester oldRequester in rickObject.GetComponents<DecisionRequester>()) Undo.DestroyObjectImmediate(oldRequester);
        if (legacyRick != null) Undo.DestroyObjectImmediate(legacyRick);
        if (legacyWalker != null)
        { Undo.RecordObject(legacyWalker, "Replace straight-line chaser"); legacyWalker.enabled = false; }
        foreach (RLExperimentControls controls in Object.FindObjectsByType<RLExperimentControls>(FindObjectsSortMode.None))
        { Undo.RecordObject(controls, "Use world training controls"); controls.enabled = false; }
        if (tour.tourBoundaryColliders != null)
            foreach (Collider fence in tour.tourBoundaryColliders)
            { if (fence != null) { Undo.RecordObject(fence, "Activate world boundaries"); fence.enabled = true; } }

        world.rick = ConfigureLearner(rickObject, SurvivorRole.Rick, world, "RickEscape");
        rickObject.transform.position = new Vector3(0, 1, 19);
        tour.world = world; tour.survivalRick = world.rick; tour.rick = null; tour.walker = legacyWalker;
        EditorUtility.SetDirty(tour);
        Transform negan = Group("Negan", root);
        negan.position = new Vector3(5, 1, 33);
        CapsuleCollider neganCapsule = negan.gameObject.AddComponent<CapsuleCollider>();
        neganCapsule.height = 2; neganCapsule.radius = .38f; neganCapsule.isTrigger = true;
        CharacterSpriteVisual visual = negan.gameObject.AddComponent<CharacterSpriteVisual>();
        SerializedObject sprite = new SerializedObject(visual);
        sprite.FindProperty("characterSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/NeganSurvivor.png");
        sprite.FindProperty("worldHeight").floatValue = 2.2f;
        sprite.ApplyModifiedPropertiesWithoutUndo();
        world.negan = ConfigureLearner(negan.gameObject, SurvivorRole.Negan, world, "NeganCombat");
        world.negan.batPivot = BuildLucille(negan);

        world.rickSpawn = Spawn("Rick Escape Start", new Vector3(0, 0, 19), root);
        world.neganSpawn = Spawn("Negan Combat Start", new Vector3(5, 0, 33), root);
        Transform walkerGroup = Group("Active Walkers", root);
        var walkers = new List<SurvivalWalker>();
        var spawns = new List<Transform>();
        Vector3[] spawnPositions = {
            new Vector3(0,0,13), new Vector3(10,0,17), new Vector3(4,0,45),
            new Vector3(-15.85f,0,44.5f), new Vector3(-8,0,38), new Vector3(0,4.2f,40.3f)
        };
        string[] names = { "Yard Pursuer", "East Yard Walker", "Cell 104 Walker", "Dark Hallway Walker", "Common Room Walker", "Upper Gallery Walker" };
        for (int i = 0; i < spawnPositions.Length; i++)
        {
            GameObject go;
            if (i == 0) go = walkerObject;
            else
            {
                go = new GameObject(names[i]); go.transform.SetParent(walkerGroup, false);
                CapsuleCollider capsule = go.AddComponent<CapsuleCollider>(); capsule.height = 2; capsule.radius = .36f; capsule.isTrigger = true;
                CharacterSpriteVisual walkerVisual = go.AddComponent<CharacterSpriteVisual>();
                SerializedObject sv = new SerializedObject(walkerVisual);
                sv.FindProperty("characterSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Walker.png");
                sv.FindProperty("worldHeight").floatValue = 2.2f; sv.ApplyModifiedPropertiesWithoutUndo();
            }
            go.tag = "Untagged";
            ConfigurePhysics(go);
            NavMeshAgent nav = ConfigureNavigation(go, 1.7f);
            SurvivalWalker walker = go.GetComponent<SurvivalWalker>();
            if (walker == null) walker = go.AddComponent<SurvivalWalker>();
            walker.world = world; walker.navigation = nav;
            go.transform.position = spawnPositions[i] + Vector3.up;
            walkers.Add(walker); spawns.Add(Spawn(names[i] + " Spawn", spawnPositions[i], root));
        }
        world.walkers = walkers.ToArray(); world.walkerSpawns = spawns.ToArray();
        world.activeWalkerCount = 4; world.walkerSpeed = 1.7f; world.walkerHealth = 60;
        world.episodeSeconds = 45; world.simulationSpeed = 1; world.scriptedPreview = true;
        world.trainingFocus = SurvivalWorld.TrainingFocus.Both;

        Transform existingCellDoor = tour.transform.Find("Twelve Cells - Two Levels/Cell 104/Open Cell Door");
        if (existingCellDoor != null) { Undo.RecordObject(existingCellDoor.gameObject, "Replace cell door"); existingCellDoor.gameObject.SetActive(false); }
        Transform oldHallGate = tour.transform.Find("Chained West Wing");
        if (oldHallGate != null) { Undo.RecordObject(oldHallGate.gameObject, "Open dark hallway connection"); oldHallGate.gameObject.SetActive(false); }
        Transform doors = Group("Learnable Doors", root);
        world.doors = new[] {
            BuildDoor("Prison Entrance", new Vector3(0,1.7f,28.5f), 5.65f, 3.4f, true, doors),
            BuildDoor("Cell 104", new Vector3(4.04f,1.65f,41.65f), 1.48f, 3.3f, false, doors),
            BuildDoor("Dark Hallway", new Vector3(-15.85f,1.65f,39.72f), 1.48f, 3.3f, false, doors)
        };
        MakeHallLamp(root);

        // Bake using the existing physical floors/walls. Agents and dynamic doors are excluded.
        NavMeshSurface surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.ignoreNavMeshAgent = true; surface.ignoreNavMeshObstacle = true;
        surface.overrideVoxelSize = true; surface.voxelSize = .06f;
        surface.overrideTileSize = true; surface.tileSize = 128;
        foreach (SurvivalDoor door in world.doors)
        { door.blockingCollider.enabled = false; door.obstacle.enabled = false; }
        surface.BuildNavMesh();
        if (surface.navMeshData == null) throw new InvalidOperationException("Could not bake the connected prison navigation mesh.");
        const string meshPath = "Assets/Survival/Navigation/PrisonSurvivalNavMesh.asset";
        NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(meshPath);
        if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, meshPath);
        else
        {
            NavMeshData generated = surface.navMeshData; surface.RemoveData();
            EditorUtility.CopySerialized(generated, existing);
            surface.navMeshData = existing; Object.DestroyImmediate(generated); surface.AddData(); EditorUtility.SetDirty(existing);
        }
        foreach (SurvivalDoor door in world.doors) door.ResetDoor();
        foreach (SurvivalWalker walker in world.walkers) walker.navigation.enabled = true;
        world.rick.navigation.enabled = true; world.negan.navigation.enabled = true;
        EditorUtility.SetDirty(world);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root.gameObject;
        WorldControlWindow.Open();
        Debug.Log("SURVIVAL WORLD READY: independent RickEscape and NeganCombat policies, Lucille combat, three operable doors, yard/cell/hallway walkers, and connected navigation. No trained model is claimed until a trainer exports one. Scripted preview is clearly marked in World Control.");
    }

    private static SurvivalAgent ConfigureLearner(GameObject go, SurvivorRole role, SurvivalWorld world, string behaviorName)
    {
        ConfigurePhysics(go);
        NavMeshAgent nav = ConfigureNavigation(go, role == SurvivorRole.Rick ? 4 : 3.5f);
        BehaviorParameters behavior = go.GetComponent<BehaviorParameters>();
        if (behavior == null) behavior = go.AddComponent<BehaviorParameters>();
        Undo.RecordObject(behavior, "Assign independent survival policy");
        behavior.BehaviorName = behaviorName;
        behavior.BehaviorType = BehaviorType.Default;
        behavior.Model = null;
        behavior.BrainParameters.VectorObservationSize = SurvivalAgent.ObservationSize;
        behavior.BrainParameters.NumStackedVectorObservations = 1;
        behavior.BrainParameters.ActionSpec = new ActionSpec(2, new[] { 3 });
        foreach (DecisionRequester oldRequester in go.GetComponents<DecisionRequester>()) Undo.DestroyObjectImmediate(oldRequester);
        SurvivalAgent agent = go.GetComponent<SurvivalAgent>();
        if (agent == null) agent = go.AddComponent<SurvivalAgent>();
        agent.enabled = true; agent.role = role; agent.world = world; agent.navigation = nav; agent.MaxStep = 0;
        SurvivalDecisionRequester requester = go.GetComponent<SurvivalDecisionRequester>();
        if (requester == null) requester = go.AddComponent<SurvivalDecisionRequester>();
        requester.agent = agent; requester.enabled = true; requester.DecisionPeriod = 5; requester.DecisionStep = 0; requester.TakeActionsBetweenDecisions = true;
        EditorUtility.SetDirty(behavior); EditorUtility.SetDirty(requester); EditorUtility.SetDirty(agent);
        return agent;
    }

    private static void ConfigurePhysics(GameObject go)
    {
        Rigidbody body = go.GetComponent<Rigidbody>();
        if (body != null)
        { Undo.RecordObject(body, "Let navigation move character"); body.isKinematic = true; body.useGravity = false; }
        foreach (Collider collider in go.GetComponents<Collider>())
        { Undo.RecordObject(collider, "Configure navigation actor"); collider.isTrigger = true; }
    }
    private static NavMeshAgent ConfigureNavigation(GameObject go, float speed)
    {
        NavMeshAgent nav = go.GetComponent<NavMeshAgent>();
        if (nav == null) nav = go.AddComponent<NavMeshAgent>();
        nav.enabled = false; nav.radius = .38f; nav.height = 2; nav.baseOffset = 1;
        nav.speed = speed; nav.acceleration = 20; nav.angularSpeed = 480;
        nav.stoppingDistance = .1f; nav.updateRotation = false; nav.autoBraking = true;
        return nav;
    }

    private static SurvivalDoor BuildDoor(string name, Vector3 position, float width, float height, bool open, Transform parent)
    {
        Transform root = Group(name, parent); root.position = position;
        SurvivalDoor door = root.gameObject.AddComponent<SurvivalDoor>(); door.displayName = name;
        Transform leaf = Group("Sliding Barred Door", root);
        BoxCollider collider = leaf.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(width, height, .18f);
        NavMeshObstacle obstacle = leaf.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = new Vector3(width, height, .3f); obstacle.carving = true;
        for (int i = 0; i <= Mathf.CeilToInt(width / .23f); i++)
        {
            float x = -width / 2 + i * width / Mathf.CeilToInt(width / .23f);
            Box("Door Vertical Bar", new Vector3(x,0,0), new Vector3(.055f,height,.065f), iron, leaf);
        }
        foreach (float y in new[] { -height / 2, 0f, height / 2 })
            Box("Door Crossbar", new Vector3(0,y,0), new Vector3(width,.10f,.10f), rust, leaf);
        Box("Door Lock", new Vector3(width / 2 - .12f, -.12f, -.1f), new Vector3(.18f,.26f,.13f), rust, leaf);
        Box("Sliding Door Track", new Vector3(width / 2,height / 2 + .15f,0), new Vector3(width * 2 + .25f,.14f,.18f), iron, root);
        door.leaf = leaf; door.blockingCollider = collider; door.obstacle = obstacle;
        door.closedLocalPosition = Vector3.zero;
        door.openLocalPosition = new Vector3(width + .15f,0,0);
        door.startOpen = open;
        return door;
    }

    private static Transform BuildLucille(Transform negan)
    {
        Transform mount = Group("Lucille Billboard Mount", negan);
        mount.gameObject.AddComponent<SurvivalBatMount>();
        Transform pivot = Group("Lucille Swing Pivot", mount);
        pivot.localPosition = new Vector3(-.294f,-.029f,-.1f);
        pivot.localRotation = Quaternion.Euler(0,0,25);
        Cylinder("Bat Handle", new Vector3(0,.17f,0), .031f,.34f,wood,pivot);
        Cylinder("Bat Barrel", new Vector3(0,.67f,0), .066f,.72f,wood,pivot);
        Cylinder("Bat Knob", new Vector3(0,-.015f,0), .046f,.055f,wood,pivot);
        for (int i = 0; i < 66; i++)
        {
            float t = i / 65f, t2 = (i + 1) / 65f;
            Vector3 a = new Vector3(Mathf.Cos(t * Mathf.PI * 10) * .079f, .45f + t * .6f, Mathf.Sin(t * Mathf.PI * 10) * .079f);
            Vector3 b = new Vector3(Mathf.Cos(t2 * Mathf.PI * 10) * .079f, .45f + t2 * .6f, Mathf.Sin(t2 * Mathf.PI * 10) * .079f);
            Rod("Lucille Barbed Wire",a,b,.008f,iron,pivot);
            if (i % 5 == 0)
            {
                Vector3 radial = new Vector3(a.x,0,a.z).normalized;
                Rod("Wire Barb",a-radial*.01f,a+radial*.07f+Vector3.up*.025f,.008f,iron,pivot);
            }
        }
        return pivot;
    }
    private static void MakeHallLamp(Transform root)
    {
        Box("Dark Hall Warning Lamp",new Vector3(-15.8f,3.3f,43),new Vector3(.25f,.18f,.2f),glow,root);
        Transform light = Group("Dark Hall Practical Light",root); light.position = new Vector3(-15.85f,3,44);
        Light l=light.gameObject.AddComponent<Light>();l.color=new Color(1,.23f,.07f);l.intensity=2.1f;l.range=5;
    }
    private static Transform Spawn(string name, Vector3 position, Transform parent)
    { Transform t=Group(name,parent);t.position=position;return t; }
    private static GameObject FindSceneObject(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
        return null;
    }
    private static Transform Group(string name, Transform parent)
    { Transform t=new GameObject(name).transform;t.SetParent(parent,false);return t; }
    private static void Box(string name,Vector3 p,Vector3 scale,Material m,Transform parent)
    { GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>()); }
    private static GameObject Cylinder(string name,Vector3 p,float radius,float height,Material m,Transform parent)
    { GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());return g; }
    private static void Rod(string name,Vector3 a,Vector3 b,float radius,Material m,Transform parent)
    { GameObject g=Cylinder(name,(a+b)/2,radius,Vector3.Distance(a,b),m,parent);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized); }
    private static void ConfigureSprite(string path)
    {
        TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Negan's sprite is missing: "+path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.spritePixelsPerUnit=100;
        importer.maxTextureSize=2048;importer.SaveAndReimport();
    }
    private static void EnsureFolder(string path)
    { if(AssetDatabase.IsValidFolder(path))return;string p=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(p);AssetDatabase.CreateFolder(p,Path.GetFileName(path)); }
}
#endif
