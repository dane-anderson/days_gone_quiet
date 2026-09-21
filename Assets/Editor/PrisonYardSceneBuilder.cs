#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class PrisonYardSceneBuilder
{
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string TargetScene = "Assets/Scenes/PrisonYard.unity";
    private const string EnvironmentRootName = "Prison Yard Environment";
    private const string ArtFolder = "Assets/Art/Environment/PrisonYard";
    private const string MaterialFolder = ArtFolder + "/Materials";
    private const string TextureFolder = ArtFolder + "/Textures";
    private const string YardConcreteTexture = TextureFolder + "/YardConcreteV2.png";
    private const string PrisonWallTexture = TextureFolder + "/PrisonWallConcreteV2.png";
    private const string DeadIvyTexture = TextureFolder + "/DeadIvyV2.png";
    private const string YardProfile = "Assets/Settings/PrisonYardProfile.asset";

    [MenuItem("Days Gone Quiet/Build Prison Yard Scene")]
    public static void BuildPrisonYardScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before building the Prison Yard scene.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(ArtFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(TextureFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ConfigureTextureImport(YardConcreteTexture, true, false);
        ConfigureTextureImport(PrisonWallTexture, true, false);
        ConfigureTextureImport(DeadIvyTexture, false, true);

        bool targetAlreadyExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null;
        if (!targetAlreadyExists)
        {
            if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
                throw new IOException($"Could not copy {SourceScene} to {TargetScene}.");
            AssetDatabase.Refresh();
        }

        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

        Camera sceneCamera = Camera.main;
        bool preserveCameraView = targetAlreadyExists && sceneCamera != null;
        Vector3 savedCameraPosition = preserveCameraView ? sceneCamera.transform.position : Vector3.zero;
        Quaternion savedCameraRotation = preserveCameraView ? sceneCamera.transform.rotation : Quaternion.identity;
        float savedCameraFieldOfView = preserveCameraView ? sceneCamera.fieldOfView : 52f;

        GameObject oldRoot = GameObject.Find(EnvironmentRootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);

        Material concrete = GetOrCreateTexturedMaterial("Yard Concrete", new Color(0.90f, 0.91f, 0.89f), 0f, 0.12f,
            YardConcreteTexture, new Vector2(8f, 8f));
        Material fadedPaint = GetOrCreateMaterial("Faded Court Paint", new Color(0.67f, 0.65f, 0.54f), 0f, 0.22f);
        Material crack = GetOrCreateMaterial("Concrete Cracks", new Color(0.09f, 0.10f, 0.095f), 0f, 0.05f);
        Material fence = GetOrCreateMaterial("Charcoal Fence", new Color(0.12f, 0.14f, 0.14f), 0.7f, 0.28f);
        Material rust = GetOrCreateMaterial("Oxidized Metal", new Color(0.28f, 0.17f, 0.105f), 0.35f, 0.18f);
        Material prisonWall = GetOrCreateTexturedMaterial("Institutional Concrete", new Color(0.91f, 0.89f, 0.84f), 0f, 0.10f,
            PrisonWallTexture, new Vector2(5f, 2f));
        Material darkWindow = GetOrCreateMaterial("Dark Windows", new Color(0.035f, 0.05f, 0.055f), 0.45f, 0.48f);
        Material wood = GetOrCreateMaterial("Weathered Wood", new Color(0.22f, 0.15f, 0.09f), 0f, 0.12f);
        Material deadOlive = GetOrCreateMaterial("Dead Olive", new Color(0.25f, 0.26f, 0.12f), 0f, 0.08f);
        Material barrier = GetOrCreateMaterial("Aged Barrier", new Color(0.46f, 0.44f, 0.36f), 0f, 0.14f);
        Material amber = GetOrCreateEmissiveMaterial("Amber Warning Light", new Color(1f, 0.38f, 0.055f), 2.6f);
        Material damp = GetOrCreateMaterial("Damp Concrete", new Color(0.105f, 0.125f, 0.12f), 0f, 0.38f);
        Material dryGrass = GetOrCreateMaterial("Dry Yard Grass", new Color(0.34f, 0.33f, 0.13f), 0f, 0.04f);
        Material cloth = GetOrCreateMaterial("Abandoned Cloth", new Color(0.25f, 0.23f, 0.18f), 0f, 0.04f);
        Material road = GetOrCreateMaterial("Old Asphalt", new Color(0.105f, 0.115f, 0.11f), 0f, 0.08f);
        Material paper = GetOrCreateMaterial("Sun Bleached Paper", new Color(0.62f, 0.59f, 0.47f), 0f, 0.02f);
        Material ivy = GetOrCreateCutoutMaterial("Dead Ivy", DeadIvyTexture, new Color(0.78f, 0.75f, 0.61f));
        ConfigureDoubleSidedFoliage(deadOlive);
        ConfigureDoubleSidedFoliage(dryGrass);

        GameObject environment = new GameObject(EnvironmentRootName);
        Transform root = environment.transform;

        SetupGround(concrete);
        SetupCamera();
        if (preserveCameraView)
        {
            sceneCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            sceneCamera.fieldOfView = savedCameraFieldOfView;
        }
        SetupLightingAndAtmosphere();
        SetupSceneVolume();

        Transform markings = NewGroup("Faded Court Markings", root);
        BuildCourt(markings, fadedPaint, crack);

        Mesh chainLinkMesh = GetOrCreateChainLinkMesh();
        Transform perimeter = NewGroup("Perimeter Fence", root);
        BuildFence(perimeter, chainLinkMesh, fence, rust, amber);

        Transform prison = NewGroup("Grayhaven Cell Block", root);
        BuildPrisonBlock(prison, prisonWall, darkWindow, rust, barrier, amber);

        Transform tower = NewGroup("Northwest Guard Tower", root);
        BuildGuardTower(tower, prisonWall, fence, rust, darkWindow, amber);

        Transform shed = NewGroup("Southeast Maintenance Shed", root);
        BuildMaintenanceShed(shed, prisonWall, rust, darkWindow, amber);

        Transform details = NewGroup("Edge Story Details", root);
        BuildEdgeDetails(details, rust, wood, deadOlive, barrier, darkWindow);

        Transform v2 = NewGroup("V2 Art Pass", root);
        BuildV2ArtPass(v2, concrete, prisonWall, darkWindow, fence, rust, wood, deadOlive, dryGrass,
            barrier, damp, cloth, road, paper, ivy, amber);

        AddSceneToBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TargetScene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = environment;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log("Grayhaven Prison Yard V2 created. Rick, Walker, learning settings, physics roots, and the saved camera view were preserved unchanged.");
    }

    private static void SetupGround(Material concrete)
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
            throw new MissingReferenceException("The copied scene does not contain the expected Ground object.");

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = concrete;
    }

    private static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            throw new MissingReferenceException("The copied scene does not contain a Main Camera.");

        camera.transform.position = new Vector3(0f, 40f, -50f);
        camera.transform.LookAt(new Vector3(0f, 0f, 2f));
        camera.fieldOfView = 52f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 180f;
        camera.backgroundColor = new Color(0.25f, 0.29f, 0.30f);
    }

    private static void SetupLightingAndAtmosphere()
    {
        Light sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(36f, -40f, 0f);
            sun.color = new Color(1f, 0.78f, 0.58f);
            sun.intensity = 1.10f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.24f, 0.28f, 0.29f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.35f, 0.39f, 0.39f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 115f;
    }

    private static void SetupSceneVolume()
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(YardProfile) == null)
        {
            if (!AssetDatabase.CopyAsset("Assets/Settings/SampleSceneProfile.asset", YardProfile))
                throw new IOException("Could not create the scene-specific Prison Yard volume profile.");
            AssetDatabase.Refresh();
        }

        Volume volume = Object.FindAnyObjectByType<Volume>();
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(YardProfile);
        if (volume != null && profile != null)
            volume.sharedProfile = profile;

        if (profile != null)
        {
            if (!profile.TryGet(out ColorAdjustments color))
                color = profile.Add<ColorAdjustments>(true);

            color.contrast.Override(13f);
            color.saturation.Override(-20f);
            color.colorFilter.Override(new Color(0.93f, 0.95f, 0.89f, 1f));
            EditorUtility.SetDirty(profile);
        }
    }

    private static void BuildCourt(Transform parent, Material paint, Material crack)
    {
        CreateBox("North Boundary", new Vector3(0f, 0.025f, 16f), new Vector3(25f, 0.025f, 0.10f), paint, parent);
        CreateBox("South Boundary", new Vector3(0f, 0.025f, -16f), new Vector3(25f, 0.025f, 0.10f), paint, parent);
        CreateBox("West Boundary", new Vector3(-12.5f, 0.025f, 0f), new Vector3(0.10f, 0.025f, 32f), paint, parent);
        CreateBox("East Boundary", new Vector3(12.5f, 0.025f, 0f), new Vector3(0.10f, 0.025f, 32f), paint, parent);
        CreateBox("Center Line", new Vector3(0f, 0.027f, 0f), new Vector3(25f, 0.026f, 0.08f), paint, parent);

        const int circleSegments = 28;
        const float radius = 3.2f;
        for (int i = 0; i < circleSegments; i++)
        {
            float a = i * Mathf.PI * 2f / circleSegments;
            float segmentLength = (Mathf.PI * 2f * radius / circleSegments) * 0.78f;
            Vector3 position = new Vector3(Mathf.Cos(a) * radius, 0.03f, Mathf.Sin(a) * radius);
            GameObject segment = CreateBox($"Center Circle {i + 1:00}", position, new Vector3(segmentLength, 0.027f, 0.08f), paint, parent);
            segment.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
        }

        Vector3[] crackStarts =
        {
            new(-19f, 0.035f, -10f), new(-17f, 0.035f, 12f), new(-8f, 0.035f, 19f),
            new(9f, 0.035f, -19f), new(18f, 0.035f, 8f), new(20f, 0.035f, -13f),
            new(-3f, 0.035f, -21f), new(15f, 0.035f, 19f)
        };
        float[] angles = { 28f, -18f, 42f, -33f, 17f, 54f, -48f, 9f };
        float[] lengths = { 4.2f, 3.3f, 5.0f, 3.7f, 4.5f, 3.2f, 4.0f, 3.8f };
        for (int i = 0; i < crackStarts.Length; i++)
        {
            GameObject line = CreateBox($"Crack {i + 1:00}", crackStarts[i], new Vector3(lengths[i], 0.03f, 0.055f), crack, parent);
            line.transform.rotation = Quaternion.Euler(0f, angles[i], 0f);
        }
    }

    private static void BuildFence(Transform parent, Mesh mesh, Material fence, Material rust, Material amber)
    {
        BuildFenceSideZ("North Fence", 25f, parent, mesh, fence, rust, false);
        BuildFenceSideZ("South Fence and Gate", -25f, parent, mesh, fence, rust, true);
        BuildFenceSideX("East Fence", 25f, parent, mesh, fence, rust);
        BuildFenceSideX("West Fence", -25f, parent, mesh, fence, rust);

        Transform gate = NewGroup("Vehicle Gate", parent);
        CreateBox("Gate Center Post", new Vector3(0f, 2.45f, -24.88f), new Vector3(0.22f, 4.9f, 0.22f), rust, gate);
        CreateBox("Left Gate Brace A", new Vector3(-2.5f, 2.25f, -24.82f), new Vector3(0.10f, 5.6f, 0.10f), rust, gate).transform.rotation = Quaternion.Euler(0f, 0f, 55f);
        CreateBox("Left Gate Brace B", new Vector3(-2.5f, 2.25f, -24.82f), new Vector3(0.10f, 5.6f, 0.10f), rust, gate).transform.rotation = Quaternion.Euler(0f, 0f, -55f);
        CreateBox("Right Gate Brace A", new Vector3(2.5f, 2.25f, -24.82f), new Vector3(0.10f, 5.6f, 0.10f), rust, gate).transform.rotation = Quaternion.Euler(0f, 0f, 55f);
        CreateBox("Right Gate Brace B", new Vector3(2.5f, 2.25f, -24.82f), new Vector3(0.10f, 5.6f, 0.10f), rust, gate).transform.rotation = Quaternion.Euler(0f, 0f, -55f);

        CreateWarningBeacon("West Gate Beacon", new Vector3(-5f, 5.25f, -24.8f), amber, gate);
        CreateWarningBeacon("East Gate Beacon", new Vector3(5f, 5.25f, -24.8f), amber, gate);
    }

    private static void BuildFenceSideZ(string name, float z, Transform parent, Mesh mesh, Material fence, Material rust, bool gateSide)
    {
        Transform group = NewGroup(name, parent);
        for (int i = 0; i < 10; i++)
        {
            float x = -22.5f + i * 5f;
            CreateMeshObject(gateSide && Mathf.Abs(x) < 5f ? $"Gate Mesh {i + 1:00}" : $"Chain Link {i + 1:00}", mesh,
                fence, new Vector3(x, 0.25f, z), Quaternion.identity, group);
            CreateBox($"Bottom Rail {i + 1:00}", new Vector3(x, 0.35f, z), new Vector3(5f, 0.10f, 0.10f), rust, group);
            CreateBox($"Top Rail {i + 1:00}", new Vector3(x, 4.25f, z), new Vector3(5f, 0.10f, 0.10f), rust, group);
        }

        for (int i = 0; i <= 10; i++)
        {
            float x = -25f + i * 5f;
            CreateBox($"Post {i + 1:00}", new Vector3(x, 2.45f, z), new Vector3(0.16f, 4.9f, 0.16f), rust, group);
            if (i < 10)
            {
                CreateBox($"Razor Strand A {i + 1:00}", new Vector3(x + 2.5f, 4.62f, z - 0.10f), new Vector3(5f, 0.035f, 0.035f), fence, group);
                CreateBox($"Razor Strand B {i + 1:00}", new Vector3(x + 2.5f, 4.80f, z + 0.10f), new Vector3(5f, 0.035f, 0.035f), fence, group);
            }
        }
    }

    private static void BuildFenceSideX(string name, float x, Transform parent, Mesh mesh, Material fence, Material rust)
    {
        Transform group = NewGroup(name, parent);
        for (int i = 0; i < 10; i++)
        {
            float z = -22.5f + i * 5f;
            CreateMeshObject($"Chain Link {i + 1:00}", mesh, fence, new Vector3(x, 0.25f, z), Quaternion.Euler(0f, 90f, 0f), group);
            CreateBox($"Bottom Rail {i + 1:00}", new Vector3(x, 0.35f, z), new Vector3(0.10f, 0.10f, 5f), rust, group);
            CreateBox($"Top Rail {i + 1:00}", new Vector3(x, 4.25f, z), new Vector3(0.10f, 0.10f, 5f), rust, group);
        }

        for (int i = 0; i <= 10; i++)
        {
            float z = -25f + i * 5f;
            CreateBox($"Post {i + 1:00}", new Vector3(x, 2.45f, z), new Vector3(0.16f, 4.9f, 0.16f), rust, group);
            if (i < 10)
            {
                CreateBox($"Razor Strand A {i + 1:00}", new Vector3(x - 0.10f, 4.62f, z + 2.5f), new Vector3(0.035f, 0.035f, 5f), fence, group);
                CreateBox($"Razor Strand B {i + 1:00}", new Vector3(x + 0.10f, 4.80f, z + 2.5f), new Vector3(0.035f, 0.035f, 5f), fence, group);
            }
        }
    }

    private static void BuildPrisonBlock(Transform parent, Material wall, Material window, Material metal, Material barrier, Material amber)
    {
        CreateBox("Main Cell Block", new Vector3(0f, 4.5f, 31.5f), new Vector3(34f, 9f, 6f), wall, parent);
        CreateBox("Roof Slab", new Vector3(0f, 9.15f, 31.5f), new Vector3(35f, 0.35f, 6.5f), barrier, parent);
        CreateBox("Left Roof Parapet", new Vector3(-17.25f, 9.65f, 31.5f), new Vector3(0.35f, 1f, 6.5f), wall, parent);
        CreateBox("Right Roof Parapet", new Vector3(17.25f, 9.65f, 31.5f), new Vector3(0.35f, 1f, 6.5f), wall, parent);
        CreateBox("Front Roof Parapet", new Vector3(0f, 9.65f, 28.35f), new Vector3(35f, 1f, 0.35f), wall, parent);

        float[] xs = { -14f, -10f, -6f, 6f, 10f, 14f };
        foreach (float x in xs)
        {
            CreateBarredWindow($"Lower Window {x}", new Vector3(x, 3.0f, 28.43f), window, metal, parent);
            CreateBarredWindow($"Upper Window {x}", new Vector3(x, 6.7f, 28.43f), window, metal, parent);
        }

        CreateBox("Entrance Projection", new Vector3(0f, 3.3f, 27.9f), new Vector3(7.2f, 6.6f, 1.5f), wall, parent);
        CreateBox("Left Steel Door", new Vector3(-1.35f, 1.65f, 27.08f), new Vector3(2.35f, 3.3f, 0.16f), metal, parent);
        CreateBox("Right Steel Door", new Vector3(1.35f, 1.65f, 27.08f), new Vector3(2.35f, 3.3f, 0.16f), metal, parent);
        CreateBox("Entrance Awning", new Vector3(0f, 4.05f, 26.85f), new Vector3(7.5f, 0.22f, 2.2f), metal, parent);
        CreateWarningBeacon("Entrance Warning Lamp", new Vector3(0f, 5.35f, 27.05f), amber, parent);

        for (int i = -3; i <= 3; i++)
            CreateBox($"Concrete Barrier {i + 4:00}", new Vector3(i * 4.5f, 0.55f, 26.2f), new Vector3(3.2f, 1.1f, 0.8f), barrier, parent);
    }

    private static void BuildGuardTower(Transform parent, Material wall, Material fence, Material metal, Material window, Material amber)
    {
        Vector3 center = new Vector3(-20.5f, 0f, 29.5f);
        Vector3[] legs =
        {
            center + new Vector3(-1.4f, 3.2f, -1.4f), center + new Vector3(1.4f, 3.2f, -1.4f),
            center + new Vector3(-1.4f, 3.2f, 1.4f), center + new Vector3(1.4f, 3.2f, 1.4f)
        };
        for (int i = 0; i < legs.Length; i++)
            CreateBox($"Tower Leg {i + 1}", legs[i], new Vector3(0.28f, 6.4f, 0.28f), metal, parent);

        CreateBox("Tower Platform", center + new Vector3(0f, 6.4f, 0f), new Vector3(4.6f, 0.3f, 4.6f), metal, parent);
        CreateBox("Tower Cabin", center + new Vector3(0f, 8.0f, 0f), new Vector3(4f, 3f, 4f), wall, parent);
        CreateBox("Tower Front Window", center + new Vector3(0f, 8.3f, -2.03f), new Vector3(2.5f, 1.25f, 0.12f), window, parent);
        CreateBox("Tower Roof", center + new Vector3(0f, 9.65f, 0f), new Vector3(5f, 0.35f, 5f), metal, parent);
        CreateBox("Tower Railing Front", center + new Vector3(0f, 7.15f, -2.25f), new Vector3(4.8f, 0.10f, 0.10f), fence, parent);
        CreateBox("Tower Railing Left", center + new Vector3(-2.25f, 7.15f, 0f), new Vector3(0.10f, 0.10f, 4.8f), fence, parent);
        CreateWarningBeacon("Tower Amber Lamp", center + new Vector3(0f, 9.0f, -2.18f), amber, parent);
    }

    private static void BuildMaintenanceShed(Transform parent, Material wall, Material metal, Material window, Material amber)
    {
        Vector3 center = new Vector3(20.5f, 0f, -28.3f);
        CreateBox("Shed Body", center + new Vector3(0f, 2f, 0f), new Vector3(7f, 4f, 5f), wall, parent);
        CreateBox("Corrugated Roof", center + new Vector3(0f, 4.2f, 0f), new Vector3(7.8f, 0.25f, 5.8f), metal, parent);
        CreateBox("Shed Door", center + new Vector3(-1.3f, 1.55f, 2.55f), new Vector3(2.2f, 3.1f, 0.15f), metal, parent);
        CreateBox("Shed Window", center + new Vector3(1.7f, 2.25f, 2.56f), new Vector3(1.7f, 1.25f, 0.12f), window, parent);
        CreateWarningBeacon("Shed Lamp", center + new Vector3(0f, 3.65f, 2.68f), amber, parent);
    }

    private static void BuildEdgeDetails(Transform parent, Material metal, Material wood, Material weeds, Material barrier, Material dark)
    {
        CreateBasketballHoop("North Basketball Hoop", new Vector3(-16f, 0f, 21.8f), 180f, metal, barrier, parent);
        CreateBasketballHoop("South Basketball Hoop", new Vector3(16f, 0f, -21.8f), 0f, metal, barrier, parent);

        Vector3[] barrelPositions =
        {
            new(21.5f, 0.75f, -22.5f), new(22.5f, 0.75f, -21.1f), new(-22.4f, 0.75f, 20.5f)
        };
        for (int i = 0; i < barrelPositions.Length; i++)
            CreateCylinder($"Weathered Barrel {i + 1}", barrelPositions[i], 0.7f, 1.5f, metal, parent);

        for (int p = 0; p < 3; p++)
        {
            Vector3 basePos = new Vector3(-22f + p * 2.3f, 0.15f, -22.2f);
            CreateBox($"Pallet {p + 1} Deck", basePos + new Vector3(0f, 0.2f, 0f), new Vector3(1.8f, 0.18f, 1.25f), wood, parent);
            CreateBox($"Pallet {p + 1} Runner A", basePos + new Vector3(-0.6f, 0.05f, 0f), new Vector3(0.14f, 0.3f, 1.25f), wood, parent);
            CreateBox($"Pallet {p + 1} Runner B", basePos + new Vector3(0.6f, 0.05f, 0f), new Vector3(0.14f, 0.3f, 1.25f), wood, parent);
        }

        Vector3[] barrierPositions =
        {
            new(-20f, 0.48f, -25.9f), new(-15.7f, 0.48f, -25.9f), new(14.5f, 0.48f, 25.8f), new(19f, 0.48f, 25.8f)
        };
        for (int i = 0; i < barrierPositions.Length; i++)
            CreateBox($"Loose Concrete Barrier {i + 1}", barrierPositions[i], new Vector3(3.5f, 0.95f, 0.75f), barrier, parent);

        Vector3[] weedPatches =
        {
            new(-22f, 0.18f, -14f), new(-21f, 0.18f, 8f), new(-14f, 0.18f, 22f),
            new(19f, 0.18f, 18f), new(22f, 0.18f, 5f), new(13f, 0.18f, -22f),
            new(22f, 0.18f, -16f), new(-18f, 0.18f, -21f)
        };
        for (int i = 0; i < weedPatches.Length; i++)
        {
            Transform patch = NewGroup($"Weed Patch {i + 1:00}", parent);
            for (int blade = 0; blade < 5; blade++)
            {
                float angle = blade * 36f - 72f;
                GameObject stem = CreateBox($"Blade {blade + 1}", weedPatches[i], new Vector3(0.045f, 0.5f + blade * 0.04f, 0.045f), weeds, patch);
                stem.transform.rotation = Quaternion.Euler(0f, blade * 67f, angle);
            }
        }

        CreateBox("Abandoned Cart Bed", new Vector3(19.5f, 0.85f, 24.8f), new Vector3(3.2f, 0.35f, 1.8f), metal, parent);
        CreateCylinder("Cart Wheel A", new Vector3(18.4f, 0.55f, 23.85f), 0.45f, 0.25f, dark, parent).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        CreateCylinder("Cart Wheel B", new Vector3(20.6f, 0.55f, 23.85f), 0.45f, 0.25f, dark, parent).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void BuildV2ArtPass(Transform parent, Material concrete, Material wall, Material window,
        Material fence, Material metal, Material wood, Material weeds, Material dryGrass, Material barrier,
        Material damp, Material cloth, Material road, Material paper, Material ivy, Material amber)
    {
        Transform architecture = NewGroup("Deeper Prison Architecture", parent);
        BuildJailExpansion(architecture, wall, window, fence, metal, barrier, amber);

        Transform yardWear = NewGroup("Aged Concrete Surface", parent);
        BuildConcreteWear(yardWear, concrete, damp, metal);

        Transform overgrowth = NewGroup("Edge Overgrowth", parent);
        BuildOvergrowth(overgrowth, ivy, weeds, dryGrass, wood);

        Transform story = NewGroup("Abandoned Yard Stories", parent);
        BuildAbandonedYardStory(story, metal, wood, barrier, cloth, paper, window, amber);

        Transform roadTeaser = NewGroup("Road Beyond South Gate", parent);
        BuildRoadTeaser(roadTeaser, road, metal, barrier, weeds, wood);
    }

    private static void BuildJailExpansion(Transform parent, Material wall, Material window, Material fence,
        Material metal, Material barrier, Material amber)
    {
        // The side wings and rooftop structures all sit behind the north fence so the learning arena remains untouched.
        CreateBox("West Cell Wing", new Vector3(-21f, 4.0f, 37f), new Vector3(8f, 8f, 10f), wall, parent);
        CreateBox("West Wing Roof", new Vector3(-21f, 8.2f, 37f), new Vector3(8.6f, 0.35f, 10.6f), barrier, parent);
        CreateBox("East Administration Wing", new Vector3(21f, 4.5f, 37f), new Vector3(9f, 9f, 10f), wall, parent);
        CreateBox("East Wing Roof", new Vector3(21f, 9.2f, 37f), new Vector3(9.6f, 0.35f, 10.6f), barrier, parent);

        CreateBox("Rooftop Stairwell", new Vector3(0f, 11.15f, 32.6f), new Vector3(8.4f, 3.7f, 4.8f), wall, parent);
        CreateBox("Stairwell Roof", new Vector3(0f, 13.15f, 32.6f), new Vector3(9f, 0.28f, 5.3f), metal, parent);
        CreateBox("Stairwell Door", new Vector3(0f, 11.1f, 30.15f), new Vector3(1.8f, 2.8f, 0.14f), metal, parent);
        CreateBox("Stairwell Vent", new Vector3(2.8f, 11.55f, 30.12f), new Vector3(1.6f, 1.1f, 0.16f), window, parent);

        float[] pilasterX = { -16.5f, -4f, 4f, 16.5f };
        foreach (float x in pilasterX)
            CreateBox($"Facade Pilaster {x}", new Vector3(x, 4.6f, 28.28f), new Vector3(0.48f, 8.9f, 0.42f), barrier, parent);

        CreateBox("Lower Facade Band", new Vector3(0f, 4.55f, 28.18f), new Vector3(34.4f, 0.28f, 0.38f), barrier, parent);
        CreateBox("Upper Facade Band", new Vector3(0f, 8.65f, 28.18f), new Vector3(34.4f, 0.22f, 0.38f), barrier, parent);
        CreateBox("Damp Foundation Band", new Vector3(0f, 0.38f, 28.15f), new Vector3(34.2f, 0.76f, 0.30f), metal, parent);

        Transform catwalk = NewGroup("Second Floor Service Catwalk", parent);
        CreateBox("Catwalk Deck", new Vector3(0f, 5.1f, 27.25f), new Vector3(28.5f, 0.18f, 1.25f), metal, catwalk);
        CreateBox("Catwalk Front Rail", new Vector3(0f, 6.05f, 26.65f), new Vector3(28.5f, 0.08f, 0.08f), fence, catwalk);
        for (int i = 0; i <= 9; i++)
        {
            float x = -14.1f + i * 3.15f;
            CreateBox($"Catwalk Rail Post {i + 1:00}", new Vector3(x, 5.62f, 26.65f), new Vector3(0.07f, 1.05f, 0.07f), fence, catwalk);
        }
        for (int step = 0; step < 8; step++)
        {
            float t = step / 7f;
            CreateBox($"East Catwalk Step {step + 1:00}", new Vector3(14.7f + t * 3.6f, 4.75f - t * 4.0f, 27.25f),
                new Vector3(0.72f, 0.16f, 1.15f), metal, catwalk);
        }
        CreateBranch("East Stair Rail", new Vector3(14.55f, 5.85f, 26.66f), new Vector3(18.4f, 1.8f, 26.66f), 0.055f, fence, catwalk);

        float[] pipeX = { -15.4f, 12.7f };
        foreach (float x in pipeX)
        {
            CreateCylinder($"Facade Drain Pipe {x}", new Vector3(x, 3.9f, 27.98f), 0.09f, 7.7f, metal, parent);
            CreateBranch($"Drain Elbow {x}", new Vector3(x, 0.20f, 27.98f), new Vector3(x + Mathf.Sign(x) * 0.75f, 0.20f, 27.65f), 0.09f, metal, parent);
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 center = new Vector3(-8f + i * 8f, 9.7f, 32f + (i % 2) * 0.8f);
            CreateBox($"Rooftop Air Handler {i + 1}", center, new Vector3(2.4f, 1.0f, 1.8f), metal, parent);
            CreateCylinder($"Rooftop Vent {i + 1}", center + new Vector3(0f, 0.85f, 0f), 0.35f, 0.7f, metal, parent);
        }

        BuildDamagedWatchTower(parent, wall, window, fence, metal, amber);

        Transform northwestDetails = NewGroup("Northwest Tower Additions", parent);
        CreateBranch("Tower Ladder Left", new Vector3(-22.05f, 0.3f, 27.95f), new Vector3(-22.05f, 6.7f, 27.95f), 0.055f, metal, northwestDetails);
        CreateBranch("Tower Ladder Right", new Vector3(-21.55f, 0.3f, 27.95f), new Vector3(-21.55f, 6.7f, 27.95f), 0.055f, metal, northwestDetails);
        for (int i = 0; i < 12; i++)
            CreateBox($"Tower Ladder Rung {i + 1:00}", new Vector3(-21.8f, 0.55f + i * 0.52f, 27.95f), new Vector3(0.58f, 0.055f, 0.055f), metal, northwestDetails);
        CreateBranch("Tower Brace A", new Vector3(-22f, 0.5f, 28.2f), new Vector3(-19f, 6.2f, 28.2f), 0.08f, metal, northwestDetails);
        CreateBranch("Tower Brace B", new Vector3(-19f, 0.5f, 28.2f), new Vector3(-22f, 6.2f, 28.2f), 0.08f, metal, northwestDetails);
        CreateCylinder("Searchlight Housing", new Vector3(-20.5f, 9.35f, 27.15f), 0.38f, 0.7f, metal, northwestDetails).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        CreateCylinder("Searchlight Lens", new Vector3(-20.5f, 9.35f, 26.78f), 0.30f, 0.06f, amber, northwestDetails).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void BuildDamagedWatchTower(Transform parent, Material wall, Material window, Material fence,
        Material metal, Material amber)
    {
        Transform tower = NewGroup("Damaged Northeast Watchtower", parent);
        Vector3 center = new Vector3(20.5f, 0f, 29.5f);
        Vector3[] legs =
        {
            center + new Vector3(-1.45f, 3.15f, -1.45f), center + new Vector3(1.45f, 3.15f, -1.45f),
            center + new Vector3(-1.45f, 3.15f, 1.45f), center + new Vector3(1.45f, 2.25f, 1.45f)
        };
        for (int i = 0; i < legs.Length; i++)
            CreateBox($"Damaged Tower Leg {i + 1}", legs[i], new Vector3(0.28f, i == 3 ? 4.5f : 6.3f, 0.28f), metal, tower);

        CreateBox("Damaged Tower Platform", center + new Vector3(0f, 6.25f, 0f), new Vector3(4.8f, 0.28f, 4.8f), metal, tower);
        CreateBox("Damaged Cabin Back", center + new Vector3(0f, 7.75f, 1.85f), new Vector3(4f, 2.7f, 0.22f), wall, tower);
        CreateBox("Damaged Cabin Left", center + new Vector3(-1.88f, 7.75f, 0f), new Vector3(0.22f, 2.7f, 3.8f), wall, tower);
        CreateBox("Damaged Front Window", center + new Vector3(-0.45f, 8.05f, -1.88f), new Vector3(2.5f, 1.15f, 0.12f), window, tower);
        GameObject roofHalf = CreateBox("Collapsed Roof Half", center + new Vector3(-0.85f, 9.35f, 0.35f), new Vector3(3.3f, 0.28f, 4.8f), metal, tower);
        roofHalf.transform.rotation = Quaternion.Euler(0f, 0f, -7f);
        CreateBox("Broken Front Rail", center + new Vector3(-0.85f, 7.0f, -2.3f), new Vector3(2.8f, 0.08f, 0.08f), fence, tower).transform.rotation = Quaternion.Euler(0f, 0f, 8f);
        CreateWarningBeacon("Faint Northeast Lamp", center + new Vector3(-0.8f, 8.9f, -2.02f), amber, tower);
    }

    private static void BuildConcreteWear(Transform parent, Material concrete, Material damp, Material metal)
    {
        float[] seamCoordinates = { -15f, -5f, 5f, 15f };
        foreach (float coordinate in seamCoordinates)
        {
            CreateBox($"North South Slab Seam {coordinate}", new Vector3(coordinate, 0.032f, 0f), new Vector3(0.035f, 0.012f, 48f), metal, parent);
            CreateBox($"East West Slab Seam {coordinate}", new Vector3(0f, 0.033f, coordinate), new Vector3(48f, 0.012f, 0.035f), metal, parent);
        }

        Vector3[] patchCenters =
        {
            new(-16f, 0.042f, -12f), new(13f, 0.042f, 13f),
            new(4f, 0.042f, -17f), new(-18f, 0.042f, 17f)
        };
        Vector2[] patchSizes = { new(5.2f, 2.7f), new(3.9f, 3.2f), new(4.6f, 2.4f), new(3.5f, 4.1f) };
        float[] patchAngles = { 12f, -18f, -7f, 23f };
        for (int i = 0; i < patchCenters.Length; i++)
        {
            GameObject patch = CreateBox($"Old Concrete Repair {i + 1}", patchCenters[i],
                new Vector3(patchSizes[i].x, 0.014f, patchSizes[i].y), damp, parent);
            patch.transform.rotation = Quaternion.Euler(0f, patchAngles[i], 0f);
            CreateBox($"Repair Edge {i + 1}", patchCenters[i] + new Vector3(patchSizes[i].x * 0.2f, 0.008f, 0f),
                new Vector3(patchSizes[i].x * 0.75f, 0.010f, 0.035f), metal, parent).transform.rotation = Quaternion.Euler(0f, patchAngles[i] + 11f, 0f);
        }

        Vector3[] dampCenters =
        {
            new(-21.5f, 0.032f, 7f), new(21f, 0.032f, -4f), new(-9f, 0.032f, 21.5f), new(15f, 0.032f, -21f)
        };
        for (int i = 0; i < dampCenters.Length; i++)
        {
            GameObject stain = CreateCylinder($"Damp Stain {i + 1}", dampCenters[i], 1.5f + i * 0.22f, 0.014f, damp, parent);
            stain.transform.localScale = new Vector3(1.3f + i * 0.15f, 0.007f, 0.65f + i * 0.08f);
            stain.transform.rotation = Quaternion.Euler(0f, i * 31f, 0f);
        }

        // Small concrete overlays create missing, weathered sections in the old court paint without replacing its layout.
        Vector3[] paintWear =
        {
            new(-8.5f, 0.055f, 16f), new(5.5f, 0.055f, 16f), new(12.5f, 0.055f, 7f),
            new(-12.5f, 0.055f, -6f), new(6.5f, 0.055f, -16f), new(-2f, 0.055f, 0f)
        };
        for (int i = 0; i < paintWear.Length; i++)
        {
            Vector3 scale = (i == 2 || i == 3) ? new Vector3(0.28f, 0.012f, 2.0f) : new Vector3(2.0f, 0.012f, 0.28f);
            CreateBox($"Court Paint Wear {i + 1}", paintWear[i], scale, concrete, parent);
        }
    }

    private static void BuildOvergrowth(Transform parent, Material ivy, Material weeds, Material dryGrass, Material wood)
    {
        Transform wallIvy = NewGroup("Dead Ivy On Cell Block", parent);
        CreateIvyQuad("West Facade Ivy Mass", new Vector3(-11.5f, 5.15f, 28.02f), new Vector2(9.2f, 6.2f), Quaternion.identity, ivy, wallIvy);
        CreateIvyQuad("West Facade Ivy Crown", new Vector3(-13.5f, 7.8f, 28.0f), new Vector2(5.6f, 3.9f), Quaternion.identity, ivy, wallIvy);
        CreateIvyQuad("East Facade Ivy", new Vector3(9.5f, 4.5f, 28.0f), new Vector2(5.5f, 4.8f), Quaternion.identity, ivy, wallIvy);

        Transform fenceVines = NewGroup("Fence Vines", parent);
        CreateIvyQuad("Southwest Fence Vine", new Vector3(-20.5f, 2.1f, -24.72f), new Vector2(5.8f, 4.0f), Quaternion.identity, ivy, fenceVines);
        CreateIvyQuad("Northeast Fence Vine", new Vector3(18.5f, 2.0f, 24.72f), new Vector2(5.2f, 3.8f), Quaternion.identity, ivy, fenceVines);
        CreateIvyQuad("East Fence Vine", new Vector3(24.72f, 2.15f, 12.5f), new Vector2(6.0f, 4.1f), Quaternion.Euler(0f, 90f, 0f), ivy, fenceVines);

        Mesh clumpMesh = GetOrCreateDryWeedMesh();
        Vector3[] weedPositions =
        {
            new(-23f, 0.02f, -19f), new(-21f, 0.02f, -11f), new(-22.5f, 0.02f, -4f),
            new(-22f, 0.02f, 5f), new(-23f, 0.02f, 13f), new(-21f, 0.02f, 20f),
            new(-16f, 0.02f, 22f), new(-9f, 0.02f, 23f), new(8f, 0.02f, 22.5f),
            new(15f, 0.02f, 22f), new(21f, 0.02f, 20f), new(22.5f, 0.02f, 13f),
            new(22f, 0.02f, 5f), new(23f, 0.02f, -3f), new(22f, 0.02f, -11f),
            new(22.5f, 0.02f, -19f), new(16f, 0.02f, -22f), new(9f, 0.02f, -23f),
            new(-9f, 0.02f, -22.5f), new(-16f, 0.02f, -22f), new(-19.2f, 0.02f, 18.2f),
            new(19.5f, 0.02f, -18.5f), new(-18.5f, 0.02f, -19.5f), new(18.5f, 0.02f, 19.2f)
        };
        for (int i = 0; i < weedPositions.Length; i++)
        {
            Material material = i % 3 == 0 ? weeds : dryGrass;
            GameObject clump = CreateMeshObject($"Dry Weed Clump {i + 1:00}", clumpMesh, material, weedPositions[i],
                Quaternion.Euler(0f, (i * 47f) % 360f, 0f), parent);
            float scale = 0.65f + (i % 5) * 0.11f;
            clump.transform.localScale = new Vector3(scale, scale * (0.88f + (i % 3) * 0.12f), scale);
            MeshRenderer renderer = clump.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        CreateDeadTree("Dead Sycamore West", new Vector3(-31f, 0f, -10f), 1.05f, wood, parent);
        CreateDeadTree("Dead Sycamore East", new Vector3(31f, 0f, 11f), 0.9f, wood, parent);
    }

    private static void BuildAbandonedYardStory(Transform parent, Material metal, Material wood, Material barrier,
        Material cloth, Material paper, Material dark, Material amber)
    {
        Transform bleachers = NewGroup("Rusted West Bleachers", parent);
        for (int row = 0; row < 3; row++)
        {
            CreateBox($"Bleacher Seat {row + 1}", new Vector3(-21.8f, 0.65f + row * 0.55f, -3.5f + row * 0.65f),
                new Vector3(5.4f, 0.16f, 0.65f), wood, bleachers);
            CreateBox($"Bleacher Support A {row + 1}", new Vector3(-23.7f, 0.34f + row * 0.28f, -3.5f + row * 0.65f),
                new Vector3(0.12f, 0.8f + row * 0.55f, 0.12f), metal, bleachers);
            CreateBox($"Bleacher Support B {row + 1}", new Vector3(-19.9f, 0.34f + row * 0.28f, -3.5f + row * 0.65f),
                new Vector3(0.12f, 0.8f + row * 0.55f, 0.12f), metal, bleachers);
        }

        Transform eastDebris = NewGroup("East Side Abandonment", parent);
        GameObject table = CreateBox("Overturned Meal Table", new Vector3(21.5f, 0.72f, 3.5f), new Vector3(4.2f, 0.18f, 1.6f), metal, eastDebris);
        table.transform.rotation = Quaternion.Euler(0f, 17f, 18f);
        CreateBranch("Table Leg A", new Vector3(20.2f, 0.7f, 3.2f), new Vector3(19.8f, 1.9f, 3.0f), 0.09f, metal, eastDebris);
        CreateBranch("Table Leg B", new Vector3(22.7f, 0.8f, 3.8f), new Vector3(23.1f, 1.9f, 4.0f), 0.09f, metal, eastDebris);
        GameObject mattress = CreateBox("Discarded Mattress", new Vector3(21f, 0.15f, -7.4f), new Vector3(2.6f, 0.22f, 5.2f), cloth, eastDebris);
        mattress.transform.rotation = Quaternion.Euler(0f, -11f, 3f);
        CreateBox("Blanket Fold", new Vector3(21.3f, 0.35f, -6.9f), new Vector3(2.2f, 0.10f, 2.4f), cloth, eastDebris).transform.rotation = Quaternion.Euler(0f, -19f, 7f);
        for (int i = 0; i < 8; i++)
        {
            GameObject sheet = CreateBox($"Loose Paper {i + 1:00}", new Vector3(18.8f + (i % 4) * 0.9f, 0.06f, -1.0f + (i / 4) * 1.1f),
                new Vector3(0.55f, 0.012f, 0.38f), paper, eastDebris);
            sheet.transform.rotation = Quaternion.Euler(i % 2 == 0 ? 4f : -3f, i * 31f, i % 3 == 0 ? 6f : 0f);
        }

        Transform utility = NewGroup("Southeast Utility Debris", parent);
        CreateBox("Open Toolbox Base", new Vector3(18.8f, 0.35f, -20.6f), new Vector3(1.8f, 0.55f, 0.8f), metal, utility);
        GameObject toolboxLid = CreateBox("Open Toolbox Lid", new Vector3(18.8f, 0.85f, -21.0f), new Vector3(1.8f, 0.10f, 0.8f), metal, utility);
        toolboxLid.transform.rotation = Quaternion.Euler(-60f, 0f, 0f);
        CreateCylinder("Cable Spool Left", new Vector3(21.8f, 0.75f, -19.8f), 0.75f, 0.22f, wood, utility).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        CreateCylinder("Cable Spool Right", new Vector3(23.0f, 0.75f, -19.8f), 0.75f, 0.22f, wood, utility).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        CreateCylinder("Cable Spool Core", new Vector3(22.4f, 0.75f, -19.8f), 0.28f, 1.2f, dark, utility).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        CreateBranch("Loose Cable", new Vector3(21.8f, 0.09f, -19.5f), new Vector3(18.4f, 0.08f, -18.0f), 0.035f, dark, utility);

        Transform entrance = NewGroup("Entrance Barricade Story", parent);
        CreateBox("Abandoned Handcart", new Vector3(-5.6f, 0.65f, 25.8f), new Vector3(2.7f, 0.28f, 1.3f), metal, entrance).transform.rotation = Quaternion.Euler(0f, 17f, 4f);
        CreateCylinder("Handcart Wheel A", new Vector3(-6.4f, 0.42f, 25.2f), 0.38f, 0.18f, dark, entrance).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        CreateCylinder("Handcart Wheel B", new Vector3(-4.8f, 0.42f, 25.7f), 0.38f, 0.18f, dark, entrance).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        CreateBox("Bent Entrance Sign", new Vector3(6.2f, 1.15f, 26.1f), new Vector3(2.8f, 1.25f, 0.12f), barrier, entrance).transform.rotation = Quaternion.Euler(0f, -8f, 7f);
        CreateWarningBeacon("Hanging Entrance Lamp", new Vector3(3.0f, 4.5f, 26.5f), amber, entrance);
    }

    private static void BuildRoadTeaser(Transform parent, Material road, Material metal, Material barrier, Material weeds, Material wood)
    {
        CreateBox("Cracked Access Road", new Vector3(0f, -0.055f, -45f), new Vector3(8.5f, 0.11f, 40f), road, parent);
        CreateBox("Faded Road Center Mark", new Vector3(0f, 0.012f, -45f), new Vector3(0.13f, 0.018f, 29f), barrier, parent);
        for (int i = 0; i < 6; i++)
        {
            float z = -30f - i * 6f;
            GameObject tireMark = CreateBox($"Gate Tire Mark {i + 1}", new Vector3(i % 2 == 0 ? -1.5f : 1.5f, 0.016f, z),
                new Vector3(0.16f, 0.014f, 4.2f), metal, parent);
            tireMark.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? 2.5f : -3f, 0f);
        }

        CreateBox("Gate Checkpoint Post", new Vector3(-6.5f, 1.35f, -29f), new Vector3(0.25f, 2.7f, 0.25f), metal, parent);
        CreateBox("Gate Warning Board", new Vector3(-6.5f, 2.3f, -28.9f), new Vector3(2.8f, 1.1f, 0.15f), barrier, parent).transform.rotation = Quaternion.Euler(0f, 8f, -5f);
        CreateBranch("Sagging Gate Chain", new Vector3(-4.8f, 1.2f, -27.5f), new Vector3(4.8f, 0.65f, -27.5f), 0.045f, metal, parent);

        Vector3[] poleBases = { new(9.5f, 0f, -31f), new(9.5f, 0f, -45f), new(9.5f, 0f, -59f) };
        for (int i = 0; i < poleBases.Length; i++)
        {
            CreateCylinder($"Road Utility Pole {i + 1}", poleBases[i] + new Vector3(0f, 3.8f, 0f), 0.14f, 7.6f, wood, parent);
            CreateBox($"Road Crossarm {i + 1}", poleBases[i] + new Vector3(0f, 7.15f, 0f), new Vector3(2.3f, 0.14f, 0.14f), wood, parent);
            if (i < poleBases.Length - 1)
            {
                CreateBranch($"Road Wire A {i + 1}", poleBases[i] + new Vector3(-0.9f, 7.18f, 0f), poleBases[i + 1] + new Vector3(-0.9f, 7.05f, 0f), 0.025f, metal, parent);
                CreateBranch($"Road Wire B {i + 1}", poleBases[i] + new Vector3(0.9f, 7.18f, 0f), poleBases[i + 1] + new Vector3(0.9f, 7.05f, 0f), 0.025f, metal, parent);
            }
        }

        Mesh weedMesh = GetOrCreateDryWeedMesh();
        for (int i = 0; i < 12; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 position = new Vector3(side * (5.3f + (i % 3)), 0f, -29f - i * 3f);
            GameObject clump = CreateMeshObject($"Road Shoulder Weed {i + 1:00}", weedMesh, weeds, position,
                Quaternion.Euler(0f, i * 41f, 0f), parent);
            clump.transform.localScale = Vector3.one * (0.75f + (i % 4) * 0.12f);
        }

        CreateDeadTree("Roadline Dead Tree", new Vector3(-12f, 0f, -48f), 0.8f, wood, parent);
    }

    private static void CreateIvyQuad(string name, Vector3 position, Vector2 size, Quaternion rotation, Material material, Transform parent)
    {
        if (material == null || material.GetTexture("_BaseMap") == null)
            return;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent);
        quad.transform.SetPositionAndRotation(position, rotation);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        RemoveCollider(quad);
    }

    private static void CreateDeadTree(string name, Vector3 basePosition, float scale, Material wood, Transform parent)
    {
        Transform tree = NewGroup(name, parent);
        CreateBranch("Trunk", basePosition, basePosition + new Vector3(0.25f, 7.5f * scale, 0.15f), 0.38f * scale, wood, tree);
        Vector3 crown = basePosition + new Vector3(0.25f, 5.4f * scale, 0.15f);
        CreateBranch("Branch West", crown, crown + new Vector3(-2.5f * scale, 2.4f * scale, 0.4f), 0.16f * scale, wood, tree);
        CreateBranch("Branch East", crown + new Vector3(0f, 0.8f * scale, 0f), crown + new Vector3(2.4f * scale, 2.8f * scale, -0.3f), 0.15f * scale, wood, tree);
        CreateBranch("Branch Rear", crown + new Vector3(0f, 1.2f * scale, 0f), crown + new Vector3(-0.4f, 3.2f * scale, 2.1f * scale), 0.13f * scale, wood, tree);
        CreateBranch("West Twig", crown + new Vector3(-2.0f * scale, 1.9f * scale, 0.35f), crown + new Vector3(-3.2f * scale, 3.2f * scale, 0.8f), 0.07f * scale, wood, tree);
        CreateBranch("East Twig", crown + new Vector3(1.8f * scale, 2.2f * scale, -0.25f), crown + new Vector3(3.0f * scale, 3.4f * scale, -0.8f), 0.07f * scale, wood, tree);
    }

    private static GameObject CreateBranch(string name, Vector3 start, Vector3 end, float radius, Material material, Transform parent)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        GameObject branch = CreateCylinder(name, (start + end) * 0.5f, radius, length, material, parent);
        if (length > 0.0001f)
            branch.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction / length);
        return branch;
    }

    private static Mesh GetOrCreateDryWeedMesh()
    {
        string path = ArtFolder + "/DryWeedClump.asset";
        List<Vector3> vertices = new();
        List<int> triangles = new();
        for (int blade = 0; blade < 7; blade++)
        {
            float angle = blade * 137.5f * Mathf.Deg2Rad;
            float distance = (blade % 3) * 0.08f;
            Vector3 center = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            Vector3 side = new Vector3(Mathf.Cos(angle + Mathf.PI * 0.5f), 0f, Mathf.Sin(angle + Mathf.PI * 0.5f)) * (0.065f + (blade % 2) * 0.025f);
            Vector3 tip = center + new Vector3(Mathf.Cos(angle) * 0.16f, 0.75f + (blade % 4) * 0.16f, Mathf.Sin(angle) * 0.16f);
            int start = vertices.Count;
            vertices.Add(center - side);
            vertices.Add(center + side);
            vertices.Add(tip);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool createAsset = mesh == null;
        if (createAsset)
            mesh = new Mesh { name = "Dry Weed Clump" };
        else
            mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (createAsset)
            AssetDatabase.CreateAsset(mesh, path);
        else
            EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void CreateBasketballHoop(string name, Vector3 basePosition, float yaw, Material metal, Material board, Transform parent)
    {
        Transform group = NewGroup(name, parent);
        group.position = basePosition;
        group.rotation = Quaternion.Euler(0f, yaw, 0f);
        CreateBox("Pole", new Vector3(0f, 2.2f, 0f), new Vector3(0.16f, 4.4f, 0.16f), metal, group, true);
        CreateBox("Support", new Vector3(0f, 4.05f, 0.6f), new Vector3(0.12f, 0.12f, 1.25f), metal, group, true);
        CreateBox("Backboard", new Vector3(0f, 4.15f, 1.15f), new Vector3(2.1f, 1.35f, 0.13f), board, group, true);
        CreateBox("Rim", new Vector3(0f, 3.75f, 1.72f), new Vector3(0.85f, 0.08f, 0.08f), metal, group, true);
    }

    private static void CreateBarredWindow(string name, Vector3 position, Material window, Material metal, Transform parent)
    {
        Transform group = NewGroup(name, parent);
        CreateBox("Recess", position, new Vector3(1.7f, 1.9f, 0.12f), window, group);
        for (int i = -2; i <= 2; i++)
            CreateBox($"Vertical Bar {i + 3}", position + new Vector3(i * 0.32f, 0f, -0.09f), new Vector3(0.055f, 1.85f, 0.055f), metal, group);
        CreateBox("Horizontal Bar", position + new Vector3(0f, 0f, -0.09f), new Vector3(1.65f, 0.055f, 0.055f), metal, group);
    }

    private static void CreateWarningBeacon(string name, Vector3 position, Material material, Transform parent)
    {
        Transform group = NewGroup(name, parent);
        CreateBox("Mount", position + new Vector3(0f, -0.15f, 0f), new Vector3(0.42f, 0.20f, 0.42f), material, group);
        CreateCylinder("Amber Lens", position, 0.17f, 0.32f, material, group);

        GameObject lightObject = new GameObject("Warm Point Light");
        lightObject.transform.SetParent(group);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.35f, 0.06f);
        light.intensity = 1.6f;
        light.range = 5.5f;
        light.shadows = LightShadows.None;
    }

    private static Mesh GetOrCreateChainLinkMesh()
    {
        string path = ArtFolder + "/ChainLinkPanel.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
            return existing;

        const float width = 5f;
        const float height = 4f;
        const float spacing = 0.42f;
        const float thickness = 0.024f;
        List<Vector3> vertices = new();
        List<int> triangles = new();

        for (float start = -width * 0.5f - height; start <= width * 0.5f; start += spacing)
        {
            float y0 = Mathf.Max(0f, -width * 0.5f - start);
            float y1 = Mathf.Min(height, width * 0.5f - start);
            if (y1 > y0)
                AddMeshStrip(vertices, triangles, new Vector2(start + y0, y0), new Vector2(start + y1, y1), thickness);
        }

        for (float start = -width * 0.5f; start <= width * 0.5f + height; start += spacing)
        {
            float y0 = Mathf.Max(0f, start - width * 0.5f);
            float y1 = Mathf.Min(height, start + width * 0.5f);
            if (y1 > y0)
                AddMeshStrip(vertices, triangles, new Vector2(start - y0, y0), new Vector2(start - y1, y1), thickness);
        }

        Mesh mesh = new Mesh { name = "Chain Link Panel 5x4" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void AddMeshStrip(List<Vector3> vertices, List<int> triangles, Vector2 a, Vector2 b, float thickness)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 offset = new Vector2(-direction.y, direction.x) * thickness;
        int start = vertices.Count;
        vertices.Add(new Vector3(a.x + offset.x, a.y + offset.y, 0f));
        vertices.Add(new Vector3(a.x - offset.x, a.y - offset.y, 0f));
        vertices.Add(new Vector3(b.x - offset.x, b.y - offset.y, 0f));
        vertices.Add(new Vector3(b.x + offset.x, b.y + offset.y, 0f));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start);
        triangles.Add(start + 3); triangles.Add(start + 2); triangles.Add(start);
    }

    private static Material GetOrCreateMaterial(string displayName, Color color, float metallic, float smoothness)
    {
        string safeName = displayName.Replace(" ", string.Empty);
        string path = $"{MaterialFolder}/{safeName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new MissingReferenceException("Universal Render Pipeline/Lit shader is unavailable.");
            material = new Material(shader) { name = displayName };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateTexturedMaterial(string displayName, Color tint, float metallic, float smoothness,
        string texturePath, Vector2 tiling)
    {
        Material material = GetOrCreateMaterial(displayName, tint, metallic, smoothness);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureScale("_MainTex", tiling);
        }
        else
        {
            Debug.LogWarning($"Prison Yard texture is missing at {texturePath}; using the material's color fallback.");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateCutoutMaterial(string displayName, string texturePath, Color tint)
    {
        Material material = GetOrCreateMaterial(displayName, tint, 0f, 0.05f);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_MainTex", null);
            Debug.LogWarning($"Prison Yard ivy texture is missing at {texturePath}; ivy billboards will be skipped.");
            return material;
        }

        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.35f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.doubleSidedGI = true;
        material.enableInstancing = true;
        BaseShaderGUI.SetMaterialKeywords(material);
        material.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureDoubleSidedFoliage(Material material)
    {
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.doubleSidedGI = true;
        material.enableInstancing = true;
        BaseShaderGUI.SetMaterialKeywords(material);
        material.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(material);
    }

    private static Material GetOrCreateEmissiveMaterial(string displayName, Color color, float intensity)
    {
        Material material = GetOrCreateMaterial(displayName, color, 0.1f, 0.32f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * intensity);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureTextureImport(string path, bool repeat, bool alphaCutout)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        bool changed = false;
        TextureWrapMode desiredWrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }
        if (importer.wrapMode != desiredWrap)
        {
            importer.wrapMode = desiredWrap;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Trilinear)
        {
            importer.filterMode = FilterMode.Trilinear;
            changed = true;
        }
        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            changed = true;
        }
        if (!importer.sRGBTexture)
        {
            importer.sRGBTexture = true;
            changed = true;
        }

        TextureImporterNPOTScale desiredNpot = alphaCutout ? TextureImporterNPOTScale.None : TextureImporterNPOTScale.ToNearest;
        if (importer.npotScale != desiredNpot)
        {
            importer.npotScale = desiredNpot;
            changed = true;
        }
        if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            changed = true;
        }
        int desiredAniso = alphaCutout ? 2 : (path == YardConcreteTexture ? 8 : 4);
        if (importer.anisoLevel != desiredAniso)
        {
            importer.anisoLevel = desiredAniso;
            changed = true;
        }

        int desiredMaximumSize = alphaCutout ? 2048 : 1024;
        if (importer.maxTextureSize != desiredMaximumSize)
        {
            importer.maxTextureSize = desiredMaximumSize;
            changed = true;
        }
        if (importer.alphaSource != (alphaCutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None))
        {
            importer.alphaSource = alphaCutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            changed = true;
        }
        if (importer.alphaIsTransparency != alphaCutout)
        {
            importer.alphaIsTransparency = alphaCutout;
            changed = true;
        }
        if (importer.mipMapsPreserveCoverage != alphaCutout)
        {
            importer.mipMapsPreserveCoverage = alphaCutout;
            changed = true;
        }
        if (alphaCutout && !Mathf.Approximately(importer.alphaTestReferenceValue, 0.35f))
        {
            importer.alphaTestReferenceValue = 0.35f;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool local = false)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, !local);
        if (local)
            gameObject.transform.localPosition = position;
        else
            gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        RemoveCollider(gameObject);
        return gameObject;
    }

    private static GameObject CreateCylinder(string name, Vector3 position, float radius, float height, Material material, Transform parent)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gameObject.name = name;
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = position;
        gameObject.transform.localScale = new Vector3(radius, height * 0.5f, radius);
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        RemoveCollider(gameObject);
        return gameObject;
    }

    private static GameObject CreateMeshObject(string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent);
        gameObject.transform.SetPositionAndRotation(position, rotation);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return gameObject;
    }

    private static void RemoveCollider(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent);
        return group.transform;
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Split('/').Skip(1))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != TargetScene)
            .ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(TargetScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
