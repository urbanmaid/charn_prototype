using UnityEngine;

/// <summary>Stationary practice target with an unlimited health reservoir and real hit feedback.</summary>
public sealed class StationaryTrainingDummy : HealthManager, ITeamCode
{
    public int TeamCode { get; set; } = 1;
    public int GetTeamCode() => TeamCode;
    public void SetTeamCode(int value) => TeamCode = value;
    public int Hits { get; private set; }
    public int Headshots { get; private set; }
    private Collider head;
    private float nextSpray, nextMark;
    private Material material;
    private Renderer flashingPart;
    private float flashUntil;
    private MaterialPropertyBlock flashTint;

    private void TintPart(Renderer target, Color color)
    {
        if (target == null) return;
        flashTint ??= new MaterialPropertyBlock();
        flashTint.SetColor("_BaseColor", color);
        flashTint.SetColor("_Color", color);
        target.SetPropertyBlock(flashTint);
    }

    private void RestoreFlash()
    {
        if (flashingPart == null) return;
        TintPart(flashingPart, flashingPart.GetComponent<Collider>() == head
            ? new Color(1f, .6f, .15f) : new Color(.6f, .66f, .7f));
        flashingPart = null;
    }

    private void Update()
    {
        if (Time.time >= flashUntil) RestoreFlash();
    }

    public static StationaryTrainingDummy Create(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("Training Dummy - Infinite HP");
        root.transform.SetPositionAndRotation(position, rotation);
        int layer = LayerMask.NameToLayer("Enemy");
        if (layer >= 0) root.layer = layer;
        var dummy = root.AddComponent<StationaryTrainingDummy>();
        dummy.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        dummy.material.color = new Color(.6f, .66f, .7f);
        dummy.Part("Torso", PrimitiveType.Capsule, new Vector3(0f, 1.02f, 0f), new Vector3(.48f, .4f, .3f));
        dummy.head = dummy.Part("Head", PrimitiveType.Sphere, new Vector3(0f, 1.65f, 0f), Vector3.one * .34f);
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", new Color(1f, .6f, .15f));
        tint.SetColor("_Color", new Color(1f, .6f, .15f));
        dummy.head.GetComponent<Renderer>().SetPropertyBlock(tint);
        dummy.Part("Left arm", PrimitiveType.Capsule, new Vector3(-.34f, 1.04f, 0f), new Vector3(.15f, .32f, .15f));
        dummy.Part("Right arm", PrimitiveType.Capsule, new Vector3(.34f, 1.04f, 0f), new Vector3(.15f, .32f, .15f));
        dummy.Part("Left leg", PrimitiveType.Capsule, new Vector3(-.13f, .35f, 0f), new Vector3(.19f, .35f, .19f));
        dummy.Part("Right leg", PrimitiveType.Capsule, new Vector3(.13f, .35f, 0f), new Vector3(.19f, .35f, .19f));
        return dummy;
    }

    private Collider Part(string name, PrimitiveType type, Vector3 position, Vector3 scale)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.layer = gameObject.layer;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part.GetComponent<Collider>();
    }

    public override void TakeDamage(Vector3 location, Vector3 direction, float damage,
        Collider bodyPart = null, GameObject origin = null, DamageType damageType = DamageType.Normal)
    {
        if (damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        if (origin != null && TeamCodeUtility.IsSameTeam(this, origin.transform)) return;
        bool headshot = bodyPart == head && damageType == DamageType.Normal;
        Hits++;
        if (headshot) Headshots++;
        RestoreFlash();
        flashingPart = bodyPart != null ? bodyPart.GetComponent<Renderer>() : null;
        TintPart(flashingPart, headshot ? new Color(1f, .25f, .12f) : Color.white);
        flashUntil = Time.time + (headshot ? .13f : .085f);
        // Accept damage for feedback without subtracting finite health or entering any death state.
        dead = false;
        CombatKillFeedback.Confirm(origin, this, damage, 0f, wasHeadshot: headshot);
        if (Time.time >= nextSpray)
        {
            CombatBloodMark.SpawnImpact(location, direction);
            if (damageType == DamageType.Normal) CombatBloodMark.PlayImpactSound(location, origin, headshot);
            nextSpray = Time.time + .065f;
        }
        if (Time.time >= nextMark)
        {
            CombatBloodMark.Spawn(location, direction, false);
            nextMark = Time.time + .8f;
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
