using UnityEngine;
using Unity.MLAgents;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class RLExperimentControls : MonoBehaviour
{
    [Header("Scene References (leave assigned)")]
    [SerializeField] private RickAgent rickAgent;
    [SerializeField] private WalkerChase walkerChase;
    [SerializeField] private DecisionRequester decisionRequester;

    [Header("Difficulty")]
    [Tooltip("How fast Rick can move.")]
    [Min(0f)] public float rickMoveSpeed = 4f;
    [Tooltip("How fast the Walker chases Rick.")]
    [Min(0f)] public float walkerMoveSpeed = 2f;

    [Header("Rewards")]
    [Tooltip("Total positive reward Rick earns if he survives all the way to Max Episode Steps. It is divided evenly across the episode.")]
    public float fullEpisodeSurvivalReward = 1f;
    [Tooltip("Reward applied when the Walker catches Rick.")]
    public float caughtPenalty = -1f;
    [Tooltip("Reward applied when Rick leaves the arena or falls.")]
    public float boundaryPenalty = -1f;

    [Header("Arena And Reset")]
    [Tooltip("Half-width of the playable square arena.")]
    [Min(1f)] public float arenaHalfExtent = 24.5f;
    [Tooltip("Walker spawns randomly between minus and plus this value on X and Z.")]
    [Min(0.5f)] public float walkerSpawnHalfExtent = 8f;
    [Tooltip("Minimum starting distance between Rick and the Walker.")]
    [Min(0f)] public float minimumWalkerSpawnDistance = 4f;
    [Tooltip("Walker height when an episode starts.")]
    public float walkerSpawnY = 1f;

    [Header("Episode And Decisions")]
    [Tooltip("Episode length in physics steps. At 50 Hz, 1000 steps is about 20 simulated seconds.")]
    [Min(1)] public int maxEpisodeSteps = 1000;
    [Tooltip("Physics steps between new policy decisions. The previous action repeats between decisions.")]
    [Range(1, 20)] public int decisionPeriod = 1;

    [Header("Training Speed")]
    [Tooltip("Simulation speed while the Python trainer is connected. Normal Play mode remains at 1x.")]
    [Range(1f, 20f)] public float trainingTimeScale = 20f;

    public float RickMoveSpeed => rickMoveSpeed;
    public float WalkerMoveSpeed => walkerMoveSpeed;
    public float SurvivalRewardPerStep =>
        fullEpisodeSurvivalReward / Mathf.Max(1, maxEpisodeSteps);
    public float CaughtPenalty => caughtPenalty;
    public float BoundaryPenalty => boundaryPenalty;
    public float ArenaHalfExtent => arenaHalfExtent;
    public float WalkerSpawnHalfExtent => walkerSpawnHalfExtent;
    public float MinimumWalkerSpawnDistance => minimumWalkerSpawnDistance;
    public float WalkerSpawnY => walkerSpawnY;

    private void Reset()
    {
        ResolveReferences();
        ApplySettings();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplySettings();
    }

    private void OnValidate()
    {
        rickMoveSpeed = Mathf.Max(0f, rickMoveSpeed);
        walkerMoveSpeed = Mathf.Max(0f, walkerMoveSpeed);
        arenaHalfExtent = Mathf.Max(1f, arenaHalfExtent);
        walkerSpawnHalfExtent = Mathf.Max(0.5f, walkerSpawnHalfExtent);
        minimumWalkerSpawnDistance = Mathf.Max(0f, minimumWalkerSpawnDistance);
        maxEpisodeSteps = Mathf.Max(1, maxEpisodeSteps);
        decisionPeriod = Mathf.Clamp(decisionPeriod, 1, 20);
        trainingTimeScale = Mathf.Clamp(trainingTimeScale, 1f, 20f);

        ResolveReferences();
        ApplySettings();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !Academy.IsInitialized)
            return;

        float desiredTimeScale = Academy.Instance.IsCommunicatorOn
            ? trainingTimeScale
            : 1f;

        if (Time.timeScale > 0f &&
            !Mathf.Approximately(Time.timeScale, desiredTimeScale))
        {
            Time.timeScale = desiredTimeScale;
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying && Time.timeScale > 0f)
            Time.timeScale = 1f;
    }

    private void ResolveReferences()
    {
        if (rickAgent == null)
            rickAgent = FindAnyObjectByType<RickAgent>();

        if (walkerChase == null)
            walkerChase = FindAnyObjectByType<WalkerChase>();

        if (decisionRequester == null && rickAgent != null)
            decisionRequester = rickAgent.GetComponent<DecisionRequester>();
    }

    private void ApplySettings()
    {
        if (rickAgent != null)
        {
            rickAgent.SetExperimentControls(this);
            rickAgent.MaxStep = maxEpisodeSteps;
        }

        if (walkerChase != null)
            walkerChase.SetExperimentControls(this);

        if (decisionRequester != null)
        {
            decisionRequester.DecisionPeriod = decisionPeriod;
            decisionRequester.DecisionStep = 0;
            decisionRequester.TakeActionsBetweenDecisions = true;
        }
    }
}
