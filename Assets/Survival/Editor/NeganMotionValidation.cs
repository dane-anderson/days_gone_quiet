#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;

/// <summary>Observes the live learner; never submits actions or changes world state.</summary>
public static class NeganMotionValidation
{
    private static SurvivalWorld world;
    private static SurvivalAgent agent;
    private static SurvivalCombatVisual visual;
    private static Transform body, mount;
    private static double began;
    private static int frame, previousAttack, previousHit, previousRound, sampledFrames, lockedSamples, observedHitsInAttack;
    private static bool previousSwing;
    private static Vector3 previousFacing;
    private static float minAngle, maxAngle;
    private static readonly HashSet<int> sampledAttacks = new HashSet<int>();
    private static readonly HashSet<int> completedAttacks = new HashSet<int>();

    [MenuItem("Days Gone Quiet/Validate Live Negan Motion (Read Only)", priority = 41)]
    public static void ObserveLive()
    {
        Stop();
        try
        {
            Require(Application.isPlaying, "Start Play before observing live motion.");
            world = UnityEngine.Object.FindAnyObjectByType<SurvivalWorld>();
            Require(world != null && world.negan != null && world.rick != null, "Both survivors must exist.");
            agent = world.negan;
            visual = agent.GetComponent<SurvivalCombatVisual>();
            body = agent.transform.Find("Character Visual");
            mount = agent.batPivot != null ? agent.batPivot.parent : null;
            Require(visual != null && visual.enabled && body != null && mount != null, "Negan's body and combat presentation must be present.");
            foreach (SurvivalAgent survivor in new[] { world.rick, world.negan })
            {
                BehaviorParameters policy = survivor.GetComponent<BehaviorParameters>();
                Require(policy != null && policy.BrainParameters.VectorObservationSize == SurvivalAgent.ObservationSize,
                    "Observation shape must remain 54 for " + survivor.name);
                Require(policy.BrainParameters.ActionSpec.NumContinuousActions == 2
                    && policy.BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 3 }),
                    "Action shape must remain 2 continuous and one 3-choice branch for " + survivor.name);
                Require(policy.BehaviorName == (survivor.role == SurvivorRole.Rick ? "RickEscape" : "NeganCombat"),
                    "The existing training behavior name must be preserved.");
            }
            CheckPose();
            began = EditorApplication.timeSinceStartup;
            frame = Time.frameCount;
            previousAttack = agent.AttackSequence;
            previousHit = agent.HitSequence;
            previousRound = world.Round;
            previousSwing = agent.IsSwinging;
            previousFacing = agent.FacingDirection;
            sampledFrames = lockedSamples = observedHitsInAttack = 0;
            minAngle = float.PositiveInfinity;
            maxAngle = float.NegativeInfinity;
            sampledAttacks.Clear();
            completedAttacks.Clear();
            EditorApplication.update += ObserveFrame;
            Debug.Log("LIVE NEGAN MOTION: observing for up to 20 seconds; training, actions, rewards, spawns, and pause state remain untouched.");
        }
        catch (Exception error) { Stop(); Debug.LogError("LIVE NEGAN MOTION FAIL: " + error.Message); }
    }

    private static void ObserveFrame()
    {
        try
        {
            if (!Application.isPlaying || world == null || agent == null)
            { Stop(); Debug.LogWarning("LIVE NEGAN MOTION INCONCLUSIVE: Play ended during observation."); return; }
            double elapsed = EditorApplication.timeSinceStartup - began;
            if (Time.frameCount != frame)
            {
                frame = Time.frameCount;
                int attacks = agent.AttackSequence, hits = agent.HitSequence;
                Require(attacks >= previousAttack && hits >= previousHit, "Attack and hit counters must remain monotonic across rounds.");
                // Across skipped frames, allow one hit for each newly started attack
                // plus the previously in-progress attack. The latter can hit once.
                int allowedHits = attacks - previousAttack + (previousSwing ? 1 - observedHitsInAttack : 0);
                Require(hits - previousHit <= allowedHits, "More hits were reported than the observed attacks could deliver.");
                if (attacks == previousAttack)
                {
                    observedHitsInAttack += hits - previousHit;
                    Require(observedHitsInAttack <= 1, "One swing delivered more than one hit.");
                }
                else
                    observedHitsInAttack = !previousSwing && attacks == previousAttack + 1 ? hits - previousHit : 0;
                if (previousSwing && (attacks != previousAttack || !agent.IsSwinging))
                    completedAttacks.Add(previousAttack);
                if (agent.IsSwinging)
                {
                    sampledAttacks.Add(attacks);
                    if (previousSwing && attacks == previousAttack && world.Round == previousRound)
                    {
                        Require(Vector3.Angle(previousFacing, agent.FacingDirection) < .1f, "Facing changed during a committed swing.");
                        lockedSamples++;
                    }
                }
                // Use the visual's own phase: at high training speed the next
                // physics step may already have advanced the agent's phase.
                float phase = visual.DisplayedSwingProgress;
                if (phase > 0f && phase < 1f)
                {
                    SurvivalCombatVisual.EvaluatePose(phase, true, out float expected, out _, out _);
                    Require(Mathf.Abs(Mathf.Abs(expected) - Mathf.Abs(visual.DisplayedBatAngle)) < .1f, "The displayed bat pose does not match its swing phase.");
                    Require(Quaternion.Angle(body.rotation, mount.rotation) < .1f, "Body and bat mount use different billboard rotations.");
                    minAngle = Mathf.Min(minAngle, visual.DisplayedBatAngle);
                    maxAngle = Mathf.Max(maxAngle, visual.DisplayedBatAngle);
                    sampledFrames++;
                }
                previousAttack = attacks; previousHit = hits; previousSwing = agent.IsSwinging;
                previousFacing = agent.FacingDirection; previousRound = world.Round;
            }
            if (elapsed >= 3d && completedAttacks.Count >= 2 && sampledFrames >= 4 && lockedSamples >= 2 && maxAngle - minAngle >= 30f)
            {
                Stop();
                Debug.Log("LIVE NEGAN MOTION PASS: unchanged policy shapes; " + completedAttacks.Count
                    + " completed swings, " + lockedSamples + " facing-lock samples, " + sampledFrames
                    + " aligned body/bat frames, visible bat arc " + (maxAngle - minAngle).ToString("0")
                    + " degrees; hit counters consistent. No world or learner state was changed.");
            }
            else if (elapsed >= 20d)
            {
                Stop();
                Debug.LogWarning("LIVE NEGAN MOTION INCONCLUSIVE: observed " + completedAttacks.Count
                    + " completed swings and " + sampledFrames + " visual samples. Fast training, pause, or infrequent attacks can hide phases; observe again at speed 1 for full motion. No world state was changed.");
            }
        }
        catch (Exception error) { Stop(); Debug.LogError("LIVE NEGAN MOTION FAIL: " + error.Message); }
    }

    private static void CheckPose()
    {
        SurvivalCombatVisual.EvaluatePose(.3f, true, out float windup, out _, out _);
        SurvivalCombatVisual.EvaluatePose(.5f, true, out float strike, out _, out _);
        SurvivalCombatVisual.EvaluatePose(.68f, true, out float followThrough, out _, out _);
        SurvivalCombatVisual.EvaluatePose(1f, true, out float recovered, out _, out _);
        SurvivalCombatVisual.EvaluatePose(.5f, false, out float idle, out _, out _);
        Require(windup > 40f && strike < -100f && followThrough < strike && Mathf.Abs(recovered) < .01f && Mathf.Abs(idle) < .01f,
            "Swing must have anticipation, strike, follow-through, and neutral recovery.");
        Require(Mathf.Approximately(SurvivalAgent.SwingDuration, .6f)
            && Mathf.Approximately(SurvivalAgent.SwingImpactProgress, .45f), "Impact timing must match the visible strike.");
    }

    private static void Stop() { EditorApplication.update -= ObserveFrame; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
