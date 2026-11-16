using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>Per-caster placement preview and one-barricade ownership.</summary>
public sealed class AmeliaCoverPlacement : MonoBehaviour
{
    private SkillBehavior owner;
    private SkillSlot slot;
    private float range;
    private GameObject preview;
    private LineRenderer outline;
    private Material material;
    private DestructibleCover activeCover;
    private float nextPreviewAt;
    public bool IsPreviewing { get; private set; }

    // First press enters placement mode. SkillBehavior consumes cooldown only on confirmation.
    public bool BeginOrConfirm(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        if (!IsPreviewing)
        {
            IsPreviewing = true;
            nextPreviewAt = 0;
            RefreshPreview();
            return false;
        }
        if (!TryPlacement(out position, out rotation)) return false;
        CancelPreview();
        return true;
    }

    public void CancelPreview()
    {
        IsPreviewing = false;
        if (preview != null) preview.SetActive(false);
    }

    public void Configure(SkillBehavior skillOwner, SkillSlot skillSlot, float placementRange)
    {
        owner = skillOwner;
        slot = skillSlot;
        range = placementRange;
        if (!CompareTag("Player") || preview != null) return;
        preview = new GameObject("Barricade placement preview");
        outline = preview.AddComponent<LineRenderer>();
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        outline.sharedMaterial = material;
        outline.useWorldSpace = false;
        outline.widthMultiplier = .035f;
        outline.positionCount = 17;
        float x = DestructibleCover.Size.x * .5f, z = DestructibleCover.Size.z * .5f;
        outline.SetPositions(new[] {
            new Vector3(-x,.04f,-z), new Vector3(x,.04f,-z), new Vector3(x,.04f,z),
            new Vector3(-x,.04f,z), new Vector3(-x,.04f,-z),
            new Vector3(-x,1.35f,-z), new Vector3(x,1.35f,-z), new Vector3(x,.04f,-z),
            new Vector3(x,1.35f,-z), new Vector3(-x,1.35f,-z),
            new Vector3(-x,1.35f,z), new Vector3(x,1.35f,z), new Vector3(x,.04f,z),
            new Vector3(x,1.35f,z), new Vector3(0,1.35f,0), new Vector3(0,5,0),
            new Vector3(0,1.35f,0) });
        preview.SetActive(false);
    }

    public bool TryPlacement(out Vector3 position, out Quaternion rotation)
    {
        Vector3 forward = PlacementDirection();
        rotation = Quaternion.LookRotation(forward);
        Vector3 desired = transform.position + forward * Mathf.Min(4, range);
        position = desired;
        // Ground must be close to the caster's floor, not a roof above it.
        if (!Physics.Raycast(desired + Vector3.up * 2, Vector3.down, out RaycastHit ground, 4,
                ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < .92f ||
            ground.collider.GetComponentInParent<HealthManager>() != null) return false;
        position = ground.point;
        if (!NavMesh.SamplePosition(position, out NavMeshHit nav, .3f, NavMesh.AllAreas)) return false;
        if (Mathf.Abs(nav.position.y - position.y) > .15f) return false;
        position = nav.position;
        Vector3 center = position + Vector3.up * (DestructibleCover.Size.y * .5f + .06f);
        // Includes the caster and all characters. Recasting cannot overlap the existing cover.
        if (Physics.CheckBox(center, DestructibleCover.Size * .5f, rotation, ~0,
            QueryTriggerInteraction.Ignore)) return false;
        if (Physics.BoxCast(center, DestructibleCover.Size * .5f, Vector3.up, out _, rotation,
            DestructibleCover.DropHeight, ~0, QueryTriggerInteraction.Ignore)) return false;
        // Both ends need walkable clearance: do not seal a narrow corridor.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 end = position + rotation * new Vector3(side * 2.15f, 0, 0);
            if (!NavMesh.SamplePosition(end, out NavMeshHit edge, .2f, NavMesh.AllAreas) ||
                Mathf.Abs(edge.position.y - position.y) > .15f ||
                Physics.CheckCapsule(end + Vector3.up * .4f, end + Vector3.up * 1.3f, .32f,
                    ~0, QueryTriggerInteraction.Ignore)) return false;
        }
        // Four feet must all be supported; don't bridge a ledge or stair edge.
        foreach (float x in new[] { -1.5f, 1.5f })
            foreach (float z in new[] { -.22f, .22f })
            {
                Vector3 foot = position + rotation * new Vector3(x, .25f, z);
                if (!Physics.Raycast(foot, Vector3.down, out RaycastHit support, .45f, ~0,
                    QueryTriggerInteraction.Ignore) || support.normal.y < .92f ||
                    support.collider.GetComponentInParent<HealthManager>() != null) return false;
            }
        // Reachability prevents placing through a nearby wall, even if the camera can see over it.
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 1, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete) return false;
        float length = 0;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i-1], path.corners[i]);
        return length <= range * 1.5f;
    }

    public Vector3 PlacementDirection()
    {
        // Use the same camera-relative input basis as MoveBehaviour, including strafing/backpedalling.
        BasicBehaviour movement = GetComponent<BasicBehaviour>();
        if (movement != null && movement.playerCamera != null &&
            new Vector2(movement.GetH, movement.GetV).sqrMagnitude > .01f)
        {
            Vector3 forward = Vector3.ProjectOnPlane(movement.playerCamera.forward, Vector3.up).normalized;
            Vector3 direction = forward * movement.GetV + Vector3.Cross(Vector3.up, forward) * movement.GetH;
            if (direction.sqrMagnitude > .001f) return direction.normalized;
        }
        Rigidbody body = GetComponent<Rigidbody>();
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh) velocity = agent.velocity;
        velocity.y = 0;
        if (velocity.sqrMagnitude > .04f) return velocity.normalized;
        Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return facing.sqrMagnitude > .001f ? facing.normalized : Vector3.forward;
    }

    public void Deploy(Vector3 position, Quaternion rotation, float health, float duration)
    {
        CancelPreview();
        if (activeCover != null) activeCover.Remove();
        activeCover = DestructibleCover.Create(gameObject, position, rotation, health, duration, true,
            () =>
            {
                if (owner != null && owner.GetCooldowns() != null)
                    owner.GetCooldowns()[slot] = Time.time;
                if (CompareTag("Player")) SkillToast.Show("DROP BLOCKED", Color.red);
            });
        if (preview != null)
        {
            preview.transform.SetPositionAndRotation(position, rotation);
            SetPreviewColor(true);
            preview.SetActive(true);
        }
    }

    private void SetPreviewColor(bool valid)
    {
        Color color = valid ? new Color(.3f, .95f, .8f) : new Color(1, .2f, .15f);
        material.SetColor("_BaseColor", color);
        material.color = color;
    }

    private void RefreshPreview()
    {
        if (preview == null) return;
        bool falling = activeCover != null && !activeCover.IsLanded && !activeCover.dead;
        preview.SetActive(IsPreviewing || falling);
        if (falling || !IsPreviewing) return; // Confirmed marker stays at the locked landing point.
        bool valid = TryPlacement(out Vector3 position, out Quaternion rotation);
        preview.transform.SetPositionAndRotation(position, rotation);
        SetPreviewColor(valid);
    }

    private void Update()
    {
        var health = GetComponent<HealthManager>();
        if ((health != null && health.dead) || (IsPreviewing && owner != null && !owner.IsSkillReady(slot)))
            CancelPreview();
        if (IsPreviewing && CompareTag("Player") && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CancelPreview();
        if (Time.time < nextPreviewAt) return;
        nextPreviewAt = Time.time + .05f;
        RefreshPreview();
    }

    private void OnDisable() { CancelPreview(); }
    private void OnDestroy()
    {
        if (activeCover != null) activeCover.Remove();
        if (preview != null) Destroy(preview);
        if (material != null) Destroy(material);
    }
}
