#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds decorative walkers. The enclosing prison builder owns replacement of its generated root.</summary>
public static class RestrainedWalkersBuilder
{
    private const string ArtFolder = "Assets/Art/Environment/PrisonInterior";
    private const string MaterialFolder = ArtFolder + "/Materials";
    private const string WalkerSpritePath = "Assets/Art/Characters/Walker.png";

    public static Transform Build(Transform parent)
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WalkerSpritePath);
        if (sprite == null)
            throw new InvalidOperationException("The existing Walker sprite is required at " + WalkerSpritePath);

        EnsureFolder(ArtFolder);
        EnsureFolder(MaterialFolder);
        Material chain = MaterialAsset("RestraintChainSteel", new Color(0.44f, 0.47f, 0.46f), 0.82f, 0.36f);
        Material post = MaterialAsset("RestraintPosts", new Color(0.20f, 0.145f, 0.105f), 0.5f, 0.18f);
        Material footing = MaterialAsset("RestraintFootings", new Color(0.32f, 0.31f, 0.27f), 0f, 0.1f);
        Material yellow = MaterialAsset("RestraintWarningYellow", new Color(0.58f, 0.43f, 0.14f), 0.2f, 0.14f);
        Material dark = MaterialAsset("RestraintWarningDark", new Color(0.065f, 0.067f, 0.059f), 0.15f, 0.1f);
        Mesh linkMesh = ChainLinkMesh();
        Transform group = Group("Restrained Walkers - Decorative", parent, Vector3.zero);

        float[] positions = { -9f, -6f, 6f, 9f };
        for (int i = 0; i < positions.Length; i++)
            BuildWalker(group, positions[i], i, sprite, linkMesh, chain, post, footing, dark);

        BuildWarning(group, -7.5f, yellow, dark, post);
        BuildWarning(group, 7.5f, yellow, dark, post);
        return group;
    }

    private static void BuildWalker(Transform parent, float x, int index, Sprite sprite, Mesh linkMesh,
        Material chainMaterial, Material postMaterial, Material footing, Material dark)
    {
        Transform root = Group("Restrained Walker " + (index + 1) + " - Visual Only", parent, Vector3.zero);
        root.position = new Vector3(x, 0f, 24f);

        Primitive("Concrete Anchor Footing", PrimitiveType.Cube, root, new Vector3(0f, 0.1f, 0.7f),
            new Vector3(0.64f, 0.2f, 0.64f), footing);
        Primitive("Iron Restraint Post", PrimitiveType.Cylinder, root, new Vector3(0f, 1.35f, 0.7f),
            new Vector3(0.17f, 1.35f, 0.17f), postMaterial);
        Primitive("Post Cap", PrimitiveType.Sphere, root, new Vector3(0f, 2.72f, 0.7f),
            new Vector3(0.2f, 0.13f, 0.2f), postMaterial);
        Primitive("Post Crossbar", PrimitiveType.Cube, root, new Vector3(0f, 1.46f, 0.7f),
            new Vector3(0.56f, 0.09f, 0.1f), postMaterial);
        Primitive("Contact Shadow", PrimitiveType.Cylinder, root, new Vector3(0f, 0.012f, 0f),
            new Vector3(0.66f, 0.008f, 0.39f), dark);

        Transform pivot = Group("Fixed Feet - Strain Pivot", root, Vector3.zero);
        Transform body = Group("Restrained Walker Body", pivot, new Vector3(0f, 1.05f, 0f));
        Transform artwork = Group("Existing Walker Artwork", body, Vector3.zero);
        SpriteRenderer renderer = artwork.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = index % 2 == 0 ? Color.white : new Color(0.91f, 0.95f, 0.89f, 1f);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 0;
        artwork.localScale = Vector3.one * (2.2f / Mathf.Max(0.01f, sprite.bounds.size.y));

        // Coordinates follow the existing image: its raised wrist is on the
        // viewer's left. Mesh chains sit just in front of the billboard plane.
        StaticChain("Upper Torso Chain", body, new Vector3(-0.26f, 0.37f, -0.065f),
            new Vector3(0.28f, 0.29f, -0.065f), linkMesh, chainMaterial);
        StaticChain("Lower Torso Chain", body, new Vector3(-0.26f, 0.12f, -0.072f),
            new Vector3(0.27f, 0.055f, -0.072f), linkMesh, chainMaterial);
        StaticChain("Diagonal Harness Chain", body, new Vector3(-0.22f, 0.36f, -0.082f),
            new Vector3(0.24f, 0.06f, -0.082f), linkMesh, chainMaterial);
        Primitive("Harness Lock", PrimitiveType.Cube, body, new Vector3(0.018f, 0.17f, -0.10f),
            new Vector3(0.078f, 0.105f, 0.036f), postMaterial);

        Transform leftWrist = Group("Left Wrist Attachment", body, new Vector3(-0.30f, 0.28f, -0.08f));
        Transform rightWrist = Group("Right Wrist Attachment", body, new Vector3(0.405f, -0.01f, -0.08f));
        Cuff("Left Wrist Cuff", leftWrist, linkMesh, chainMaterial);
        Cuff("Right Wrist Cuff", rightWrist, linkMesh, chainMaterial);

        Transform leftPost = Group("Fixed Left Post Anchor", root, new Vector3(-0.21f, 1.46f, 0.64f));
        Transform rightPost = Group("Fixed Right Post Anchor", root, new Vector3(0.21f, 1.46f, 0.64f));
        Cuff("Left Post Ring", leftPost, linkMesh, chainMaterial);
        Cuff("Right Post Ring", rightPost, linkMesh, chainMaterial);

        RestrainedWalkerVisual visual = root.gameObject.AddComponent<RestrainedWalkerVisual>();
        // Configure once to billboard the attachments before measuring tethers.
        visual.Configure(pivot, body, Array.Empty<RestrainedWalkerVisual.Tether>(), index * 1.71f);
        RestrainedWalkerVisual.Tether[] tethers =
        {
            Tether("Left Wrist Tether", root, leftWrist, leftPost, linkMesh, chainMaterial),
            Tether("Right Wrist Tether", root, rightWrist, rightPost, linkMesh, chainMaterial)
        };
        visual.Configure(pivot, body, tethers, index * 1.71f);
    }

    private static void BuildWarning(Transform parent, float x, Material yellow, Material dark, Material post)
    {
        Transform sign = Group("Restrained Walkers Warning", parent, Vector3.zero);
        sign.position = new Vector3(x, 0f, 25.22f);
        Primitive("Sign Support", PrimitiveType.Cube, sign, new Vector3(0f, 1.16f, 0f),
            new Vector3(0.08f, 2.32f, 0.08f), post);
        Primitive("Warning Backplate", PrimitiveType.Cube, sign, new Vector3(0f, 2.18f, 0f),
            new Vector3(2.05f, 0.61f, 0.045f), dark);
        Primitive("Warning Face", PrimitiveType.Cube, sign, new Vector3(0f, 2.18f, -0.03f),
            new Vector3(1.94f, 0.51f, 0.018f), yellow);
        Transform label = Group("KEEP CLEAR - RESTRAINED", sign, new Vector3(0f, 2.18f, -0.045f));
        TextMesh text = label.gameObject.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "KEEP CLEAR\nRESTRAINED";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 64;
        text.characterSize = 0.027f;
        text.lineSpacing = 0.9f;
        text.color = new Color(0.045f, 0.049f, 0.043f);
        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (text.font != null)
            renderer.sharedMaterial = PrisonInteriorBuilder.WorldTextMaterial(text.font);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Vector3 labelSize = renderer.bounds.size;
        if (labelSize.x > 0f && labelSize.y > 0f)
            label.localScale = Vector3.one * Mathf.Min(1.75f / labelSize.x, 0.39f / labelSize.y);
    }

    private static RestrainedWalkerVisual.Tether Tether(string name, Transform parent, Transform bodyAttachment,
        Transform postAttachment, Mesh mesh, Material material)
    {
        Transform group = Group(name, parent, Vector3.zero);
        int count = Mathf.Max(3, Mathf.CeilToInt(Vector3.Distance(bodyAttachment.position, postAttachment.position) / 0.053f));
        Transform[] links = new Transform[count];
        for (int i = 0; i < count; i++)
            links[i] = Link("Tether Link " + i, group, mesh, material);
        return new RestrainedWalkerVisual.Tether
        {
            bodyAttachment = bodyAttachment,
            postAttachment = postAttachment,
            links = links,
            sag = 0.075f
        };
    }

    private static void StaticChain(string name, Transform parent, Vector3 from, Vector3 to, Mesh mesh, Material material)
    {
        Transform group = Group(name, parent, Vector3.zero);
        Vector3 delta = to - from;
        int count = Mathf.Max(2, Mathf.CeilToInt(delta.magnitude / 0.053f));
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        for (int i = 0; i < count; i++)
        {
            Transform link = Link("Harness Link " + i, group, mesh, material);
            link.localPosition = Vector3.Lerp(from, to, (i + 0.5f) / count);
            link.localRotation = rotation * Quaternion.Euler(0f, i % 2 == 0 ? 0f : 62f, 0f);
            link.localScale = new Vector3(1f, delta.magnitude / count / 0.053f, 1f);
        }
    }

    private static void Cuff(string name, Transform parent, Mesh mesh, Material material)
    {
        Transform cuff = Link(name, parent, mesh, material);
        cuff.localRotation = Quaternion.Euler(0f, 0f, 90f);
        cuff.localScale = new Vector3(1.7f, 1.5f, 1.25f);
    }

    private static Transform Link(string name, Transform parent, Mesh mesh, Material material)
    {
        Transform link = Group(name, parent, Vector3.zero);
        link.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = link.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return link;
    }

    private static Transform Group(string name, Transform parent, Vector3 localPosition)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.localPosition = localPosition;
        return group;
    }

    private static void Primitive(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject item = GameObject.CreatePrimitive(shape);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = scale;
        Collider collider = item.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
        item.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material MaterialAsset(string name, Color color, float metallic, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No supported lit shader found for prison restraints.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh ChainLinkMesh()
    {
        string path = ArtFolder + "/RestraintChainLink.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
            return existing;

        const int ringSegments = 16;
        const int tubeSegments = 6;
        const float radiusX = 0.020f;
        const float radiusY = 0.032f;
        const float tube = 0.008f;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (int ring = 0; ring <= ringSegments; ring++)
        {
            float theta = 2f * Mathf.PI * ring / ringSegments;
            Vector3 center = new Vector3(Mathf.Cos(theta) * radiusX, Mathf.Sin(theta) * radiusY, 0f);
            Vector3 outward = new Vector3(Mathf.Cos(theta) / radiusX, Mathf.Sin(theta) / radiusY, 0f).normalized;
            for (int side = 0; side <= tubeSegments; side++)
            {
                float phi = 2f * Mathf.PI * side / tubeSegments;
                Vector3 normal = outward * Mathf.Cos(phi) + Vector3.forward * Mathf.Sin(phi);
                vertices.Add(center + normal * tube);
                normals.Add(normal);
                uv.Add(new Vector2((float)ring / ringSegments, (float)side / tubeSegments));
                if (ring == ringSegments || side == tubeSegments)
                    continue;
                int a = ring * (tubeSegments + 1) + side;
                int b = a + tubeSegments + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }
        }
        Mesh mesh = new Mesh { name = "Restraint Chain Link" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
#endif
