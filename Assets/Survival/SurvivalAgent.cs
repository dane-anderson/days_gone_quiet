using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

public enum SurvivorRole { Rick, Negan }

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class SurvivalAgent : Agent
{
    public const int ObservationSize = 54;
    public SurvivorRole role;
    public SurvivalWorld world;
    public NavMeshAgent navigation;
    public Transform batPivot;
    public Vector3 refugeGroundPoint = new Vector3(0f, 0f, 32f);
    [Min(1f)] public float maximumHealth = 100f;
    [Min(0.1f)] public float meleeRange = 2.15f;
    [Min(1f)] public float meleeDamage = 35f;
    [Min(0.1f)] public float meleeCooldown = 0.65f;
    [Range(0f, 0.5f)] public float movementDeadZone = 0.1f;
    [Min(0.1f)] public float movementAcceleration = 18f;
    [Min(1f)] public float turnSpeed = 420f;

    private float health = 100f;
    private int kills;
    private bool paused;
    private Vector3 facing = Vector3.forward;
    private Vector3 lastVelocity;
    private Vector3 smoothedVelocity;
    private Vector3 strikeFacing = Vector3.forward;
    private float attackReadyAt;
    private float swingElapsed = -1f;
    private bool swingDamageApplied;
    private bool wasThreatenedOutside;
    private bool escapeAwarded;
    private float initialEscapeDistance;
    private float bestEscapePotential;
    private BehaviorParameters behavior;
    private NavMeshPath previewPath;
    private float nextPreviewPathAt;
    private float nextPreviewDoorAt;
    private Vector3 previewGoal;
    private int previewCorner;
    private readonly SurvivalWalker[] observedWalkers = new SurvivalWalker[3];
    private readonly float[] observedWalkerDistances = new float[3];
    private readonly RaycastHit[] physicsHits = new RaycastHit[48];

    public const float SwingDuration = 0.6f;
    public const float SwingImpactProgress = 0.45f;
    private const float AttackArcCosine = 0.6427876f; // 100-degree full arc.
    private const float RayLength = 8f;
    public bool IsAlive => health > 0f;
    public float Health => health;
    public int Kills => kills;
    public bool IsSwinging => swingElapsed >= 0f;
    public float SwingProgress => IsSwinging ? Mathf.Clamp01(swingElapsed / SwingDuration) : 0f;
    public int AttackSequence { get; private set; }
    public int HitSequence { get; private set; }
    public Vector3 LastHitPosition { get; private set; }
    public Vector3 FacingDirection => facing;
    public Vector3 MovementVelocity => lastVelocity;
    private bool TrainerConnected => Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;
    private bool CanAct => isActiveAndEnabled && IsAlive && !paused && world != null
        && !world.IsPaused && !world.IsRoundEnding;

    public string PolicyStatus
    {
        get
        {
            if (!isActiveAndEnabled) return "Inactive";
            if (!IsAlive) return "Down";
            if (paused || (world != null && world.IsPaused)) return "Paused";
            if (behavior == null) behavior = GetComponent<BehaviorParameters>();
            bool hasModel = behavior != null && behavior.Model != null;
            if (behavior != null && behavior.BehaviorType == BehaviorType.HeuristicOnly
                && (TrainerConnected || hasModel)) return "Idle: set Behavior Type to Default";
            if (TrainerConnected) return "Trainer";
            if (hasModel) return "Model inference";
            return world != null && world.scriptedPreview ? "Scripted preview (not learned)" : "Idle (no model)";
        }
    }

    public override void Initialize()
    {
        if (navigation == null) navigation = GetComponent<NavMeshAgent>();
        if (world == null) world = FindAnyObjectByType<SurvivalWorld>();
        behavior = GetComponent<BehaviorParameters>();
        MaxStep = 0;
        if (navigation != null) navigation.updateRotation = false;
        if (role == SurvivorRole.Negan && GetComponent<SurvivalCombatVisual>() == null)
            gameObject.AddComponent<SurvivalCombatVisual>();
    }

    public override void OnEpisodeBegin()
    {
        // A shared round is reset exclusively by SurvivalWorld after all learners end.
    }

    public void ResetForRound(Vector3 groundPosition)
    {
        if (navigation == null) navigation = GetComponent<NavMeshAgent>();
        health = maximumHealth;
        kills = 0;
        facing = Vector3.forward;
        lastVelocity = Vector3.zero;
        smoothedVelocity = Vector3.zero;
        strikeFacing = facing;
        attackReadyAt = 0f;
        swingElapsed = -1f;
        swingDamageApplied = false;
        wasThreatenedOutside = false;
        escapeAwarded = false;
        bestEscapePotential = 0f;
        initialEscapeDistance = Mathf.Max(1f, PlanarDistance(groundPosition, refugeGroundPoint));
        nextPreviewPathAt = 0f;
        nextPreviewDoorAt = 0f;
        previewPath = null;

        if (navigation != null && navigation.enabled)
        {
            if (navigation.isOnNavMesh) navigation.ResetPath();
            if (NavMesh.SamplePosition(groundPosition, out NavMeshHit hit, 3f, navigation.areaMask))
                navigation.Warp(hit.position);
            else
                transform.position = groundPosition + Vector3.up * navigation.baseOffset;
            navigation.updateRotation = false;
        }
        else
            transform.position = groundPosition + Vector3.up * (navigation != null ? navigation.baseOffset : 1f);
        transform.rotation = Quaternion.identity;
        SetPaused(world != null && world.IsPaused);
    }

    public void SetPaused(bool value)
    {
        paused = value;
        if (paused) lastVelocity = smoothedVelocity = Vector3.zero;
        if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
            navigation.isStopped = paused || !IsAlive || !isActiveAndEnabled;
    }

    public void TakeDamage(float amount)
    {
        if (!CanAct || amount <= 0f) return;
        float taken = Mathf.Min(health, amount);
        health -= taken;
        AddReward(-0.6f * taken / Mathf.Max(1f, maximumHealth));
        if (IsAlive) return;
        AddReward(-1f);
        swingElapsed = -1f;
        lastVelocity = smoothedVelocity = Vector3.zero;
        if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
            navigation.isStopped = true;
        world.EndRound(this);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 position = transform.position;
        AddPosition(sensor, position); // 3: absolute position.
        sensor.AddObservation(Mathf.Clamp(lastVelocity.x / 4f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(lastVelocity.z / 4f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp01(health / Mathf.Max(1f, maximumHealth)));
        sensor.AddObservation(world != null && world.IsInsidePrison(position));
        sensor.AddObservation(facing.x);
        sensor.AddObservation(facing.z);
        sensor.AddObservation(role == SurvivorRole.Negan && !IsSwinging && Time.time >= attackReadyAt);
        sensor.AddObservation(world != null ? Mathf.Clamp01(world.Elapsed / Mathf.Max(1f, world.episodeSeconds)) : 0f);
        sensor.AddObservation(Mathf.Clamp((refugeGroundPoint.x - position.x) / 30f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp((refugeGroundPoint.z - position.z) / 60f, -1f, 1f)); // 13 total.

        FindObservedWalkers();
        for (int i = 0; i < observedWalkers.Length; i++)
        {
            SurvivalWalker target = observedWalkers[i];
            AddPosition(sensor, target != null ? target.transform.position - position : Vector3.zero);
            sensor.AddObservation(target != null);
            sensor.AddObservation(target != null ? Mathf.Clamp01(target.Health / Mathf.Max(1f, world.walkerHealth)) : 0f);
            sensor.AddObservation(target != null ? Mathf.Clamp01(observedWalkerDistances[i] / 30f) : 0f);
        } // 18, running total 31.

        SurvivalDoor door = world != null ? world.NearestDoor(position, 1000f) : null;
        AddPosition(sensor, door != null ? door.InteractionPoint - position : Vector3.zero);
        sensor.AddObservation(door != null);
        sensor.AddObservation(door != null && door.IsOpen);
        sensor.AddObservation(door != null ? Mathf.Clamp01(door.Health / Mathf.Max(1f, door.maximumHealth)) : 0f); // 6, total 37.

        SurvivalAgent peer = world != null ? (role == SurvivorRole.Rick ? world.negan : world.rick) : null;
        bool peerActive = peer != null && peer.isActiveAndEnabled && peer.IsAlive;
        AddPosition(sensor, peerActive ? peer.transform.position - position : Vector3.zero);
        sensor.AddObservation(peerActive);
        sensor.AddObservation(peerActive ? Mathf.Clamp01(peer.Health / Mathf.Max(1f, peer.maximumHealth)) : 0f); // 5, total 42.

        for (int ray = 0; ray < 12; ray++)
        {
            float angle = ray * Mathf.PI / 6f;
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            sensor.AddObservation(ObstacleDistance(position + Vector3.up * 0.2f, direction) / RayLength);
        } // 12, total 54.
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!CanAct || navigation == null || !navigation.enabled || !navigation.isOnNavMesh) return;
        Vector3 direction = Vector3.ClampMagnitude(new Vector3(actions.ContinuousActions[0], 0f,
            actions.ContinuousActions[1]), 1f);
        float step = Time.fixedDeltaTime;
        Vector3 velocity = direction * navigation.speed;
        if (role == SurvivorRole.Negan)
        {
            if (direction.sqrMagnitude < movementDeadZone * movementDeadZone)
                direction = Vector3.zero;
            smoothedVelocity = Vector3.MoveTowards(smoothedVelocity, direction * navigation.speed,
                Mathf.Max(0.1f, movementAcceleration) * step);
            velocity = smoothedVelocity;
        }
        Vector3 movement = velocity * step;
        movement = BlockSolidGeometry(movement);
        Vector3 before = transform.position;
        navigation.Move(movement);
        lastVelocity = (transform.position - before) / Mathf.Max(0.001f, step);
        if (direction.sqrMagnitude > 0.001f)
        {
            if (role != SurvivorRole.Negan)
                facing = direction.normalized;
            else if (!IsSwinging)
                facing = Vector3.RotateTowards(facing, direction.normalized,
                    Mathf.Max(1f, turnSpeed) * Mathf.Deg2Rad * step, 0f).normalized;
            transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        int interaction = actions.DiscreteActions.Length > 0 ? actions.DiscreteActions[0] : 0;
        if (interaction == 1)
        {
            SurvivalDoor door = world.NearestDoor(transform.position, 3f);
            if (door != null) door.TryToggle(this);
        }
        else if (interaction == 2 && role == SurvivorRole.Negan && !IsSwinging && Time.time >= attackReadyAt)
        {
            attackReadyAt = Time.time + meleeCooldown;
            swingElapsed = 0f;
            swingDamageApplied = false;
            strikeFacing = facing;
            AttackSequence++;
        }
        RewardSurvivalAndEscape(step);
    }

    private void FixedUpdate()
    {
        if (!CanAct || swingElapsed < 0f) return;
        swingElapsed += Time.fixedDeltaTime;
        if (!swingDamageApplied && swingElapsed >= SwingDuration * SwingImpactProgress)
        {
            swingDamageApplied = true;
            StrikeWalker();
        }
        if (swingElapsed >= SwingDuration) swingElapsed = -1f;
    }

    private void StrikeWalker()
    {
        SurvivalWalker victim = null;
        float nearest = meleeRange;
        if (world.walkers == null) return;
        foreach (SurvivalWalker target in world.walkers)
        {
            if (target == null || !target.IsAlive || target.IsRestrained || !target.gameObject.activeInHierarchy) continue;
            Vector3 offset = target.transform.position - transform.position;
            float distance = offset.magnitude;
            if (distance > nearest || Mathf.Abs(offset.y) > 1.4f) continue;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0.001f && Vector3.Dot(strikeFacing, offset.normalized) < AttackArcCosine) continue;
            if (!world.HasLineOfSight(transform.position + Vector3.up * 0.2f,
                target.transform.position + Vector3.up * 0.2f)) continue;
            nearest = distance;
            victim = target;
        }
        if (victim == null)
        {
            AddReward(-0.005f);
            return;
        }
        float previousHealth = victim.Health;
        victim.TakeDamage(meleeDamage, this);
        float damage = Mathf.Max(0f, previousHealth - victim.Health);
        if (damage > 0f)
        {
            HitSequence++;
            LastHitPosition = victim.transform.position + Vector3.up * 0.2f;
        }
        AddReward(0.1f * damage / Mathf.Max(1f, world.walkerHealth));
        if (!victim.IsAlive && previousHealth > 0f)
        {
            kills++;
            AddReward(1f);
        }
    }

    private void RewardSurvivalAndEscape(float step)
    {
        AddReward((role == SurvivorRole.Rick ? 0.2f : 0.1f) * step / Mathf.Max(1f, world.episodeSeconds));
        if (role != SurvivorRole.Rick) return;
        bool inside = world.IsInsidePrison(transform.position);
        SurvivalWalker danger = world.NearestWalker(transform.position);
        bool threatenedOutside = !inside && danger != null && danger.IsAlive
            && Vector3.Distance(danger.transform.position, transform.position) < 12f;
        if (threatenedOutside)
        {
            wasThreatenedOutside = true;
            float potential = Mathf.Clamp01(1f - PlanarDistance(transform.position, refugeGroundPoint)
                / Mathf.Max(1f, initialEscapeDistance));
            if (potential > bestEscapePotential)
            {
                AddReward((potential - bestEscapePotential) * 0.35f);
                bestEscapePotential = potential;
            }
        }
        if (inside && wasThreatenedOutside && !escapeAwarded)
        {
            escapeAwarded = true;
            AddReward(1f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuous = actionsOut.ContinuousActions;
        ActionSegment<int> discrete = actionsOut.DiscreteActions;
        continuous[0] = 0f;
        continuous[1] = 0f;
        if (discrete.Length > 0) discrete[0] = 0;
        if (!CanAct || !world.scriptedPreview || TrainerConnected
            || (behavior != null && behavior.Model != null)) return;

        SurvivalWalker target = world.NearestWalker(transform.position);
        Vector3 goal = refugeGroundPoint;
        if (role == SurvivorRole.Negan)
        {
            if (target == null) return;
            goal = target.transform.position;
        }
        else if (world.IsInsidePrison(transform.position) && target != null
            && PlanarDistance(transform.position, target.transform.position) < 7f)
        {
            Vector3 away = (transform.position - target.transform.position).normalized;
            goal = new Vector3(Mathf.Clamp(transform.position.x + away.x * 6f, -9f, 9f), 0f,
                Mathf.Clamp(transform.position.z + away.z * 6f, 31f, 38f));
        }

        Vector3 movement = PreviewDirection(goal);
        int interaction = 0;
        SurvivalDoor door = world.NearestDoor(transform.position, 2.8f);
        if (door != null && Time.time >= nextPreviewDoorAt)
        {
            Vector3 towardGoal = goal - transform.position;
            Vector3 towardDoor = door.InteractionPoint - transform.position;
            towardGoal.y = towardDoor.y = 0f;
            bool blocksGoal = Vector3.Dot(towardGoal, towardDoor) > 0f;
            bool closeBehind = role == SurvivorRole.Rick && world.IsInsidePrison(transform.position)
                && target != null && !world.IsInsidePrison(target.transform.position)
                && PlanarDistance(transform.position, target.transform.position) < 9f;
            if ((!door.IsOpen && blocksGoal) || (door.IsOpen && closeBehind))
            {
                interaction = 1;
                nextPreviewDoorAt = Time.time + 0.7f;
            }
        }
        if (role == SurvivorRole.Negan && target != null
            && Vector3.Distance(target.transform.position, transform.position) < meleeRange * 0.95f
            && world.HasLineOfSight(transform.position, target.transform.position))
        {
            Vector3 towardTarget = target.transform.position - transform.position;
            towardTarget.y = 0f;
            movement = towardTarget.normalized * Mathf.Max(0.12f, movementDeadZone + 0.02f);
            if (Time.time >= attackReadyAt) interaction = 2;
        }
        continuous[0] = movement.x;
        continuous[1] = movement.z;
        if (discrete.Length > 0) discrete[0] = interaction;
    }

    private Vector3 PreviewDirection(Vector3 goal)
    {
        if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh) return Vector3.zero;
        if (previewPath == null) previewPath = new NavMeshPath();
        if (Time.time >= nextPreviewPathAt || (goal - previewGoal).sqrMagnitude > 1f)
        {
            previewGoal = goal;
            nextPreviewPathAt = Time.time + 0.35f;
            previewCorner = 1;
            if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 3f, navigation.areaMask))
                navigation.CalculatePath(hit.position, previewPath);
            else
                previewPath.ClearCorners();
        }
        Vector3 waypoint = goal;
        Vector3[] corners = previewPath.corners;
        while (previewCorner < corners.Length && PlanarDistance(transform.position, corners[previewCorner]) <= 0.45f)
            previewCorner++;
        if (previewCorner < corners.Length) waypoint = corners[previewCorner];
        Vector3 direction = waypoint - transform.position;
        direction.y = 0f;
        return direction.magnitude > 0.2f ? direction.normalized : Vector3.zero;
    }

    private void FindObservedWalkers()
    {
        for (int i = 0; i < 3; i++) { observedWalkers[i] = null; observedWalkerDistances[i] = float.PositiveInfinity; }
        if (world == null || world.walkers == null) return;
        foreach (SurvivalWalker target in world.walkers)
        {
            if (target == null || !target.IsAlive || target.IsRestrained || !target.gameObject.activeInHierarchy) continue;
            float distance = Vector3.Distance(transform.position, target.transform.position);
            for (int slot = 0; slot < 3; slot++)
            {
                if (distance >= observedWalkerDistances[slot]) continue;
                for (int shift = 2; shift > slot; shift--)
                { observedWalkers[shift] = observedWalkers[shift - 1]; observedWalkerDistances[shift] = observedWalkerDistances[shift - 1]; }
                observedWalkers[slot] = target;
                observedWalkerDistances[slot] = distance;
                break;
            }
        }
    }

    private float ObstacleDistance(Vector3 origin, Vector3 direction)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, physicsHits, RayLength,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = RayLength;
        for (int i = 0; i < count; i++)
            if (!IsCharacter(physicsHits[i].collider)) closest = Mathf.Min(closest, physicsHits[i].distance);
        return closest;
    }

    private Vector3 BlockSolidGeometry(Vector3 movement)
    {
        float distance = movement.magnitude;
        if (distance < 0.0001f) return Vector3.zero;
        float radius = Mathf.Max(0.1f, navigation.radius);
        float half = Mathf.Max(radius, navigation.height * 0.5f);
        Vector3 bottom = transform.position + Vector3.up * (-half + radius + 0.03f);
        Vector3 top = transform.position + Vector3.up * (half - radius);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, movement / distance, physicsHits,
            distance + 0.03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float allowed = distance;
        for (int i = 0; i < count; i++)
        {
            if (IsCharacter(physicsHits[i].collider) || physicsHits[i].normal.y > 0.55f) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, physicsHits[i].distance - 0.03f));
        }
        return movement * (allowed / distance);
    }

    private bool IsCharacter(Collider collider)
    {
        return collider == null || collider.transform.IsChildOf(transform) || collider is CharacterController
            || collider.GetComponentInParent<SurvivalAgent>() != null
            || collider.GetComponentInParent<SurvivalWalker>() != null;
    }

    private static void AddPosition(VectorSensor sensor, Vector3 position)
    {
        sensor.AddObservation(Mathf.Clamp(position.x / 30f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(position.y / 10f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(position.z / 60f, -1f, 1f));
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
