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
        FollowRick = 4
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
        if (!enableNumberKeys || Keyboard.current == null)
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
    }

    private void LateUpdate()
    {
        if (currentView != YardView.FollowRick)
            return;

        EnsureReferences();
        if (rick == null)
            return;

        Vector3 desiredPosition = rick.position + followOffset;
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, -22f, 22f);
        desiredPosition.z = Mathf.Clamp(desiredPosition.z, -22f, 22f);
        float blend = 1f - Mathf.Exp(-followSmoothing * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);

        Vector3 lookDirection = rick.position + followLookOffset - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }
    }

    public void ShowOverview() => ShowView(YardView.Overview);
    public void ShowFrontGate() => ShowView(YardView.FrontGate);
    public void ShowCourtyard() => ShowView(YardView.Courtyard);
    public void ShowGuardTower() => ShowView(YardView.GuardTower);
    public void ShowFollowRick() => ShowView(YardView.FollowRick);

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
                if (rick != null)
                {
                    Vector3 followPosition = rick.position + followOffset;
                    followPosition.x = Mathf.Clamp(followPosition.x, -22f, 22f);
                    followPosition.z = Mathf.Clamp(followPosition.z, -22f, 22f);
                    ApplyPreset(followPosition, rick.position + followLookOffset, 52f);
                }
                else
                    ApplyPreset(new Vector3(0f, 12f, -8f), new Vector3(0f, 0.8f, 1.5f), 52f);
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
