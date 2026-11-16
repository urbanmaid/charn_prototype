using System.Collections.Generic;
using UnityEngine;

public static class CombatSkillAccent
{
    private static readonly Queue<GameObject> active = new Queue<GameObject>();

    // Called only after cooldown, resource and target validation. Passive/AI casts never shake the player's view.
    public static void Cast(SkillContext context, SkillLogic logic, SkillSlot slot)
    {
        if (!context.caster || !context.caster.GetComponent<PlayerHealth>()) return;
        // These effects own their impact timing: forge cracks and the flag's actual landing.
        if (logic is AmeliaSkillInfiniteMagazine || logic is AmeliaSkillExtendMagazine ||
            logic is AmeliaSkillSupplyOverdrive || logic is AmeliaSkillPowerBuff) return;
        Camera view = Camera.main;
        var orbit = view ? view.GetComponent<ThirdPersonOrbitCam>() : null;
        if (!orbit || !orbit.player ||
            orbit.player.GetComponentInParent<PlayerHealth>() != context.caster.GetComponent<PlayerHealth>()) return;

        Color color = new Color(.35f, .75f, 1f);
        float strength = .35f, duration = .35f, frequency = 30f, fov = .25f;
        if (logic is LuciaSkillHealArea) {
            color = new Color(.25f, 1f, .55f); strength = .18f; duration = .65f; frequency = 12f;
        } else if (logic is LuciaSkillNanoBoost) {
            color = new Color(.2f, .8f, 1f); strength = .55f; duration = .4f; frequency = 45f;
        } else if (logic is LuciaSkillReviveOneAlly || logic is LuciaSkillReviveMultiAllies) {
            color = new Color(1f, .8f, .3f); strength = .45f; duration = .8f; frequency = 16f;
        } else if (logic is FionaSkillLizardBite) {
            color = new Color(.5f, 1f, .15f); strength = .32f; duration = .5f; frequency = 24f;
        } else if (logic is FionaSkillCrowWingbeat) {
            color = new Color(.65f, .4f, 1f); strength = .55f; duration = .55f; frequency = 20f;
        } else if (logic is FionaSkillHawkAmbush) {
            color = new Color(1f, .5f, .12f); strength = .85f; duration = .6f; frequency = 42f;
        } else if (logic is AmeliaSkillClusterGrenade || logic is ProfSkillGrenade) {
            color = new Color(1f, .5f, .18f); strength = .22f; duration = .22f; frequency = 28f;
        } else if (logic is AmeliaSkillExtendMagazine || logic is AmeliaSkillSupplyOverdrive || logic is AmeliaSkillPowerBuff) {
            color = new Color(1f, .68f, .2f); strength = .5f; duration = .32f; frequency = 48f;
        } else if (logic is AmeliaSkillInfiniteMagazine) {
            color = new Color(1f, .78f, .25f); strength = .85f; duration = .65f; frequency = 36f;
        } else if (logic is ProfSkillShield) {
            color = new Color(.25f, .65f, 1f); strength = .45f; duration = .45f; frequency = 38f;
        } else if (logic is ProfSkillVisor) {
            color = new Color(1f, .3f, .2f); strength = .4f; duration = .5f; frequency = 32f;
        }
        bool ultimate = slot == SkillSlot.GameChanger;
        if (ultimate) { fov = .9f; strength = Mathf.Max(strength, .65f); }
        CombatCameraFeedback.Get(orbit).SkillPulse(strength, duration, frequency, fov);
        SkillScreenFx.SkillEdges(color, Mathf.Max(duration, .45f), ultimate ? .32f : .22f, frequency);
        Burst(context.caster.transform.position + Vector3.up, color, ultimate ? 2.4f : 1.25f);
    }

    // Short punctuation around the contact point; existing skill effects retain their identity.
    public static void Burst(Vector3 position, Color color, float size = 0.8f)
    {
        while (active.Count > 0 && !active.Peek()) active.Dequeue();
        if (active.Count >= 24) return;
        GameObject root = SkillVfxFactory.CreateRoot("VFX_CombatAccent", null, position);
        active.Enqueue(root);
        var runner = root.AddComponent<SkillVfxRunner>();
        runner.lifetime = 0.4f;
        runner.stopEmittingAt = 0.06f;
        var ring = SkillVfxFactory.CreateParticles(root.transform, "ContactRing", Vector3.zero, SkillVfxFactory.Ring);
        var main = ring.main;
        main.duration = 0.05f;
        main.startLifetime = 0.24f;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 1;
        var growth = ring.sizeOverLifetime;
        growth.enabled = true;
        growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
        SkillVfxFactory.SetBurst(ring, 1);
        SkillVfxFactory.SetFadeOut(ring);
        runner.Play();
    }
}
