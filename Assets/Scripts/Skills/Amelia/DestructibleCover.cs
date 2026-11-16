using System.Collections;
using System.Collections.Generic;
using EnemyAI;
using UnityEngine;
using UnityEngine.AI;

/// <summary>A solid, damageable field barricade. It is scenery, never a combat target or ally.</summary>
public sealed class DestructibleCover : HealthManager
{
    public static readonly Vector3 Size = new Vector3(3.2f, 1.35f, .55f);
    public const float DropHeight = 12f;
    public const float DropSeconds = .85f;
    public bool IsLanded { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    private float expiresAt;
    private BoxCollider body;
    private NavMeshObstacle obstacle;
    private readonly List<CoverLookup> lookups = new();
    private readonly List<Renderer> panels = new();
    private readonly List<Material> materials = new();
    private MaterialPropertyBlock tint;
    private Transform healthFill, healthBar;
    private GameObject owner;

    public static DestructibleCover Create(GameObject owner, Vector3 position, Quaternion rotation,
        float health, float duration, bool dropFromSky = false, System.Action onDropBlocked = null)
    {
        var go = new GameObject("Amelia Field Barricade");
        go.transform.SetPositionAndRotation(position, rotation);
        int layer = LayerMask.NameToLayer("Cover");
        if (layer >= 0) go.layer = layer;
        var cover = go.AddComponent<DestructibleCover>();
        cover.owner = owner;
        cover.MaxHealth = cover.Health = Mathf.Max(1, health);
        cover.expiresAt = float.PositiveInfinity;
        cover.Build();
        if (dropFromSky)
        {
            cover.body.enabled = false;
            cover.obstacle.enabled = false;
            cover.healthBar.gameObject.SetActive(false);
            cover.transform.position = position + Vector3.up * DropHeight;
            cover.StartCoroutine(cover.Fall(position, duration, onDropBlocked));
        }
        else cover.Land(duration);
        return cover;
    }

    private IEnumerator Fall(Vector3 landing, float duration, System.Action onBlocked)
    {
        float elapsed = 0;
        while (elapsed < DropSeconds)
        {
            float progress = Mathf.Clamp01(elapsed / DropSeconds);
            transform.position = landing + Vector3.up * (DropHeight * (1 - progress * progress));
            yield return null;
            elapsed += Time.deltaTime;
        }
        transform.position = landing;
        Physics.SyncTransforms();
        // The landing point is fixed on confirmation. Do not trap anyone who walked into it meanwhile.
        Vector3 center = landing + Vector3.up * (Size.y * .5f + .06f);
        if (Physics.CheckBox(center, Size * .5f, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            onBlocked?.Invoke();
            Remove();
            yield break;
        }
        Land(duration);
    }

    private void Land(float duration)
    {
        IsLanded = true;
        body.enabled = true;
        obstacle.enabled = true;
        healthBar.gameObject.SetActive(true);
        expiresAt = Time.time + Mathf.Max(.1f, duration);
        StartCoroutine(RegisterAfterCarving());
    }

    private void Build()
    {
        body = gameObject.AddComponent<BoxCollider>();
        body.center = Vector3.up * Size.y * .5f;
        body.size = Size;
        obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = body.center;
        obstacle.size = Size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        Material steel = MakeMaterial(new Color(.14f, .17f, .19f), .7f);
        Material wine = MakeMaterial(new Color(.28f, .055f, .09f), .45f);
        Material gold = MakeMaterial(new Color(.8f, .59f, .24f), .65f);
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 1.05f;
            panels.Add(Part("Armor plate", new Vector3(x, .73f, 0), new Vector3(1.01f, 1.2f, .42f), wine));
            Part("Upper rail", new Vector3(x, 1.3f, 0), new Vector3(1.04f, .1f, .55f), steel);
            Part("Reinforcement", new Vector3(x, .73f, -.235f), new Vector3(.08f, 1.1f, .05f), gold);
            Part("Foot", new Vector3(x, .07f, 0), new Vector3(.7f, .14f, .55f), steel);
        }
        Part("Lower rail", new Vector3(0, .22f, 0), new Vector3(3.2f, .17f, .5f), steel);
        var bar = new GameObject("Durability");
        healthBar = bar.transform;
        healthBar.SetParent(transform, false);
        healthBar.localPosition = new Vector3(0, 1.65f, 0);
        Renderer background = Part("Durability background", Vector3.zero, new Vector3(1.5f, .1f, .035f), steel);
        background.transform.SetParent(healthBar, false);
        Renderer fill = Part("Durability remaining", Vector3.zero, new Vector3(1.44f, .065f, .045f), gold);
        healthFill = fill.transform;
        healthFill.SetParent(healthBar, false);
        tint = new MaterialPropertyBlock();
    }

    private Material MakeMaterial(Color color, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader);
        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", .35f);
        materials.Add(material);
        return material;
    }

    private Renderer Part(string label, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = label;
        part.layer = gameObject.layer;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    public Vector3[] GetCoverSpots()
    {
        var spots = new List<Vector3>();
        if (!IsLanded || dead) return spots.ToArray();
        // Explicit points outside the carved footprint; avoid sampling into the wall.
        foreach (float side in new[] { -1f, 1f })
            foreach (float x in new[] { -.85f, .85f })
            {
                Vector3 point = transform.TransformPoint(new Vector3(x, 0, side * .9f));
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, .35f, NavMesh.AllAreas))
                    spots.Add(hit.position);
            }
        return spots.ToArray();
    }

    private IEnumerator RegisterAfterCarving()
    {
        // Carving is asynchronous. Refresh the few points once it has settled.
        yield return null;
        yield return null;
        Register();
        yield return new WaitForSeconds(.25f);
        Register();
    }

    private void Register()
    {
        if (dead || !IsLanded) return;
        Vector3[] spots = GetCoverSpots();
        foreach (CoverLookup lookup in FindObjectsByType<CoverLookup>(FindObjectsSortMode.None))
        {
            lookup.RegisterDynamicCover(gameObject, spots);
            if (!lookups.Contains(lookup)) lookups.Add(lookup);
        }
    }

    public override void TakeDamage(Vector3 location, Vector3 direction, float damage,
        Collider bodyPart = null, GameObject origin = null, DamageType damageType = DamageType.Normal)
    {
        if (dead || !IsLanded || !float.IsFinite(damage) || damage <= 0) return;
        Health = Mathf.Max(0, Health - damage);
        float ratio = Health / MaxHealth;
        healthFill.localScale = new Vector3(1.44f * ratio, .065f, .045f);
        healthFill.localPosition = new Vector3(-.72f * (1 - ratio), 0, 0);
        Color stage = ratio > .66f ? new Color(.28f, .055f, .09f)
            : ratio > .33f ? new Color(.37f, .2f, .075f) : new Color(.12f, .09f, .075f);
        tint.SetColor("_BaseColor", stage);
        foreach (Renderer panel in panels) panel.SetPropertyBlock(tint);
        if (Health <= 0) Remove();
    }

    private void Update()
    {
        if (Time.time >= expiresAt || owner == null) { Remove(); return; }
        Camera camera = Camera.main;
        if (camera != null && healthBar != null) healthBar.rotation = camera.transform.rotation;
    }

    public void Remove()
    {
        if (dead) return;
        dead = true;
        gameObject.SetActive(false); // Immediately release collider, path obstruction and cover protection.
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        foreach (CoverLookup lookup in lookups)
            if (lookup != null) lookup.UnregisterDynamicCover(gameObject);
        lookups.Clear();
        // Includes lookups initialized after this cover was created.
        foreach (CoverLookup lookup in FindObjectsByType<CoverLookup>(FindObjectsSortMode.None))
            lookup.UnregisterDynamicCover(gameObject);
        foreach (StateController controller in FindObjectsByType<StateController>(FindObjectsSortMode.None))
            controller.NotifyCoverRemoved(gameObject.GetHashCode());
    }

    private void OnDestroy()
    {
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
