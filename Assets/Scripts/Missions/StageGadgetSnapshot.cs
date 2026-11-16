using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    [Serializable] public sealed class SavedGadget
    {
        public string id, definition, owner;
        public int state, slot, effectOverride; // 0 world, 1 held, 2 consumed
        public Vector3 position;
        public Quaternion rotation;
    }

    /// <summary>Stage-authored gadgets; no Unity object references are written to disk.</summary>
    public sealed class StageGadgetSnapshot
    {
        private sealed class Placement
        {
            public Gadget gadget;
            public string definition;
            public int effectOverride;
        }
        private readonly Dictionary<string, Placement> placed = new Dictionary<string, Placement>();
        private readonly Dictionary<string, GameObject> squad;
        public StageGadgetSnapshot(StageMissionDirector director, Dictionary<string, GameObject> squad)
        {
            this.squad = squad;
            foreach (var gadget in director.GetComponentsInChildren<Gadget>(true))
            {
                var identity = gadget.GetComponent<StageObjectId>();
                if (!identity || string.IsNullOrEmpty(gadget.SaveDefinitionId))
                    throw new InvalidOperationException("Stage gadget needs a stable placement and definition ID.");
                placed.Add(identity.Id, new Placement { gadget = gadget, definition = gadget.SaveDefinitionId, effectOverride = gadget.OverrideValue });
            }
        }
        private static List<GameObject> Inventory(GameObject actor) => actor.GetComponent<PlayerGadgetManager>()?.CaptureGadgetObjects()
            ?? actor.GetComponent<TeamGadgetManager>()?.CaptureGadgetObjects()
            ?? actor.GetComponent<AllyStateController>()?.CaptureGadgetObjects() ?? new List<GameObject>();
        public List<SavedGadget> Capture()
        {
            var result = new List<SavedGadget>();
            var owners = new Dictionary<GameObject, (string owner, int slot)>();
            foreach (var actor in squad)
            {
                var list = Inventory(actor.Value);
                if (list.Count > 4) throw new InvalidOperationException("Gadget inventory exceeds capacity.");
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (!item || !placed.Values.Any(p => p.gadget && p.gadget.gameObject == item) || !owners.TryAdd(item, (actor.Key, i)))
                        throw new InvalidOperationException("Unknown or multiply owned gadget.");
                }
            }
            foreach (var pair in placed)
            {
                var p = pair.Value;
                var value = new SavedGadget { id = pair.Key, definition = p.definition, effectOverride = p.effectOverride, state = 2 };
                if (p.gadget && !p.gadget.IsConsumed)
                {
                    value.position = p.gadget.transform.position; value.rotation = p.gadget.transform.rotation;
                    if (owners.TryGetValue(p.gadget.gameObject, out var owner))
                    { value.state = 1; value.owner = owner.owner; value.slot = owner.slot; }
                    else if (p.gadget.gameObject.activeSelf) value.state = 0;
                    else throw new InvalidOperationException("Inactive gadget has no inventory owner.");
                }
                result.Add(value);
            }
            return result;
        }
        public void Validate(List<SavedGadget> values)
        {
            if (values == null && placed.Count == 0) return; // Existing actors-v2 without gadgets.
            if (values == null || values.Count != placed.Count) throw new InvalidOperationException("Gadget layout changed.");
            var ids = new HashSet<string>();
            foreach (var v in values)
            {
                if (v == null || !ids.Add(v.id) || !placed.TryGetValue(v.id, out var p) || v.definition != p.definition
                    || v.effectOverride != p.effectOverride || v.state < 0 || v.state > 2)
                    throw new InvalidOperationException("Invalid gadget snapshot.");
                if (v.state == 1 && (!squad.ContainsKey(v.owner ?? "") || v.slot < 0 || v.slot >= 4))
                    throw new InvalidOperationException("Invalid gadget owner/slot.");
                if (v.state != 2 && (!float.IsFinite(v.position.x) || !float.IsFinite(v.position.y) || !float.IsFinite(v.position.z)
                    || !float.IsFinite(v.rotation.x) || !float.IsFinite(v.rotation.y) || !float.IsFinite(v.rotation.z) || !float.IsFinite(v.rotation.w)
                    || Quaternion.Dot(v.rotation, v.rotation) < .9f || Quaternion.Dot(v.rotation, v.rotation) > 1.1f))
                    throw new InvalidOperationException("Invalid gadget pose.");
            }
            foreach (var actor in squad.Keys)
            {
                var slots = values.Where(v => v.state == 1 && v.owner == actor).Select(v => v.slot).OrderBy(i => i).ToArray();
                for (int i = 0; i < slots.Length; i++) if (slots[i] != i) throw new InvalidOperationException("Duplicate or missing gadget slot.");
            }
        }
        public void Restore(List<SavedGadget> values)
        {
            Validate(values);
            values ??= new List<SavedGadget>();
            foreach (var v in values)
            {
                var gadget = placed[v.id].gadget;
                gadget.gameObject.SetActive(false);
                if (v.state == 2) { gadget.MarkConsumed(); UnityEngine.Object.Destroy(gadget.gameObject); continue; }
                gadget.transform.SetPositionAndRotation(v.position, v.rotation);
                if (v.state == 1) gadget.PrepareRuntimeData();
                gadget.gameObject.SetActive(v.state == 0);
            }
            foreach (var pair in squad)
            {
                var list = values.Where(v => v.state == 1 && v.owner == pair.Key).OrderBy(v => v.slot)
                    .Select(v => placed[v.id].gadget.gameObject).ToList();
                var player = pair.Value.GetComponent<PlayerGadgetManager>();
                if (player) player.RestoreGadgetObjects(list);
                else if (pair.Value.TryGetComponent<TeamGadgetManager>(out var team)) team.RestoreGadgetObjects(list);
                else pair.Value.GetComponent<AllyStateController>()?.RestoreGadgetObjects(list);
            }
        }
    }
}
