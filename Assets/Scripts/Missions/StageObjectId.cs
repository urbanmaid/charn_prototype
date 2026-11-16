using System;
using UnityEngine;

namespace Charm.Mechanism.Missions
{
    /// <summary>Authored placement identity, scoped to a stage. Never regenerate it during load.</summary>
    [DisallowMultipleComponent]
    public sealed class StageObjectId : MonoBehaviour
    {
        [SerializeField, Tooltip("스테이지 내 고유 ID. 복제 후 중복 검사를 실행하세요. 저장 배포 후 변경하면 이전 저장과 호환되지 않습니다.")]
        private string id;
        public string Id => id;

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
        }

        [ContextMenu("Assign ID If Empty")]
        private void AssignIfEmpty()
        {
            if (Application.isPlaying) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Assign stage object ID");
#endif
            Reset();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
