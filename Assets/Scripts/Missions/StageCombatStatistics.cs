using System;
using System.Collections.Generic;
using UnityEngine;
using EnemyAI;

namespace Charm.Mechanism.Missions
{
    // Stage-scoped event consumer. Returned DTOs are copies; capture/retry cannot mutate the live ledger.
    public sealed class StageCombatStatistics : IDisposable
    {
        private readonly StageMissionDirector director;
        private readonly StageResumeCoordinator coordinator;
        private readonly Dictionary<PersonaData, string> ids = new Dictionary<PersonaData, string>();
        private readonly HashSet<GameObject> actors = new HashSet<GameObject>();
        private readonly List<HealthManager> health = new List<HealthManager>();
        private readonly HashSet<HealthManager> enemies = new HashSet<HealthManager>();
        private readonly HashSet<HealthManager> kills = new HashSet<HealthManager>();
        private readonly Dictionary<string, PersonaCombatTotals> valid = new Dictionary<string, PersonaCombatTotals>();

        private readonly Dictionary<string, Dictionary<string, PersonaCombatTotals>> missions = new Dictionary<string, Dictionary<string, PersonaCombatTotals>>();
        private readonly Dictionary<string, CombatDownSnapshot> down = new Dictionary<string, CombatDownSnapshot>();

        private readonly Dictionary<string, float> downStarted = new Dictionary<string, float>();
        private bool listening;

        public StageCombatStatistics(StageMissionDirector director, StageResumeCoordinator coordinator,
            IEnumerable<GameObject> squad, SavedPersonaReference[] catalog, StageSaveData saved)
        {
            this.director = director; this.coordinator = coordinator;
            foreach (var persona in catalog) ids.Add(persona.asset, persona.id);
            foreach (var actor in squad) { actors.Add(actor); health.Add(actor.GetComponent<HealthManager>()); }
            foreach (var enemy in director.GetComponentsInChildren<EnemyHealth>(true)) enemies.Add(enemy);


            var cp = saved.latestCheckpoint;
            if (cp == null) return;
            Load(cp.validCombat, valid);
            foreach (var mission in cp.missionCombat ?? new List<MissionCombatTotals>())
            {
                if (mission == null || !KnownMission(mission.missionId) || missions.ContainsKey(mission.missionId))
                    throw new ArgumentException("Invalid combat mission identity.");
                var values = new Dictionary<string, PersonaCombatTotals>(); Load(mission.personas, values);
                missions.Add(mission.missionId, values);
            }
            foreach (var state in cp.downStates ?? new List<CombatDownSnapshot>())
            {
                if (state == null || string.IsNullOrEmpty(state.personaId) || string.IsNullOrEmpty(state.episodeId) || down.ContainsKey(state.personaId))
                    throw new ArgumentException("Invalid downed combat state.");
                down.Add(state.personaId, Copy(state));
            }
        }

        private bool Accepting => coordinator.IsReady && !coordinator.IsSuspended && coordinator.State == StageResumeCoordinator.RecoveryState.Playing
            && director.Controller.Runtime != null && !director.Controller.Runtime.IsFailed;
        private string CurrentMission => director.Controller.Runtime.Definitions[Math.Min(director.Controller.Runtime.CurrentMissionIndex,
            director.Controller.Runtime.Definitions.Count - 1)].Id;
        private bool KnownMission(string id)
        { foreach (var mission in director.Controller.Runtime.Definitions) if (mission.Id == id) return true; return false; }
        private string MissionForActor(GameObject actor) => actor && actors.Contains(actor) ? CurrentMission : null;
        private string Persona(HealthManager actor)
        {
            var data = actor ? actor.GetComponent<PersonaBehavior>()?.GetPersona() : null;
            return data && ids.TryGetValue(data, out string id) ? id : null;
        }

        public void StartObserving(string runId)
        {
            // Restore supplies health/death timestamps before observing. It does not replay down/revive facts.
            foreach (var actor in health)
            {
                string id = Persona(actor);
                if (actor && actor.dead && id != null)
                {
                    if (!down.ContainsKey(id)) down.Add(id, new CombatDownSnapshot { personaId = id, episodeId = runId + "/legacy/" + id });
                    downStarted[id] = ReviveWindow.CaptureDeathTime(actor) ?? Time.time;
                }
            }
            CombatTelemetry.Recorded += Record;
            CombatTelemetry.MissionForActor += MissionForActor;
            listening = true;
        }

        private void Record(CombatFact fact)
        {
            if (!Accepting || !fact.Source.Actor || !actors.Contains(fact.Source.Actor)
                || !fact.Source.Persona || !ids.TryGetValue(fact.Source.Persona, out string id)) return;
            if (fact.Metric == CombatMetric.DamageDealt && !enemies.Contains(fact.Target)) return;
            if (fact.Metric == CombatMetric.Kill && (!enemies.Contains(fact.Target) || !kills.Add(fact.Target))) return;
            if (fact.Metric == CombatMetric.Down)
            {
                if (down.ContainsKey(id)) return;
                down.Add(id, new CombatDownSnapshot { personaId = id, episodeId = Guid.NewGuid().ToString("N") });
                downStarted[id] = ReviveWindow.CaptureDeathTime(fact.Target) ?? Time.time;
            }
            if (fact.Metric == CombatMetric.Revive)
            {
                if (!down.TryGetValue(id, out var state)) return;
                if (downStarted.TryGetValue(id, out float start) && Time.time - start >= CombatTelemetry.CommonReviveSeconds)
                    CountDeath(id, state);
                down.Remove(id); downStarted.Remove(id);
            }
            string mission = fact.Metric == CombatMetric.Gadget && KnownMission(fact.Source.MissionId) ? fact.Source.MissionId : CurrentMission;
            Add(Get(valid, id), fact.Metric, fact.Amount);

            if (!missions.TryGetValue(mission, out var values)) missions.Add(mission, values = new Dictionary<string, PersonaCombatTotals>());
            Add(Get(values, id), fact.Metric, fact.Amount);
        }

        public void PollDeaths()
        {
            if (!Accepting) return;
            foreach (var actor in health)
            {
                string id = Persona(actor);
                if (!actor || !actor.dead || id == null || !down.TryGetValue(id, out var state) || state.deathCounted) continue;
                if (ReviveWindow.TimeLeft(actor, CombatTelemetry.CommonReviveSeconds) > 0f) continue;
                CountDeath(id, state);
            }
        }
        private void CountDeath(string id, CombatDownSnapshot state)
        {
            if (state.deathCounted) return;
            state.deathCounted = true;
            Get(valid, id).deaths++;
            if (!missions.TryGetValue(CurrentMission, out var values)) missions.Add(CurrentMission, values = new Dictionary<string, PersonaCombatTotals>());
            Get(values, id).deaths++;
            // The checkpoint owns this flag, so replay follows the restored valid timeline.

        }

        public List<PersonaCombatTotals> CaptureValid() => Values(valid);

        public List<PersonaCombatTotals> CaptureMission(int index)
            => missions.TryGetValue(director.Controller.Runtime.Definitions[index].Id, out var values) ? Values(values) : new List<PersonaCombatTotals>();
        public void WriteCheckpoint(StageCheckpointSnapshot snapshot)
        {
            snapshot.validCombat = CaptureValid();
            snapshot.missionCombat = new List<MissionCombatTotals>();
            foreach (var mission in director.Controller.Runtime.Definitions)
                if (missions.TryGetValue(mission.Id, out var values)) snapshot.missionCombat.Add(new MissionCombatTotals { missionId = mission.Id, personas = Values(values) });
            snapshot.downStates = new List<CombatDownSnapshot>();
            foreach (var state in down.Values) snapshot.downStates.Add(Copy(state));
        }
        private static PersonaCombatTotals Get(Dictionary<string, PersonaCombatTotals> values, string id)
        {
            if (!values.TryGetValue(id, out var result)) values.Add(id, result = new PersonaCombatTotals { personaId = id });
            return result;
        }
        private static void Add(PersonaCombatTotals value, CombatMetric metric, double amount)
        {
            switch (metric)
            {
                case CombatMetric.Kill: value.kills++; break;
                case CombatMetric.Down: value.downs++; break;
                case CombatMetric.Revive: value.revives++; break;
                case CombatMetric.Gadget: value.gadgetUses++; break;
                case CombatMetric.DamageDealt: value.damageDealt += amount; break;
                case CombatMetric.HealthDamage: value.healthDamage += amount; break;
                case CombatMetric.Healing: value.healingReceived += amount; break;
            }
        }
        private static void Load(List<PersonaCombatTotals> source, Dictionary<string, PersonaCombatTotals> target)
        {
            foreach (var value in source ?? new List<PersonaCombatTotals>())
            {
                if (value == null || string.IsNullOrEmpty(value.personaId) || value.kills < 0 || value.downs < 0 || value.deaths < 0
                    || !double.IsFinite(value.damageDealt) || value.damageDealt < 0
                    || value.revives < 0 || value.gadgetUses < 0 || !double.IsFinite(value.healthDamage) || value.healthDamage < 0
                    || !double.IsFinite(value.healingReceived) || value.healingReceived < 0 || target.ContainsKey(value.personaId))
                    throw new ArgumentException("Invalid persona combat totals.");
                target.Add(value.personaId, Copy(value));
            }
        }
        private static List<PersonaCombatTotals> Values(Dictionary<string, PersonaCombatTotals> source)
        {
            var result = new List<PersonaCombatTotals>();
            foreach (var value in source.Values) result.Add(Copy(value));
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.personaId, b.personaId));
            return result;
        }
        private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        public void Dispose()
        {
            if (!listening) return;
            CombatTelemetry.Recorded -= Record;
            CombatTelemetry.MissionForActor -= MissionForActor;
            listening = false;
        }
    }
}
