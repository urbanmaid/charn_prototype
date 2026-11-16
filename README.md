# Charming Illusion

<img width="2456" height="802" alt="logo" src="https://github.com/user-attachments/assets/cfb059cd-8fc0-4caa-b04d-db941c23eda1" />4

**캐릭터 전환과 AI 동료 지휘를 결합한 PC 싱글플레이 전술 액션 TPS**

플레이어가 직접 조준하고 사격하면서, 서로 다른 능력을 가진 캐릭터로 전환하고 AI 동료와 함께 임무를 수행하는 Unity 졸업 프로젝트입니다. 히어로 슈터의 캐릭터별 역할과 분대 전술을 하나의 전투 흐름으로 연결하는 것을 목표로 개발했습니다.

[프로젝트 자료 · Notion](https://app.notion.com/p/QR-41aa5a9966be803cb45adb4af34fe7fd?source=copy_link#41aa5a9966be80e1bec7fb6ba8857832) · [실행 방법](#실행-방법) · [팀 구성](#팀-구성)

| 구분 | 내용 |
| --- | --- |
| 개발팀 | 팀 신우 |
| 프로젝트 | 졸업작품 · 전투 중심 프로토타입 |
| 장르 | 싱글플레이 전술 액션 TPS |
| 대상 플랫폼 | PC / Windows |
| 엔진 및 언어 | Unity 6.4 (`6000.4.0f1`) / C# |
| 렌더링 | Universal Render Pipeline |

## 게임 소개

AI 동료가 기본적인 이동과 전투를 담당하고, 플레이어는 직접 사격·캐릭터 전환·스킬·분대 명령으로 전황에 개입합니다. 동료의 세부 행동을 계속 관리하기보다 필요한 순간에 분대를 재정비하거나 다른 캐릭터의 능력을 활용하는 방식입니다.

주요 임무인 **반달리즘 저지**에서는 교량과 건물 구역을 이동하며 교전하고, 장비 확보와 조건부 상호작용을 통해 목표를 진행합니다. 공습으로 인한 교량 붕괴와 미션 전환을 실제 플레이 공간에 연결했습니다.

<img width="2560" height="1440" alt="gameplay" src="https://github.com/user-attachments/assets/0cb4ed47-e3c4-4d06-b0c6-a092eda52474" />
## 주요 기능

- **캐릭터 전환과 분대 지휘**: 캐릭터의 페르소나·위치·체력·탄약 및 지원되는 실행 상태를 교환하고, 분대 명령을 동료의 추종·전투 행동에 연결합니다.
- **캐릭터별 스킬**: 세 캐릭터의 일반 스킬과 게임 체인저를 공통 실행 경로에 연결하고, 지속 효과와 종료·취소 상태를 관리합니다.
- **전투 FSM**: 관측·기억·표적 정보와 엄폐·재장전 상태를 바탕으로 행동을 선택하고, 여러 판단이 동시에 발생할 때 처리 순서를 제어합니다.
- **타격감과 전투 정보**: 총기 반동, 카메라 충격, 일반 명중·헤드샷·보호막·처치 표시와 재질별 탄착 효과를 제공합니다.
- **미션과 진행 저장**: 도달·수집·처치·상호작용 목표를 순차 진행에 연결하고, 체크포인트의 진행·분대·월드 상태를 수집하고 복원합니다.
- **연출과 확인 공간**: 전투기 공습·교량 붕괴, 캐릭터 도감 및 캐릭터별 스킬 연습장을 구성했습니다.

### 캐릭터별 스킬

| 캐릭터 | 전투 역할 | Q | E | X / 게임 체인저 |
| --- | --- | --- | --- | --- |
| 루시아 | 치유와 소생 지원 | 치유 영역 | 나노 강화 | 광역 소생 |
| 아멜리아 | 보급과 화력 지원 | 통합 보급 강화 | 집속 수류탄 | 보급 깃발 |
| 피오나 | 공격 강화와 생존 | 독탄 | 까마귀 보호막 | 매 기습 |

아멜리아의 현재 E 스킬은 **집속 수류탄**입니다. 소스에 남아 있는 이전 설치형 스킬은 위 활성 스킬 구성에 포함하지 않습니다. 플레이어와 AI 동료는 일부 스킬의 실행 경로가 다르며, 모든 효과가 두 대상에게 동일하게 적용되는 것은 아닙니다.

### 구현 장면

| 실제 인게임의 공습과 교량 붕괴 | 연습장의 피오나 스킬 효과 |
| --- | --- |
| <img width="1920" height="1080" alt="airstrike" src="https://github.com/user-attachments/assets/12f2f33a-c5ce-46f4-a9eb-1873f20e0c4f" /> | <img width="1920" height="1080" alt="fiona-skill" src="https://github.com/user-attachments/assets/85d47a95-34e8-4aee-94a0-aeb23de9d5f5" /> |

## 구현 구조

설정 데이터와 개체별 실행 상태를 분리하고, 공통 컴포넌트에 캐릭터·분대·스테이지 구성을 연결합니다.

| 영역 | 주요 구성 | 역할 |
| --- | --- | --- |
| 캐릭터 적용 | [`PersonaBehavior`](Assets/Scripts/PersonaBehavior.cs), [`PersonaData`](Assets/Scripts/Skills/PersonaData.cs) | 외형·능력치·무기·스킬 설정을 런타임 개체에 적용 |
| 스킬 실행 | [`SkillBehavior`](Assets/Scripts/Skills/SkillBehavior.cs), [`PersonaDataSkill`](Assets/Scripts/Skills/PersonaDataSkill.cs) | 슬롯·쿨다운·포인트 검사와 효과 실행, 장착·해제 처리 |
| 조종 전환 | [`AllySquadManager`](Assets/Scripts/AllyFSM/AllySquadManager.cs) | 조종 캐릭터와 동료의 페르소나·위치·상태 교환 |
| 전투 판단 | [`StateController`](Assets/Scripts/Core/Enemy/EnemyScripts/StateController.cs) | 관측·표적·이동·엄폐 및 행동 전이 관리 |
| 미션 진행 | [`StageMissionRuntime`](Assets/Scripts/Missions/StageMissionRuntime.cs), [`StageMissionDirector`](Assets/Scripts/Missions/StageMissionDirector.cs) | 목표 평가와 장면 내 적·상호작용·진행 연결 |
| 저장과 복원 | [`CheckpointTransaction`](Assets/Scripts/Missions/CheckpointTransaction.cs), [`StageResumeCoordinator`](Assets/Scripts/Missions/StageResumeCoordinator.cs) | 저장 후보와 확정 상태 구분, 월드 복원과 실패 처리 |
| 스테이지 초기화 | [`StageSceneInitializer`](Assets/Scripts/StageSceneInitializer.cs) | 선택된 스테이지의 배경·미션 개체 생성 및 시작 위치 연결 |

`PersonaDataSkill`은 스킬 구성을 담는 ScriptableObject이며, 슬롯 내부의 `SkillData`는 직렬화된 실행 로직을 참조합니다. 현재 쿨다운이나 지속 효과처럼 개체마다 달라지는 상태는 실행 컴포넌트에서 관리합니다.

체크포인트는 저장 후보를 수집·검증한 뒤 파일 기록이 성공한 경우에만 확정 상태를 갱신합니다. 스테이지 재개 시에는 개체 식별자와 저장 어댑터를 통해 월드 상태를 대응시킵니다.

## 개발 환경

| 항목 | 프로젝트 설정 |
| --- | --- |
| Unity Editor | `6000.4.0f1` |
| Universal RP | `17.4.0` |
| Input System | `1.19.0` |
| AI Navigation | `2.0.11` |
| UI | Unity UI / TextMeshPro |
| 데이터 및 저장 | ScriptableObject / Prefab / JSON 로컬 파일 |
| 협업 | Unity Version Control |

버전 기준은 [`ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt)와 [`Packages/manifest.json`](Packages/manifest.json)입니다.

## 실행 방법

### Unity Editor에서 실행

1. Unity Hub에 **Unity `6000.4.0f1`**을 설치합니다. Windows 실행 파일을 빌드하려면 해당 플랫폼의 Build Support도 준비합니다.
2. 이 저장소를 내려받고, Unity Hub에서 `Assets`, `Packages`, `ProjectSettings`가 있는 프로젝트 루트를 추가합니다.
3. 패키지 복원과 에셋 임포트가 끝날 때까지 기다립니다. 컴파일 오류와 누락된 에셋 참조가 없는지 확인합니다.
4. [`Assets/Scenes/Outgame/Menu.unity`](Assets/Scenes/Outgame/Menu.unity)를 열고 Play를 실행합니다.
5. 메뉴에서 저장 슬롯과 준비된 스테이지를 선택해 게임을 시작합니다.

**일반 플레이는 메뉴 씬에서 시작합니다.** `MainGameplay`는 선택한 스테이지 데이터를 받아 배경과 미션 개체를 생성하는 공용 씬입니다. 이 씬만 단독 실행하면 스테이지 선택 정보나 fallback 설정이 없어 맵이 생성되지 않을 수 있습니다.

### 캐릭터별 스킬 연습장

메뉴의 캐릭터 도감을 통해 진입하거나, 아래 씬을 열어 스킬을 확인할 수 있습니다.

- [루시아 연습장](Assets/Scenes/Test/LuciaSkillTest.unity)
- [아멜리아 연습장](Assets/Scenes/Test/AmeliaSkillTest.unity)
- [피오나 연습장](Assets/Scenes/Test/FionaSkillTest.unity)

연습장에는 쿨다운 초기화·게임 체인저 포인트 충전·피격 시험 등의 테스트 기능이 있습니다. `F6`으로 테스트 안내를 표시하거나 숨길 수 있으며, `Esc`로 일시정지와 메뉴 복귀를 사용할 수 있습니다.

### Windows 빌드

메뉴 씬을 시작 씬으로 두고 `MainGameplay` 및 사용하는 스킬 연습장 등 씬 전환에 필요한 씬을 빌드 목록에 포함합니다. Windows 대상으로 빌드한 뒤 메뉴 진입, 스테이지 선택, 연습장 진입과 메뉴 복귀를 확인합니다.

## 기본 조작

| 입력 | 기능 |
| --- | --- |
| `W`, `A`, `S`, `D` | 이동 |
| 마우스 이동 | 시점 조절 |
| 마우스 왼쪽 / 오른쪽 버튼 | 사격 / 조준 |
| `Left Shift` / `Space` | 달리기 / 점프 |
| `R` | 재장전 |
| `Left Ctrl` | 엄폐 |
| `Q` / `E` | 일반 스킬 1 / 2 |
| `X` | 게임 체인저 |
| `F` / `G` | 상호작용 / 가젯 사용 |
| `C` 길게 누르기 + 마우스 휠 | 조종 전환 대상 선택, 키를 놓아 확정 |
| `Esc` | 일시정지 |

기본 키는 [`PlayerControls.inputactions`](Assets/Resources/Controls/PlayerControls.inputactions)와 조종 전환 코드를 기준으로 정리했습니다. 상세 조작은 게임 내 조작 안내와 현재 입력 설정을 확인할 수 있습니다.

## 프로젝트 구조

```text
Assets/
├─ Scripts/
│  ├─ AllyFSM/       # 분대와 조종 전환
│  ├─ Core/          # 플레이어·적 전투와 공통 기능
│  ├─ Skills/        # 스킬 공통 실행과 캐릭터별 효과
│  ├─ Missions/      # 목표 진행·체크포인트·월드 저장
│  ├─ Interactive/   # 스테이지 상호작용
│  ├─ Outgame/       # 메뉴·캐릭터 도감·스테이지 진입
│  ├─ Status/        # 인게임 상태 효과
│  └─ UI/            # HUD와 전투 정보 표시
├─ Gadget/           # 가젯과 명령 관련 기능
├─ Scenes/           # 메뉴·공용 게임플레이·연습장 씬
├─ Resources/        # 런타임 데이터와 프리팹
└─ _ExternalAsset/   # 외부 에셋
Packages/            # 패키지 설정 및 로컬 패키지
ProjectSettings/     # Unity 프로젝트 설정
Docs/                # 작업 기록과 기능 설명
```

## 팀 구성

**팀 신우 — 신입이지만 우승하고 싶어**

| 팀원 | 주요 담당 |
| --- | --- |
| 박강 | 게임 기획과 세계관·캐릭터 설정, 캐릭터·프롭 모델링, 페르소나·효과·상호작용 공용 구조, UI 디자인과 아웃게임 연결 |
| 박세웅 | 아군·적군 전투 FSM, 조종 전환과 분대 기능 통합, 미션·저장·복원 시스템, 레벨 구성 및 전체 기능 통합·검증·최적화 |
| 정재헌 | 캐릭터 스킬 시스템, 분대 지휘 기능, 타격감·전투 피드백, 공습·교량 붕괴 및 일부 BGM 구현·연결, UI·편의 기능 보완과 레퍼런스 분석 |

페르소나, 조종 전환과 분대 명령 등은 공동 작업 영역입니다. 공용 구조의 원형 제작, 개별 기능 구현과 후속 통합·보완을 구분해 담당을 정리했습니다.

### 정재헌 세부 구현

- **캐릭터 스킬 시스템**: 캐릭터별 고유 스킬·게임 체인저, 스킬 효과·이펙트·사운드 연결 및 사용·종료·취소 처리
- **분대 지휘 기능**: 집결·산개 대형 배치와 간격 조절, 조종 전환 시 스킬·캐릭터 상태 처리 보완
- **타격감 및 전투 피드백**: 총기 반동과 화면 충격, 명중·헤드샷·피격·처치 효과, 재질별 탄착음과 탄착 효과
- **인게임 연출**: 전투기 공습·교량 붕괴, 스킬 사용 중 총기 파지·소생 동작 보완 및 보급 깃발 텍스처 작업
- **BGM**: 배경음악 재생·연결 기능 일부 구현
- **UI 및 편의 기능**: 캐릭터 도감과 PA관리 메뉴 연결, 연습장·일시정지 기능 보완
- **기획 참여**: 레퍼런스 게임 분석 및 프로젝트와의 비교

## 개발 상태

현재는 직접 전투·캐릭터 스킬·동료 운용·미션 진행을 연결한 프로토타입입니다. 후속 작업은 전투와 저장 경로의 안정화, 목표 이용자 플레이테스트 및 콘텐츠 보강에 두고 있습니다.

- 근접 사격 시 무기와 몸·벽의 시각적 관통, 장애물 주변 사격 판정을 추가 점검합니다.
- 다수 개체가 동시에 관측·엄폐 탐색을 수행하는 교전 조건의 성능을 확인합니다.
- 새 맵과 미션을 추가할 때 저장 대상·개체 ID·복원 어댑터를 함께 연결해야 합니다.
- 호감도·대화·캐릭터 관계 및 추가 스토리는 후속 확장 항목이며 현재 구현 성과에 포함하지 않습니다.

## 참고 및 외부 에셋

기존 TPS 에셋의 이동·사격·전투 기반을 프로젝트에 맞게 확장했습니다. 외부 에셋의 기본 기능 전체를 팀의 독자 제작물로 포함하지 않습니다.

- **기획 참고**: Overwatch의 캐릭터별 역할과 능력, Blue Archive의 캐릭터 중심 표현, Operation Flashpoint와 Ghost Recon Advanced Warfighter의 분대 전투 경험
- **전투 기반**: Third Person Shooter Bundle
- **환경**: Military Airport Military Base Environment, NewGen Urban, Roadside Tales Free Modular Bridge, Industrial Buildings Props, URP Scene Level Water, UAA City Props Vehicles
- **모션**: Human Throwing Animations FREE
- **캐릭터 관련 패키지**: UniVRM / glTF 로컬 패키지

프로젝트에 포함된 외부 코드·모델·텍스처·사운드의 권리와 라이선스는 각 제작자의 고지에 따릅니다. 이 README 자체가 외부 에셋의 사용·재배포 권한을 부여하지는 않습니다.



# 포함시키면 안 되는 폴더

- Assets/_ExternalAsset
    - 주요 외부 아트 어셋이 포함됨
- Assets/Kevin Iglesias
    - 모션 관련 외부 아트 어셋이 포함됨
- Assets/Scripts/Core 중 Assets/Scripts/Core/Player, Assets/Scripts/Core/Deprecated
    - 유료 리버스 엔지니어링 어셋 기반이 포함되며, 일부는 프로젝트 내에서 미사용
- Assets/Scripts/_PK_Private
    - 타 프로젝트 과제용으로 사용했던 스크립트가 포함되어 있음

https://app.notion.com/p/416a5a9966be800bbe4df170236b9efd?source=copy_link
