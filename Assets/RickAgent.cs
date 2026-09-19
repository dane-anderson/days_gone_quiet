using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RickAgent : Agent
{
    public Transform walker;
    public float moveSpeed = 4f;

    private Vector3 rickStartPosition;

    public override void Initialize()
    {
        rickStartPosition = transform.localPosition;
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = rickStartPosition;

        walker.localPosition = new Vector3(
            Random.Range(-8f, 8f),
            1f,
            Random.Range(-8f, 8f)
        );
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(walker.localPosition);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float x = actions.ContinuousActions[0];
        float z = actions.ContinuousActions[1];

        Vector3 movement = new Vector3(x, 0f, z);

        transform.position += movement * moveSpeed * Time.deltaTime;

        AddReward(0.001f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Walker"))
        {
            AddReward(-1f);
            EndEpisode();
        }
    }
}