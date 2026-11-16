using System;
using System.Collections.Generic;

namespace Charm.Mechanism.Missions
{
    /// <summary>Stage-scoped facts and party item balances. Only the runtime may mutate it.</summary>
    public sealed class StageActionLedger
    {
        private readonly HashSet<string> defeated = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> interacted = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, MissionPickupRecord> pickups = new Dictionary<string, MissionPickupRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> acquired = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> held = new Dictionary<string, int>(StringComparer.Ordinal);

        public bool WasDefeated(string id) => id != null && defeated.Contains(id);
        public int CollectionSequence(string id) => id != null && pickups.TryGetValue(id, out var p) ? p.sequence : 0;
        public bool WasCollected(string id) => id != null && pickups.ContainsKey(id);
        public bool WasInteracted(string id) => id != null && interacted.Contains(id);
        public int AcquiredQuantity(string itemId) => itemId != null && acquired.TryGetValue(itemId, out int n) ? n : 0;
        public int HeldQuantity(string itemId) => itemId != null && held.TryGetValue(itemId, out int n) ? n : 0;

        internal bool RecordDefeat(string id) => defeated.Add(MissionIds.Require(id, nameof(id)));
        internal bool RecordInteraction(string id) => interacted.Add(MissionIds.Require(id, nameof(id)));

        internal bool RecordPickup(string objectId, string itemId, int quantity)
        {
            MissionIds.Require(objectId, nameof(objectId));
            MissionIds.Require(itemId, nameof(itemId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (pickups.TryGetValue(objectId, out MissionPickupRecord previous))
            {
                if (previous.itemId != itemId || previous.quantity != quantity)
                    throw new ArgumentException("The same placed object cannot represent different pickups.");
                return false;
            }
            int total = checked(AcquiredQuantity(itemId) + quantity);
            int balance = checked(HeldQuantity(itemId) + quantity);
            pickups.Add(objectId, new MissionPickupRecord { objectId = objectId, itemId = itemId, quantity = quantity, sequence = pickups.Count + 1 });
            acquired[itemId] = total;
            held[itemId] = balance;
            return true;
        }

        internal bool TryConsume(string itemId, int quantity)
        {
            MissionIds.Require(itemId, nameof(itemId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            int balance = HeldQuantity(itemId);
            if (balance < quantity) return false;
            held[itemId] = balance - quantity;
            return true;
        }

        internal StageActionSnapshot Capture()
        {
            var data = new StageActionSnapshot();
            data.defeatedEnemyIds.AddRange(defeated);
            data.interactedTargetIds.AddRange(interacted);
            data.defeatedEnemyIds.Sort(StringComparer.Ordinal);
            data.interactedTargetIds.Sort(StringComparer.Ordinal);
            foreach (MissionPickupRecord p in pickups.Values)
                data.pickups.Add(new MissionPickupRecord { objectId = p.objectId, itemId = p.itemId, quantity = p.quantity, sequence = p.sequence });
            data.pickups.Sort((a, b) => StringComparer.Ordinal.Compare(a.objectId, b.objectId));
            foreach (var pair in held) data.balances.Add(new MissionItemBalance { itemId = pair.Key, quantity = pair.Value });
            data.balances.Sort((a, b) => StringComparer.Ordinal.Compare(a.itemId, b.itemId));
            return data;
        }

        internal static StageActionLedger Restore(StageActionSnapshot data)
        {
            if (data == null || data.defeatedEnemyIds == null || data.interactedTargetIds == null || data.pickups == null || data.balances == null)
                throw new ArgumentException("Missing action snapshot fields.");
            var result = new StageActionLedger();
            foreach (string id in data.defeatedEnemyIds)
                if (!result.RecordDefeat(id)) throw new ArgumentException("Duplicate enemy ID.");
            foreach (string id in data.interactedTargetIds)
                if (!result.RecordInteraction(id)) throw new ArgumentException("Duplicate interaction ID.");
            var sequences = new HashSet<int>();
            foreach (MissionPickupRecord p in data.pickups)
            {
                if (p == null || !result.RecordPickup(p.objectId, p.itemId, p.quantity)) throw new ArgumentException("Duplicate/missing pickup.");
                if (p.sequence < 0 || p.sequence > data.pickups.Count || (p.sequence > 0 && !sequences.Add(p.sequence)))
                    throw new ArgumentException("Invalid pickup sequence.");
                result.pickups[p.objectId].sequence = p.sequence;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (MissionItemBalance b in data.balances)
            {
                if (b == null || !seen.Add(MissionIds.Require(b.itemId, "itemId")) || !result.acquired.ContainsKey(b.itemId)
                    || b.quantity < 0 || b.quantity > result.AcquiredQuantity(b.itemId))
                    throw new ArgumentException("Invalid item balance.");
                result.held[b.itemId] = b.quantity;
            }
            if (seen.Count != result.acquired.Count) throw new ArgumentException("Missing item balance.");
            return result;
        }
    }
}
