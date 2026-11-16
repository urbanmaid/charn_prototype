using System;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    public enum CombatMetric { Kill, HealthDamage, Healing, Down, Revive, Gadget, DamageDealt }

    // Capture at attack creation; a later control swap must not change the attacker identity.
    public readonly struct CombatSource
    {
        public readonly GameObject Actor;
        public readonly PersonaData Persona;
        public readonly string MissionId;
        public CombatSource(GameObject actor, PersonaData persona, string missionId)
        { Actor = actor; Persona = persona; MissionId = missionId; }
    }

    public readonly struct CombatFact
    {
        public readonly CombatMetric Metric;
        public readonly CombatSource Source;
        public readonly HealthManager Target;
        public readonly double Amount;
        public CombatFact(CombatMetric metric, CombatSource source, HealthManager target, double amount)
        { Metric = metric; Source = source; Target = target; Amount = amount; }
    }

    // Observation only: failures in statistics must never interrupt damage, healing or item use.
    public static class CombatTelemetry
    {
        public const float CommonReviveSeconds = 20f;
        public static event Action<CombatFact> Recorded;
        internal static Func<GameObject, string> MissionForActor;
        private static CombatSource scopedSource;
        private static bool hasScope;
        private static int suppressHealing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { Recorded = null; MissionForActor = null; hasScope = false; suppressHealing = 0; scopedSource = default; }

        public static CombatSource Capture(GameObject actor)
        {
            if (hasScope && scopedSource.Actor == actor) return scopedSource;
            var persona = actor ? actor.GetComponentInParent<PersonaBehavior>() : null;
            var root = persona ? persona.gameObject : actor;
            return new CombatSource(root, persona ? persona.GetPersona() : null, MissionForActor?.Invoke(root));
        }

        public readonly struct SourceScope : IDisposable
        {
            private readonly CombatSource previous;
            private readonly bool wasScoped;
            public SourceScope(CombatSource source)
            { previous = scopedSource; wasScoped = hasScope; scopedSource = source; hasScope = true; }
            public void Dispose() { scopedSource = previous; hasScope = wasScoped; }
        }
        public static SourceScope UseSource(CombatSource source) => new SourceScope(source);

        public readonly struct HealingScope : IDisposable
        {
            public HealingScope(bool suppress) { if (suppress) suppressHealing++; }
            public void Dispose() { suppressHealing--; }
        }
        public static HealingScope SuppressReviveHealing() => new HealingScope(true);

        public static void DealtDamage(HealthManager target, GameObject attacker, float before, float after)
        {
            double delta = Math.Max(0, before) - Math.Max(0, after);
            if (attacker && double.IsFinite(delta) && delta > 0)
                Emit(new CombatFact(CombatMetric.DamageDealt, Capture(attacker), target, delta));
        }

        public static void LostHealth(HealthManager target, float before, float after)
        {
            double delta = Math.Max(0, before) - Math.Max(0, after);
            if (double.IsFinite(delta) && delta > 0) Emit(new CombatFact(CombatMetric.HealthDamage, Capture(target.gameObject), target, delta));
        }
        public static void Healed(HealthManager target, float before, float after)
        {
            double delta = after - before;
            if (suppressHealing == 0 && double.IsFinite(delta) && delta > 0)
                Emit(new CombatFact(CombatMetric.Healing, Capture(target.gameObject), target, delta));
        }
        public static void Downed(HealthManager target) => Emit(new CombatFact(CombatMetric.Down, Capture(target.gameObject), target, 1));
        public static void Revived(HealthManager target) => Emit(new CombatFact(CombatMetric.Revive, Capture(target.gameObject), target, 1));
        public static void Defeated(HealthManager enemy, CombatSource attacker) => Emit(new CombatFact(CombatMetric.Kill, attacker, enemy, 1));
        public static void GadgetUsed(CombatSource actor) => Emit(new CombatFact(CombatMetric.Gadget, actor, null, 1));

        private static void Emit(CombatFact fact)
        {
            var handlers = Recorded;
            if (handlers == null) return;
            foreach (Action<CombatFact> handler in handlers.GetInvocationList())
                try { handler(fact); } catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
