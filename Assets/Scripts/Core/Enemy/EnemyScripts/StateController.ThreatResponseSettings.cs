using UnityEngine;

namespace EnemyAI
{
    public partial class StateController
    {
        [HideInInspector]
        [Header("Target knowledge")]
        [Tooltip("현재 대상의 새로운 위치 정보가 끊긴 뒤 일반 자동 감지 후보로 교체할 수 있기까지의 유예(초). 기본 2초. 근접 위협 대응은 별도 우선순위를 사용하며, 대상 기억의 전체 수명이나 임시 교전 유지 시간과는 별개입니다.")]
        [Min(0f)] public float targetSwitchGrace = 2f;

        public float TargetSwitchGrace => threatResponseStats != null ? threatResponseStats.targetSwitchGrace : targetSwitchGrace;

        // 기존 씬/프리팹과 오버라이드의 필드 이름·타입을 보존한다. 에셋 미지정 시에만 사용한다.
        // 새 게임 코드는 아래 유효값 접근자를 사용하고, 연결된 설정은 ThreatResponseStats에서 편집한다.
        [HideInInspector]
        [Header("Damage reaction / 피격 방향 확인")]
        [Tooltip("피격 방향 확인과 관측 후 임시 교전을 사용합니다. 끄면 기존 피해 위치 기록과 근접 대응만 사용합니다.")]
        public bool enableDamageReaction = true;
        [HideInInspector]
        [Min(0f), Tooltip("보류된 피격 이벤트를 반응 후보로 사용할 시간(초). 기본 3초. 위치 기억 전체 수명과 별개입니다.")]
        public float damageReactionMemory = 3f;
        [HideInInspector]
        [Min(0f), Tooltip("한 번 피격 방향을 확인할 최대 시간(초). 기본 1.2초. 반복 피격은 진행 중인 확인의 위치나 시간을 재설정하지 않습니다.")]
        public float damageCheckDuration = 1.2f;
        [HideInInspector]
        [Min(0f), Tooltip("피격 방향 확인 중 몸을 돌리는 속도(도/초). 기본 240. 기존 전투 조준 속도와 별개입니다.")]
        public float damageCheckTurnSpeed = 240f;
        [HideInInspector]
        [Min(0f), Tooltip("새 피격 반응 사이의 최소 간격(초). 기본 1.5초. 명령 자체가 새 피격을 차단하지는 않습니다.")]
        public float damageReactionCooldown = 1.5f;
        [HideInInspector]
        [Min(0f), Tooltip("방향 확인에 실패한 같은 공격자를 다시 확인하기까지의 간격(초). 기본 3초. 새 피격 이벤트도 필요합니다.")]
        public float damageFailedCheckCooldown = 3f;
        [HideInInspector]
        [Min(0f), Tooltip("직접 관측한 피격 공격자와의 최소 교전 유지시간(초). 기본 1.5초. 사망/무효/새 명령은 즉시 처리합니다.")]
        public float damageEngagementMinimum = 1.5f;
        [HideInInspector]
        [Min(0f), Tooltip("피격 대응 대상의 관측이 끊긴 뒤 종료 유예(초). 기본 1초. 최소 유지시간도 충족해야 종료하며 7m 거리 해제는 적용하지 않습니다.")]
        public float damageEngagementSightGrace = 1f;
        [HideInInspector]
        [Header("Close threat response / 근접 위협 대응")]
        [Tooltip("적·아군이 직접 관측한 다른 위협에게 임시 대응할 진입 수평 거리(m). 기본 5m. 최근 근접 공격자를 우선하고, 그 외에는 충분히 더 가까운 위협을 선택합니다. 0이면 신규 진입을 막으며 장전·엄폐 이동 중 교체는 보류합니다.")]
        [Min(0f)] public float temporaryEngageDistance = 5f;
        [HideInInspector]
        [Tooltip("일반 근접 대응(CloseThreat) 대상이 직접 보이는 상태에서 이 수평 거리(m)를 초과하면 최소 유지 시간 후 종료합니다. 기본 7m. 진입 거리 이상으로 사용합니다. 피격 대응(DamageResponse)에는 거리 해제를 적용하지 않습니다.")]
        [Min(0f)] public float temporaryReleaseDistance = 7f;
        [HideInInspector]
        [Tooltip("일반 근접 대응을 거리·시야 상실 때문에 종료하기 전 최소 유지 시간(초). 기본 1초. 사망·무효·새 명령에는 기다리지 않습니다. 피격 대응은 Damage Engagement Minimum을 사용합니다.")]
        [Min(0f)] public float temporaryMinimumDuration = 1f;
        [HideInInspector]
        [Tooltip("일반 근접 대응 대상이 직접 보이지 않게 된 뒤 종료 유예(초). 기본 1초. 최소 유지 시간도 충족해야 종료합니다. 피격 대응은 Damage Engagement Sight Grace를 사용합니다. 숨은 적 추적이나 벽 너머 사격을 허용하지 않습니다.")]
        [Min(0f)] public float temporarySightGrace = 1f;
        [HideInInspector]
        [Tooltip("임시 대응 종료 또는 새 명령 후 일반 근접 대응에 재진입할 대기(초). 기본 2초. 새 피격 반응은 이 대기 대신 Damage Reaction Cooldown을 사용하므로 새 명령 뒤의 새로운 피격도 평가합니다.")]
        [Min(0f)] public float temporaryCooldown = 2f;
        [HideInInspector]
        [Tooltip("직접 보이는 근접 적이 실제 피해를 준 사실을 우선순위에 반영할 시간(초). 기본 2초. 0이면 피격 우선권을 사용하지 않으며 숨은 공격자에게 시야를 부여하지 않습니다.")]
        [Min(0f)] public float closeThreatDamageMemory = 2f;
        [HideInInspector]
        [Tooltip("현재 대상도 근접해 있고 새 후보의 피격 우선권이 없을 때, 새 후보가 더 가까워야 할 거리 차이(m). 기본 1m, 최소 0.1m. 작은 거리 변화로 타깃이 왕복하는 것을 줄입니다.")]
        [Min(.1f)] public float closeThreatDistanceAdvantage = 1f;

        // 설정만 공유한다. 피격 사건·확인 방향·교전 대상·소비 순번·대기 타이머는 복사하거나 초기화하지 않는다.
        public bool EnableDamageReaction => threatResponseStats != null ? threatResponseStats.enableDamageReaction : enableDamageReaction;
        public float DamageReactionMemory => threatResponseStats != null ? threatResponseStats.damageReactionMemory : damageReactionMemory;
        public float DamageCheckDuration => threatResponseStats != null ? threatResponseStats.damageCheckDuration : damageCheckDuration;
        public float DamageCheckTurnSpeed => threatResponseStats != null ? threatResponseStats.damageCheckTurnSpeed : damageCheckTurnSpeed;
        public float DamageReactionCooldown => threatResponseStats != null ? threatResponseStats.damageReactionCooldown : damageReactionCooldown;
        public float DamageFailedCheckCooldown => threatResponseStats != null ? threatResponseStats.damageFailedCheckCooldown : damageFailedCheckCooldown;
        public float DamageEngagementMinimum => threatResponseStats != null ? threatResponseStats.damageEngagementMinimum : damageEngagementMinimum;
        public float DamageEngagementSightGrace => threatResponseStats != null ? threatResponseStats.damageEngagementSightGrace : damageEngagementSightGrace;
        public float TemporaryEngageDistance => threatResponseStats != null ? threatResponseStats.temporaryEngageDistance : temporaryEngageDistance;
        public float TemporaryReleaseDistance => threatResponseStats != null ? threatResponseStats.temporaryReleaseDistance : temporaryReleaseDistance;
        public float TemporaryMinimumDuration => threatResponseStats != null ? threatResponseStats.temporaryMinimumDuration : temporaryMinimumDuration;
        public float TemporarySightGrace => threatResponseStats != null ? threatResponseStats.temporarySightGrace : temporarySightGrace;
        public float TemporaryCooldown => threatResponseStats != null ? threatResponseStats.temporaryCooldown : temporaryCooldown;
        public float CloseThreatDamageMemory => threatResponseStats != null ? threatResponseStats.closeThreatDamageMemory : closeThreatDamageMemory;
        public float CloseThreatDistanceAdvantage => threatResponseStats != null ? threatResponseStats.closeThreatDistanceAdvantage : closeThreatDistanceAdvantage;

        /// <summary>현재 실제 사용하는 설정을 독립 사본으로 만든다. 에셋 저장과 연결은 호출자가 담당한다.</summary>
        public ThreatResponseStats CreateThreatResponseStatsSnapshot()
        {
            var snapshot = ScriptableObject.CreateInstance<ThreatResponseStats>();
            snapshot.targetSwitchGrace = TargetSwitchGrace;
            snapshot.enableDamageReaction = EnableDamageReaction;
            snapshot.damageReactionMemory = DamageReactionMemory;
            snapshot.damageCheckDuration = DamageCheckDuration;
            snapshot.damageCheckTurnSpeed = DamageCheckTurnSpeed;
            snapshot.damageReactionCooldown = DamageReactionCooldown;
            snapshot.damageFailedCheckCooldown = DamageFailedCheckCooldown;
            snapshot.damageEngagementMinimum = DamageEngagementMinimum;
            snapshot.damageEngagementSightGrace = DamageEngagementSightGrace;
            snapshot.temporaryEngageDistance = TemporaryEngageDistance;
            snapshot.temporaryReleaseDistance = TemporaryReleaseDistance;
            snapshot.temporaryMinimumDuration = TemporaryMinimumDuration;
            snapshot.temporarySightGrace = TemporarySightGrace;
            snapshot.temporaryCooldown = TemporaryCooldown;
            snapshot.closeThreatDamageMemory = CloseThreatDamageMemory;
            snapshot.closeThreatDistanceAdvantage = CloseThreatDistanceAdvantage;
            return snapshot;
        }
    }
}
