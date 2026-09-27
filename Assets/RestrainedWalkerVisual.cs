using System;
using UnityEngine;

/// <summary>
/// Decorative, camera-facing walker with a small staged struggle. It has no
/// navigation, damage, physics or learning behavior and never moves its anchor.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RestrainedWalkerVisual : MonoBehaviour
{
    [Serializable]
    public sealed class Tether
    {
        public Transform bodyAttachment;
        public Transform postAttachment;
        public Transform[] links;
        [Min(0f)] public float sag = 0.09f;
    }

    [SerializeField] private Transform feetPivot;
    [SerializeField] private Transform body;
    [SerializeField] private Vector3 bodyRestPosition = new Vector3(0f, 1.05f, 0f);
    [SerializeField] private Tether[] tethers = Array.Empty<Tether>();
    [SerializeField] private float phase;
    [SerializeField, Range(0f, 3f)] private float maximumLeanDegrees = 2.5f;
    private Camera viewingCamera;

    public void Configure(Transform pivot, Transform bodyTransform, Tether[] wristTethers, float animationPhase)
    {
        feetPivot = pivot;
        body = bodyTransform;
        bodyRestPosition = new Vector3(0f, 1.05f, 0f);
        tethers = wristTethers ?? Array.Empty<Tether>();
        phase = animationPhase;
        RefreshPose();
    }

    private void OnEnable()
    {
        RefreshPose();
    }

    private void LateUpdate()
    {
        RefreshPose();
    }

    public void RefreshPose()
    {
        if (feetPivot == null || body == null)
            return;

        if (viewingCamera == null || !viewingCamera.isActiveAndEnabled)
            viewingCamera = Camera.main;

        float lean = Application.isPlaying
            ? Mathf.Sin(Time.time * 0.83f + phase) * maximumLeanDegrees
            : 0f;

        // A 2.5 degree lean at the 1.05m body center shifts it only 0.046m.
        // The world-space feet pivot and post stay put, with no root motion.
        Vector3 leanAxis = viewingCamera != null ? viewingCamera.transform.forward : Vector3.forward;
        Quaternion leanRotation = Quaternion.AngleAxis(lean, leanAxis);
        feetPivot.rotation = leanRotation;
        body.localPosition = bodyRestPosition;
        body.rotation = leanRotation * (viewingCamera != null ? viewingCamera.transform.rotation : Quaternion.identity);

        if (tethers == null)
            return;

        foreach (Tether tether in tethers)
            PositionTether(tether);
    }

    private static void PositionTether(Tether tether)
    {
        if (tether == null || tether.bodyAttachment == null || tether.postAttachment == null ||
            tether.links == null || tether.links.Length == 0)
            return;

        Vector3 start = tether.bodyAttachment.position;
        Vector3 finish = tether.postAttachment.position;
        int count = tether.links.Length;
        for (int index = 0; index < count; index++)
        {
            Transform link = tether.links[index];
            if (link == null)
                continue;

            float t = (index + 0.5f) / count;
            Vector3 center = Vector3.Lerp(start, finish, t) + Vector3.down * (4f * tether.sag * t * (1f - t));
            Vector3 tangent = finish - start + Vector3.down * (4f * tether.sag * (1f - 2f * t));
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.up;

            Quaternion alongChain = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
            link.SetPositionAndRotation(center, alongChain * Quaternion.Euler(0f, index % 2 == 0 ? 0f : 70f, 0f));

            // Keep neighboring links interlocked as the staged pose changes.
            float spacing = tangent.magnitude / count;
            link.localScale = new Vector3(1f, Mathf.Clamp(spacing / 0.053f, 0.75f, 1.45f), 1f);
        }
    }
}
