using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;

/// <summary>Owns the shared round. Individual learners never reset the other actor.</summary>
[DefaultExecutionOrder(-250)]
[DisallowMultipleComponent]
public sealed class SurvivalWorld : MonoBehaviour
{
    public enum TrainingFocus { RickEscape, NeganCombat, Both }

    public SurvivalAgent rick, negan;
    public SurvivalWalker[] walkers = Array.Empty<SurvivalWalker>();
    public SurvivalDoor[] doors = Array.Empty<SurvivalDoor>();
    public Transform rickSpawn, neganSpawn;
    public Transform[] walkerSpawns = Array.Empty<Transform>();
    public bool scriptedPreview = true;
    public TrainingFocus trainingFocus = TrainingFocus.Both;
    [Min(1f)] public float episodeSeconds = 45f;
    [Range(1f, 20f)] public float simulationSpeed = 1f;
    [Min(0)] public int activeWalkerCount = 4;
    [Min(0f)] public float walkerSpeed = 1.7f;
    [Min(1f)] public float walkerHealth = 60f;

    private bool paused;
    private bool initialized;
    private bool pendingReset;
    private bool resetting;
    private bool rickParticipating, neganParticipating;
    private TrainingFocus configuredFocus;
    private int configuredWalkerCount;
    private int round;
    private float elapsed;

    public bool IsPaused => paused || pendingReset || resetting || !initialized || !isActiveAndEnabled;
    public bool IsRoundEnding => pendingReset || resetting;
    public int Round => round;
    public float Elapsed => elapsed;

    private void Start() => ResetRound();

    private void FixedUpdate()
    {
        // Speed control is limited to an explicitly connected training session.
        bool trainer = Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;
        float desiredSpeed = trainer ? Mathf.Clamp(simulationSpeed, 1f, 20f) : 1f;
        if (Time.timeScale > 0f && !Mathf.Approximately(Time.timeScale, desiredSpeed))
            Time.timeScale = desiredSpeed;

        if (initialized && (trainingFocus != configuredFocus || ActiveCount != configuredWalkerCount))
            ResetRound();
        if (pendingReset && !resetting)
        {
            ApplyRoundResetNow();
            return;
        }
        if (IsPaused)
            return;

        elapsed += Time.fixedDeltaTime;
        if (elapsed >= Mathf.Max(1f, episodeSeconds))
        {
            EndRound();
            return;
        }
        if ((rickParticipating && (rick == null || !rick.IsAlive)) ||
            (neganParticipating && (negan == null || !negan.IsAlive)))
        {
            EndRound();
            return;
        }
        if (configuredWalkerCount > 0)
        {
            bool anyAlive = false;
            for (int i = 0; i < configuredWalkerCount; i++)
                anyAlive |= walkers[i] != null && walkers[i].IsAlive;
            if (!anyAlive) EndRound();
        }
    }

    private int ActiveCount => Mathf.Clamp(activeWalkerCount, 0, walkers != null ? walkers.Length : 0);

    public void ResetRound()
    {
        if (!Application.isPlaying || resetting)
            return;
        pendingReset = true;
        FreezeActors();
    }

    public void EndRound(SurvivalAgent fallen = null)
    {
        if (!Application.isPlaying || pendingReset || resetting)
            return;
        if (fallen != null && !IsParticipant(fallen))
            return;
        // Damage and terminal rewards belong to SurvivalAgent, so none are repeated here.
        ResetRound();
    }

    /// <summary>Used by the editor validator; gameplay queues this work for FixedUpdate.</summary>
    public void ApplyRoundResetNow()
    {
        if (!Application.isPlaying || resetting)
            return;
        resetting = true;
        pendingReset = true;
        FreezeActors();
        try
        {
            if (initialized)
            {
                if (rickParticipating && rick != null && rick.enabled) rick.EndEpisode();
                if (neganParticipating && negan != null && negan.enabled) negan.EndEpisode();
            }

            configuredFocus = trainingFocus;
            configuredWalkerCount = ActiveCount;
            rickParticipating = rick != null && configuredFocus != TrainingFocus.NeganCombat;
            neganParticipating = negan != null && configuredFocus != TrainingFocus.RickEscape;

            // Remove old carving before warping actors back into their own spawn areas.
            if (doors != null)
                foreach (SurvivalDoor door in doors)
                    if (door != null) door.SetOpen(true);

            ConfigureLearner(rick, rickParticipating, Ground(rickSpawn, new Vector3(0, 0, 19)));
            ConfigureLearner(negan, neganParticipating, Ground(neganSpawn, new Vector3(5, 0, 33)));
            if (walkers != null)
                for (int i = 0; i < walkers.Length; i++)
                {
                    SurvivalWalker walker = walkers[i];
                    if (walker == null) continue;
                    walker.world = this;
                    bool active = i < configuredWalkerCount;
                    walker.gameObject.SetActive(active);
                    if (!active) continue;
                    Transform spawn = walkerSpawns != null && i < walkerSpawns.Length ? walkerSpawns[i] : null;
                    walker.ResetForRound(Ground(spawn, new Vector3(i * 3f, 0, 13)), Mathf.Max(1f, walkerHealth));
                }
            if (doors != null)
                foreach (SurvivalDoor door in doors)
                    if (door != null) door.ResetDoor();

            elapsed = 0f;
            round++;
            initialized = true;
        }
        finally
        {
            resetting = false;
            pendingReset = false;
            Physics.SyncTransforms();
            ApplyPauseStates();
        }
    }

    private void ConfigureLearner(SurvivalAgent agent, bool participant, Vector3 spawn)
    {
        if (agent == null) return;
        agent.world = this;
        SurvivalDecisionRequester requester = agent.GetComponent<SurvivalDecisionRequester>();
        if (requester != null) requester.enabled = false;
        agent.enabled = participant;
        agent.ResetForRound(spawn);
        agent.SetPaused(true);
    }

    private static Vector3 Ground(Transform spawn, Vector3 fallback) => spawn != null ? spawn.position : fallback;

    public bool IsParticipant(SurvivalAgent agent)
    {
        if (agent == null) return false;
        if (!initialized)
            return (agent == rick && trainingFocus != TrainingFocus.NeganCombat) ||
                (agent == negan && trainingFocus != TrainingFocus.RickEscape);
        return (agent == rick && rickParticipating) || (agent == negan && neganParticipating);
    }

    public SurvivalAgent NearestSurvivor(Vector3 position)
    {
        SurvivalAgent result = null;
        float best = float.PositiveInfinity;
        foreach (SurvivalAgent candidate in new[] { rick, negan })
        {
            if (!IsParticipant(candidate) || !candidate.isActiveAndEnabled || !candidate.IsAlive) continue;
            float distance = (candidate.transform.position - position).sqrMagnitude;
            if (distance < best) { best = distance; result = candidate; }
        }
        return result;
    }

    public void SetPaused(bool value)
    {
        paused = value;
        ApplyPauseStates();
    }

    private void FreezeActors()
    {
        if (rick != null) { rick.SetPaused(true); PauseRequester(rick, true); }
        if (negan != null) { negan.SetPaused(true); PauseRequester(negan, true); }
        if (walkers != null)
            foreach (SurvivalWalker walker in walkers)
                if (walker != null) walker.SetPaused(true);
    }

    private void ApplyPauseStates()
    {
        bool freeze = IsPaused;
        if (rick != null)
        {
            rick.SetPaused(freeze || !IsParticipant(rick));
            PauseRequester(rick, freeze || !IsParticipant(rick) || !rick.IsAlive);
        }
        if (negan != null)
        {
            negan.SetPaused(freeze || !IsParticipant(negan));
            PauseRequester(negan, freeze || !IsParticipant(negan) || !negan.IsAlive);
        }
        if (walkers != null)
            foreach (SurvivalWalker walker in walkers)
                if (walker != null) walker.SetPaused(freeze);
    }

    private static void PauseRequester(SurvivalAgent agent, bool value)
    {
        SurvivalDecisionRequester requester = agent.GetComponent<SurvivalDecisionRequester>();
        if (requester != null) requester.enabled = !value && agent.enabled;
    }

    public SurvivalWalker NearestWalker(Vector3 position)
    {
        SurvivalWalker result = null;
        float best = float.PositiveInfinity;
        if (walkers == null) return null;
        foreach (SurvivalWalker candidate in walkers)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.IsAlive) continue;
            float distance = (candidate.transform.position - position).sqrMagnitude;
            if (distance < best) { best = distance; result = candidate; }
        }
        return result;
    }

    public SurvivalDoor NearestDoor(Vector3 position, float maxDistance = 3f)
    {
        SurvivalDoor result = null;
        float best = maxDistance * maxDistance;
        if (doors == null) return null;
        foreach (SurvivalDoor candidate in doors)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
            float distance = (candidate.InteractionPoint - position).sqrMagnitude;
            if (distance <= best) { best = distance; result = candidate; }
        }
        return result;
    }

    public bool IsInsidePrison(Vector3 position) => Mathf.Abs(position.x) < 17f && position.z > 29f && position.z < 48f;

    public bool HasLineOfSight(Vector3 a, Vector3 b) => HasLineOfSight(a, b, null);

    public bool HasLineOfSight(Vector3 a, Vector3 b, SurvivalDoor allowedDoor)
    {
        Vector3 offset = b - a;
        float distance = offset.magnitude;
        if (distance < .001f) return true;
        RaycastHit[] hits = Physics.RaycastAll(a, offset / distance, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Collider obstacle = hit.collider;
            if (obstacle == null) continue;
            SurvivalDoor door = obstacle.GetComponentInParent<SurvivalDoor>();
            if (door != null)
            {
                if (door == allowedDoor || door.IsOpen) continue;
                return false;
            }
            if (obstacle is CharacterController || obstacle.attachedRigidbody != null ||
                obstacle.GetComponentInParent<SurvivalAgent>() != null ||
                obstacle.GetComponentInParent<SurvivalWalker>() != null ||
                obstacle.GetComponentInParent<NavMeshAgent>() != null)
                continue;
            return false;
        }
        return true;
    }

    private void OnEnable()
    {
        if (Application.isPlaying && initialized) ApplyPauseStates();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        FreezeActors();
        if (Time.timeScale > 0f) Time.timeScale = 1f;
    }
}
