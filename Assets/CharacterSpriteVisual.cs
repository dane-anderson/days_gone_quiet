using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class CharacterSpriteVisual : MonoBehaviour
{
    private const string VisualChildName = "Character Visual";

    [SerializeField] private Sprite characterSprite;
    [SerializeField, Min(0.1f)] private float worldHeight = 2.2f;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private bool faceMainCamera = true;
    [SerializeField] private bool hideOriginalMesh = true;

    private Transform visualTransform;
    private SpriteRenderer spriteRenderer;
    private MeshRenderer originalMeshRenderer;

    private void OnEnable()
    {
        RefreshVisual();
    }

    private void OnValidate()
    {
        worldHeight = Mathf.Max(0.1f, worldHeight);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall -= RefreshVisualInEditor;
            UnityEditor.EditorApplication.delayCall += RefreshVisualInEditor;
            return;
        }
#endif
        RefreshVisual();
    }

#if UNITY_EDITOR
    private void RefreshVisualInEditor()
    {
        if (this != null && isActiveAndEnabled)
            RefreshVisual();
    }
#endif

    private void LateUpdate()
    {
        if (visualTransform == null || spriteRenderer == null)
            RefreshVisual();

        if (faceMainCamera && visualTransform != null && Camera.main != null)
            visualTransform.rotation = Camera.main.transform.rotation;
    }

    private void OnDisable()
    {
        if (originalMeshRenderer != null && hideOriginalMesh)
            originalMeshRenderer.enabled = true;
    }

    private void RefreshVisual()
    {
        if (!gameObject.scene.IsValid())
            return;

        originalMeshRenderer = GetComponent<MeshRenderer>();
        if (originalMeshRenderer != null)
            originalMeshRenderer.enabled = !hideOriginalMesh || characterSprite == null;

        Transform existingChild = transform.Find(VisualChildName);
        if (existingChild == null)
        {
            GameObject visualObject = new GameObject(VisualChildName);
            existingChild = visualObject.transform;
            existingChild.SetParent(transform, false);
        }

        visualTransform = existingChild;
        spriteRenderer = existingChild.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = existingChild.gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = characterSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        spriteRenderer.sortingOrder = 0;

        visualTransform.localPosition = localOffset;
        visualTransform.localRotation = Quaternion.identity;

        if (characterSprite == null || characterSprite.bounds.size.y <= 0f)
        {
            existingChild.gameObject.SetActive(false);
            return;
        }

        float uniformScale = worldHeight / characterSprite.bounds.size.y;
        visualTransform.localScale = Vector3.one * uniformScale;
        existingChild.gameObject.SetActive(true);
    }
}
