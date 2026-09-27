using UnityEngine;
using Unity.MLAgents;

// An explicit learner binding and enable/disable lifecycle keep paused rounds out of training.
[DisallowMultipleComponent]
[RequireComponent(typeof(SurvivalAgent))]
[DefaultExecutionOrder(-10)]
public sealed class SurvivalDecisionRequester : MonoBehaviour
{
    public SurvivalAgent agent;
    [Range(1, 20)] public int DecisionPeriod = 5;
    [Range(0, 19)] public int DecisionStep;
    public bool TakeActionsBetweenDecisions = true;
    private Academy subscribedAcademy;

    public SurvivalAgent Agent => agent;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (agent == null) agent = GetComponent<SurvivalAgent>();
        Unsubscribe();
        subscribedAcademy = Academy.Instance;
        subscribedAcademy.AgentPreStep += RequestForStep;
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        if (subscribedAcademy == null) return;
        subscribedAcademy.AgentPreStep -= RequestForStep;
        subscribedAcademy = null;
    }

    private void RequestForStep(int academyStep)
    {
        if (!isActiveAndEnabled || agent == null || !agent.isActiveAndEnabled || !agent.IsAlive) return;
        SurvivalWorld world = agent.world;
        if (world == null || world.IsPaused || world.IsRoundEnding || !world.IsParticipant(agent)) return;
        int period = Mathf.Clamp(DecisionPeriod, 1, 20);
        int decisionStep = Mathf.Clamp(DecisionStep, 0, period - 1);
        if (academyStep % period == decisionStep)
            agent.RequestDecision();
        else if (TakeActionsBetweenDecisions)
            agent.RequestAction();
    }
}
