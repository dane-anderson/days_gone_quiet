using UnityEngine;
using UnityEngine.Rendering;

// Presentation only: the learner still chooses attacks and owns hit tests/rewards.
// Runs after both existing billboard scripts so the body and weapon share one pose.
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class SurvivalCombatVisual : MonoBehaviour
{
    private SurvivalAgent agent;
    private Transform body, pivot, mount;
    private Vector3 bodyRestPosition, pivotRestPosition, mountRestPosition;
    private Quaternion pivotRestRotation, mountRestRotation;
    private bool captured;
    private TrailRenderer swingTrail;
    private Material trailMaterial;
    private int shownAttack = -1;
    private float swingSide = 1f;

    public float DisplayedSwingProgress { get; private set; }
    public float DisplayedBatAngle { get; private set; }

    private void LateUpdate()
    {
        if (!Capture()) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        bool swinging = agent.IsSwinging;
        float phase = swinging ? Mathf.Clamp01(agent.SwingProgress) : 0f;
        if (shownAttack != agent.AttackSequence)
        {
            shownAttack = agent.AttackSequence;
            // Commit the screen-space direction for the whole swing, rather than
            // flipping the weapon when the camera or movement changes mid-attack.
            float towardScreenRight = Vector3.Dot(agent.FacingDirection, camera.transform.right);
            swingSide = towardScreenRight < -.15f ? -1f : 1f;
            if (swingTrail != null) swingTrail.Clear();
        }
        DisplayedSwingProgress = phase;
        EvaluatePose(phase, swinging, out float batAngle, out float lean, out float roll);
        batAngle *= swingSide; lean *= swingSide; roll *= swingSide;
        DisplayedBatAngle = batAngle;

        Quaternion billboard = camera.transform.rotation;
        Quaternion bodyPose = billboard * Quaternion.AngleAxis(roll, Vector3.forward);
        Vector3 leanWorld = camera.transform.right * lean;
        body.rotation = bodyPose;
        body.localPosition = bodyRestPosition + transform.InverseTransformVector(leanWorld);
        // Apply exactly the same camera/torso pose to the weapon's mount.
        mount.rotation = bodyPose;
        mount.position = transform.TransformPoint(bodyRestPosition) + leanWorld;
        pivot.localPosition = pivotRestPosition;
        pivot.localRotation = Quaternion.Euler(0f, 0f, batAngle) * pivotRestRotation;

        if (swingTrail != null)
        {
            bool paused = agent.world == null || agent.world.IsPaused;
            swingTrail.emitting = swinging && !paused && phase >= .30f && phase <= .66f;
            if (!swinging) swingTrail.Clear();
        }
    }

    // A distinct anticipation, fast cross-body strike, follow-through, and recovery.
    public static void EvaluatePose(float phase, bool swinging, out float batAngle, out float lean, out float roll)
    {
        batAngle = lean = roll = 0f;
        if (!swinging) return;
        phase = Mathf.Clamp01(phase);
        if (phase < .30f)
        {
            float t = Smooth(phase / .30f);
            batAngle = 50f * t; lean = -.035f * t; roll = -3f * t;
        }
        else if (phase < .50f)
        {
            float t = Smooth((phase - .30f) / .20f);
            batAngle = Mathf.Lerp(50f, -110f, t);
            lean = Mathf.Lerp(-.035f, .055f, t); roll = Mathf.Lerp(-3f, 5f, t);
        }
        else if (phase < .68f)
        {
            float t = Smooth((phase - .50f) / .18f);
            batAngle = Mathf.Lerp(-110f, -130f, t);
            lean = Mathf.Lerp(.055f, .045f, t); roll = Mathf.Lerp(5f, 3f, t);
        }
        else
        {
            float t = Smooth((phase - .68f) / .32f);
            batAngle = Mathf.Lerp(-130f, 0f, t);
            lean = Mathf.Lerp(.045f, 0f, t); roll = Mathf.Lerp(3f, 0f, t);
        }
    }

    private static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

    private bool Capture()
    {
        if (captured) return body != null && pivot != null && mount != null && agent != null;
        agent = GetComponent<SurvivalAgent>();
        if (agent == null || agent.role != SurvivorRole.Negan || agent.batPivot == null) return false;
        body = transform.Find("Character Visual");
        if (body == null) return false;
        pivot = agent.batPivot; mount = pivot.parent;
        if (mount == null) return false;
        bodyRestPosition = body.localPosition;
        pivotRestPosition = pivot.localPosition;
        pivotRestRotation = pivot.localRotation;
        mountRestPosition = mount.localPosition; mountRestRotation = mount.localRotation;
        captured = true;
        AddSwingTrail();
        return true;
    }

    private void AddSwingTrail()
    {
        GameObject tip = new GameObject("Lucille Motion Trail");
        tip.transform.SetParent(pivot, false);
        tip.transform.localPosition = new Vector3(0f, 1.04f, -.01f);
        swingTrail = tip.AddComponent<TrailRenderer>();
        swingTrail.emitting = false;
        swingTrail.time = .075f;
        swingTrail.minVertexDistance = .025f;
        swingTrail.startWidth = .045f; swingTrail.endWidth = .005f;
        swingTrail.startColor = new Color(.74f, .63f, .44f, .24f);
        swingTrail.endColor = new Color(.74f, .63f, .44f, 0f);
        swingTrail.shadowCastingMode = ShadowCastingMode.Off;
        swingTrail.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            trailMaterial = new Material(shader) { name = "Lucille Motion Trail Material" };
            swingTrail.sharedMaterial = trailMaterial;
        }
        else swingTrail.enabled = false;
    }

    private void OnDisable()
    {
        if (!captured) return;
        if (body != null) body.localPosition = bodyRestPosition;
        if (pivot != null) { pivot.localPosition = pivotRestPosition; pivot.localRotation = pivotRestRotation; }
        if (mount != null) { mount.localPosition = mountRestPosition; mount.localRotation = mountRestRotation; }
        if (swingTrail != null) { swingTrail.emitting = false; swingTrail.Clear(); }
    }

    private void OnDestroy()
    {
        if (swingTrail != null)
        { if (Application.isPlaying) Destroy(swingTrail.gameObject); else DestroyImmediate(swingTrail.gameObject); }
        if (trailMaterial != null)
        { if (Application.isPlaying) Destroy(trailMaterial); else DestroyImmediate(trailMaterial); }
    }
}
