#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Unity.AI.Navigation;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Tests real navigation, door geometry, bat actions and shared rounds in Play mode.</summary>
public static class SurvivalValidation
{
    private static IEnumerator checks;
    private static double nextCheckAt;
    private static string passMessage;

    [MenuItem("Days Gone Quiet/Validate Survival Structure", priority = 110)]
    public static void ValidateStructure()
    {
        SurvivalWorld world = Object.FindAnyObjectByType<SurvivalWorld>();
        Require(world != null, "Survival world exists");
        Require(world.rick != null && world.negan != null, "Both survivor references exist");
        Require(world.rick.role == SurvivorRole.Rick && world.negan.role == SurvivorRole.Negan, "Distinct survivor roles");
        Require(world.rickSpawn != null && world.neganSpawn != null, "Survivor spawns exist");
        Require(world.walkers != null && world.walkers.Length >= 4 && world.walkerSpawns.Length == world.walkers.Length,
            "Walker references and parallel spawn array exist");
        Require(world.doors != null && world.doors.Length == 3, "Three functional doors exist");
        NavMeshSurface surface = world.GetComponent<NavMeshSurface>();
        Require(surface != null && surface.navMeshData != null, "Navigation is baked and saved");
        foreach (SurvivalAgent agent in new[] { world.rick, world.negan })
        {
            Require(agent.GetComponents<Agent>().Length == 1, "Exactly one ML agent on " + agent.name);
            BehaviorParameters behavior = agent.GetComponent<BehaviorParameters>();
            Require(behavior != null && behavior.BrainParameters.VectorObservationSize == SurvivalAgent.ObservationSize,
                "Observation contract for " + agent.name);
            Require(behavior.BrainParameters.ActionSpec.NumContinuousActions == 2 &&
                behavior.BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 3 }), "Movement and interaction actions for " + agent.name);
            Require(behavior.BehaviorName == (agent.role == SurvivorRole.Rick ? "RickEscape" : "NeganCombat"),
                "Independent policy name for " + agent.name);
            SurvivalDecisionRequester requester = agent.GetComponent<SurvivalDecisionRequester>();
            Require(requester != null && requester.Agent == agent, "Explicit decision binding for " + agent.name);
            Require(agent.GetComponent<DecisionRequester>() == null, "No legacy automatic requester on " + agent.name);
            Require(agent.navigation != null && agent.MaxStep == 0, "World owns episode limits for " + agent.name);
        }
        Require(world.negan.batPivot != null, "Negan has an animated bat");
        foreach (SurvivalDoor door in world.doors)
            Require(door != null && door.leaf != null && door.blockingCollider != null && door.obstacle != null,
                "Door has a leaf, solid blocker and navigation obstacle");
        foreach (Transform spawn in world.walkerSpawns)
        {
            Require(spawn != null, "Walker spawn assigned");
            NavPoint(spawn.position);
        }
        NavPoint(world.rickSpawn.position);
        NavPoint(world.neganSpawn.position);
        Debug.Log("SURVIVAL STRUCTURE VALIDATION: PASS. Separate learner bindings, action/observation contracts, Lucille, doors and navigable spawns verified.");
    }

    [MenuItem("Days Gone Quiet/Test Survival Gameplay in Play Mode", priority = 111)]
    public static void TestGameplay()
    {
        Require(Application.isPlaying && !EditorApplication.isPaused, "Enter unpaused Play mode first");
        Require(!Academy.IsInitialized || !Academy.Instance.IsCommunicatorOn, "Run gameplay validation without an attached trainer");
        Require(checks == null, "A gameplay validation is not already running");
        ValidateStructure();
        checks = GameplayChecks(Object.FindAnyObjectByType<SurvivalWorld>());
        passMessage = "SURVIVAL PLAY VALIDATION: PASS. Yard/interior/gallery routes, door blocking/carving/clearance/bashing, actual bat damage and kill, pause, safe manual tours, focus selection and coherent shared round resets verified.";
        nextCheckAt = 0;
        EditorApplication.update += AdvanceChecks;
        Debug.Log("SURVIVAL PLAY VALIDATION: running navigation, doors, combat, pause and round checks.");
    }

    [MenuItem("Days Gone Quiet/Test Survival Tour Safety in Play Mode", priority = 112)]
    public static void TestTourSafety()
    {
        Require(Application.isPlaying && !EditorApplication.isPaused, "Enter unpaused Play mode first");
        Require(!Academy.IsInitialized || !Academy.Instance.IsCommunicatorOn, "Run tour validation without an attached trainer");
        Require(checks == null, "A gameplay validation is not already running");
        SurvivalWorld world = Object.FindAnyObjectByType<SurvivalWorld>();
        Require(world != null && !world.IsRoundEnding, "A stable survival round exists");
        checks = TourSafetyChecks(world);
        passMessage = "SURVIVAL TOUR VALIDATION: PASS. Ground-floor and upper-gallery tours freeze world/navigation and restore Rick's pivot, floor position and previous running/paused state.";
        nextCheckAt = 0;
        EditorApplication.update += AdvanceChecks;
        Debug.Log("SURVIVAL TOUR VALIDATION: checking running ground-floor and paused upper-gallery return positions.");
    }

    private static void AdvanceChecks()
    {
        if (EditorApplication.timeSinceStartup < nextCheckAt) return;
        try
        {
            Require(Application.isPlaying && !EditorApplication.isPaused, "Keep Play mode running while validation completes");
            if (!checks.MoveNext())
            {
                FinishChecks();
                Debug.Log(passMessage);
                return;
            }
            float wait = checks.Current is float seconds ? seconds : 0f;
            nextCheckAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception exception)
        {
            FinishChecks();
            Debug.LogException(exception);
        }
    }

    private static void FinishChecks()
    {
        EditorApplication.update -= AdvanceChecks;
        (checks as IDisposable)?.Dispose();
        checks = null;
    }

    private static IEnumerator GameplayChecks(SurvivalWorld world)
    {
        bool originalPaused = world.IsPaused;
        bool preview = world.scriptedPreview;
        var focus = world.trainingFocus;
        int count = world.activeWalkerCount;
        float speed = world.walkerSpeed, duration = world.episodeSeconds;
        bool[] walkerEnabled = world.walkers.Select(w => w != null && w.enabled).ToArray();
        try
        {
            world.SetPaused(true);
            world.scriptedPreview = false;
            world.trainingFocus = SurvivalWorld.TrainingFocus.Both;
            world.activeWalkerCount = 4;
            world.walkerSpeed = 0f;
            world.episodeSeconds = 600f;
            world.ApplyRoundResetNow();
            foreach (SurvivalWalker walker in world.walkers) if (walker != null) walker.enabled = false;
            world.SetPaused(false);
            StopRequests(world);
            foreach (SurvivalDoor door in world.doors) door.SetOpen(true);
            yield return .2f; // Allow asynchronous obstacle carving to update.

            CompletePath(new Vector3(0, 0, 19), new Vector3(0, 0, 32), false);
            CompletePath(new Vector3(10.5f, 0, 30), new Vector3(9, 4.2f, 40.3f), true);
            CompletePath(new Vector3(4, 0, 40), new Vector3(4, 0, 45), false);
            CompletePath(new Vector3(-15.85f, 0, 38), new Vector3(-15.85f, 0, 44.5f), false);

            SurvivalDoor cellDoor = world.doors.First(d => d.displayName.Contains("Cell"));
            cellDoor.SetOpen(false);
            yield return .2f;
            Require(!cellDoor.IsOpen && cellDoor.blockingCollider.enabled && cellDoor.obstacle.enabled,
                "Closed door activates solid collision and carving");
            NavMeshPath blocked = new NavMeshPath();
            NavMesh.CalculatePath(NavPoint(new Vector3(4, 0, 40)), NavPoint(new Vector3(4, 0, 45)), NavMesh.AllAreas, blocked);
            Require(blocked.status != NavMeshPathStatus.PathComplete, "Closed cell door blocks its navigation route");
            Require(!world.HasLineOfSight(new Vector3(4, 1.2f, 40), new Vector3(4, 1.2f, 44)), "Closed door blocks melee line of sight");

            // Let one real walker pursue Rick through the closed cell doorway.
            // It must stay inside and batter the leaf rather than walk through it.
            SurvivalWalker victim = world.walkers[0];
            Place(world.rick, new Vector3(4.04f, 0, 40.05f));
            victim.ResetForRound(new Vector3(4.04f, 0, 44f), 60f);
            victim.enabled = true;
            world.walkerSpeed = 1.7f;
            float doorBeforePursuit = cellDoor.Health;
            yield return 2.2f;
            Require(victim.transform.position.z > 41.85f && !cellDoor.IsOpen && cellDoor.Health < doorBeforePursuit,
                "Walker stays behind a closed door and batters it over time");
            Require(Mathf.Approximately(world.rick.Health, world.rick.maximumHealth), "Walker cannot attack Rick through the closed leaf");
            victim.enabled = false;
            world.walkerSpeed = 0f;
            victim.ResetForRound(new Vector3(0, 0, 13), 60f);
            Require(cellDoor.TryToggle(world.rick), "Nearby survivor can operate a closed door");
            Require(!cellDoor.TryToggle(world.rick), "Door interaction cooldown rejects an immediate second toggle");

            cellDoor.SetOpen(true);
            Place(world.rick, new Vector3(4.04f, 0, 41.65f));
            Physics.SyncTransforms();
            cellDoor.SetOpen(false);
            Require(cellDoor.IsOpen, "Door refuses to close on an actor");
            Place(world.rick, new Vector3(0, 0, 32));
            cellDoor.SetOpen(false);
            Require(!cellDoor.IsOpen, "Door closes after its doorway clears");
            float initialDoorHealth = cellDoor.Health;
            cellDoor.Bash(initialDoorHealth * .5f);
            Require(!cellDoor.IsOpen && cellDoor.Health > 0f && cellDoor.Health < initialDoorHealth, "Bashing damages a closed door incrementally");
            cellDoor.Bash(initialDoorHealth);
            Require(cellDoor.IsOpen && cellDoor.Health == 0f, "Destroyed door opens");
            cellDoor.SetOpen(false);
            Require(cellDoor.IsOpen, "Broken door stays open until the next round");
            cellDoor.ResetDoor();
            Require(cellDoor.IsOpen == cellDoor.startOpen && Mathf.Approximately(cellDoor.Health, cellDoor.maximumHealth), "Door reset restores durability and initial state");

            // The first real bat swing is within range but separated by a closed door.
            // Start this isolated combat check facing the walker, with no prior
            // committed swing, cooldown, or smoothed movement left over.
            world.negan.ResetForRound(new Vector3(4.04f, 0, 40.85f));
            Physics.SyncTransforms();
            victim.ResetForRound(new Vector3(4.04f, 0, 42.55f), 60f);
            yield return .15f;
            int initialKills = world.negan.Kills;
            IEnumerator batCheck = CheckBatSwing(world.negan, victim, false);
            while (batCheck.MoveNext()) yield return batCheck.Current;
            Require(Mathf.Approximately(victim.Health, 60f), "Bat cannot damage a walker through a closed door");
            cellDoor.SetOpen(true);
            yield return .25f;
            batCheck = CheckBatSwing(world.negan, victim, true);
            while (batCheck.MoveNext()) yield return batCheck.Current;
            Require(victim.IsAlive && victim.Health > 0f && victim.Health < 60f, "Actual bat swing damages a nearby walker");
            batCheck = CheckBatSwing(world.negan, victim, true);
            while (batCheck.MoveNext()) yield return batCheck.Current;
            Require(!victim.IsAlive && world.negan.Kills == initialKills + 1, "Second bat swing kills and credits exactly one kill");
            Require(!victim.navigation.enabled && victim.GetComponentsInChildren<Renderer>().All(r => !r.enabled),
                "Dead walker stops navigating and hides its visuals");

            int priorRound = world.Round;
            world.rick.TakeDamage(world.rick.Health + 1);
            Require(world.Round == priorRound && world.IsRoundEnding, "Terminal damage queues the round instead of recursively resetting");
            yield return .15f;
            StopRequests(world);
            Require(world.Round == priorRound + 1 && world.rick.IsAlive && world.negan.IsAlive && world.negan.Kills == 0,
                "Both survivors reset in the same world round");
            Require(world.walkers.Take(4).All(w => w.IsAlive && Mathf.Approximately(w.Health, world.walkerHealth)), "Walker health resets with the round");
            Require(world.doors.All(d => d.IsOpen == d.startOpen), "Door states reset with the round");

            world.SetPaused(true);
            float pausedAt = world.Elapsed;
            Vector3 rickPosition = world.rick.transform.position;
            Vector3 walkerPosition = world.walkers[0].transform.position;
            yield return .2f;
            Require(Mathf.Approximately(world.Elapsed, pausedAt) && Vector3.Distance(rickPosition, world.rick.transform.position) < .001f
                && Vector3.Distance(walkerPosition, world.walkers[0].transform.position) < .001f, "Pause freezes timer and actors");
            Require(!world.rick.GetComponent<SurvivalDecisionRequester>().enabled && !world.negan.GetComponent<SurvivalDecisionRequester>().enabled,
                "Pause disables decision requests");

            IEnumerator tourChecks = TourSafetyChecks(world);
            try { while (tourChecks.MoveNext()) yield return tourChecks.Current; }
            finally { (tourChecks as IDisposable)?.Dispose(); }

            world.trainingFocus = SurvivalWorld.TrainingFocus.NeganCombat;
            world.ApplyRoundResetNow();
            Require(!world.rick.enabled && world.negan.enabled && !world.IsParticipant(world.rick) && world.IsParticipant(world.negan),
                "Negan-only focus excludes Rick from training and targeting");
            world.trainingFocus = SurvivalWorld.TrainingFocus.RickEscape;
            world.ApplyRoundResetNow();
            Require(world.rick.enabled && !world.negan.enabled, "Rick-only focus selects the independent learner");
            world.trainingFocus = SurvivalWorld.TrainingFocus.Both;
            world.ApplyRoundResetNow();
            world.episodeSeconds = .1f;
            world.SetPaused(false);
            StopRequests(world);
            priorRound = world.Round;
            yield return 1.2f; // Runtime clamps the shortest supported round to one simulated second.
            Require(world.Round > priorRound, "Bounded world timer ends shared rounds");
        }
        finally
        {
            if (world != null && Application.isPlaying)
            {
                world.SetPaused(true);
                world.scriptedPreview = preview;
                world.trainingFocus = focus;
                world.activeWalkerCount = count;
                world.walkerSpeed = speed;
                world.episodeSeconds = duration;
                for (int i = 0; i < world.walkers.Length && i < walkerEnabled.Length; i++)
                    if (world.walkers[i] != null) world.walkers[i].enabled = walkerEnabled[i];
                world.ApplyRoundResetNow();
                world.SetPaused(originalPaused);
            }
        }
    }

    private static IEnumerator TourSafetyChecks(SurvivalWorld world)
    {
        PrisonInteriorTour tour = Object.FindAnyObjectByType<PrisonInteriorTour>();
        Require(tour != null && !tour.IsTourActive && tour.survivalRick == world.rick && tour.world == world,
            "Manual tour is assigned to the survival Rick and is stopped");
        Require(world.rick.navigation != null && world.rick.navigation.enabled && world.rick.navigation.isOnNavMesh,
            "Rick has active navigation before the tour");
        bool originalPaused = world.IsPaused, originalPreview = world.scriptedPreview;
        float originalDuration = world.episodeSeconds;
        bool[] walkerEnabled = world.walkers.Select(w => w != null && w.enabled).ToArray();
        SurvivalDecisionRequester[] requesters = new[] {
            world.rick.GetComponent<SurvivalDecisionRequester>(), world.negan.GetComponent<SurvivalDecisionRequester>() };
        bool[] requesterEnabled = requesters.Select(r => r != null && r.enabled).ToArray();
        NavigationState[] navigation = new[] { world.rick.navigation, world.negan.navigation }
            .Concat(world.walkers.Where(w => w != null).Select(w => w.navigation))
            .Where(n => n != null).Distinct().Select(n => new NavigationState(n)).ToArray();
        try
        {
            world.SetPaused(true);
            world.scriptedPreview = false;
            world.episodeSeconds = Mathf.Max(originalDuration, world.Elapsed + 5f);
            foreach (SurvivalWalker walker in world.walkers) if (walker != null) walker.enabled = false;
            foreach (NavigationState state in navigation) state.ClearPath();

            Vector3[] testSpawns = { new Vector3(0, 0, 32), new Vector3(9, 4.2f, 40.3f) };
            for (int test = 0; test < testSpawns.Length; test++)
            {
                bool pausedBeforeTour = test == 1;
                world.SetPaused(pausedBeforeTour);
                StopRequests(world);
                Place(world.rick, testSpawns[test]);
                yield return .12f; // Let Warp and baseOffset settle before capturing the pivot.
                Vector3 expectedPivot = world.rick.transform.position;
                Vector3 expectedGround = expectedPivot - Vector3.up * world.rick.navigation.baseOffset;
                float elapsedBeforeTour = world.Elapsed;
                int roundBeforeTour = world.Round;
                Vector3 neganBeforeTour = world.negan.transform.position;
                tour.StartTour();
                Require(tour.IsTourActive && world.IsPaused && !world.rick.navigation.enabled,
                    "Starting a manual tour pauses the world and disables Rick navigation");
                CharacterController controller = world.rick.GetComponent<CharacterController>();
                Require(controller != null && controller.enabled, "Manual tour enables physical walking");
                Require(requesters.All(r => r == null || !r.enabled), "Manual tour stops learner decisions");
                Require(navigation.All(n => n.IsStopped), "Manual tour stops all remaining navigation");
                controller.Move(Vector3.forward * .6f);
                yield return .15f;
                Require(Mathf.Approximately(world.Elapsed, elapsedBeforeTour) && world.Round == roundBeforeTour
                    && Vector3.Distance(neganBeforeTour, world.negan.transform.position) < .02f,
                    "Timer, shared round and other survivor stay frozen during manual exploration");
                tour.EndTour();
                Require(!tour.IsTourActive && world.IsPaused == pausedBeforeTour && world.rick.navigation.enabled,
                    "Ending the tour restores navigation and its previous paused/running state");
                StopRequests(world);
                yield return .12f;
                Require(world.rick.navigation.isOnNavMesh && Vector3.Distance(expectedPivot, world.rick.transform.position) < .2f,
                    "Rick returns to the same character pivot after the tour on floor " + testSpawns[test].y);
                Vector3 actualGround = world.rick.transform.position - Vector3.up * world.rick.navigation.baseOffset;
                Require(Vector3.Distance(expectedGround, actualGround) < .2f,
                    "Rick returns to the same ground surface without adding baseOffset twice");
                Require(!controller.enabled, "Manual walking controller is disabled after the tour");
            }
        }
        finally
        {
            if (tour != null && tour.IsTourActive) tour.EndTour();
            if (world != null && Application.isPlaying)
            {
                world.SetPaused(true);
                world.scriptedPreview = originalPreview;
                world.episodeSeconds = originalDuration;
                foreach (NavigationState state in navigation) state.Restore();
                for (int i = 0; i < world.walkers.Length; i++)
                    if (world.walkers[i] != null) world.walkers[i].enabled = walkerEnabled[i];
                world.SetPaused(originalPaused);
                for (int i = 0; i < requesters.Length; i++)
                    if (requesters[i] != null) requesters[i].enabled = requesterEnabled[i];
                Physics.SyncTransforms();
            }
        }
    }

    private sealed class NavigationState
    {
        private readonly NavMeshAgent navigation;
        private readonly Vector3 position, destination;
        private readonly Quaternion rotation;
        private readonly bool enabled, stopped, hadPath;

        public bool IsStopped => navigation == null || !navigation.isActiveAndEnabled
            || !navigation.isOnNavMesh || navigation.isStopped;

        public NavigationState(NavMeshAgent agent)
        {
            navigation = agent;
            position = agent.transform.position;
            rotation = agent.transform.rotation;
            enabled = agent.enabled;
            bool onMesh = agent.isActiveAndEnabled && agent.isOnNavMesh;
            stopped = onMesh && agent.isStopped;
            hadPath = onMesh && agent.hasPath;
            destination = hadPath ? agent.destination : position;
        }

        public void ClearPath()
        {
            if (navigation != null && navigation.isActiveAndEnabled && navigation.isOnNavMesh)
            { navigation.isStopped = true; navigation.ResetPath(); }
        }

        public void Restore()
        {
            if (navigation == null) return;
            navigation.enabled = enabled;
            if (navigation.isActiveAndEnabled)
            {
                Vector3 ground = position - Vector3.up * navigation.baseOffset;
                if (NavMesh.SamplePosition(ground, out NavMeshHit hit, .75f, navigation.areaMask)
                    && Mathf.Abs(hit.position.y - ground.y) < .5f && navigation.Warp(hit.position) && navigation.isOnNavMesh)
                {
                    if (hadPath) navigation.SetDestination(destination);
                    else navigation.ResetPath();
                    navigation.isStopped = stopped;
                }
            }
            else navigation.transform.position = position;
            navigation.transform.rotation = rotation;
        }
    }

    private static void StopRequests(SurvivalWorld world)
    {
        world.rick.GetComponent<SurvivalDecisionRequester>().enabled = false;
        world.negan.GetComponent<SurvivalDecisionRequester>().enabled = false;
    }

    private static IEnumerator CheckBatSwing(SurvivalAgent agent, SurvivalWalker victim, bool shouldHit)
    {
        Require(!agent.IsSwinging, "Previous committed swing has recovered before the next test attack");
        float initialHealth = victim.Health;
        int initialAttack = agent.AttackSequence, initialHit = agent.HitSequence;
        Vector3 committedFacing = agent.FacingDirection;
        float beganAt = Time.time;
        double timeoutAt = EditorApplication.timeSinceStartup + 10d;
        agent.OnActionReceived(new ActionBuffers(new ActionSegment<float>(new[] { 0f, 0f }),
            new ActionSegment<int>(new[] { 2 })));
        Require(agent.IsSwinging && agent.AttackSequence == initialAttack + 1,
            "Attack action starts exactly one committed swing");

        // Wait in simulated time: impact and recovery should stay reliable if
        // editor frame delivery or simulation speed differs between test runs.
        float impactAt = beganAt + SurvivalAgent.SwingDuration * SurvivalAgent.SwingImpactProgress
            + Time.fixedDeltaTime * 2f;
        while (Time.time < impactAt)
        {
            Require(EditorApplication.timeSinceStartup < timeoutAt, "Combat simulation advances before the test timeout");
            if (agent.IsSwinging)
                Require(Vector3.Angle(committedFacing, agent.FacingDirection) < .1f, "Committed swing keeps its facing");
            yield return 0f;
        }
        float expectedHealth = shouldHit ? Mathf.Max(0f, initialHealth - agent.meleeDamage) : initialHealth;
        int expectedHits = initialHit + (shouldHit ? 1 : 0);
        Require(Mathf.Approximately(victim.Health, expectedHealth) && agent.HitSequence == expectedHits,
            shouldHit ? "Impact applies exactly one bat hit" : "Closed door prevents bat impact damage");

        float readyAt = beganAt + Mathf.Max(SurvivalAgent.SwingDuration, agent.meleeCooldown)
            + Time.fixedDeltaTime * 2f;
        while (Time.time < readyAt || agent.IsSwinging)
        {
            Require(EditorApplication.timeSinceStartup < timeoutAt, "Swing recovers and its cooldown expires before the test timeout");
            if (agent.IsSwinging)
                Require(Vector3.Angle(committedFacing, agent.FacingDirection) < .1f, "Follow-through keeps its committed facing");
            yield return 0f;
        }
        Require(agent.AttackSequence == initialAttack + 1 && agent.HitSequence == expectedHits
            && Mathf.Approximately(victim.Health, expectedHealth), "Recovery cannot apply a second hit");
    }

    private static void Place(SurvivalAgent actor, Vector3 ground)
    {
        Require(actor.navigation.Warp(NavPoint(ground)), "Actor warp for deterministic gameplay check");
        actor.navigation.ResetPath();
        Physics.SyncTransforms();
    }

    private static Vector3 NavPoint(Vector3 ground)
    {
        Require(NavMesh.SamplePosition(ground, out NavMeshHit hit, 1.25f, NavMesh.AllAreas), "Navigation near " + ground);
        Require(Mathf.Abs(hit.position.y - ground.y) < .45f, "Spawn/path stays on the requested floor at " + ground);
        return hit.position;
    }

    private static void CompletePath(Vector3 from, Vector3 to, bool requireClimb)
    {
        NavMeshPath path = new NavMeshPath();
        Require(NavMesh.CalculatePath(NavPoint(from), NavPoint(to), NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
            "Complete navigation route from " + from + " to " + to);
        if (requireClimb) Require(path.corners.Any(c => c.y > 3.8f), "Stair route climbs to upper cells");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("SURVIVAL VALIDATION FAILED: " + message);
    }
}
#endif
