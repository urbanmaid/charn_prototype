using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// 엄폐 판단에 사용하는 공유 설정. 타이머, 현재 엄폐점, 후보 시도 기록 등 실행 상태는 넣지 않는다.
    /// 일반 평가 설정은 공유한다. 탐색 영역의 초기값은 각 StateController가 복사해 사용한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy AI/Cover Stats", fileName = "CoverStats")]
    public class CoverStats : ScriptableObject
    {
        [Min(0f), Tooltip("주변 엄폐 탐색 반경에 더할 초기 여유(m). NPC 초기화 때 개별 복사하며 사거리는 바꾸지 않습니다.")]
        public float coverSearchNearbyMargin = 1f;
        [Min(0f), Tooltip("교전 탐색 거리대의 안쪽·바깥쪽 경계 여유(m). 초기화 후에는 NPC별 실행 값을 사용합니다.")]
        public float coverSearchCombatMargin = 1f;
        [Header("Cover departure / 엄폐에서 접근 시작 대기")]
        [Min(-1f), Tooltip("팀 코드 0 이외 NPC의 수색·사격 위치 접근 대기(초). 기본 0.75초. -1이면 기존 숨기 시간을 사용합니다. 일반 점사·숨기와 이동 속도는 바꾸지 않으며, 필요한 장전과 동시에 기다립니다.")]
        public float enemyCoverApproachDelay = .75f;
        [Min(-1f), Tooltip("팀 코드 0 아군의 접근 대기(초). 기본 -1로 기존 숨기 시간을 유지합니다. 0 이상이면 적과 같은 별도 접근 대기 및 조기 장전 방식을 사용합니다.")]
        public float allyCoverApproachDelay = -1f;
        [Header("Cover validation")]
        [Tooltip("엄폐 목적지에 도착했다고 인정할 거리(m). 기본 0.25m. 값을 키우면 목적지에서 더 멀리 멈출 수 있습니다. 정렬 이동은 별도로 최대 0.1m까지 엄격하게 도착을 검사합니다.")]
        [Min(.05f)] public float coverArrivalTolerance = .25f;
        [Tooltip("도착 후 숨기 상태를 유지할 위치 오차(m). 기본 0.4m. 실제 차폐도 충족해야 하며, 첫 도착 허용 거리보다 작게 설정해도 첫 도착 거리 이상을 사용합니다.")]
        [Min(.05f)] public float coverHoldTolerance = .4f;
        [Tooltip("웅크린 몸의 보호 여부를 검사하는 높이(m). 바닥 기준 기본 0.8m. 애니메이션 자세 자체를 바꾸지는 않습니다.")]
        [Min(.1f)] public float coverProbeHeight = .8f;
        [Tooltip("엄폐 실패 후 새 시도까지 기다리는 시간(초). 기본 2초. 줄이면 재시도가 빨라지지만 실패가 반복될 수 있습니다.")]
        [Min(.1f)] public float coverRetryDelay = 2f;
        [Tooltip("엄폐 이동에서 남은 경로 거리가 줄지 않을 때 실패 처리할 대기 시간(초). 기본 4초. 너무 짧으면 일시적 회피나 정체도 실패로 처리할 수 있습니다.")]
        [Min(.5f)] public float coverNoProgressTimeout = 4f;
        [Tooltip("안전한 기존 엄폐점에서 몸만 벗어났을 때, 같은 지점으로 보정할 최대 시간(초). 기본 2초. 새 엄폐물을 찾는 거리나 정렬 이동 거리 설정이 아닙니다.")]
        [Min(.1f)] public float coverRecoveryTimeout = 2f;
        [Tooltip("엄폐 보호를 잃었을 때 더 안전한 위치를 재검토하는 최소 간격(초). 기본 2초. 이동 속도나 숨기 유지 시간을 조절하지 않습니다.")]
        [Min(.1f)] public float coverRelocationInterval = 2f;
        [Header("Cover alignment / 같은 벽 정렬 이동")]
        [Tooltip("같은 벽의 같은 면에 있는 등록 엄폐점으로 정렬 이동할 최대 거리(m). 기본 2m. 후보까지의 직선 거리와 실제 경로 길이 모두 이 값 이하여야 합니다. 늘려도 차폐·점유·경로 노출 증가 제한은 유지되며, 다른 엄폐물 재배치에는 적용되지 않습니다.")]
        [Min(.1f)] public float coverAlignmentMaxDistance = 2f;
        [Tooltip("정렬 후보와 실제 정렬 도착 위치에서 몸의 검사점이 벽 표면과 떨어져도 되는 최대 간격(m). 기본 0.75m. 이동 가능 거리를 늘리려면 Cover Alignment Max Distance를 조절하세요. 너무 키우면 벽에서 떨어져 숨을 수 있습니다.")]
        [Min(.1f)] public float coverAlignmentWallGap = .75f;
        [Header("Close counterfire / 탄창 소진 후 재배치")]
        [Tooltip("엄폐 보호를 잃으면 가까운 적에게 남은 탄창을 소진할 때까지 반격합니다. 대상 상실·발사 불능·보호 회복은 예외입니다. 끄면 즉시 재배치 흐름을 사용합니다. 적과 아군 공용입니다.")]
        public bool enableCloseCounterfire = true;
        [Min(0f), Tooltip("직접 관측하고 사선이 열린 적에게 반격할 최대 수평 거리(m). 기본 5m. 안전한 엄폐 유지나 이미 진행 중인 엄폐 이동에는 반격을 새로 시작하지 않습니다.")]
        public float closeCounterfireDistance = 5f;
        [Min(0f), Tooltip("무한탄약일 때만 사용하는 반격 최대 시간(초). 기본 0.6초. 일반 탄창 반격은 이 값과 무관하게 잔탄 소진 또는 예외 조건까지 유지합니다.")]
        public float closeCounterfireDuration = .6f;
        [Min(.1f), Tooltip("시야·사선이 끊어진 뒤 재배치까지의 유예(초). 기본 0.5초. 관측이 복구되면 초기화합니다. 사망·대상 교체는 즉시 재판단합니다.")]
        public float counterfireSightLossGrace = .5f;
        [Min(.1f), Tooltip("첫 발의 조준 준비 유예(초). 기본 1초. 이 유예 뒤에도 No Shot Timeout 동안 발사하지 못하면 재배치합니다.")]
        public float counterfireAimGrace = 1f;
        [Min(.1f), Tooltip("실제 발사가 없는 상태를 허용할 시간(초). 기본 1.5초. 정상 발사 간격보다 짧게 설정해도 무기의 발사 간격을 보장합니다.")]
        public float counterfireNoShotTimeout = 1.5f;
        [Min(.1f), Tooltip("다른 엄폐점에서 연속으로 안전하게 숨은 뒤 같은 적에게 다시 반격할 수 있는 시간(초). 기본 2초. 이동·보호 상실 시 다시 셉니다.")]
        public float counterfireRearmHold = 2f;
        [Header("Cover tactics (initial tuning, not weapon ballistics)")]
        [Tooltip("엄폐 후보 평가에서 선호하는 적과의 최소 거리(m). 기본 8m. 노출·보호 평가가 우선이며, 실제 발사 가능 최소 거리나 명중률을 직접 바꾸는 값은 아닙니다. 시야 반경 범위로 제한됩니다.")]
        [Min(0f)] public float coverPreferredMin = 8f;
        [Tooltip("엄폐 후보 평가에서 선호하는 적과의 최대 거리(m). 기본 18m. 더 안전한 후보가 우선하므로 이 범위를 벗어난 엄폐도 선택할 수 있습니다. 시야 반경과 최소 선호 거리를 함께 사용합니다.")]
        [Min(0f)] public float coverPreferredMax = 18f;
        [Tooltip("일반 엄폐 후보의 이동 경로에서 사선 노출을 검사하는 표본 간격(m). 기본 0.75m. 줄이면 검사가 촘촘해지지만 물리 검사량이 늘어납니다. 정렬 이동의 별도 정밀 검사는 이 값과 구분됩니다.")]
        [Min(.1f)] public float coverPathSampleSpacing = .75f;
        [Tooltip("새 엄폐 후보를 더 안전하다고 인정할 위험도 감소량. 기본 0.1. 위험도는 알려진 위협에 대한 가중 노출 비율이며 거리(m)가 아닙니다. 높이면 작은 안전 개선만 있는 이동을 거절합니다.")]
        [Min(.01f)] public float coverRiskImprovement = .1f;
        [Tooltip("위험도가 나빠지지 않는 후보가 선호 거리 범위에 얼마나 더 가까워져야 이동 개선으로 인정할지 정하는 값(m). 기본 2m. 적과 무조건 이만큼 가까워진다는 의미가 아니며 보호 상실 재배치는 거리 개선만으로 허용하지 않습니다.")]
        [Min(.1f)] public float coverRangeImprovement = 2f;
        [Tooltip("경로 노출 시간 비교의 허용 차이(초). 기본 0.15초. 노출 시간·연속 노출의 차이가 이 범위면 다른 기준으로 비교하며, 후보 이동 허용 검사에도 사용합니다. 늘리면 노출 차이에 더 관대해집니다.")]
        [Min(0f)] public float coverExposureTolerance = .15f;
        [Tooltip("알려진 위협 위치가 이 거리(m) 이상 바뀌면 기존 엄폐 후보 시도 기록을 초기화합니다. 기본 2m. 너무 작으면 잦은 재선정이 가능해집니다. 위협 구성 자체가 바뀌어도 기록을 초기화합니다.")]
        [Min(.1f)] public float coverThreatChangeDistance = 2f;
    }
}
