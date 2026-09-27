using UnityEngine;
using UnityEngine.AI;

/// <summary>A sliding door with a real blocker, navigation carving and guarded closing.</summary>
[DisallowMultipleComponent]
public sealed class SurvivalDoor : MonoBehaviour
{
    public string displayName = "Security door";
    public Transform leaf;
    public Collider blockingCollider;
    public NavMeshObstacle obstacle;
    public Vector3 closedLocalPosition, openLocalPosition;
    public bool startOpen = true;
    [Min(1f)] public float maximumHealth = 100f;
    [Min(.1f)] public float toggleCooldown = .75f;

    [SerializeField] private bool isOpen = true;
    [SerializeField] private float health = 100f;
    private float nextToggleTime;

    public bool IsOpen => isOpen;
    public float Health => health;
    public Vector3 InteractionPoint
    {
        get { ClosedBox(out Vector3 center, out _, out _); return center; }
    }

    private void Awake() => ResetDoor();

    public void ResetDoor()
    {
        health = Mathf.Max(1f, maximumHealth);
        nextToggleTime = 0f;
        // The shared world resets doors after placing everyone at clear spawn points.
        ApplyOpen(startOpen);
    }

    public bool TryToggle(SurvivalAgent actor)
    {
        if (actor == null || !actor.IsAlive || !actor.isActiveAndEnabled ||
            (actor.world != null && (actor.world.IsPaused || !actor.world.IsParticipant(actor))) ||
            Time.time < nextToggleTime || health <= 0f)
            return false;
        Vector3 from = actor.transform.position + Vector3.up * .25f;
        if ((InteractionPoint - actor.transform.position).sqrMagnitude > 9f ||
            (actor.world != null && !actor.world.HasLineOfSight(from, InteractionPoint, this)))
            return false;
        if (isOpen && !DoorwayClear())
            return false;
        ApplyOpen(!isOpen);
        nextToggleTime = Time.time + Mathf.Max(.1f, toggleCooldown);
        return true;
    }

    public void SetOpen(bool open)
    {
        if (!open && (health <= 0f || !DoorwayClear()))
            return;
        ApplyOpen(open);
    }

    public void Bash(float damage)
    {
        if (isOpen || health <= 0f || damage <= 0f)
            return;
        health = Mathf.Max(0f, health - damage);
        if (health <= 0f)
        {
            ApplyOpen(true);
            nextToggleTime = float.PositiveInfinity;
        }
    }

    private void ApplyOpen(bool open)
    {
        isOpen = open;
        if (obstacle != null) obstacle.enabled = false;
        if (blockingCollider != null) blockingCollider.enabled = false;
        if (leaf != null) leaf.localPosition = open ? openLocalPosition : closedLocalPosition;
        if (blockingCollider != null) blockingCollider.enabled = !open;
        if (obstacle != null)
        {
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingTimeToStationary = 0f;
            obstacle.enabled = !open;
        }
        if (Application.isPlaying) Physics.SyncTransforms();
    }

    public bool DoorwayClear()
    {
        ClosedBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
        // Include trigger capsules as well as solid actors. A small margin avoids closing
        // a leaf against an actor standing exactly on the edge of its future volume.
        Collider[] overlaps = Physics.OverlapBox(center, halfExtents + Vector3.one * .12f,
            rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        foreach (Collider item in overlaps)
        {
            if (item == null || item == blockingCollider || item.transform.IsChildOf(transform)) continue;
            if (item is CharacterController || item.GetComponentInParent<SurvivalAgent>() != null ||
                item.GetComponentInParent<SurvivalWalker>() != null ||
                item.GetComponentInParent<NavMeshAgent>() != null)
                return false;
        }
        return true;
    }

    private void ClosedBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        Vector3 shift = Vector3.zero;
        if (leaf != null && (blockingCollider == null || blockingCollider.transform == leaf ||
            blockingCollider.transform.IsChildOf(leaf)))
            shift = leaf.parent != null ? leaf.parent.TransformVector(closedLocalPosition - leaf.localPosition)
                : closedLocalPosition - leaf.localPosition;
        if (blockingCollider is BoxCollider box)
        {
            center = box.transform.TransformPoint(box.center) + shift;
            Vector3 scale = box.transform.lossyScale;
            halfExtents = Vector3.Scale(box.size * .5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            rotation = box.transform.rotation;
            return;
        }
        if (blockingCollider != null && blockingCollider.enabled)
        {
            center = blockingCollider.bounds.center + shift;
            halfExtents = blockingCollider.bounds.extents;
            rotation = Quaternion.identity;
            return;
        }
        center = leaf != null ? leaf.position + shift : transform.position + Vector3.up;
        halfExtents = new Vector3(.75f, 1.7f, .15f);
        rotation = leaf != null ? leaf.rotation : transform.rotation;
    }
}
