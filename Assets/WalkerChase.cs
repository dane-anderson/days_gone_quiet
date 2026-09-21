using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WalkerChase : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 2f;

    private Rigidbody rb;
    private RLExperimentControls experimentControls;

    public void SetExperimentControls(RLExperimentControls controls)
    {
        experimentControls = controls;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (experimentControls == null)
            experimentControls = FindAnyObjectByType<RLExperimentControls>();
    }

    void FixedUpdate()
    {
        if (target == null || rb == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        float currentMoveSpeed = experimentControls != null
            ? experimentControls.WalkerMoveSpeed
            : moveSpeed;

        Vector3 nextPosition = rb.position +
            direction.normalized * currentMoveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);
    }
}
