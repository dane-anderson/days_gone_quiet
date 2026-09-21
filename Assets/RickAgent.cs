using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class RickAgent : Agent
{
    public Transform walker;
    public float moveSpeed = 4f;

    private Vector3 rickStartPosition;
    private Rigidbody rickBody;
    private Rigidbody walkerBody;
    private RLExperimentControls experimentControls;

    public void SetExperimentControls(RLExperimentControls controls)
    {
        experimentControls = controls;
    }

    public override void Initialize()
    {
        rickBody = GetComponent<Rigidbody>();
        walkerBody = walker != null ? walker.GetComponent<Rigidbody>() : null;
        if (experimentControls == null)
            experimentControls = FindAnyObjectByType<RLExperimentControls>();
        rickStartPosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        rickBody.position = rickStartPosition;
        rickBody.linearVelocity = Vector3.zero;
        rickBody.angularVelocity = Vector3.zero;

        if (walker == null)
            return;

        float spawnHalfExtent = experimentControls != null
            ? experimentControls.WalkerSpawnHalfExtent
            : 8f;
        float minimumSpawnDistance = experimentControls != null
            ? experimentControls.MinimumWalkerSpawnDistance
            : 4f;
        float spawnY = experimentControls != null
            ? experimentControls.WalkerSpawnY
            : 1f;

        Vector3 walkerPosition = new Vector3(
            rickStartPosition.x >= 0f ? -spawnHalfExtent : spawnHalfExtent,
            spawnY,
            rickStartPosition.z >= 0f ? -spawnHalfExtent : spawnHalfExtent
        );

        for (int attempt = 0; attempt < 64; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(-spawnHalfExtent, spawnHalfExtent),
                spawnY,
                Random.Range(-spawnHalfExtent, spawnHalfExtent)
            );

            if (Vector3.Distance(candidate, rickStartPosition) >= minimumSpawnDistance)
            {
                walkerPosition = candidate;
                break;
            }
        }

        if (walkerBody != null)
        {
            walkerBody.position = walkerPosition;
            if (!walkerBody.isKinematic)
            {
                walkerBody.linearVelocity = Vector3.zero;
                walkerBody.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            walker.position = walkerPosition;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(walker != null ? walker.localPosition : Vector3.zero);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float x = actions.ContinuousActions[0];
        float z = actions.ContinuousActions[1];

        Vector3 movement = Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f);

        float currentMoveSpeed = experimentControls != null
            ? experimentControls.RickMoveSpeed
            : moveSpeed;
        float arenaHalfExtent = experimentControls != null
            ? experimentControls.ArenaHalfExtent
            : 24.5f;

        Vector3 nextPosition = rickBody.position +
            movement * currentMoveSpeed * Time.fixedDeltaTime;

        if (Mathf.Abs(nextPosition.x) > arenaHalfExtent ||
            Mathf.Abs(nextPosition.z) > arenaHalfExtent ||
            nextPosition.y < 0f)
        {
            AddReward(experimentControls != null
                ? experimentControls.BoundaryPenalty
                : -1f);
            EndEpisode();
            return;
        }

        rickBody.MovePosition(nextPosition);

        AddReward(experimentControls != null
            ? experimentControls.SurvivalRewardPerStep
            : 0.001f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> actions = actionsOut.ContinuousActions;
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            actions[0] = 0f;
            actions[1] = 0f;
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            horizontal += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            vertical -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            vertical += 1f;

        Vector2 input = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        actions[0] = input.x;
        actions[1] = input.y;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Walker"))
        {
            AddReward(experimentControls != null
                ? experimentControls.CaughtPenalty
                : -1f);
            EndEpisode();
        }
    }
}
