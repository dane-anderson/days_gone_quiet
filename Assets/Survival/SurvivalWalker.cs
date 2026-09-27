using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>A damageable walker that follows the baked navigation and batters closed doors.</summary>
[DisallowMultipleComponent]
public sealed class SurvivalWalker : MonoBehaviour
{
    public SurvivalWorld world;
    public NavMeshAgent navigation;
    public bool IsRestrained = false;
    [Min(.5f)] public float attackRange = 1.35f;
    [Min(.1f)] public float attackCooldown = 1.1f;
    [Min(0f)] public float attackDamage = 10f;
    [Min(1f)] public float doorBashDamage = 14f;
    [Min(.1f)] public float doorBashCooldown = 1f;

    private float health = 60f;
    private bool paused;
    private float nextAttackTime, nextPathTime, nextBashTime;
    private Renderer[] renderers = Array.Empty<Renderer>();
    private bool[] rendererStates = Array.Empty<bool>();
    private Collider[] colliders = Array.Empty<Collider>();
    private bool[] colliderStates = Array.Empty<bool>();
    private bool capturedAppearance;

    public bool IsAlive => health > 0f;
    public float Health => health;

    private void Awake()
    {
        CaptureAppearance();
        if (navigation == null) navigation = GetComponent<NavMeshAgent>();
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private void FixedUpdate()
    {
        if (!IsAlive || IsRestrained || paused || world == null || world.IsPaused ||
            navigation == null || !navigation.enabled || !navigation.isOnNavMesh)
            return;

        navigation.speed = Mathf.Max(0f, world.walkerSpeed);
        SurvivalAgent target = world.NearestSurvivor(transform.position);
        if (target == null)
        {
            navigation.isStopped = true;
            return;
        }

        Vector3 difference = target.transform.position - transform.position;
        bool sameLevel = Mathf.Abs(difference.y) < 1.3f;
        Vector3 horizontal = difference; horizontal.y = 0f;
        bool close = sameLevel && horizontal.sqrMagnitude <= attackRange * attackRange;
        bool sight = world.HasLineOfSight(transform.position + Vector3.up * .25f,
            target.transform.position + Vector3.up * .25f);
        if (close && sight)
        {
            navigation.isStopped = true;
            Face(horizontal);
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + Mathf.Max(.1f, attackCooldown);
                target.TakeDamage(Mathf.Max(0f, attackDamage));
            }
            return;
        }

        navigation.isStopped = false;
        if (Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + .25f;
            float offset = target.navigation != null ? target.navigation.baseOffset : 1f;
            Vector3 targetGround = target.transform.position - Vector3.up * offset;
            if (NavMesh.SamplePosition(targetGround, out NavMeshHit hit, 1.5f, navigation.areaMask))
                navigation.SetDestination(hit.position);
        }

        SurvivalDoor blockedDoor = FindNearbyBlockingDoor(target, sight);
        if (blockedDoor != null)
        {
            navigation.isStopped = true;
            Vector3 toDoor = blockedDoor.InteractionPoint - transform.position;
            toDoor.y = 0f;
            Face(toDoor);
            if (Time.time >= nextBashTime)
            {
                nextBashTime = Time.time + Mathf.Max(.1f, doorBashCooldown);
                blockedDoor.Bash(Mathf.Max(1f, doorBashDamage));
                if (blockedDoor.IsOpen) nextPathTime = 0f;
            }
        }
    }

    private SurvivalDoor FindNearbyBlockingDoor(SurvivalAgent target, bool targetVisible)
    {
        if (world.doors == null || targetVisible && navigation.pathStatus == NavMeshPathStatus.PathComplete)
            return null;
        SurvivalDoor result = null;
        float best = 2.1f * 2.1f;
        foreach (SurvivalDoor door in world.doors)
        {
            if (door == null || door.IsOpen || door.Health <= 0f) continue;
            Vector3 toDoor = door.InteractionPoint - transform.position;
            if (Mathf.Abs(toDoor.y) > 1.5f) continue;
            toDoor.y = 0f;
            if (toDoor.sqrMagnitude > best) continue;
            Vector3 towardTarget = target.transform.position - transform.position;
            towardTarget.y = 0f;
            // Do not batter a door behind us while following a target in the opposite direction.
            if (Vector3.Dot(toDoor, towardTarget) < -.1f) continue;
            if (!world.HasLineOfSight(transform.position + Vector3.up * .25f, door.InteractionPoint, door)) continue;
            best = toDoor.sqrMagnitude;
            result = door;
        }
        return result;
    }

    public void ResetForRound(Vector3 groundPosition, float startingHealth)
    {
        CaptureAppearance();
        health = Mathf.Max(1f, startingHealth);
        nextAttackTime = Time.time + .6f;
        nextPathTime = 0f;
        nextBashTime = Time.time + .5f;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = rendererStates[i];
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        if (navigation == null) navigation = GetComponent<NavMeshAgent>();
        if (navigation == null)
        {
            transform.position = groundPosition + Vector3.up;
            return;
        }
        if (navigation.enabled && navigation.isOnNavMesh)
        {
            navigation.isStopped = true;
            navigation.ResetPath();
        }
        navigation.enabled = true;
        navigation.speed = world != null ? Mathf.Max(0f, world.walkerSpeed) : 1.7f;
        navigation.stoppingDistance = .85f;
        navigation.autoBraking = true;
        navigation.autoRepath = true;
        if (NavMesh.SamplePosition(groundPosition, out NavMeshHit hit, 2f, navigation.areaMask) && navigation.Warp(hit.position))
        {
            navigation.ResetPath();
            navigation.isStopped = paused || IsRestrained || world == null || world.IsPaused;
        }
        else
        {
            navigation.enabled = false;
            transform.position = groundPosition + Vector3.up * navigation.baseOffset;
            Debug.LogWarning(name + " has no navigable spawn; check the prison NavMesh bake.", this);
        }
    }

    public void TakeDamage(float damage, SurvivalAgent attacker)
    {
        if (!IsAlive || damage <= 0f || (world != null && world.IsPaused))
            return;
        health = Mathf.Max(0f, health - damage);
        if (health > 0f) return;
        if (navigation != null)
        {
            if (navigation.enabled && navigation.isOnNavMesh)
            {
                navigation.isStopped = true;
                navigation.ResetPath();
            }
            navigation.enabled = false;
        }
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = false;
        // The attacking learner observes the alive-to-dead transition and owns kill rewards.
    }

    public void SetPaused(bool value)
    {
        paused = value;
        if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
            navigation.isStopped = value || !IsAlive || IsRestrained;
    }

    private void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), 240f * Time.fixedDeltaTime);
    }

    private void CaptureAppearance()
    {
        if (capturedAppearance) return;
        capturedAppearance = true;
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererStates = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) rendererStates[i] = renderers[i].enabled;
        colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) colliderStates[i] = colliders[i].enabled;
    }
}
