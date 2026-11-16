using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EnemyAI;

namespace Charm.Mechanism.Missions
{
    [Serializable] public sealed class SavedPersonaReference { public string id; public PersonaData asset; }
    [Serializable] public sealed class SavedEffect
    {
        public int kind;
        public string data;
        public float remaining;
    }
    [Serializable] public sealed class SavedActor
    {
        public string id, persona;
        public Vector3 position;
        public Quaternion rotation;
        public bool active, dead;
        public bool combatEnabled;
        public float health, shield, maxShield, downElapsed;
        public AmmoRuntimeStatus ammo;
        public SkillRuntimeStatus skills;
        public List<SavedEffect> effects = new List<SavedEffect>();
    }
    [Serializable] public sealed class SavedEncounter { public string id; public bool entered; }
    [Serializable] public sealed class SavedWorld
    {
        public int version = 2;
        public List<SavedActor> squad = new List<SavedActor>();
        public List<SavedActor> enemies = new List<SavedActor>();
        public List<SavedEncounter> groups = new List<SavedEncounter>();
        public bool hasAirstrikeState, airstrikeTriggered;
        public List<SavedGadget> gadgets;
    }

    /// <summary>Value conversion for a fresh scene. Call after Awake/Start, while gameplay is suspended.</summary>
    public sealed class StageWorldSnapshot
    {
        private readonly Dictionary<string, PersonaData> personas = new Dictionary<string, PersonaData>();
        private readonly Dictionary<PersonaData, string> personaIds = new Dictionary<PersonaData, string>();
        private readonly Dictionary<string, GameObject> squad = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, EnemyHealth> enemies = new Dictionary<string, EnemyHealth>();
        private readonly Dictionary<string,string> enemyGroups = new Dictionary<string,string>();
        private readonly StageMissionDirector director;
        private readonly StageGadgetSnapshot gadgets;
        public IEnumerable<GameObject> SquadActors => squad.Values;
        // Stable format values. Append only; do not use arbitrary type names from a save file.
        private static readonly Type[] effectTypes = { typeof(IEMoveSpeed), typeof(IEMoveJumpHeight), typeof(IEMoveDashCooldown),
            typeof(IEMoveSpeedOnAimPreservation), typeof(IEAttackDamage), typeof(IEAttackCrit), typeof(IEAttackReloadSpeed),
            typeof(IEAttackAimPreservation), typeof(IEAttackMagazine), typeof(IEAttackFireRate), typeof(IEAttackMagazineCapacity),
            typeof(IEDefenseDamageDamp), typeof(IEDefenseDamageDampSkill), typeof(IEDefenseHealAmplification),
            typeof(IEDefenseBlockNegativeEffects), typeof(IESpecialShield), typeof(IEIgnoreDamageDamp) };

        public StageWorldSnapshot(StageMissionDirector director, SavedPersonaReference[] catalog)
        {
            this.director = director;
            foreach (var item in catalog)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || item.asset == null) throw new InvalidOperationException("Invalid persona catalog.");
                personas.Add(item.id, item.asset); personaIds.Add(item.asset, item.id);
            }
            var player = PlayerControlResolver.Resolve(null);
            if (player == null) throw new InvalidOperationException("Controlled player missing.");
            squad.Add("player", player.gameObject);
            foreach (var ally in UnityEngine.Object.FindObjectsByType<AllyStateController>(FindObjectsInactive.Include))
                if (ally.gameObject.scene == director.gameObject.scene && ally.gameObject != player.gameObject)
                    squad.Add("ally/" + PersonaId(ally.gameObject), ally.gameObject);
            gadgets = new StageGadgetSnapshot(director, squad);
            foreach (var enemy in director.GetComponentsInChildren<EnemyHealth>(true))
            {
                var id = enemy.GetComponent<StageObjectId>();
                if (id == null || string.IsNullOrWhiteSpace(id.Id)) throw new InvalidOperationException("Enemy without stable ID.");
                enemies.Add(id.Id, enemy);
                enemyGroups.Add(id.Id, director.EncounterId(enemy.transform));
            }
        }

        // Use the same stable squad slots as resume; disabled/downed controllers still belong to it.
        public bool IsEntireSquadDown()
        {
            if (squad.Count == 0) return false;
            foreach (var actor in squad.Values)
            {
                if (!actor) return false; // Missing actors are not evidence of a downed squad.
                var health = actor.GetComponent<HealthManager>();
                if (health == null || !health.dead) return false;
            }
            return true;
        }

        private string PersonaId(GameObject actor)
        {
            var persona = actor.GetComponent<PersonaBehavior>()?.GetPersona();
            if (persona == null || !personaIds.TryGetValue(persona, out var id)) throw new InvalidOperationException("Unregistered persona.");
            return id;
        }

        public List<WorldObjectSnapshot> Capture(Transform resumePoint)
        {
            var world = new SavedWorld { groups = director.CaptureEncounters() };
            var airstrike=UnityEngine.Object.FindAnyObjectByType<VandalismBridgeAirstrike>();
            if(airstrike && airstrike.gameObject.scene==director.gameObject.scene)
            {world.hasAirstrikeState=true;world.airstrikeTriggered=airstrike.WasTriggered;}
            foreach (var pair in squad)
            {
                GameObject actor = pair.Value;
                if (!actor) throw new InvalidOperationException("Squad member disappeared.");
                var player = actor.GetComponent<PlayerHealth>(); var ally = actor.GetComponent<AllyHealthManager>();
                var health = actor.GetComponent<HealthManager>();
                if(pair.Key=="player" && health.dead)throw new InvalidOperationException("Cannot create a checkpoint while the controlled persona is down.");
                var value = Pose(pair.Key, actor); value.persona = PersonaId(actor); value.dead = health.dead;
                value.health = player ? player.health : ally.health;
                value.shield = player ? player.shieldHealth : ally.shieldHealth;
                value.maxShield = player ? player.TotalShieldHealth : ally.TotalShieldHealth;
                value.downElapsed = health.dead ? Mathf.Max(0, Time.time - (ReviveWindow.CaptureDeathTime(health) ?? Time.time)) : 0;
                if (pair.Key == "player" && resumePoint != null)
                { value.position = resumePoint.position; value.rotation = resumePoint.rotation; }
                var shoot = actor.GetComponent<ShootBehaviour>(); var ai = actor.GetComponent<AllyStateController>();
                if (ai != null && ai.CombatController != null)
                {
                    var coordinator=director.GetComponent<StageResumeCoordinator>();
                    value.combatEnabled=coordinator != null ? coordinator.CaptureCombatEnabled(ai.CombatController) : ai.CombatController.enabled;
                }
                value.ammo = shoot ? shoot.CaptureAmmoStatus() : ai.CaptureAmmoStatus();
                var skills = actor.GetComponent<SkillBehavior>(); value.skills = skills ? skills.CaptureRuntimeStatus() : ai.CaptureSkillStatus();
                value.effects = CaptureEffects(actor); world.squad.Add(value);
            }
            foreach (var pair in enemies)
            {
                if(!world.groups.Find(g=>g.id==enemyGroups[pair.Key]).entered)continue;
                bool dead = director.Controller.Runtime.Actions.WasDefeated(pair.Key);
                if (!pair.Value && !dead) throw new InvalidOperationException("Living enemy disappeared.");
                var value = pair.Value ? Pose(pair.Key, pair.Value.gameObject) : new SavedActor {id=pair.Key};
                value.dead = dead;
                if (!dead) {value.health=pair.Value.health;value.effects=CaptureEffects(pair.Value.gameObject);}
                world.enemies.Add(value);
            }
            world.gadgets = gadgets.Capture();
            return new List<WorldObjectSnapshot> {new WorldObjectSnapshot {objectId="stage-world",adapterId="actors-v2",version=2,payload=JsonUtility.ToJson(world)}};
        }

        public SavedWorld Validate(List<WorldObjectSnapshot> entries)
        {
            if (entries == null || entries.Count != 1 || entries[0] == null || entries[0].objectId != "stage-world"
                || entries[0].adapterId != "actors-v2" || entries[0].version != 2) throw new InvalidOperationException("Unsupported world snapshot.");
            var world = JsonUtility.FromJson<SavedWorld>(entries[0].payload);
            if (world == null || world.version != 2 || world.squad == null || world.enemies == null
                || world.squad.Count != squad.Count
                || world.groups == null || world.groups.Count != director.CaptureEncounters().Count)
                throw new InvalidOperationException("World layout changed.");
            var groupIds = new HashSet<string>();
            var authored = director.CaptureEncounters();
            foreach(var group in world.groups)
                if(group==null || !groupIds.Add(group.id) || !authored.Exists(g=>g.id==group.id))
                    throw new InvalidOperationException("Encounter layout changed.");
            int expected=0;
            foreach(var pair in enemyGroups)if(world.groups.Find(g=>g.id==pair.Value).entered)expected++;
            if(world.enemies.Count!=expected)throw new InvalidOperationException("Encounter enemy count changed.");
            var ids = new HashSet<string>(); var usedPersonas = new HashSet<string>();
            foreach (var value in world.squad)
            {
                if (value == null || !squad.ContainsKey(value.id) || !ids.Add(value.id) || !personas.ContainsKey(value.persona)
                    || !usedPersonas.Add(value.persona)) throw new InvalidOperationException("Squad identity mismatch.");
                ValidateActor(value); DecodeEffects(value.effects);
                if (value.id == "player" && (value.dead || !value.active || value.health <= 0))
                    throw new InvalidOperationException("Controlled persona cannot resume inactive or downed.");
                // -1 reserve is the existing unlimited-ammunition sentinel (including companions).
                if (value.ammo.currentMagazine < 0 || value.ammo.reserveBullets < -1
                    || (value.ammo.hasWeapon && value.ammo.activeWeapon < 0))
                    throw new InvalidOperationException("Invalid saved ammunition.");
                foreach (float remaining in new[] { value.skills.skill1CooldownRemaining,
                    value.skills.skill2CooldownRemaining, value.skills.gameChangerCooldownRemaining })
                    if (!float.IsFinite(remaining) || remaining < 0)
                        throw new InvalidOperationException("Invalid saved skill cooldown.");
                if (value.skills.gameChangerPoint < 0)
                    throw new InvalidOperationException("Invalid saved skill resource.");
            }
            ids.Clear();
            foreach (var value in world.enemies)
            {
                if (value == null || !enemies.ContainsKey(value.id) || !ids.Add(value.id)) throw new InvalidOperationException("Enemy identity mismatch.");
                if(!world.groups.Find(g=>g.id==enemyGroups[value.id]).entered)throw new InvalidOperationException("Snapshot enemy belongs to an unentered encounter.");
                if (!value.dead) {ValidateActor(value);DecodeEffects(value.effects);}
            }
            gadgets.Validate(world.gadgets);
            return world;
        }

        public void PrepareRestore(SavedWorld world)
        {
            // Dead actors must never awaken when their parent is activated.
            foreach(var value in world.enemies)
                if(value.dead || !value.active)enemies[value.id].gameObject.SetActive(false);
            director.RestoreEncounters(world.groups);
        }

        public void Restore(SavedWorld world)
        {
            // The caller validates the entire world and mission data before any mutation.
            var manager = AllySquadManager.Instance;
            foreach (var value in world.squad)
            {
                var actor = squad[value.id];
                var persona = personas[value.persona];
                if (actor.GetComponent<PersonaBehavior>().GetPersona() == persona) continue;
                if (manager == null || manager.gameObject.scene != director.gameObject.scene)
                    throw new InvalidOperationException("Squad manager missing for checkpoint persona restoration.");
                manager.RestoreCheckpointPersona(actor, persona);
            }
            foreach (var value in world.squad)
            {
                var actor = squad[value.id];
                actor.GetComponent<IngameEffectBehavior>()?.RestoreRuntimeStatus(DecodeEffects(value.effects));
                var player = actor.GetComponent<PlayerHealth>(); var ally = actor.GetComponent<AllyHealthManager>();
                if (player) player.RestoreRuntimeHealth(value.health,value.shield,value.maxShield);
                else ally.RestoreRuntimeHealth(value.health,value.shield,value.maxShield);
                var shoot=actor.GetComponent<ShootBehaviour>();var ai=actor.GetComponent<AllyStateController>();
                if(shoot)shoot.RestoreAmmoStatus(value.ammo);else ai.RestoreAmmoStatus(value.ammo);
                var skills=actor.GetComponent<SkillBehavior>();if(skills)skills.RestoreRuntimeStatus(value.skills);else ai.RestoreSkillStatus(value.skills);
                SetPose(actor,value);actor.SetActive(value.active);
                if(ally && value.dead)ally.Kill();
                if (ai != null && ai.CombatController != null)
                {
                    var coordinator=director.GetComponent<StageResumeCoordinator>();
                    if(coordinator != null)coordinator.RestoreSquadCombatEnabled(ai.CombatController,
                        value.combatEnabled);
                    else ai.CombatController.enabled=!value.dead && value.combatEnabled;
                }
                ReviveWindow.RestoreDeathTime(actor.GetComponent<HealthManager>(), value.dead ? Time.time-value.downElapsed : (float?)null);
            }
            foreach (var value in world.enemies)
            {
                var health=enemies[value.id];
                if(value.dead){health.health=0;health.dead=true;health.gameObject.SetActive(false);continue;}
                health.gameObject.GetComponent<IngameEffectBehavior>()?.RestoreRuntimeStatus(DecodeEffects(value.effects));
                health.health=value.health;health.dead=false;SetPose(health.gameObject,value);health.gameObject.SetActive(value.active);
            }
            gadgets.Restore(world.gadgets);
            director.RestoreEncounters(world.groups);
            var airstrike=UnityEngine.Object.FindAnyObjectByType<VandalismBridgeAirstrike>();
            if(airstrike && airstrike.gameObject.scene==director.gameObject.scene)
            {
                airstrike.RestoreCheckpointState(world.airstrikeTriggered);
            }
            Physics.SyncTransforms();
        }

        private static SavedActor Pose(string id,GameObject actor)=>new SavedActor{id=id,position=actor.transform.position,rotation=actor.transform.rotation,active=actor.activeSelf};
        private static void SetPose(GameObject actor,SavedActor value)
        {
            var agent=actor.GetComponent<NavMeshAgent>();
            if(agent && agent.enabled && agent.isOnNavMesh)agent.Warp(value.position);
            actor.transform.SetPositionAndRotation(value.position,value.rotation);
            var body=actor.GetComponent<Rigidbody>();if(body){body.position=value.position;body.rotation=value.rotation;if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}}
        }
        private static void ValidateActor(SavedActor value)
        {
            foreach(float number in new[]{value.position.x,value.position.y,value.position.z,value.rotation.x,value.rotation.y,value.rotation.z,value.rotation.w,value.health,value.shield,value.maxShield,value.downElapsed})
                if(float.IsNaN(number)||float.IsInfinity(number))throw new InvalidOperationException("Invalid actor numeric state.");
            if(value.health<0||value.shield<0||value.maxShield<value.shield||value.downElapsed<0)throw new InvalidOperationException("Invalid actor health.");
            float rotationLength=value.rotation.x*value.rotation.x+value.rotation.y*value.rotation.y
                +value.rotation.z*value.rotation.z+value.rotation.w*value.rotation.w;
            if (Mathf.Abs(rotationLength-1f) > .01f)
                throw new InvalidOperationException("Invalid actor rotation.");
        }
        private static List<SavedEffect> CaptureEffects(GameObject actor)
        {
            var result=new List<SavedEffect>();var effects=actor.GetComponent<IngameEffectBehavior>()?.CaptureRuntimeStatus();
            if(effects!=null)foreach(var state in effects){int kind=Array.IndexOf(effectTypes,state.effect.GetType());if(kind<0)throw new InvalidOperationException("Unsupported effect type.");result.Add(new SavedEffect{kind=kind,data=JsonUtility.ToJson(state.effect),remaining=state.remainingDuration});}return result;
        }
        private static List<IngameEffectRuntimeState> DecodeEffects(List<SavedEffect> effects)
        {
            var result=new List<IngameEffectRuntimeState>();if(effects==null)return result;
            foreach(var entry in effects){if(entry==null||entry.kind<0||entry.kind>=effectTypes.Length||float.IsNaN(entry.remaining)||float.IsInfinity(entry.remaining)||entry.remaining<0)throw new InvalidOperationException("Invalid saved effect.");
                var effect=(IngameEffectData)JsonUtility.FromJson(entry.data,effectTypes[entry.kind]);if(effect==null)throw new InvalidOperationException("Missing saved effect.");effect.isPropagated=true;result.Add(new IngameEffectRuntimeState(effect,entry.remaining));}return result;
        }
    }
}
