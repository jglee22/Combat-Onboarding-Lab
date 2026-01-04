## 프로젝트 소개

이 프로젝트는 **완성된 게임**이 아닙니다.  
전투 튜토리얼의 구조와 운영 방식을 구현하는 데 집중합니다.

### 핵심 가치

- 서버 정책(JSON) 변경만으로 전투 튜토리얼 UX가 달라지는 구조
- 실패 누적에 따라 상태가 변화하는 튜토리얼 상태 머신
- 전투 구현과 완전히 분리된 튜토리얼 흐름 제어
- 운영 관점에서 의미 있는 전투 튜토리얼 로그 스키마 (RunReport 시스템)

👉 전투/UI는 최소화되어 있으며, **구조와 흐름이 핵심입니다.**

### 추천 읽기 순서

1. **정책(Policy) JSON 스키마** - 서버와 클라이언트 간 데이터 계약
2. **TutorialState 및 흐름 제어** - 상태 머신 설계 원칙
3. **RunReport 로그 시스템** - 상세 이벤트 추적 및 분석
4. **정책 변경에 따른 로그 비교** - 실제 데이터로 보는 정책 효과
5. **전투 이벤트 인터페이스** - 전투와 튜토리얼의 느슨한 결합


## Intentionally Not Implemented

이 프로젝트의 목적은 전투 콘텐츠 자체가 아니라, **전투를 가르치는 구조와 운영 방식**입니다.

따라서 다음은 의도적으로 구현하지 않았습니다:

- 실제 전투 밸런스
- 다양한 적 패턴
- 화려한 UI/이펙트
- 실시간 멀티플레이어
- 서버 API 통신 (정책 로딩/로그 전송)

이 프로젝트는 "어떻게 전투를 가르칠 것인가"에 대한 구조적 답변을 구현하는 데 집중합니다.


## 프로젝트 개요

Combat Onboarding Lab은
라이브 게임 환경을 가정하고,
전투 튜토리얼 UX를 서버 정책으로 제어·개선할 수 있도록 설계한
Unity 기반 게임 클라이언트 + 백엔드 포트폴리오 프로젝트입니다.

많은 게임의 전투 튜토리얼은
클라이언트에 고정되어 있어 운영 중 개선이 어렵습니다.

본 프로젝트는
"전투를 처음 접하는 유저에게 무엇을, 언제, 어떻게 가르칠 것인가"
를 운영 관점에서 조정 가능하게 만드는 것을 목표로 합니다.

### 서버 책임

- 튜토리얼 정책 제공 (JSON)
- 힌트 노출 타이밍
- 실패 허용 횟수
- UX Variant (A/B 테스트)

### 클라이언트 책임

- 전투 상태 처리
- 애니메이션 / 피드백
- 서버 정책 적용
- 로그 수집 및 저장


## 정책(Policy) JSON 스키마

서버와 클라이언트 간의 튜토리얼 정책 데이터 계약(Contract)입니다.

### 스키마 정의

```json
{
  "tutorialVersion": "string",    // 튜토리얼 버전 (예: "1.0.0")
  "variant": "string",            // A/B 테스트용 변형 (예: "A", "B", "control")
  "hintDelaySeconds": 3.0,        // 힌트 노출까지의 지연 시간 (초)
  "showArrow": true,              // 화살표 가이드 표시 여부
  "maxFailCount": 3,              // 최대 실패 허용 횟수
  "assistEnabled": false          // 자동 도움말(Assist) 활성화 여부
}
```

### 필드 설명

| 필드 | 타입 | 설명 | 예시 |
|------|------|------|------|
| `tutorialVersion` | string | 서버에서 관리하는 정책 버전 | `"1.0.0"` |
| `variant` | string | A/B 테스트용 변형 식별자 | `"A"`, `"B"`, `"control"` |
| `hintDelaySeconds` | float | 힌트 노출까지의 지연 시간 (초) | `3.0` |
| `showArrow` | bool | 화살표 가이드 표시 여부 | `true`, `false` |
| `maxFailCount` | int | 최대 실패 허용 횟수 (초과 시 도움말 제공) | `3` |
| `assistEnabled` | bool | 자동 도움말(Assist) 활성화 여부 | `true`, `false` |

### 예시 JSON

```json
{
  "tutorialVersion": "1.2.0",
  "variant": "B",
  "hintDelaySeconds": 5.0,
  "showArrow": true,
  "maxFailCount": 5,
  "assistEnabled": true
}
```

### 구현 위치

- **스키마 정의**: `Assets/Client/Tutorial/TutorialPolicy.cs`
- **정책 적용**: `Assets/Client/Tutorial/PolicyApplier.cs`
- **정책 예시**: `Assets/Client/Tutorial/PolicyExamples.cs`


## 튜토리얼 상태(TutorialState) 및 흐름 제어

튜토리얼의 상태 관리와 흐름 제어 구조입니다.

### 핵심 설계 원칙

**"실패 → UX 변화"가 구조로 명확히 보여야 함**

상태의 개수보다는, 사용자 실패에 따른 UX 변화 흐름이 코드와 문서에서 명확히 드러나야 합니다.

### TutorialState 정의

```csharp
public enum TutorialState
{
    Init,              // 정책 로딩 직후
    WaitingForAction,  // 유저 입력 대기
    Hint,              // 힌트 노출
    Retry,             // 재시도
    Assist,            // 강제 보조
    Clear              // 튜토리얼 완료
}
```

### 상태 전환 흐름

```
┌─────────┐
│  Init   │  정책 로딩 직후
└────┬────┘
     │
     ↓
┌─────────────────┐
│ WaitingForAction│  유저 입력 대기
└────┬────────────┘
     │ (시간 초과: hintDelaySeconds)
     ↓
┌─────────┐
│  Hint   │  힌트 노출
└────┬────┘
     │
     ├─→ (사용자 행동)
     │
     ├─→ [성공] ──→ ┌───────┐
     │             │ Clear │  튜토리얼 완료
     │             └───────┘
     │
     └─→ [실패] ──→ ┌────────┐
                    │ Retry  │  재시도
                    └───┬────┘
                        │ (반복 실패: failCount > maxFailCount)
                        │ (assistEnabled == true)
                        ↓
                    ┌─────────┐
                    │ Assist  │  강제 보조
                    └────┬────┘
                         │
                         ↓
                    ┌───────┐
                    │ Clear │  튜토리얼 완료
                    └───────┘
```

### 전체 흐름 다이어그램 (ASCII)

```
튜토리얼 시작
    │
    ├─→ [정책 로딩] ──→ Init
    │
    ├─→ [유저 입력 대기] ──→ WaitingForAction
    │                          │
    │                          ├─→ (시간 초과) ──→ Hint
    │                          │
    │                          └─→ (피격 발생) ──→ OnFailure()
    │                                                 │
    │                                                 ├─→ failCount++
    │                                                 │
    │                                                 ├─→ (failCount <= maxFailCount) ──→ Retry
    │                                                 │
    │                                                 └─→ (failCount > maxFailCount && assistEnabled) ──→ Assist
    │
    └─→ [적 처치] ──→ OnSuccess() ──→ Clear ──→ [RunReport 저장]
```

### 실패 처리 흐름 (핵심)

1. **실패 발생** → `failCount` 증가
2. **`failCount <= maxFailCount`** → `Retry` 상태 (재시도 허용)
3. **`failCount > maxFailCount && assistEnabled == true`** → `Assist` 상태 (자동 도움말)
4. **성공** → `failCount` 리셋, `Clear` 상태

### TutorialController 책임

**담당하는 것:**
- ✅ 현재 `TutorialState` 관리
- ✅ `FailCount` 누적
- ✅ `Policy` 값 참조 (`hintDelaySeconds`, `maxFailCount`, `assistEnabled`)
- ✅ 상태 전환 트리거 제공
- ✅ RunReport 이벤트 로깅

**담당하지 않는 것:**
- ❌ 전투 구현
- ❌ UI 구현
- ✅ **"흐름 제어"만 담당**

### Policy 값 사용 시점

`TutorialPolicy`가 실제로 사용되기 시작하는 지점:

- **`hintDelaySeconds`**: `WaitingForAction` → `Hint` 전환 타이밍
- **`maxFailCount`**: 실패 횟수 기준으로 `Retry` → `Assist` 전환 판단
- **`assistEnabled`**: `Assist` 상태 진입 가능 여부

### 구현 위치

- **상태 정의**: `Assets/Client/Tutorial/TutorialState.cs`
- **흐름 제어**: `Assets/Client/Tutorial/TutorialController.cs`


## 정책 해석기 (PolicyApplier) 책임 분리

### 구조 의도

```
[Server JSON]
  ↓
TutorialPolicy
  ↓
PolicyApplier ← 여기서만 정책 해석
  ↓
TutorialController (질문만 함)
```

### 책임 분리 원칙

**PolicyApplier (정책 해석기):**
- ✅ 서버 JSON → TutorialPolicy 변환
- ✅ 정책 값 해석 및 제공
- ✅ 정책 기반 판단 로직 제공 (`ShouldTransitionToAssist()`)
- ✅ 정책 변경 시 UI 업데이트 (`OnPolicyUpdated()`)

**TutorialController:**
- ✅ PolicyApplier에게 질문만 함
- ❌ 직접 정책을 해석하지 않음
- ✅ 정책 값 참조만 수행

### 예시: 실패 처리 흐름

```csharp
// TutorialController는 PolicyApplier에게 질문만 함
if (policyApplier.ShouldTransitionToAssist(failCount))
{
    ChangeState(TutorialState.Assist);
}
```

이 구조는 "클린 아키텍처"라기보다, **라이브 게임에서 흔히 쓰는 책임 분리 구조**입니다.

### 정책 변경 시 UI 업데이트

정책이 변경되면 `OnPolicyUpdated()`에서 "변화가 보이는 처리"를 수행합니다:

- **힌트 타이머 재설정**: `WaitingForAction` 상태일 때 새로운 `hintDelaySeconds`로 타이머 재시작
- **UI 토글 갱신**: `showArrow` 정책에 따라 화살표 표시/숨김
- **정책 변경 이벤트**: 외부 시스템에 정책 변경 알림

**구현 위치:**
- `Assets/Client/Tutorial/PolicyApplier.cs` - `OnPolicyUpdated()`, `ResetHintTimer()`, `UpdateUIToggles()`

### 정책 초기화 및 저장

- **`GetCurrentPolicy()`**: 현재 적용된 정책 객체 반환
- **`GetCurrentPolicyJson()`**: 실제 로드된 원본 JSON 문자열 반환 (정책 스냅샷용)
- **`lastLoadedPolicyJson`**: 마지막으로 로드된 정책 JSON 저장 (원본 보존)


## 전투 이벤트 인터페이스

### 설계 목적

- 튜토리얼이 전투 구현에 의존하지 않도록 설계
- 전투를 만들 필요 없이, 전투에서 뭘 보고 판단할지만 정의

### 인터페이스 정의

```csharp
public interface ICombatEventSource
{
    event Action OnPlayerHit;        // 플레이어가 공격했을 때
    event Action OnPlayerDamaged;    // 플레이어가 피해를 입었을 때
    event Action OnEnemyDefeated;    // 적이 패배했을 때
    event Action OnPlayerDefeated;   // 플레이어가 패배했을 때
}
```

### 구현 위치

- **인터페이스 정의**: `Assets/Client/Tutorial/ICombatEventSource.cs`
- **가짜 전투 이벤트 소스 (테스트용)**: `Assets/Client/Tutorial/MockCombatEventSource.cs`
- **실제 전투 이벤트 소스**: `Assets/Client/Tutorial/CombatEventSource.cs`

### TutorialController 연결

TutorialController는 ICombatEventSource를 구독하여 전투 이벤트에 반응합니다:

- `OnPlayerDamaged` → `OnFailure()` 호출 → `failCount` 증가
- `OnEnemyDefeated` → `OnSuccess()` 호출 → `Clear` 상태

**이벤트 기반 튜토리얼 흐름 제어:**
- 가짜 전투라도 이벤트가 튜토리얼 흐름을 움직임
- 실제 전투 구현 없이도 튜토리얼 흐름 제어가 가능


## RunReport 로그 시스템

### 설계 원칙

- 로그는 '보내는 코드' 말고 '스키마'부터 정의
- 아직 API 붙일 필요 없음
- **"수집 → 판단 → 정책 변경"** 흐름이 머릿속에 보이게
- 각 필드가 왜 필요한지 설명 가능해야 함
- **메모리 버퍼 방식**: 이벤트마다 파일 쓰지 않고, 런 종료 시 한 번에 저장

### RunReport 구조

```csharp
public class RunReport
{
    public string schemaVersion;      // 스키마 버전 ("1.0.0")
    public RunMetadata run;           // 런 메타데이터 (시간, seed, variant 등)
    public TutorialPolicy policy;      // 정책 스냅샷 (객체 형태)
    public string policyData;         // 정책 실제 로드값 (JSON 문자열, 원본)
    public RunSummary summary;        // 런 요약 정보
    public List<TutorialEvent> events; // 이벤트 리스트
}
```

### RunMetadata 구조

```csharp
public class RunMetadata
{
    public string startTime;      // 런 시작 시간 (ISO 8601 형식)
    public int seed;              // 시드 값 (재현 가능성)
    public string variant;       // 정책 variant (A/B 테스트)
    public string appVersion;     // 앱 버전
    public string tutorialVersion; // 튜토리얼 정책 버전
}
```

### RunSummary 구조

```csharp
public class RunSummary
{
    public string result;         // 결과 (CLEAR, FAIL, ABORT 등)
    public float durationSeconds; // 총 소요 시간 (초)
    public string endReason;      // 종료 이유
    public int stepCount;         // 단계 수
    public int failCount;         // 실패 횟수
    public int hintShownCount;    // 힌트 표시 횟수
    public bool assistTriggered;  // 어시스트 발동 여부
    public int damageTaken;       // 받은 피해량
}
```

### TutorialEvent 구조

```csharp
public enum TutorialEventType
{
    RUN_START,        // 튜토리얼(런) 시작
    RUN_END,          // 튜토리얼(런) 종료
    STEP_START,       // 단계 시작
    STEP_CLEAR,       // 단계 클리어
    FAIL,             // 실패
    HINT_SHOWN,       // 힌트 표시
    ASSIST_TRIGGERED  // 어시스트 발동
}

public class TutorialEvent
{
    public TutorialEventType type;    // 이벤트 타입 (enum)
    public string typeName;           // 이벤트 타입 이름 (문자열)
    public float timestamp;           // 이벤트 발생 시점 (초, 튜토리얼 시작 기준)
    public object data;               // 추가 데이터 (객체)
}
```

### 이벤트 로깅 시점

**TutorialController에서 자동으로 로깅되는 이벤트:**

1. **RUN_START**: 튜토리얼 시작 시
   - `data`: `{ appVersion, policyVariant, tutorialVersion }`

2. **STEP_START**: 상태 변경 시 (새 상태 진입)
   - `data`: `{ stepName }`

3. **STEP_CLEAR**: 상태 변경 시 (이전 상태 종료)
   - `data`: `{ previousState }`

4. **HINT_SHOWN**: 힌트 표시 시
   - `data`: `{ hintDelay }`

5. **FAIL**: 실패 발생 시
   - `data`: `{ failCount, state }`

6. **ASSIST_TRIGGERED**: 어시스트 발동 시
   - `data`: `{ failCount }`

7. **RUN_END**: 튜토리얼 종료 시
   - `data`: `{ result, endReason, durationSeconds, failCount, damageTaken }`

### 로그 저장

- **저장 위치**: `Application.persistentDataPath/Reports/`
- **파일명 형식**: `run_{timestamp}_{variant}_seed{seed}_{result}.json`
  - 예: `run_20241230-114721_A_seed378692552_CLEAR.json`
- **저장 시점**: `Clear` 상태 진입 시 (`FinalizeRunReport()` 호출)
- **JSON 직렬화**: 커스텀 직렬화로 `data` 필드와 `policyData`를 객체로 변환

### 정책 스냅샷 저장

RunReport는 두 가지 형태로 정책을 저장합니다:

1. **`policy` (객체)**: 파싱된 정책 객체 (fallback 로직 적용 후)
2. **`policyData` (JSON 문자열)**: 실제 로드된 원본 JSON (서버에서 받은 그대로)

이렇게 저장하는 이유:
- `policy`: 분석 시 객체로 바로 접근 가능
- `policyData`: 서버에서 실제로 전송한 원본 데이터 보존 (디버깅/검증용)

### 구현 위치

- **RunReport**: `Assets/Client/Tutorial/RunReport.cs`
- **RunMetadata**: `Assets/Client/Tutorial/RunMetadata.cs`
- **RunSummary**: `Assets/Client/Tutorial/RunSummary.cs`
- **TutorialEvent**: `Assets/Client/Tutorial/TutorialEvent.cs`



## 튜토리얼 상태 전환 흐름 시연

### TutorialFlowDriver

**역할**: 이 상태들이 실제 플레이 흐름에서 어떻게 전환되는지 보여주는 샘플 드라이버

**포함**: 흐름만 있음 (O)  
**제외**: 전투 구현 (X), UI 구현 (X)

### 예시 흐름

```
Init
  ↓
WaitingForAction
  ↓ (시간 초과)
Hint
  ↓ (피격 발생)
Failure → Retry
  ↓ (반복 실패)
Assist
  ↓ (적 처치)
Clear
```

### 이 파일 하나로:

- ✅ 상태 머신이 머릿속이 아니라 코드에서 보이고
- ✅ "운영 가능한 튜토리얼" 구조를 보여줌

### 구현 위치

- **흐름 드라이버**: `Assets/Client/Tutorial/TutorialFlowDriver.cs`


## 정책 변경에 따른 UX 변화

### 핵심 원칙

**같은 튜토리얼 흐름이지만, 서버 정책에 따라 유저가 경험하는 UX는 달라집니다.**

코드는 안 바뀌고, JSON만 바뀜.

### 정책 비교 예시

#### Policy A: 빠른 힌트, 화살표 표시, 도움말 비활성화

```json
{
  "tutorialVersion": "1.0.0",
  "variant": "A",
  "hintDelaySeconds": 2.0,
  "showArrow": true,
  "maxFailCount": 3,
  "assistEnabled": false
}
```

**특징:**
- `hintDelay = 2초`: 빠르게 힌트 제공
- `showArrow = true`: 화살표 가이드 표시
- `assistEnabled = false`: 실패해도 자동 도움말 없음

#### Policy B: 느린 힌트, 화살표 없음, 도움말 활성화

```json
{
  "tutorialVersion": "1.0.0",
  "variant": "B",
  "hintDelaySeconds": 6.0,
  "showArrow": false,
  "maxFailCount": 3,
  "assistEnabled": true
}
```

**특징:**
- `hintDelay = 6초`: 느리게 힌트 제공 (유저가 스스로 시도할 시간 제공)
- `showArrow = false`: 화살표 가이드 없음 (자율 학습)
- `assistEnabled = true`: 실패 시 자동 도움말 제공

### 정책 변경의 효과

| 정책 | 힌트 타이밍 | 화살표 가이드 | 도움말 제공 | 유저 경험 |
|------|------------|------------|-----------|----------|
| Policy A | 빠름 (2초) | 있음 | 없음 | 빠른 가이드, 화살표로 명확한 안내 |
| Policy B | 느림 (6초) | 없음 | 있음 | 여유 있는 학습, 화살표 없이 자율 탐색, 실패 시 지원 |

### 구현 위치

- **정책 예시**: `Assets/Client/Tutorial/PolicyExamples.cs`


## 정책 변경에 따른 실제 로그 비교

**같은 튜토리얼 플레이, 다른 정책 → 다른 로그 데이터**

이것이 정책 기반 튜토리얼의 핵심입니다. 코드는 변경하지 않고, JSON만 바꿔서 운영 데이터를 수집할 수 있습니다.

### RunReport 예시

#### Policy A로 플레이했을 때의 RunReport

```json
{
  "schemaVersion": "1.0.0",
  "run": {
    "startTime": "2024-12-30T11:47:21Z",
    "seed": 378692552,
    "variant": "A",
    "appVersion": "0.1.0",
    "tutorialVersion": "1.0.0"
  },
  "policy": {
    "tutorialVersion": "1.0.0",
    "variant": "A",
    "hintDelaySeconds": 2.0,
    "showArrow": true,
    "maxFailCount": 3,
    "assistEnabled": false
  },
  "policyData": {
    "tutorialVersion": "1.0.0",
    "variant": "A",
    "hintDelaySeconds": 2.0,
    "showArrow": true,
    "maxFailCount": 3,
    "assistEnabled": false
  },
  "summary": {
    "result": "CLEAR",
    "durationSeconds": 6.0,
    "endReason": "Enemy defeated",
    "stepCount": 3,
    "failCount": 2,
    "hintShownCount": 1,
    "assistTriggered": false,
    "damageTaken": 0
  },
  "events": [
    {
      "type": 0,
      "typeName": "RUN_START",
      "timestamp": 0.0,
      "data": {
        "appVersion": "0.1.0",
        "policyVariant": "A",
        "tutorialVersion": "1.0.0"
      }
    },
    {
      "type": 2,
      "typeName": "STEP_START",
      "timestamp": 1.0,
      "data": {
        "stepName": "WaitingForAction"
      }
    },
    {
      "type": 5,
      "typeName": "HINT_SHOWN",
      "timestamp": 3.0,
      "data": {
        "hintDelay": 2
      }
    },
    {
      "type": 1,
      "typeName": "RUN_END",
      "timestamp": 6.0,
      "data": {
        "result": "CLEAR",
        "endReason": "Enemy defeated",
        "durationSeconds": 6.0,
        "failCount": 2,
        "damageTaken": 0
      }
    }
  ]
}
```

**분석:**
- `hintDelaySeconds: 2.0` → 빠른 힌트로 인해 초반 가이드가 빨랐음
- `showArrow: true` → 화살표 가이드 표시됨
- `assistEnabled: false` → 실패해도 도움말 없이 스스로 해결
- `variant: "A"` → A/B 테스트 그룹 A에 속함

#### Policy B로 플레이했을 때의 RunReport

```json
{
  "schemaVersion": "1.0.0",
  "run": {
    "startTime": "2024-12-30T11:48:15Z",
    "seed": 123456789,
    "variant": "B",
    "appVersion": "0.1.0",
    "tutorialVersion": "1.0.0"
  },
  "policy": {
    "tutorialVersion": "1.0.0",
    "variant": "B",
    "hintDelaySeconds": 6.0,
    "showArrow": false,
    "maxFailCount": 3,
    "assistEnabled": true
  },
  "policyData": {
    "tutorialVersion": "1.0.0",
    "variant": "B",
    "hintDelaySeconds": 6.0,
    "showArrow": false,
    "maxFailCount": 3,
    "assistEnabled": true
  },
  "summary": {
    "result": "CLEAR",
    "durationSeconds": 8.0,
    "endReason": "Enemy defeated",
    "stepCount": 3,
    "failCount": 2,
    "hintShownCount": 1,
    "assistTriggered": false,
    "damageTaken": 0
  },
  "events": [
    {
      "type": 0,
      "typeName": "RUN_START",
      "timestamp": 0.0,
      "data": {
        "appVersion": "0.1.0",
        "policyVariant": "B",
        "tutorialVersion": "1.0.0"
      }
    },
    {
      "type": 2,
      "typeName": "STEP_START",
      "timestamp": 1.0,
      "data": {
        "stepName": "WaitingForAction"
      }
    },
    {
      "type": 5,
      "typeName": "HINT_SHOWN",
      "timestamp": 7.0,
      "data": {
        "hintDelay": 6
      }
    },
    {
      "type": 1,
      "typeName": "RUN_END",
      "timestamp": 8.0,
      "data": {
        "result": "CLEAR",
        "endReason": "Enemy defeated",
        "durationSeconds": 8.0,
        "failCount": 2,
        "damageTaken": 0
      }
    }
  ]
}
```

**분석:**
- `hintDelaySeconds: 6.0` → 느린 힌트로 인해 초반 자율 학습 시간이 길었음
- `showArrow: false` → 화살표 가이드 없음
- `assistEnabled: true` → 실패 시 도움말 제공 옵션 (이번 플레이는 사용 안 함)
- `variant: "B"` → A/B 테스트 그룹 B에 속함

### 로그 비교의 의미

| 항목 | Policy A | Policy B | 차이점 |
|------|----------|----------|--------|
| `variant` | "A" | "B" | A/B 테스트 그룹 구분 |
| `hintDelaySeconds` (정책) | 2.0초 | 6.0초 | 힌트 타이밍 정책 차이 |
| `showArrow` (정책) | true | false | 화살표 가이드 정책 차이 |
| `assistEnabled` (정책) | false | true | 도움말 제공 정책 차이 |
| `failCount` | 2 | 2 | 동일 (이번 플레이) |
| `durationSeconds` | 6.0초 | 8.0초 | Policy B가 더 오래 걸림 (힌트 지연) |

**운영 관점:**
- 같은 플레이 결과지만, `variant` 필드로 어떤 정책으로 플레이했는지 추적 가능
- 대량의 로그를 수집하면 `hintDelaySeconds`와 `durationSeconds`의 상관관계 분석 가능
- `assistEnabled`와 `failCount`의 상관관계로 도움말 효과 측정 가능
- `showArrow`와 `clearTime`의 상관관계로 화살표 가이드 효과 측정 가능

**핵심:** 코드는 동일하지만, 정책 JSON만 바꿔서 다른 `variant` 값과 정책 설정이 로그에 기록됩니다. 이것이 정책 기반 운영의 핵심입니다.


## UI 시스템 및 런타임 정책 교체

### TutorialUIController

정책 변화가 화면에서 보이게 하는 최소 UI 구현:

- **정보 표시 텍스트**: Current State / FailCount / Policy Variant / HintDelay
- **화살표 GameObject**: `showArrow` 정책에 따라 On/Off
- **정책 교체 버튼**: Apply Policy A, Apply Policy B (런타임 정책 교체)

### 런타임 정책 교체 데모

**"클라 재배포 없이 조정"이 가능합니다.**

Unity 에디터에서:
1. **Apply Policy A 버튼 클릭**
   - Policy A 적용 (`showArrow: true`, `hintDelay: 2초`)
   - 화살표 표시됨
   - UI 텍스트 업데이트: "Policy Variant: A", "HintDelay: 2.0s"

2. **Apply Policy B 버튼 클릭**
   - Policy B 적용 (`showArrow: false`, `hintDelay: 6초`)
   - 화살표 사라짐
   - UI 텍스트 업데이트: "Policy Variant: B", "HintDelay: 6.0s"

**구현 위치:**
- `Assets/Client/Tutorial/TutorialUIController.cs`


## 프로젝트 구조

### 핵심 파일 목록

```
Assets/Client/Tutorial/
├── TutorialPolicy.cs          # 정책 JSON 스키마 정의
├── PolicyApplier.cs            # 정책 해석기 (정책 변경 시 UI 업데이트 포함)
├── PolicyExamples.cs           # 정책 예시 (Policy A/B)
├── TutorialState.cs            # 튜토리얼 상태 enum
├── TutorialController.cs       # 튜토리얼 중앙 컨트롤러 (흐름 제어 + RunReport)
├── TutorialFlowDriver.cs       # 상태 전환 흐름 시연 드라이버
├── ICombatEventSource.cs       # 전투 이벤트 인터페이스
├── CombatEventSource.cs        # 전투 이벤트 소스 (Player/Enemy 연결)
├── MockCombatEventSource.cs    # 가짜 전투 이벤트 소스 (테스트용)
├── Player.cs                   # 플레이어 컴포넌트
├── Enemy.cs                    # 에너미 컴포넌트
├── RunReport.cs                # RunReport (상세 이벤트 추적)
├── RunMetadata.cs              # 런 메타데이터
├── RunSummary.cs               # 런 요약 정보
├── TutorialEvent.cs            # 튜토리얼 이벤트 타입 및 클래스
└── TutorialUIController.cs     # 튜토리얼 UI 컨트롤러
```

### 검증 완료 항목

- ✅ AutoSimulate: 상태 전환 흐름 정상 작동
- ✅ Policy A/B 버튼: 런타임 정책 교체 정상 작동
- ✅ UI 업데이트: 정책 변경 시 화살표 On/Off 정상 작동
- ✅ RunReport 생성: 상세 이벤트 로깅 및 JSON 저장 정상 작동
- ✅ 정책 스냅샷: `policy`와 `policyData` 정상 저장
- ✅ 전투 이벤트 연결: OnPlayerDamaged → OnFailure(), OnEnemyDefeated → OnSuccess(), OnPlayerDefeated → HandlePlayerDefeated()
- ✅ 전투 결과 저장: 플레이어 사망/에너미 처치 시 자동으로 RunReport 생성 및 저장
- ✅ 로그 파일 저장: `Application.persistentDataPath/Reports/` 경로에 정상 저장


## 사용 방법

### Unity 에디터에서 테스트

1. **씬 설정**
   - Unity 씬 열기
   - Canvas 생성 및 UI 요소 배치:
     - TextMeshPro 텍스트 (정보 표시용)
     - 화살표 GameObject (Image 또는 TextMeshPro)
     - Button 2개 (Apply Policy A, Apply Policy B)

2. **컴포넌트 연결**
   - GameObject에 `TutorialController`, `PolicyApplier`, `TutorialUIController` 추가
   - `TutorialUIController` Inspector에서 모든 필드 연결
   - `CombatEventSource` 또는 `MockCombatEventSource` 추가 (전투 이벤트 발생용)
   - `Player`와 `Enemy` GameObject에 각각 컴포넌트 추가

3. **테스트 방법**
   - **AutoSimulate**: `TutorialFlowDriver`의 `autoSimulate` 체크 → Play
   - **정책 교체**: Play 모드에서 Policy A/B 버튼 클릭
   - **로그 확인**: 
     - Console 창에서 `[RunReport]` 로그 확인
     - `Application.persistentDataPath/Reports/` 폴더에서 JSON 파일 확인

### 로그 파일 확인

- **Windows**: `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Reports\`
- **Mac**: `~/Library/Application Support/<CompanyName>/<ProductName>/Reports/`
- **Linux**: `~/.config/unity3d/<CompanyName>/<ProductName>/Reports/`

파일명 예시: `run_20241230-114721_A_seed378692552_CLEAR.json`


## 기술적 세부사항

### JSON 직렬화

Unity의 `JsonUtility`는 `object` 타입을 직접 직렬화하지 않습니다. 따라서 `RunReport`는 커스텀 JSON 직렬화를 구현했습니다:

- **`ToJsonWithDataAsObject()`**: `data` 필드를 객체로 변환하여 JSON에 삽입
- **`ConvertPolicyDataToObject()`**: `policyData` 문자열을 JSON 객체로 변환
- **`SerializeDataObject()`**: 리플렉션을 사용하여 `object` 타입을 JSON으로 직렬화

### 정책 초기화 및 RunReport 생성

1. `PolicyApplier.Start()` → `InitializePolicy()` 호출하여 기본 정책 로드
2. 정책 버튼 클릭 시 (`TutorialUIController`):
   - `PolicyApplier.ApplyPolicyFromServer()` → 정책 적용
   - `TutorialController.StartNewRunWithPolicy()` → 새 RunReport 생성
3. 전투 결과 발생 시 (플레이어 사망/에너미 처치):
   - RunReport가 없으면 현재 정책으로 자동 생성
   - 전투 결과에 따라 `FAIL` 또는 `CLEAR`로 저장

이 구조를 통해 정책이 변경될 때마다 새로운 RunReport가 생성되어, 각 정책 variant별로 로그가 분리되어 저장됩니다.
