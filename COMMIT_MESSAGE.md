# Git Commit Message

## 커밋 제목
```
feat: 전투 튜토리얼 정책 기반 운영 시스템 구현 완료
```

## 커밋 본문

### 프로젝트 개요
라이브 게임 환경에서 전투 튜토리얼 UX를 서버 정책(JSON)으로 제어·개선할 수 있는 Unity 기반 포트폴리오 프로젝트

### 구현된 핵심 기능

#### 1. 정책 기반 구조 (Policy-Driven Architecture)
- **TutorialPolicy.cs**: 서버-클라이언트 간 정책 데이터 계약 정의
  - tutorialVersion, variant, hintDelaySeconds, showArrow, maxFailCount, assistEnabled
  - JSON 직렬화/역직렬화 지원
- **PolicyApplier.cs**: 정책 해석기 (Policy Interpreter)
  - 서버 JSON → TutorialPolicy 변환
  - 정책 값 해석 및 제공
  - 정책 변경 시 UI 업데이트 (힌트 타이머 재설정, UI 토글 갱신)
- **PolicyExamples.cs**: 정책 예시 (Policy A/B)
  - Policy A: 빠른 힌트(2초), 화살표 표시, 도움말 비활성화
  - Policy B: 느린 힌트(6초), 화살표 숨김, 도움말 활성화

#### 2. 튜토리얼 상태 머신 (State Machine)
- **TutorialState.cs**: 튜토리얼 상태 enum 정의
  - Init, WaitingForAction, Hint, Retry, Assist, Clear
  - "실패 → UX 변화" 구조 명확화
- **TutorialController.cs**: 튜토리얼 중앙 컨트롤러
  - 현재 TutorialState 관리
  - FailCount 누적 및 실패 처리
  - Policy 값 참조 (정책 해석은 PolicyApplier에게 위임)
  - 전투 이벤트 구독 및 상태 전환
- **TutorialFlowDriver.cs**: 상태 전환 흐름 시연 드라이버
  - 자동 시뮬레이션 기능
  - 상태 전환 흐름 시각화

#### 3. 전투 시스템 분리 (Loose Coupling)
- **ICombatEventSource.cs**: 전투 이벤트 인터페이스
  - OnPlayerHit, OnPlayerDamaged, OnEnemyDefeated
  - 튜토리얼이 전투 구현에 의존하지 않음을 증명
- **MockCombatEventSource.cs**: 가짜 전투 이벤트 소스
  - 테스트/시연용
  - 실제 전투 구현 없이 튜토리얼 흐름 제어 가능

#### 4. 로그 시스템 (Logging Schema)
- **CombatTutorialLog.cs**: 전투 튜토리얼 로그 DTO
  - tutorialVersion, variant, failCount, clearTime, damageTaken
  - "수집 → 판단 → 정책 변경" 피드백 루프 지원
- **TutorialController**: 로그 생성 및 JSON 직렬화
  - 튜토리얼 시작 시간 기록
  - Clear 상태 진입 시 clearTime 계산
  - OnPlayerDamaged 발생 시 damageTaken 누적

#### 5. UI 시스템 (Minimal UI)
- **TutorialUIController.cs**: 튜토리얼 UI 컨트롤러
  - 정보 표시 텍스트 (State, FailCount, Policy Variant, HintDelay)
  - 화살표 GameObject (showArrow 정책에 따라 On/Off)
  - 정책 교체 버튼 (Apply Policy A/B)
  - 런타임 정책 교체 데모

#### 6. 문서화 (Documentation)
- **README.md**: 완전한 프로젝트 문서화
  - 프로젝트 개요 및 핵심 가치
  - 정책 JSON 스키마 상세 설명
  - 튜토리얼 상태 머신 및 흐름 다이어그램
  - 정책 변경에 따른 실제 로그 비교 예시
  - "Intentionally Not Implemented" 섹션
  - 전체 흐름 다이어그램 (ASCII)

### 설계 원칙

1. **책임 분리 (Separation of Concerns)**
   - PolicyApplier: 정책 해석만 담당
   - TutorialController: 흐름 제어만 담당
   - TutorialUIController: UI 업데이트만 담당

2. **느슨한 결합 (Loose Coupling)**
   - ICombatEventSource 인터페이스로 전투 시스템과 분리
   - 이벤트 기반 통신

3. **데이터 기반 운영 (Data-Driven)**
   - 코드 변경 없이 JSON만으로 UX 조정 가능
   - A/B 테스트를 위한 variant 필드

4. **"실패 → UX 변화" 구조 명확화**
   - 실패 누적에 따른 상태 전환 흐름이 코드와 문서에서 명확히 드러남

### 검증 완료

- ✅ AutoSimulate: 상태 전환 흐름 정상 작동
- ✅ Policy A/B 버튼: 런타임 정책 교체 정상 작동
- ✅ UI 업데이트: 정책 변경 시 화살표 On/Off 정상 작동
- ✅ 로그 생성: TutorialLog JSON 출력 정상 작동

### 파일 목록

**핵심 스크립트:**
- Assets/Client/Tutorial/TutorialPolicy.cs
- Assets/Client/Tutorial/PolicyApplier.cs
- Assets/Client/Tutorial/TutorialState.cs
- Assets/Client/Tutorial/TutorialController.cs
- Assets/Client/Tutorial/TutorialFlowDriver.cs
- Assets/Client/Tutorial/ICombatEventSource.cs
- Assets/Client/Tutorial/MockCombatEventSource.cs
- Assets/Client/Tutorial/CombatTutorialLog.cs
- Assets/Client/Tutorial/TutorialUIController.cs
- Assets/Client/Tutorial/PolicyExamples.cs

**문서:**
- README.md

### 포트폴리오 가치

이 프로젝트는 다음을 코드로 증명합니다:
- 서버 정책(JSON) 변경만으로 전투 튜토리얼 UX가 달라지는 구조
- 실패 누적에 따라 상태가 변화하는 튜토리얼 상태 머신
- 전투 구현과 완전히 분리된 튜토리얼 흐름 제어
- 운영 관점에서 의미 있는 전투 튜토리얼 로그 스키마

---

## 간단 버전 (짧은 커밋 메시지)

```
feat: 전투 튜토리얼 정책 기반 운영 시스템 구현

- 정책 JSON 스키마 및 PolicyApplier 구현
- TutorialState 상태 머신 및 TutorialController 구현
- 전투 이벤트 인터페이스로 느슨한 결합 설계
- 로그 스키마 및 생성 로직 구현
- 최소 UI 및 런타임 정책 교체 데모
- 완전한 README 문서화

코드 변경 없이 JSON만으로 튜토리얼 UX 조정 가능한 구조 완성
```

