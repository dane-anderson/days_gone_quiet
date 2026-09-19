using UnityEngine;

public class WalkerChase : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 2f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;

        // Stop before Walker overlaps Rick
        if (distance < 1.2f)
            return;

        Vector3 nextPosition =
            rb.position + direction.normalized * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);
    }
}