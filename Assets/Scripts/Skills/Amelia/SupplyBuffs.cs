using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Magazine-bound buffs given by Amelia (supply magazine, reinforced rounds): they last until this character's
/// magazine runs dry or a reload starts, then end together. A supply refill doesn't end them, so buffs given
/// by different skills are kept (ex: reinforced rounds, then a supply magazine: still reinforced).
/// Works for the player (ShootBehaviour) and AI (StateController). Added at runtime to the character.
/// </summary>
public class SupplyBuffs : MonoBehaviour
{
    private class Buff
    {
        public IngameEffectData effect;
        public Action onEnd;
    }

    private IngameEffectBehavior effects;
    private ShootBehaviour shoot;
    private EnemyAI.StateController ai;
    private readonly Dictionary<string, Buff> buffs = new();

    public static SupplyBuffs On(GameObject character)
    {
        if (character == null) return null;
        if (!character.TryGetComponent(out SupplyBuffs supply))
        {
            supply = character.AddComponent<SupplyBuffs>();
            supply.Init();
        }
        return supply;
    }

    private void Init()
    {
        effects = GetComponent<IngameEffectBehavior>();
        if (effects == null)
        {
            effects = gameObject.AddComponent<IngameEffectBehavior>();
            effects.RefreshComponentReferences();
        }
        shoot = GetComponent<ShootBehaviour>();
        if (shoot != null)
        {
            shoot.OnShotFired += OnShotFired;
            shoot.OnReloadStarted += EndAll;
        }
        else
        {
            ai = GetComponent<EnemyAI.StateController>();
            if (ai == null)
            {
                AllyStateController ally = GetComponent<AllyStateController>();
                ai = ally != null ? ally.CombatController : null;
            }
        }
    }

    public bool Has(string key) => buffs.ContainsKey(key);

    /// <summary>
    /// Give (or renew) a buff. onEnd runs once when it ends; on renew the first onEnd is kept.
    /// Returns true if the buff is new (start its visuals then).
    /// </summary>
    public bool Give(string key, IngameEffectData effect, Action onEnd = null)
    {
        bool isNew = true;
        if (buffs.TryGetValue(key, out Buff old))
        {
            effects.RemoveIngameEffect(old.effect);
            onEnd = old.onEnd;
            isNew = false;
        }
        effects.AddIngameEffect(effect);
        buffs[key] = new Buff { effect = effect, onEnd = onEnd };
        return isNew;
    }

    /// <summary> A full magazine at once (no reload time). </summary>
    public void Refill()
    {
        if (shoot != null) shoot.RefillMagazine();
        else if (ai != null) ai.RefillMagazine();
    }

    private void OnShotFired(Vector3 from, Vector3 to)
    {
        if (buffs.Count > 0 && shoot.GetCurrentMagazine() <= 0) EndAll();
    }

    private void Update()
    {
        // AI: magazine dry or reloading
        if (ai != null && buffs.Count > 0 && (ai.bullets <= 0 || ai.reloading)) EndAll();
    }

    private void EndAll()
    {
        if (buffs.Count == 0) return;
        List<Buff> ended = new(buffs.Values);
        buffs.Clear();
        foreach (Buff buff in ended)
        {
            if (effects != null) effects.RemoveIngameEffect(buff.effect);
            buff.onEnd?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (shoot != null)
        {
            shoot.OnShotFired -= OnShotFired;
            shoot.OnReloadStarted -= EndAll;
        }
    }
}
