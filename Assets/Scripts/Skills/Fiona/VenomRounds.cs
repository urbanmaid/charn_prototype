using UnityEngine;

/// <summary>
/// Fiona "Lizard's First Bite": for one magazine, every hit poisons and slows an enemy.
/// Ends when the magazine is emptied or the weapon is reloaded. While loaded the gun drips venom and shows a
/// shimmering scale pattern; shots fly as venom needles. Added at runtime to the player.
/// </summary>
public class VenomRounds : MonoBehaviour
{
    public bool IsActive { get; private set; }

    private float slowPercent, slowDuration, poisonDamagePerSecond;
    private ShootBehaviour shoot;
    private SkillVfxRunner weaponAura;
    private Transform auraMuzzle;
    private GameObject scaledWeapon;
    private LizardBite lizard;
    private static readonly Color ScaleGlow = new Color(0.4f, 0.62f, 0.16f, 1f);

    public void Activate(float slowPercent, float slowDuration, float poisonDamagePerSecond = 8f)
    {
        if (IsActive) Deactivate();

        shoot = GetComponent<ShootBehaviour>();
        if (shoot == null) return;

        this.slowPercent = slowPercent;
        this.slowDuration = slowDuration;
        this.poisonDamagePerSecond = poisonDamagePerSecond;
        shoot.OnShotHit += HandleShotHit;
        shoot.OnShotFired += HandleShotFired;
        shoot.OnReloadStarted += Deactivate;
        shoot.muteShotSound = true;
        IsActive = true;
        RefreshWeaponAura();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;

        if (shoot != null)
        {
            shoot.OnShotHit -= HandleShotHit;
            shoot.OnShotFired -= HandleShotFired;
            shoot.OnReloadStarted -= Deactivate;
            shoot.muteShotSound = false;
        }
        if (weaponAura != null) weaponAura.StopSoon(0.8f);
        weaponAura = null;
        auraMuzzle = null;
        SkillOverlay.Remove(scaledWeapon, "venom_scales");
        scaledWeapon = null;
        ReleaseLizard();
    }

    private void Update()
    {
        // Weapon changed: move drip effect to the new gun
        if (IsActive && shoot != null && shoot.GunMuzzle != auraMuzzle)
            RefreshWeaponAura();
    }

    /// <summary> The activation lizard stays coiled on the gun, biting, until the venom rounds end. </summary>
    public void HoldLizard(LizardBite bite)
    {
        ReleaseLizard();
        lizard = bite;
        if (!IsActive) ReleaseLizard();
    }

    private void ReleaseLizard()
    {
        if (lizard != null) lizard.Release();
        lizard = null;
    }

    private void RefreshWeaponAura()
    {
        // Weapon swapped: the lizard was wrapped around the old gun
        if (auraMuzzle != null) ReleaseLizard();
        if (weaponAura != null) weaponAura.StopSoon(0.3f);
        auraMuzzle = shoot.GunMuzzle;
        weaponAura = auraMuzzle != null ? FionaVfx.SpawnVenomWeaponAura(auraMuzzle) : null;

        // Scale pattern shimmering over the gun
        SkillOverlay.Remove(scaledWeapon, "venom_scales");
        InteractiveWeapon weapon = auraMuzzle != null ? auraMuzzle.GetComponentInParent<InteractiveWeapon>() : null;
        scaledWeapon = weapon != null ? weapon.gameObject : null;
        SkillOverlay.Apply(scaledWeapon, "venom_scales", ScaleGlow, -1f, 0.6f, 3f, 0.9f, 45f);
    }

    private void HandleShotHit(HealthManager target, RaycastHit hit)
    {
        if (target == null || target.dead || target is DestructibleCover || AllyTargeting.IsAlly(target)) return;

        FionaVenomPoison poison = target.GetComponent<FionaVenomPoison>();
        if (poison == null) poison = target.gameObject.AddComponent<FionaVenomPoison>();
        poison.Apply(poisonDamagePerSecond, slowDuration, shoot != null ? shoot.gameObject : gameObject);

        IngameEffectBehavior iFx = target.GetComponent<IngameEffectBehavior>();
        if (iFx == null)
        {
            iFx = target.gameObject.AddComponent<IngameEffectBehavior>();
            iFx.RefreshComponentReferences();
        }

        // Same name refreshes duration instead of stacking
        IEMoveSpeed slow = new();
        slow.Initialize("Venom Slow", EffectDetailType.Move, -slowPercent, slowDuration);
        iFx.AddIngameEffect(slow);

        FionaVfx.SpawnVenomBite(hit.point, hit.normal);
        SkillSfx.Play("venom_hit", hit.point, 0.6f, 1f, 0.08f);
        FionaVfx.SpawnOrExtendVenomDebuff(target.transform, slowDuration);
    }

    private void HandleShotFired(Vector3 from, Vector3 to)
    {
        FionaVfx.SpawnVenomTracer(from, to);
        SkillSfx.Play("venom_shot", from, 1f, 1f, 0.06f);     // Replaces the gunshot while venom rounds are loaded

        // That was the last venom round
        if (shoot.GetCurrentMagazine() <= 0)
            Deactivate();
    }

    private void OnDisable()
    {
        Deactivate();
    }
}
