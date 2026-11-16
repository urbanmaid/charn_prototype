using UnityEngine;

namespace EnemyAI
{
    public readonly struct CoverSearchSettings
    {
        public readonly float NearbyRadius, CombatMin, CombatMax, NearbyMargin, CombatMargin;
        internal CoverSearchSettings(float radius, float min, float max, float nearbyMargin, float combatMargin)
        {
            NearbyRadius = radius; CombatMin = min; CombatMax = max;
            NearbyMargin = nearbyMargin; CombatMargin = combatMargin;
        }
    }

    // 호출 중 기준점·범위를 고정한다. 향후 명령 영역 제한도 이 판정에 결합한다.
    public readonly struct CoverSearchRegion
    {
        private readonly Vector3 origin, target;
        private readonly float nearbySquared, minSquared, maxSquared;
        public CoverSearchRegion(CoverSearchSettings settings, Vector3 origin, Vector3 target)
        {
            this.origin = origin; this.target = target;
            float nearby = settings.NearbyRadius + settings.NearbyMargin;
            float min = Mathf.Max(0f, settings.CombatMin - settings.CombatMargin);
            float max = settings.CombatMax + settings.CombatMargin;
            nearbySquared = nearby * nearby; minSquared = min * min; maxSquared = max * max;
        }
        public bool Contains(Vector3 point)
        {
            if (!CoverLookup.IsFinite(point)) return false;
            if ((point - origin).sqrMagnitude <= nearbySquared) return true;
            float distanceSquared = (point - target).sqrMagnitude;
            return distanceSquared >= minSquared && distanceSquared <= maxSquared;
        }
    }

    public partial class StateController
    {
        [HideInInspector, Min(0f), Tooltip("주변 엄폐 탐색 반경에 더할 초기 여유(m). 실행 중에는 개체별 복사값을 사용합니다.")]
        public float coverSearchNearbyMargin = 1f;
        [HideInInspector, Min(0f), Tooltip("교전 탐색 거리대의 양쪽 경계에 더할 초기 여유(m). 사거리나 평가 점수를 바꾸지 않습니다.")]
        public float coverSearchCombatMargin = 1f;
        private CoverSearchSettings coverSearchSettings;
        private bool coverSearchSettingsInitialized;
        public CoverSearchSettings SearchSettings
        {
            get { EnsureCoverSearchSettings(); return coverSearchSettings; }
        }

        private void EnsureCoverSearchSettings()
        {
            if (!coverSearchSettingsInitialized) ResetCoverSearchSettings();
        }

        // 새 스폰/풀 재사용 시 명시적으로 호출한다. OnEnable·캐릭터 교체에서는 초기화하지 않는다.
        public void ResetCoverSearchSettings()
        {
            float radius = SearchNonNegative(viewRadius);
            float min = Mathf.Min(SearchNonNegative(CoverPreferredMin), radius);
            float max = Mathf.Max(min, Mathf.Min(SearchNonNegative(CoverPreferredMax), radius));
            float nearbyMargin = SearchNonNegative(coverStats != null ? coverStats.coverSearchNearbyMargin : coverSearchNearbyMargin);
            float combatMargin = SearchNonNegative(coverStats != null ? coverStats.coverSearchCombatMargin : coverSearchCombatMargin);
            if (!TrySetCoverSearchSettings(radius, min, max, nearbyMargin, combatMargin))
                TrySetCoverSearchSettings(0f, 0f, 0f, 0f, 0f);
        }

        private static float SearchNonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        // 잘못된 지시는 현재 값을 유지한다. 공유 에셋과 사거리는 변경하지 않는다.
        public bool TrySetCoverSearchSettings(float radius, float min, float max, float nearbyMargin, float combatMargin)
        {
            if (!float.IsFinite(radius) || !float.IsFinite(min) || !float.IsFinite(max) ||
                !float.IsFinite(nearbyMargin) || !float.IsFinite(combatMargin) ||
                radius < 0f || min < 0f || max < min || nearbyMargin < 0f || combatMargin < 0f ||
                !float.IsFinite((radius + nearbyMargin) * (radius + nearbyMargin)) ||
                !float.IsFinite((max + combatMargin) * (max + combatMargin))) return false;
            coverSearchSettings = new CoverSearchSettings(radius, min, max, nearbyMargin, combatMargin);
            coverSearchSettingsInitialized = true;
            return true;
        }

        // 기존 바이너리 씬/프리팹과 인스턴스 오버라이드를 보존하기 위한 원래 이름/타입의 저장 필드.
        // 에셋 미지정 시에만 사용한다. 일반 Inspector에서는 접고, 새 설정은 CoverStats에서 편집한다.
        // 새 게임 코드는 아래 PascalCase 접근자를 사용해야 에셋 지정 여부에 관계없이 올바른 값을 읽는다.
        [HideInInspector]
        [Header("Cover departure / 엄폐에서 접근 시작 대기")]
        [Min(-1f), Tooltip("팀 코드 0 이외 NPC의 수색·사격 위치 접근 대기(초). 기본 0.75초. -1이면 기존 숨기 시간을 사용합니다. 일반 점사·숨기와 이동 속도는 바꾸지 않으며, 필요한 장전과 동시에 기다립니다.")]
        public float enemyCoverApproachDelay = .75f;
        [HideInInspector]
        [Min(-1f), Tooltip("팀 코드 0 아군의 접근 대기(초). 기본 -1로 기존 숨기 시간을 유지합니다. 0 이상이면 적과 같은 별도 접근 대기 및 조기 장전 방식을 사용합니다.")]
        public float allyCoverApproachDelay = -1f;
        [HideInInspector]
        [Header("Cover validation")]
        [Tooltip("엄폐 목적지에 도착했다고 인정할 거리(m). 기본 0.25m. 값을 키우면 목적지에서 더 멀리 멈출 수 있습니다. 정렬 이동은 별도로 최대 0.1m까지 엄격하게 도착을 검사합니다.")]
        [Min(.05f)] public float coverArrivalTolerance = .25f;
        [HideInInspector]
        [Tooltip("도착 후 숨기 상태를 유지할 위치 오차(m). 기본 0.4m. 실제 차폐도 충족해야 하며, 첫 도착 허용 거리보다 작게 설정해도 첫 도착 거리 이상을 사용합니다.")]
        [Min(.05f)] public float coverHoldTolerance = .4f;
        [HideInInspector]
        [Tooltip("웅크린 몸의 보호 여부를 검사하는 높이(m). 바닥 기준 기본 0.8m. 애니메이션 자세 자체를 바꾸지는 않습니다.")]
        [Min(.1f)] public float coverProbeHeight = .8f;
        [HideInInspector]
        [Tooltip("엄폐 실패 후 새 시도까지 기다리는 시간(초). 기본 2초. 줄이면 재시도가 빨라지지만 실패가 반복될 수 있습니다.")]
        [Min(.1f)] public float coverRetryDelay = 2f;
        [HideInInspector]
        [Tooltip("엄폐 이동에서 남은 경로 거리가 줄지 않을 때 실패 처리할 대기 시간(초). 기본 4초. 너무 짧으면 일시적 회피나 정체도 실패로 처리할 수 있습니다.")]
        [Min(.5f)] public float coverNoProgressTimeout = 4f;
        [HideInInspector]
        [Tooltip("안전한 기존 엄폐점에서 몸만 벗어났을 때, 같은 지점으로 보정할 최대 시간(초). 기본 2초. 새 엄폐물을 찾는 거리나 정렬 이동 거리 설정이 아닙니다.")]
        [Min(.1f)] public float coverRecoveryTimeout = 2f;
        [HideInInspector]
        [Tooltip("엄폐 보호를 잃었을 때 더 안전한 위치를 재검토하는 최소 간격(초). 기본 2초. 이동 속도나 숨기 유지 시간을 조절하지 않습니다.")]
        [Min(.1f)] public float coverRelocationInterval = 2f;
        [HideInInspector]
        [Header("Cover alignment / 같은 벽 정렬 이동")]
        [Tooltip("같은 벽의 같은 면에 있는 등록 엄폐점으로 정렬 이동할 최대 거리(m). 기본 2m. 후보까지의 직선 거리와 실제 경로 길이 모두 이 값 이하여야 합니다. 늘려도 차폐·점유·경로 노출 증가 제한은 유지되며, 다른 엄폐물 재배치에는 적용되지 않습니다.")]
        [Min(.1f)] public float coverAlignmentMaxDistance = 2f;
        [HideInInspector]
        [Tooltip("정렬 후보와 실제 정렬 도착 위치에서 몸의 검사점이 벽 표면과 떨어져도 되는 최대 간격(m). 기본 0.75m. 이동 가능 거리를 늘리려면 Cover Alignment Max Distance를 조절하세요. 너무 키우면 벽에서 떨어져 숨을 수 있습니다.")]
        [Min(.1f)] public float coverAlignmentWallGap = .75f;
        [HideInInspector]
        [Header("Close counterfire / 탄창 소진 후 재배치")]
        [Tooltip("엄폐 보호를 잃으면 가까운 적에게 남은 탄창을 소진할 때까지 반격합니다. 대상 상실·발사 불능·보호 회복은 예외입니다. 끄면 즉시 재배치 흐름을 사용합니다. 적과 아군 공용입니다.")]
        public bool enableCloseCounterfire = true;
        [HideInInspector]
        [Min(0f), Tooltip("직접 관측하고 사선이 열린 적에게 반격할 최대 수평 거리(m). 기본 5m. 안전한 엄폐 유지나 이미 진행 중인 엄폐 이동에는 반격을 새로 시작하지 않습니다.")]
        public float closeCounterfireDistance = 5f;
        [HideInInspector]
        [Min(0f), Tooltip("무한탄약일 때만 사용하는 반격 최대 시간(초). 기본 0.6초. 일반 탄창 반격은 이 값과 무관하게 잔탄 소진 또는 예외 조건까지 유지합니다.")]
        public float closeCounterfireDuration = .6f;
        [HideInInspector]
        [Min(.1f), Tooltip("시야·사선이 끊어진 뒤 재배치까지의 유예(초). 기본 0.5초. 관측이 복구되면 초기화합니다. 사망·대상 교체는 즉시 재판단합니다.")]
        public float counterfireSightLossGrace = .5f;
        [HideInInspector]
        [Min(.1f), Tooltip("첫 발의 조준 준비 유예(초). 기본 1초. 이 유예 뒤에도 No Shot Timeout 동안 발사하지 못하면 재배치합니다.")]
        public float counterfireAimGrace = 1f;
        [HideInInspector]
        [Min(.1f), Tooltip("실제 발사가 없는 상태를 허용할 시간(초). 기본 1.5초. 정상 발사 간격보다 짧게 설정해도 무기의 발사 간격을 보장합니다.")]
        public float counterfireNoShotTimeout = 1.5f;
        [HideInInspector]
        [Min(.1f), Tooltip("다른 엄폐점에서 연속으로 안전하게 숨은 뒤 같은 적에게 다시 반격할 수 있는 시간(초). 기본 2초. 이동·보호 상실 시 다시 셉니다.")]
        public float counterfireRearmHold = 2f;
        [HideInInspector]
        [Header("Cover tactics (initial tuning, not weapon ballistics)")]
        [Tooltip("엄폐 후보 평가에서 선호하는 적과의 최소 거리(m). 기본 8m. 노출·보호 평가가 우선이며, 실제 발사 가능 최소 거리나 명중률을 직접 바꾸는 값은 아닙니다. 시야 반경 범위로 제한됩니다.")]
        [Min(0f)] public float coverPreferredMin = 8f;
        [HideInInspector]
        [Tooltip("엄폐 후보 평가에서 선호하는 적과의 최대 거리(m). 기본 18m. 더 안전한 후보가 우선하므로 이 범위를 벗어난 엄폐도 선택할 수 있습니다. 시야 반경과 최소 선호 거리를 함께 사용합니다.")]
        [Min(0f)] public float coverPreferredMax = 18f;
        [HideInInspector]
        [Tooltip("일반 엄폐 후보의 이동 경로에서 사선 노출을 검사하는 표본 간격(m). 기본 0.75m. 줄이면 검사가 촘촘해지지만 물리 검사량이 늘어납니다. 정렬 이동의 별도 정밀 검사는 이 값과 구분됩니다.")]
        [Min(.1f)] public float coverPathSampleSpacing = .75f;
        [HideInInspector]
        [Tooltip("새 엄폐 후보를 더 안전하다고 인정할 위험도 감소량. 기본 0.1. 위험도는 알려진 위협에 대한 가중 노출 비율이며 거리(m)가 아닙니다. 높이면 작은 안전 개선만 있는 이동을 거절합니다.")]
        [Min(.01f)] public float coverRiskImprovement = .1f;
        [HideInInspector]
        [Tooltip("위험도가 나빠지지 않는 후보가 선호 거리 범위에 얼마나 더 가까워져야 이동 개선으로 인정할지 정하는 값(m). 기본 2m. 적과 무조건 이만큼 가까워진다는 의미가 아니며 보호 상실 재배치는 거리 개선만으로 허용하지 않습니다.")]
        [Min(.1f)] public float coverRangeImprovement = 2f;
        [HideInInspector]
        [Tooltip("경로 노출 시간 비교의 허용 차이(초). 기본 0.15초. 노출 시간·연속 노출의 차이가 이 범위면 다른 기준으로 비교하며, 후보 이동 허용 검사에도 사용합니다. 늘리면 노출 차이에 더 관대해집니다.")]
        [Min(0f)] public float coverExposureTolerance = .15f;
        [HideInInspector]
        [Tooltip("알려진 위협 위치가 이 거리(m) 이상 바뀌면 기존 엄폐 후보 시도 기록을 초기화합니다. 기본 2m. 너무 작으면 잦은 재선정이 가능해집니다. 위협 구성 자체가 바뀌어도 기록을 초기화합니다.")]
        [Min(.1f)] public float coverThreatChangeDistance = 2f;

        // 에셋이 없는 기존 개체는 저장된 값 그대로 동작한다. 기본 에셋을 자동 대입하거나 값을 보정하지 않는다.
        // 아래 기존 평가 설정은 에셋 변경을 즉시 반영한다. 탐색 영역의 개체별 복사값과는 별개다.
        public float EnemyCoverApproachDelay => coverStats != null ? coverStats.enemyCoverApproachDelay : enemyCoverApproachDelay;
        public float AllyCoverApproachDelay => coverStats != null ? coverStats.allyCoverApproachDelay : allyCoverApproachDelay;
        public float CoverApproachDelay => TeamCode == 0 ? AllyCoverApproachDelay : EnemyCoverApproachDelay;
        public float CoverArrivalTolerance => coverStats != null ? coverStats.coverArrivalTolerance : coverArrivalTolerance;
        public float CoverHoldTolerance => coverStats != null ? coverStats.coverHoldTolerance : coverHoldTolerance;
        public float CoverProbeHeight => coverStats != null ? coverStats.coverProbeHeight : coverProbeHeight;
        public float CoverRetryDelay => coverStats != null ? coverStats.coverRetryDelay : coverRetryDelay;
        public float CoverNoProgressTimeout => coverStats != null ? coverStats.coverNoProgressTimeout : coverNoProgressTimeout;
        public float CoverRecoveryTimeout => coverStats != null ? coverStats.coverRecoveryTimeout : coverRecoveryTimeout;
        public float CoverRelocationInterval => coverStats != null ? coverStats.coverRelocationInterval : coverRelocationInterval;
        public float CoverAlignmentMaxDistance => coverStats != null ? coverStats.coverAlignmentMaxDistance : coverAlignmentMaxDistance;
        public float CoverAlignmentWallGap => coverStats != null ? coverStats.coverAlignmentWallGap : coverAlignmentWallGap;
        public bool EnableCloseCounterfire => coverStats != null ? coverStats.enableCloseCounterfire : enableCloseCounterfire;
        public float CloseCounterfireDistance => coverStats != null ? coverStats.closeCounterfireDistance : closeCounterfireDistance;
        public float CloseCounterfireDuration => coverStats != null ? coverStats.closeCounterfireDuration : closeCounterfireDuration;
        public float CounterfireSightLossGrace => Mathf.Max(.1f, coverStats != null ? coverStats.counterfireSightLossGrace : counterfireSightLossGrace);
        public float CounterfireAimGrace => Mathf.Max(.1f, coverStats != null ? coverStats.counterfireAimGrace : counterfireAimGrace);
        public float CounterfireNoShotTimeout => Mathf.Max(.1f, coverStats != null ? coverStats.counterfireNoShotTimeout : counterfireNoShotTimeout);
        public float CounterfireRearmHold => Mathf.Max(.1f, coverStats != null ? coverStats.counterfireRearmHold : counterfireRearmHold);
        public float CoverPreferredMin => coverStats != null ? coverStats.coverPreferredMin : coverPreferredMin;
        public float CoverPreferredMax => coverStats != null ? coverStats.coverPreferredMax : coverPreferredMax;
        public float CoverPathSampleSpacing => coverStats != null ? coverStats.coverPathSampleSpacing : coverPathSampleSpacing;
        public float CoverRiskImprovement => coverStats != null ? coverStats.coverRiskImprovement : coverRiskImprovement;
        public float CoverRangeImprovement => coverStats != null ? coverStats.coverRangeImprovement : coverRangeImprovement;
        public float CoverExposureTolerance => coverStats != null ? coverStats.coverExposureTolerance : coverExposureTolerance;
        public float CoverThreatChangeDistance => coverStats != null ? coverStats.coverThreatChangeDistance : coverThreatChangeDistance;

        /// <summary>
        /// 이 개체가 현재 실제로 사용하는 설정을 독립된 에셋 인스턴스로 복사한다.
        /// 에셋 생성/연결은 호출자가 담당하며, 타이머·엄폐점·교전 상태는 복사하거나 초기화하지 않는다.
        /// </summary>
        public CoverStats CreateCoverStatsSnapshot()
        {
            var snapshot = ScriptableObject.CreateInstance<CoverStats>();
            snapshot.coverSearchNearbyMargin = coverStats != null ? coverStats.coverSearchNearbyMargin : coverSearchNearbyMargin;
            snapshot.coverSearchCombatMargin = coverStats != null ? coverStats.coverSearchCombatMargin : coverSearchCombatMargin;
            snapshot.enemyCoverApproachDelay = EnemyCoverApproachDelay;
            snapshot.allyCoverApproachDelay = AllyCoverApproachDelay;
            snapshot.coverArrivalTolerance = CoverArrivalTolerance;
            snapshot.coverHoldTolerance = CoverHoldTolerance;
            snapshot.coverProbeHeight = CoverProbeHeight;
            snapshot.coverRetryDelay = CoverRetryDelay;
            snapshot.coverNoProgressTimeout = CoverNoProgressTimeout;
            snapshot.coverRecoveryTimeout = CoverRecoveryTimeout;
            snapshot.coverRelocationInterval = CoverRelocationInterval;
            snapshot.coverAlignmentMaxDistance = CoverAlignmentMaxDistance;
            snapshot.coverAlignmentWallGap = CoverAlignmentWallGap;
            snapshot.enableCloseCounterfire = EnableCloseCounterfire;
            snapshot.closeCounterfireDistance = CloseCounterfireDistance;
            snapshot.closeCounterfireDuration = CloseCounterfireDuration;
            snapshot.counterfireSightLossGrace = CounterfireSightLossGrace;
            snapshot.counterfireAimGrace = CounterfireAimGrace;
            snapshot.counterfireNoShotTimeout = CounterfireNoShotTimeout;
            snapshot.counterfireRearmHold = CounterfireRearmHold;
            snapshot.coverPreferredMin = CoverPreferredMin;
            snapshot.coverPreferredMax = CoverPreferredMax;
            snapshot.coverPathSampleSpacing = CoverPathSampleSpacing;
            snapshot.coverRiskImprovement = CoverRiskImprovement;
            snapshot.coverRangeImprovement = CoverRangeImprovement;
            snapshot.coverExposureTolerance = CoverExposureTolerance;
            snapshot.coverThreatChangeDistance = CoverThreatChangeDistance;
            return snapshot;
        }
    }
}
