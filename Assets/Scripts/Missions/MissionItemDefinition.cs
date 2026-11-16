using System;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    [CreateAssetMenu(menuName = "Charm/Missions/Item Definition")]
    public sealed class MissionItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Only new/empty assets receive an ID; rename/reload never changes an existing ID.
            if (string.IsNullOrWhiteSpace(itemId)) itemId = Guid.NewGuid().ToString("N");
        }
#endif
    }
}
