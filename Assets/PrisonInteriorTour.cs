using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using UnityEngine.AI;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class PrisonInteriorTour : MonoBehaviour
{
    public RickAgent rick;
    public SurvivalAgent survivalRick;
    public WalkerChase walker;
    public PrisonYardCameraViews views;
    public Renderer[] exteriorRenderers;
    public Collider[] interiorColliders;
    public Collider[] tourBoundaryColliders;
    public Transform tourSpawn;
    public SurvivalWorld world;

    private bool touring;
    private bool capturedSceneSpawn;
    private Vector3 sceneRickPosition;
    private Quaternion sceneRickRotation;
    private CharacterController controller;
    private DecisionRequester requester;
    private bool controllerWasEnabled;
    private bool agentWasEnabled;
    private bool requesterWasEnabled;
    private bool walkerWasEnabled;
    private BodyState rickState;
    private BodyState walkerState;
    private Collider[] pausedColliders;
    private bool[] colliderStates;
    private Collider[] activeBoundaryColliders;
    private bool[] boundaryColliderStates;
    private Renderer[] trackedRenderers;
    private PrisonYardCameraViews.YardView previousView;
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private float cameraFieldOfView;
    private float verticalSpeed;
    private bool worldWasPaused;
    private NavMeshAgent rickNavigation;
    private bool navigationWasEnabled;
    private bool navigationWasStopped;
    private bool navigationHadPath;
    private Vector3 navigationDestination;
    private Vector3 returnRickPosition;
    private Quaternion returnRickRotation;

    public bool IsTourActive => touring;
    public bool HasTourActor => RickTransform != null;
    private Transform RickTransform => survivalRick != null ? survivalRick.transform : rick != null ? rick.transform : null;
    private bool TrainerConnected => Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;

    private void Awake()
    {
        if (Application.isPlaying && RickTransform != null)
        {
            sceneRickPosition = RickTransform.position;
            sceneRickRotation = RickTransform.rotation;
            capturedSceneSpawn = true;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (TrainerConnected)
        {
            if (touring)
                EndTour();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
        {
            if (touring) EndTour();
            else StartTour();
        }

        if (!touring || controller == null || !controller.enabled)
            return;

        Vector2 input = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        }

        input = Vector2.ClampMagnitude(input, 1f);
        float speed = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            ? 7f : 4f;
        float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        verticalSpeed = controller.isGrounded ? -2f : verticalSpeed + Physics.gravity.y * delta;
        controller.Move(new Vector3(input.x * speed, verticalSpeed, input.y * speed) * delta);

        if (world != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            SurvivalDoor door = world.NearestDoor(RickTransform.position, 2.5f);
            if (door != null)
                door.SetOpen(!door.IsOpen);
        }

        // Physical walls do the normal blocking; this catches accidental escapes or falls.
        Vector3 position = RickTransform.position;
        if (position.y < -4f || Mathf.Abs(position.x) > 30f || position.z < -30f || position.z > 62f)
            PlaceAtTourSpawn();
    }

    private void LateUpdate() => RefreshExteriorVisibility();

    public void RefreshExteriorVisibility()
    {
        TrackExteriorRenderers();
        bool cutaway = views != null && views.CurrentView == PrisonYardCameraViews.YardView.CellblockCutaway;
        if (trackedRenderers == null)
            return;

        for (int i = 0; i < trackedRenderers.Length; i++)
            if (trackedRenderers[i] != null)
                trackedRenderers[i].forceRenderingOff = cutaway;
    }

    public void StartTour()
    {
        if (!Application.isPlaying || touring || TrainerConnected || RickTransform == null || views == null)
            return;

        Transform actor = RickTransform;
        Rigidbody body = actor.GetComponent<Rigidbody>();

        if (!capturedSceneSpawn)
        {
            sceneRickPosition = actor.position;
            sceneRickRotation = actor.rotation;
            capturedSceneSpawn = true;
        }

        if (world == null)
        {
            SurvivalAgent survivor = survivalRick != null ? survivalRick : actor.GetComponent<SurvivalAgent>();
            if (survivor != null) world = survivor.world;
        }
        if (world != null && world.IsRoundEnding)
            return;
        returnRickPosition = actor.position;
        returnRickRotation = actor.rotation;
        rickNavigation = actor.GetComponent<NavMeshAgent>();
        navigationWasEnabled = rickNavigation != null && rickNavigation.enabled;
        bool onMesh = navigationWasEnabled && rickNavigation.isOnNavMesh;
        navigationWasStopped = onMesh && rickNavigation.isStopped;
        navigationHadPath = onMesh && rickNavigation.hasPath;
        navigationDestination = navigationHadPath ? rickNavigation.destination : returnRickPosition;
        worldWasPaused = world != null && world.IsPaused;
        if (world != null) world.SetPaused(true);
        if (rickNavigation != null) rickNavigation.enabled = false;

        controller = actor.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = actor.gameObject.AddComponent<CharacterController>();
            controller.enabled = false;
            CapsuleCollider capsule = actor.GetComponent<CapsuleCollider>();
            controller.center = capsule != null ? capsule.center : Vector3.zero;
            controller.height = capsule != null ? capsule.height : 2f;
            controller.radius = capsule != null ? capsule.radius : 0.5f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.03f;
        }

        controllerWasEnabled = controller.enabled;
        controller.enabled = false;
        requester = actor.GetComponent<DecisionRequester>();
        agentWasEnabled = rick != null && rick.enabled;
        requesterWasEnabled = requester != null && requester.enabled;
        walkerWasEnabled = walker != null && walker.enabled;
        rickState = new BodyState(body);
        walkerState = walker != null ? new BodyState(walker.GetComponent<Rigidbody>()) : default;

        previousView = views.CurrentView;
        cameraPosition = views.transform.position;
        cameraRotation = views.transform.rotation;
        Camera camera = views.GetComponent<Camera>();
        cameraFieldOfView = camera != null ? camera.fieldOfView : 52f;

        if (requester != null) requester.enabled = false;
        if (walker != null) walker.enabled = false;
        if (rick != null) rick.enabled = false;

        Collider[] rickColliders = actor.GetComponentsInChildren<Collider>();
        Collider[] walkerColliders = walker != null ? walker.GetComponentsInChildren<Collider>() : new Collider[0];
        pausedColliders = new Collider[rickColliders.Length + walkerColliders.Length];
        rickColliders.CopyTo(pausedColliders, 0);
        walkerColliders.CopyTo(pausedColliders, rickColliders.Length);
        colliderStates = new bool[pausedColliders.Length];
        for (int i = 0; i < pausedColliders.Length; i++)
        {
            colliderStates[i] = pausedColliders[i].enabled;
            pausedColliders[i].enabled = false;
        }

        rickState.Freeze();
        walkerState.Freeze();
        activeBoundaryColliders = tourBoundaryColliders != null
            ? (Collider[])tourBoundaryColliders.Clone() : new Collider[0];
        boundaryColliderStates = new bool[activeBoundaryColliders.Length];
        for (int i = 0; i < activeBoundaryColliders.Length; i++)
        {
            if (activeBoundaryColliders[i] == null) continue;
            boundaryColliderStates[i] = activeBoundaryColliders[i].enabled;
            activeBoundaryColliders[i].enabled = true;
        }
        touring = true;
        PlaceAtTourSpawn();
        controller.enabled = true;
        views.SetTourFollow(true);
    }

    public void EndTour()
    {
        if (!touring)
            return;
        touring = false;

        if (controller != null) controller.enabled = false;
        if (activeBoundaryColliders != null)
            for (int i = 0; i < activeBoundaryColliders.Length; i++)
                if (activeBoundaryColliders[i] != null)
                    activeBoundaryColliders[i].enabled = boundaryColliderStates[i];

        // A legacy learner must initialize at its original spawn only if it was
        // active before this tour. Survival scenes leave that legacy learner off.
        if (RickTransform != null)
        {
            Transform actor = RickTransform;
            actor.SetPositionAndRotation(agentWasEnabled ? sceneRickPosition : returnRickPosition,
                agentWasEnabled ? sceneRickRotation : returnRickRotation);
            Rigidbody body = actor.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = actor.position;
                body.rotation = actor.rotation;
                body.isKinematic = rickState.WasKinematic;
            }
            if (rick != null) rick.enabled = agentWasEnabled;
        }

        rickState.Restore();
        walkerState.Restore();
        if (pausedColliders != null)
            for (int i = 0; i < pausedColliders.Length; i++)
                if (pausedColliders[i] != null)
                    pausedColliders[i].enabled = colliderStates[i];
        if (controller != null) controller.enabled = controllerWasEnabled;
        if (requester != null) requester.enabled = requesterWasEnabled;
        if (walker != null) walker.enabled = walkerWasEnabled;

        if (rickNavigation != null)
        {
            rickNavigation.enabled = navigationWasEnabled;
            if (navigationWasEnabled)
            {
                // Warp reconciles the restored transform with the navigation
                // simulation after the CharacterController's temporary tour.
                // The captured character pivot already includes baseOffset.
                // Warp accepts a surface point, so remove the offset and sample
                // locally; the short radius cannot select a different gallery.
                Vector3 returnGround = returnRickPosition - Vector3.up * rickNavigation.baseOffset;
                bool restored = NavMesh.SamplePosition(returnGround, out NavMeshHit returnHit, 0.75f, rickNavigation.areaMask)
                    && Mathf.Abs(returnHit.position.y - returnGround.y) <= 0.5f
                    && rickNavigation.Warp(returnHit.position);
                if (restored && rickNavigation.isOnNavMesh)
                {
                    if (navigationHadPath)
                        rickNavigation.SetDestination(navigationDestination);
                    else
                        rickNavigation.ResetPath();
                    rickNavigation.isStopped = navigationWasStopped;
                }
                else
                    Debug.LogWarning("Rick could not return to his navigation position after the tour. Reset the round to place him at his spawn.", this);
            }
        }

        if (views != null)
        {
            views.SetTourFollow(false);
            views.ShowView(previousView);
            views.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            Camera camera = views.GetComponent<Camera>();
            if (camera != null) camera.fieldOfView = cameraFieldOfView;
        }
        Physics.SyncTransforms();
        if (world != null) world.SetPaused(worldWasPaused);
    }

    private void PlaceAtTourSpawn()
    {
        if (RickTransform == null)
            return;
        bool enabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        RickTransform.position = tourSpawn != null ? tourSpawn.position : sceneRickPosition;
        Rigidbody body = RickTransform.GetComponent<Rigidbody>();
        if (body != null) body.position = RickTransform.position;
        verticalSpeed = 0f;
        Physics.SyncTransforms();
        if (controller != null) controller.enabled = enabled;
    }

    private void TrackExteriorRenderers()
    {
        int count = exteriorRenderers != null ? exteriorRenderers.Length : 0;
        bool changed = trackedRenderers == null || trackedRenderers.Length != count;
        for (int i = 0; !changed && i < count; i++)
            changed = trackedRenderers[i] != exteriorRenderers[i];
        if (!changed)
            return;
        RestoreRenderers();
        trackedRenderers = new Renderer[count];
        for (int i = 0; i < count; i++)
            trackedRenderers[i] = exteriorRenderers[i];
    }

    private void RestoreRenderers()
    {
        if (trackedRenderers == null)
            return;
        for (int i = 0; i < trackedRenderers.Length; i++)
            if (trackedRenderers[i] != null)
                trackedRenderers[i].forceRenderingOff = false;
    }

    private void OnDisable()
    {
        if (touring) EndTour();
        RestoreRenderers();
        trackedRenderers = null;
    }

    private struct BodyState
    {
        private Rigidbody body;
        private Vector3 position;
        private Quaternion rotation;
        private Vector3 velocity;
        private Vector3 angularVelocity;
        private bool kinematic;

        public bool WasKinematic => kinematic;

        public BodyState(Rigidbody target)
        {
            body = target;
            position = target != null ? target.position : Vector3.zero;
            rotation = target != null ? target.rotation : Quaternion.identity;
            velocity = target != null && !target.isKinematic ? target.linearVelocity : Vector3.zero;
            angularVelocity = target != null && !target.isKinematic ? target.angularVelocity : Vector3.zero;
            kinematic = target != null && target.isKinematic;
        }

        public void Freeze()
        {
            if (body == null) return;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }

        public void Restore()
        {
            if (body == null) return;
            body.transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.isKinematic = kinematic;
            if (!kinematic)
            {
                body.linearVelocity = velocity;
                body.angularVelocity = angularVelocity;
            }
        }
    }
}
