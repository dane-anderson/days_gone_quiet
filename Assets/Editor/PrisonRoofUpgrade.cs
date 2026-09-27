#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Encloses the existing cellblock without regenerating its cells, props or agents.</summary>
public static class PrisonRoofUpgrade
{
    private const string GroupName = "Enclosed Ceiling and Lights";
    private const string MaterialFolder = "Assets/Art/Environment/PrisonInterior/Materials";

    [MenuItem("Days Gone Quiet/Enclose Prison Roof", priority = 21)]
    public static void UpgradeOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before enclosing the prison.");
        PrisonInteriorTour tour = Object.FindAnyObjectByType<PrisonInteriorTour>();
        if (tour == null || tour.gameObject.scene.name != "PrisonYard")
            throw new InvalidOperationException("Open the existing PrisonYard scene with its interior first.");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Enclose Prison Roof");
        ApplyTo(tour);
        Camera camera = tour.views.GetComponent<Camera>();
        Undo.RecordObjects(new Object[] { tour.views, camera, camera.transform }, "Show enclosed cellblock");
        tour.views.ShowCellblock();
        EditorUtility.SetDirty(tour.views);
        EditorSceneManager.MarkSceneDirty(tour.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(tour.gameObject.scene);
        Selection.activeGameObject = camera.gameObject;
        Undo.CollapseUndoOperations(group);
        Debug.Log("PRISON ROOF ENCLOSED: visible concrete ceiling, service pipes, fluorescent fixtures and interior lighting added. View 6 is inside the roof; view 8 is the optional overhead cutaway. Manual tours keep the roof and walls visible.");
    }

    public static void ApplyTo(PrisonInteriorTour tour)
    {
        Transform prior = tour.transform.Find(GroupName);
        if (prior != null) Undo.DestroyObjectImmediate(prior.gameObject);
        Material ceiling = Material("CeilingConcrete", new Color(.46f, .49f, .47f), 0, .13f);
        Material steel = Material("CeilingSteel", new Color(.17f, .21f, .21f), .65f, .24f);
        Material pipe = Material("CeilingServicePipes", new Color(.30f, .27f, .21f), .5f, .19f);
        Material lightGlass = Material("CeilingFluorescentGlass", new Color(.70f, .83f, .76f), .1f, .2f);
        lightGlass.EnableKeyword("_EMISSION");
        lightGlass.SetColor("_EmissionColor", new Color(.60f, .82f, .72f) * 2.5f);
        Texture wallTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/PrisonYard/Textures/PrisonWallConcreteV2.png");
        ceiling.SetTexture("_BaseMap", wallTexture);
        ceiling.SetTextureScale("_BaseMap", new Vector2(5, 3));
        EditorUtility.SetDirty(ceiling);
        EditorUtility.SetDirty(lightGlass);

        Transform root = new GameObject(GroupName).transform;
        root.SetParent(tour.transform, false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add enclosed ceiling");
        // Overlaps the wall tops so grazing camera angles cannot expose a sky seam.
        Box("Visible Concrete Ceiling", new Vector3(0, 8.98f, 38), new Vector3(34, .18f, 20), ceiling, root, true);
        for (int i = 0; i < 5; i++)
        {
            float z = 30.5f + i * 4;
            Box("Ceiling Rib " + (i + 1), new Vector3(0, 8.72f, z), new Vector3(33.4f, .22f, .18f), steel, root);
        }
        foreach (float x in new[] { -15.8f, 15.8f })
        {
            Rod("Overhead Water Main", new Vector3(x, 8.48f, 29.1f), new Vector3(x, 8.48f, 47.2f), .085f, pipe, root);
            for (int i = 0; i < 6; i++)
                Box("Pipe Hanger", new Vector3(x, 8.72f, 30 + i * 3), new Vector3(.04f, .45f, .04f), steel, root);
        }
        foreach (float x in new[] { -8f, 0f, 8f })
        {
            Fixture("Entrance Hall Fluorescent", new Vector3(x, 8.35f, 32.5f), root, steel, lightGlass);
            Fixture("Upper Gallery Fluorescent", new Vector3(x, 8.35f, 40.3f), root, steel, lightGlass);
        }

        // Soft practical lights keep the closed building readable with its roof casting shadows.
        Point("Common Room Ceiling Fill", new Vector3(0, 5.8f, 34.8f), new Color(.68f, .79f, .73f), 4.5f, 15, root);
        Point("Upper Gallery Ceiling Fill", new Vector3(0, 7.8f, 42.5f), new Color(.70f, .81f, .76f), 3f, 16, root);
        for (int bay = 0; bay < 6; bay++)
        {
            float x = -12.5f + bay * 5;
            Box("Lower Cell Ceiling Light", new Vector3(x, 3.85f, 44.6f), new Vector3(.75f, .07f, .22f), lightGlass, root);
            Point("Lower Cell Practical Light", new Vector3(x, 3.55f, 44.6f), new Color(.74f, .80f, .69f), 1.2f, 4.8f, root);
        }

        Undo.RecordObject(tour, "Configure ceiling cutaway");
        tour.exteriorRenderers = (tour.exteriorRenderers ?? new Renderer[0])
            .Where(r => r != null).Concat(root.GetComponentsInChildren<Renderer>()).Distinct().ToArray();
        tour.interiorColliders = tour.GetComponentsInChildren<Collider>(true)
            .Where(c => tour.tourBoundaryColliders == null || !tour.tourBoundaryColliders.Contains(c)).ToArray();
        foreach (Renderer r in tour.exteriorRenderers) r.forceRenderingOff = false;
        EditorUtility.SetDirty(tour);
    }

    private static Material Material(string name, Color color, float metallic, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool solid = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static void Rod(string name, Vector3 from, Vector3 to, float radius, Material material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = (from + to) / 2;
        go.transform.up = (to - from).normalized;
        go.transform.localScale = new Vector3(radius * 2, Vector3.Distance(from, to) / 2, radius * 2);
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static void Fixture(string name, Vector3 p, Transform parent, Material steel, Material glass)
    {
        Box(name + " Housing", p, new Vector3(1.9f, .12f, .5f), steel, parent);
        foreach (float dz in new[] { -.12f, .12f })
            Box(name + " Tube", p + new Vector3(0, -.09f, dz), new Vector3(1.65f, .075f, .085f), glass, parent);
    }
    private static void Point(string name, Vector3 p, Color color, float intensity, float range, Transform parent)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = p;
        Light light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color;
        light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
    }
}
#endif
