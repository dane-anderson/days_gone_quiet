using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class PrisonYardCameraViews : MonoBehaviour
{
    public enum YardView
    {
        Overview = 0,
        FrontGate = 1,
        Courtyard = 2,
        GuardTower = 3,
        FollowRick = 4,
        Cellblock = 5,
        RestrainedWalkers = 6,
        CellblockCutaway = 7
    }

    [Header("Play Mode")]
    [SerializeField] private bool enableNumberKeys = true;
    [SerializeField] private YardView startingView = YardView.Overview;

    [Header("Follow Rick")]
    [SerializeField, Min(0.1f)] private float followSmoothing = 5f;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 12f, -8f);
    [SerializeField] private Vector3 followLookOffset = new Vector3(0f, 0.8f, 1.5f);

    [SerializeField, HideInInspector] private Transform rick;
    [SerializeField, HideInInspector] private YardView currentView = YardView.Overview;

    private Camera viewCamera;
    private bool tourFollow;
    private float preTourNearClip;
    private readonly RaycastHit[] cameraHits = new RaycastHit[64];
    private const float TourCameraRadius = 0.25f;
    private const float CameraCollisionPadding = 0.06f;

    public YardView CurrentView => currentView;

    private void Reset()
    {
        EnsureReferences();
        ShowOverview();
    }

    private void Awake()
    {
        EnsureReferences();
        ShowView(startingView);
    }

    private void Update()
    {
        if (tourFollow || !enableNumberKeys || Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            ShowOverview();
        else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            ShowFrontGate();
        else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            ShowCourtyard();
        else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            ShowGuardTower();
        else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
            ShowFollowRick();
        else if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame)
            ShowCellblock();
        else if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame)
            ShowRestrainedWalkers();
        else if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame)
            ShowCellblockCutaway();
    }

    private void LateUpdate()
    {
        if (currentView != YardView.FollowRick)
            return;

        EnsureReferences();
        if (rick == null)
            return;

        Vector3 lookAt = rick.position + GetFollowLookOffset();
        Vector3 collisionAnchor = rick.position + Vector3.up * 0.35f;
        Vector3 desiredPosition = GetFollowPosition();
        if (tourFollow)
            desiredPosition = ConstrainTourCamera(collisionAnchor, desiredPosition);
        float blend = 1f - Mathf.Exp(-followSmoothing * Time.unscaledDeltaTime);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, blend);
        // Recheck the interpolated position: smoothing must not leave the camera beyond a wall.
        transform.position = tourFollow ? ConstrainTourCamera(collisionAnchor, smoothedPosition) : smoothedPosition;

        Vector3 lookDirection = lookAt - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            transform.rotation = tourFollow ? desiredRotation
                : Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }
    }

    public void ShowOverview() => ShowView(YardView.Overview);
    public void ShowFrontGate() => ShowView(YardView.FrontGate);
    public void ShowCourtyard() => ShowView(YardView.Courtyard);
    public void ShowGuardTower() => ShowView(YardView.GuardTower);
    public void ShowFollowRick() => ShowView(YardView.FollowRick);
    public void ShowCellblock() => ShowView(YardView.Cellblock);
    public void ShowRestrainedWalkers() => ShowView(YardView.RestrainedWalkers);
    public void ShowCellblockCutaway() => ShowView(YardView.CellblockCutaway);

    public void SetTourFollow(bool active)
    {
        EnsureReferences();
        if (viewCamera != null && active != tourFollow)
        {
            if (active)
            {
                preTourNearClip = viewCamera.nearClipPlane;
                viewCamera.nearClipPlane = 0.1f;
            }
            else
                viewCamera.nearClipPlane = preTourNearClip;
        }
        tourFollow = active;
        if (active)
            ShowFollowRick();
    }

    public void SnapToTourView()
    {
        EnsureReferences();
        if (!tourFollow || rick == null)
            return;

        Physics.SyncTransforms();
        currentView = YardView.FollowRick;
        Vector3 lookAt = rick.position + GetFollowLookOffset();
        Vector3 collisionAnchor = rick.position + Vector3.up * 0.35f;
        ApplyPreset(ConstrainTourCamera(collisionAnchor, GetFollowPosition()), lookAt, 52f);
    }

    private Vector3 GetFollowPosition()
    {
        if (tourFollow)
            return rick.position + new Vector3(0f, 1.55f, -3.8f);

        Vector3 position = rick.position + followOffset;
        position.x = Mathf.Clamp(position.x, -22f, 22f);
        position.z = Mathf.Clamp(position.z, -22f, 22f);
        return position;
    }

    private Vector3 GetFollowLookOffset()
    {
        return tourFollow ? new Vector3(0f, 0.35f, 0.4f) : followLookOffset;
    }

    private Vector3 ConstrainTourCamera(Vector3 anchor, Vector3 candidate)
    {
        // The aim point is slightly ahead of Rick; the cast starts inside his wall clearance.
        Vector3 offset = candidate - anchor;
        float distance = offset.magnitude;
        if (distance < 0.001f)
            return candidate;

        Vector3 direction = offset / distance;
        int hitCount = Physics.SphereCastNonAlloc(anchor, TourCameraRadius, direction,
            cameraHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = cameraHits;
        if (hitCount == cameraHits.Length)
        {
            hits = Physics.SphereCastAll(anchor, TourCameraRadius, direction,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            hitCount = hits.Length;
        }

        float clearDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider obstacle = hits[i].collider;
            if (obstacle == null || IsCharacterCollider(obstacle))
                continue;
            clearDistance = Mathf.Min(clearDistance, Mathf.Max(0f, hits[i].distance - CameraCollisionPadding));
        }
        return anchor + direction * clearDistance;
    }

    private bool IsCharacterCollider(Collider collider)
    {
        return collider is CharacterController || collider.transform.IsChildOf(rick)
            || collider.GetComponentInParent<RickAgent>() != null
            || collider.GetComponentInParent<WalkerChase>() != null
            || collider.CompareTag("Walker");
    }

    public void RestoreDefaultSettings()
    {
        enableNumberKeys = true;
        startingView = YardView.Overview;
        followSmoothing = 5f;
        followOffset = new Vector3(0f, 12f, -8f);
        followLookOffset = new Vector3(0f, 0.8f, 1.5f);
    }

    public void ShowView(YardView view)
    {
        if (tourFollow && view != YardView.FollowRick)
            return;

        EnsureReferences();
        currentView = view;

        switch (view)
        {
            case YardView.Overview:
                ApplyPreset(new Vector3(0f, 40f, -50f), new Vector3(0f, 0f, 2f), 52f);
                break;
            case YardView.FrontGate:
                ApplyPreset(new Vector3(-8f, 6.8f, -36f), new Vector3(2f, 3f, 18f), 46f);
                break;
            case YardView.Courtyard:
                ApplyPreset(new Vector3(18f, 14f, -18f), new Vector3(0f, 1.5f, 3f), 48f);
                break;
            case YardView.GuardTower:
                ApplyPreset(new Vector3(-8f, 3.2f, 10f), new Vector3(-20.5f, 8.1f, 29.5f), 38f);
                break;
            case YardView.FollowRick:
                if (tourFollow && rick != null)
                    SnapToTourView();
                else if (rick != null)
                {
                    Vector3 followPosition = GetFollowPosition();
                    ApplyPreset(followPosition, rick.position + GetFollowLookOffset(), 52f);
                }
                else
                    ApplyPreset(new Vector3(0f, 12f, -8f), new Vector3(0f, 0.8f, 1.5f), 52f);
                break;
            case YardView.Cellblock:
                ApplyPreset(new Vector3(0f, 2.7f, 30.2f), new Vector3(0f, 3.7f, 42f), 70f);
                break;
            case YardView.RestrainedWalkers:
                ApplyPreset(new Vector3(10f, 5f, 17f), new Vector3(7f, 1.3f, 25f), 48f);
                break;
            case YardView.CellblockCutaway:
                ApplyPreset(new Vector3(0f, 14f, 18f), new Vector3(0f, 2.5f, 35f), 50f);
                break;
        }
    }

    private void ApplyPreset(Vector3 position, Vector3 lookAt, float fieldOfView)
    {
        transform.position = position;
        Vector3 lookDirection = lookAt - position;
        if (lookDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);

        if (viewCamera != null)
            viewCamera.fieldOfView = fieldOfView;
    }

    private void EnsureReferences()
    {
        if (viewCamera == null)
            viewCamera = GetComponent<Camera>();

        if (rick == null)
        {
            GameObject rickObject = GameObject.Find("Rick");
            if (rickObject != null)
                rick = rickObject.transform;
        }
    }
}
