using UnityEngine;
using System;

/// <summary>
/// 튜토리얼 중앙 컨트롤러
/// 
/// 책임:
/// - 현재 TutorialState 관리
/// - FailCount 누적
/// - Policy 값 참조
/// - 상태 전환 트리거 제공
/// 
/// 책임이 아님:
/// - 전투 구현 (X)
/// - UI 구현 (X)
/// - "흐름 제어"만 담당
/// </summary>
public class TutorialController : MonoBehaviour
{
    [Header("의존성")]
    [SerializeField] private PolicyApplier policyApplier;
    
    [Header("전투 이벤트 소스 (자동으로 찾거나 수동으로 연결)")]
    [Tooltip("CombatEventSource 또는 MockCombatEventSource 컴포넌트 참조. 비어있으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private MonoBehaviour combatEventSourceMonoBehaviour;
    
    private ICombatEventSource combatEventSource;

    [Header("현재 상태")]
    [SerializeField] private TutorialState currentState = TutorialState.Init;
    
    [Header("실패 카운트")]
    [SerializeField] private int failCount = 0;

    // 로그 수집
    private float tutorialStartTime;
    private int damageTaken = 0;

    // RunReport (메모리 버퍼)
    private RunReport runReport;

    // 이벤트: 상태 변경 시 외부에 알림
    public event Action<TutorialState> OnStateChanged;

    private void Awake()
    {
        Debug.Log("[TutorialController] ===== Awake() 호출됨 =====");
        Debug.Log($"[TutorialController] GameObject 이름: {gameObject.name}");
        Debug.Log($"[TutorialController] GameObject 활성화 여부: {gameObject.activeSelf}");
        Debug.Log($"[TutorialController] 컴포넌트 활성화 여부: {enabled}");
    }

    private void Start()
    {
        Debug.Log("[TutorialController] ===== Start() 호출됨 =====");
        
        if (policyApplier == null)
        {
            policyApplier = GetComponent<PolicyApplier>();
            if (policyApplier == null)
            {
                Debug.LogError("[TutorialController] PolicyApplier가 필요합니다!");
            }
        }

        // 전투 이벤트 소스 연결 및 구독
        TryConnectCombatEventSource();
        SubscribeToCombatEvents();

        // 튜토리얼 시작 시간 기록
        tutorialStartTime = Time.time;

        // 초기 상태로 전환
        ChangeState(TutorialState.Init);
    }

    /// <summary>
    /// CombatEventSource 연결 시도
    /// </summary>
    private void TryConnectCombatEventSource()
    {
        if (combatEventSource != null)
        {
            Debug.Log($"[TutorialController] CombatEventSource가 이미 연결되어 있습니다: {combatEventSource.GetType().Name}");
            return;
        }

        Debug.Log("[TutorialController] ===== CombatEventSource를 찾는 중... =====");
        
        // 먼저 Inspector에서 할당된 MonoBehaviour를 ICombatEventSource로 캐스팅
        if (combatEventSourceMonoBehaviour != null)
        {
            Debug.Log($"[TutorialController] Inspector에 할당된 MonoBehaviour 확인: {combatEventSourceMonoBehaviour.GetType().Name} ({combatEventSourceMonoBehaviour.gameObject.name})");
            combatEventSource = combatEventSourceMonoBehaviour as ICombatEventSource;
            if (combatEventSource != null)
            {
                Debug.Log($"[TutorialController] Inspector에서 할당된 CombatEventSource를 사용합니다: {combatEventSourceMonoBehaviour.gameObject.name}");
                return;
            }
            else
            {
                Debug.LogError($"[TutorialController] ⚠️ 할당된 MonoBehaviour가 ICombatEventSource를 구현하지 않습니다!");
                Debug.LogError($"[TutorialController] 타입: {combatEventSourceMonoBehaviour.GetType().Name}");
                Debug.LogError($"[TutorialController] GameObject: {combatEventSourceMonoBehaviour.gameObject.name}");
                Debug.LogError($"[TutorialController] Unity Inspector에서 'Combat Event Source Mono Behaviour' 필드를 비우거나 올바른 CombatEventSource를 할당하세요!");
            }
        }
        
        // 없으면 같은 GameObject에서 찾기
        combatEventSource = GetComponent<ICombatEventSource>();
        if (combatEventSource != null)
        {
            Debug.Log("[TutorialController] 같은 GameObject에서 CombatEventSource를 찾았습니다.");
            return;
        }
        
        // 없으면 씬에서 CombatEventSource 찾기
        CombatEventSource foundSource = FindObjectOfType<CombatEventSource>();
        if (foundSource != null)
        {
            combatEventSource = foundSource;
            Debug.Log($"[TutorialController] 씬에서 CombatEventSource를 찾았습니다: {foundSource.gameObject.name}");
            return;
        }
        
        // 그래도 없으면 MockCombatEventSource 찾기
        MockCombatEventSource mockSource = FindObjectOfType<MockCombatEventSource>();
        if (mockSource != null)
        {
            combatEventSource = mockSource;
            Debug.Log($"[TutorialController] 씬에서 MockCombatEventSource를 찾았습니다: {mockSource.gameObject.name}");
            return;
        }
        
        Debug.LogError("[TutorialController] CombatEventSource를 찾을 수 없습니다! 씬에 CombatEventSource 또는 MockCombatEventSource가 있는지 확인하세요.");
    }

    /// <summary>
    /// 전투 이벤트 구독
    /// </summary>
    private void SubscribeToCombatEvents()
    {
        if (combatEventSource == null)
        {
            Debug.LogError("[TutorialController] combatEventSource가 null이어서 이벤트를 구독할 수 없습니다!");
            Debug.LogError("[TutorialController] TryConnectCombatEventSource()가 제대로 실행되었는지 확인하세요.");
            return;
        }

        Debug.Log($"[TutorialController] 전투 이벤트 구독 시작... (combatEventSource 타입: {combatEventSource.GetType().Name})");
        
        // 기존 구독 해제 (중복 방지)
        try
        {
            combatEventSource.OnPlayerDamaged -= HandlePlayerDamaged;
            combatEventSource.OnEnemyDefeated -= HandleEnemyDefeated;
            combatEventSource.OnPlayerDefeated -= HandlePlayerDefeated;
            Debug.Log("[TutorialController] 기존 구독 해제 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TutorialController] 기존 구독 해제 중 예외 발생 (무시 가능): {e.Message}");
        }
        
        // 새로 구독
        try
        {
            combatEventSource.OnPlayerDamaged += HandlePlayerDamaged;
            Debug.Log("[TutorialController] OnPlayerDamaged 구독 완료");
            
            combatEventSource.OnEnemyDefeated += HandleEnemyDefeated;
            Debug.Log("[TutorialController] OnEnemyDefeated 구독 완료");
            
            combatEventSource.OnPlayerDefeated += HandlePlayerDefeated;
            Debug.Log("[TutorialController] OnPlayerDefeated 구독 완료");
            
            Debug.Log("[TutorialController] ===== 전투 이벤트 구독 완료 =====");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TutorialController] 이벤트 구독 중 예외 발생: {e.Message}");
            Debug.LogError($"[TutorialController] 스택 트레이스: {e.StackTrace}");
        }
    }

    private void OnDestroy()
    {
        // 전투 이벤트 구독 해제
        if (combatEventSource != null)
        {
            combatEventSource.OnPlayerDamaged -= HandlePlayerDamaged;
            combatEventSource.OnEnemyDefeated -= HandleEnemyDefeated;
            combatEventSource.OnPlayerDefeated -= HandlePlayerDefeated;
            Debug.Log("[TutorialController] 전투 이벤트 구독 해제 완료");
        }
    }

    /// <summary>
    /// 플레이어 피해 이벤트 처리 → OnFailure()
    /// </summary>
    private void HandlePlayerDamaged()
    {
        damageTaken++;
        OnFailure();
    }

    /// <summary>
    /// RunReport가 없으면 현재 정책으로 자동 생성
    /// </summary>
    private void EnsureRunReportExists(string autoCreateReason)
    {
        if (runReport != null) return;

        Debug.LogWarning("[TutorialController] runReport가 없어서 현재 정책으로 자동 생성합니다.");
        
        TutorialPolicy policy = null;
        string policyJson = null;
        
        // PolicyApplier에서 현재 정책 가져오기
        if (policyApplier != null)
        {
            policy = policyApplier.GetCurrentPolicy();
            policyJson = policyApplier.GetCurrentPolicyJson();
        }
        
        // 정책이 없으면 기본 정책 사용
        if (policy == null)
        {
            policy = TutorialPolicy.GetDefault();
            policyJson = policy.ToJson();
            Debug.LogWarning("[TutorialController] PolicyApplier가 없거나 정책이 없어서 기본 정책을 사용합니다.");
        }
        
        // RunReport 생성
        int seed = UnityEngine.Random.Range(0, int.MaxValue);
        runReport = new RunReport(policy, policyJson, seed);
        
        // RUN_START 이벤트 추가 (늦은 시작)
        runReport.AddEvent(TutorialEventType.RUN_START, new
        {
            appVersion = Application.version,
            policyVariant = policy.variant,
            tutorialVersion = policy.tutorialVersion,
            reason = autoCreateReason
        });
        
        Debug.Log($"[TutorialController] RunReport 자동 생성 완료 (variant: {policy.variant})");
    }

    /// <summary>
    /// 적 처치 이벤트 처리 → OnSuccess()
    /// </summary>
    private void HandleEnemyDefeated()
    {
        Debug.Log("[TutorialController] ===== HandleEnemyDefeated 호출됨 =====");
        Debug.Log($"[TutorialController] runReport null 여부: {runReport == null}");
        
        EnsureRunReportExists("Auto-created on enemy defeat");
        OnSuccess();
    }

    /// <summary>
    /// 플레이어 사망 이벤트 처리
    /// </summary>
    private void HandlePlayerDefeated()
    {
        Debug.Log("[TutorialController] ===== HandlePlayerDefeated 호출됨 =====");
        Debug.Log($"[TutorialController] runReport null 여부: {runReport == null}");
        Debug.Log($"[TutorialController] combatEventSource null 여부: {combatEventSource == null}");

        EnsureRunReportExists("Auto-created on player defeat");

        Debug.Log("[TutorialController] Player defeated - RunReport 저장 시작");

        // 실패 이벤트 로깅
        damageTaken++; // 플레이어 사망도 데미지로 간주
        failCount++;
        var failData = new { failCount = failCount, state = currentState.ToString(), reason = "Player defeated" };
        runReport.AddEvent(TutorialEventType.FAIL, failData);
        runReport.summary.failCount = failCount;

        // 튜토리얼 종료 상태로 변경
        ChangeState(TutorialState.Clear);
        
        // 로그 저장
        FinalizeRunReport("FAIL", "Player defeated");
        Debug.Log("[TutorialController] ===== HandlePlayerDefeated 완료 =====");
    }

    /// <summary>
    /// 현재 튜토리얼 상태 반환
    /// </summary>
    public TutorialState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// 현재 실패 횟수 반환
    /// </summary>
    public int GetFailCount()
    {
        return failCount;
    }

    /// <summary>
    /// 상태 변경
    /// </summary>
    public void ChangeState(TutorialState newState)
    {
        if (currentState == newState) return;

        // B. 단계 시작/클리어: STEP_START, STEP_CLEAR 이벤트 추가
        if (runReport != null)
        {
            // 이전 상태가 Init이 아니면 STEP_CLEAR
            if (currentState != TutorialState.Init)
            {
                runReport.AddEvent(TutorialEventType.STEP_CLEAR, new { previousState = currentState.ToString() });
            }

            // 새 상태로 STEP_START
            if (newState != TutorialState.Init && newState != TutorialState.Clear)
            {
                runReport.AddEvent(TutorialEventType.STEP_START, new { stepName = newState.ToString() });
                runReport.summary.stepCount++;
            }
        }

        currentState = newState;
        OnStateChanged?.Invoke(currentState);
        Debug.Log($"TutorialState changed to: {currentState}");
    }

    /// <summary>
    /// 사용자 행동 대기 상태로 전환
    /// </summary>
    public void StartWaitingForAction()
    {
        ChangeState(TutorialState.WaitingForAction);
    }

    /// <summary>
    /// 힌트 상태로 전환 (hintDelaySeconds 후 자동 호출 예정)
    /// </summary>
    public void ShowHint()
    {
        // C. 힌트 표시: HINT_SHOWN 이벤트 + summary.hintShownCount++
        if (runReport != null)
        {
            float hintDelay = policyApplier?.GetHintDelaySeconds() ?? 3.0f;
            runReport.AddEvent(TutorialEventType.HINT_SHOWN, new { hintDelay = hintDelay });
            runReport.summary.hintShownCount++;
        }

        ChangeState(TutorialState.Hint);
    }

    /// <summary>
    /// 실패 처리 (FailCount 증가 및 상태 전환)
    /// 
    /// PolicyApplier에게 질문만 함 (정책 해석은 PolicyApplier가 담당)
    /// </summary>
    public void OnFailure()
    {
        failCount++;
        Debug.Log($"Failure occurred. FailCount: {failCount}");

        // C. 실패: FAIL 이벤트 + summary.failCount++
        if (runReport != null)
        {
            // data를 object로 전달 (더 유연함)
            var failData = new { failCount = failCount, state = currentState.ToString() };
            runReport.AddEvent(TutorialEventType.FAIL, failData);
            runReport.summary.failCount = failCount;
        }

        if (policyApplier == null)
        {
            Debug.LogWarning("PolicyApplier가 없어 기본값으로 처리합니다.");
            ChangeState(TutorialState.Retry);
            return;
        }

        // PolicyApplier에게 질문: Assist 상태로 전환해야 하는가?
        if (policyApplier.ShouldTransitionToAssist(failCount))
        {
            // C. 어시스트 발동: summary.assistTriggered = true
            if (runReport != null)
            {
                runReport.summary.assistTriggered = true;
                runReport.AddEvent(TutorialEventType.ASSIST_TRIGGERED, new { failCount = failCount, maxFailCount = policyApplier.GetMaxFailCount() });
            }
            ChangeState(TutorialState.Assist);
        }
        else
        {
            // 재시도 허용
            ChangeState(TutorialState.Retry);
        }
    }

    /// <summary>
    /// 성공 처리
    /// </summary>
    public void OnSuccess()
    {
        Debug.Log("[TutorialController] ===== OnSuccess 호출됨 =====");
        Debug.Log($"[TutorialController] runReport null 여부: {runReport == null}");
        
        failCount = 0; // 성공 시 실패 카운트 리셋
        ChangeState(TutorialState.Clear);
        
        // D. 튜토리얼 종료 시점: summary 최종화, RUN_END, SaveToFile
        FinalizeRunReport("CLEAR", "Enemy defeated");
        Debug.Log("[TutorialController] ===== OnSuccess 완료 =====");
    }

    /// <summary>
    /// 정책 값 참조: 힌트 지연 시간
    /// </summary>
    public float GetHintDelaySeconds()
    {
        return policyApplier?.GetHintDelaySeconds() ?? 3.0f;
    }

    /// <summary>
    /// 정책 값 참조: 최대 실패 허용 횟수
    /// </summary>
    public int GetMaxFailCount()
    {
        return policyApplier?.GetMaxFailCount() ?? 3;
    }

    /// <summary>
    /// 정책 값 참조: 도움말 활성화 여부
    /// </summary>
    public bool IsAssistEnabled()
    {
        return policyApplier?.IsAssistEnabled() ?? false;
    }


    /// <summary>
    /// D. 튜토리얼 종료 시점: summary 최종화, RUN_END, SaveToFile
    /// </summary>
    private void FinalizeRunReport(string result, string endReason)
    {
        if (runReport == null) return;

        // summary 최종화
        runReport.summary.damageTaken = damageTaken;
        runReport.FinalizeSummary(result, endReason);

        // RUN_END 이벤트 추가 (summary.failCount 사용 - 리셋 전 값)
        runReport.AddEvent(TutorialEventType.RUN_END, new { 
            result = result, 
            endReason = endReason,
            durationSeconds = Time.time - tutorialStartTime,
            failCount = runReport.summary.failCount, // summary에 저장된 실제 failCount 사용
            damageTaken = damageTaken
        });

        // 파일 저장
        string reportsDirectory = System.IO.Path.Combine(Application.persistentDataPath, "Reports");
        runReport.SaveToFile(reportsDirectory);
    }

    /// <summary>
    /// 정책 변경 시 새 Run 시작
    /// </summary>
    public void StartNewRunWithPolicy(TutorialPolicy policy, string policyJson)
    {
        if (policy == null)
        {
            Debug.LogError("[TutorialController] 정책이 null입니다!");
            return;
        }

        // 기존 RunReport가 있으면 버림
        if (runReport != null)
        {
            runReport = null;
        }

        // 전투 상태 리셋
        failCount = 0;
        damageTaken = 0;
        tutorialStartTime = Time.time;
        currentState = TutorialState.Init;

        // 새 RunReport 생성
        int seed = UnityEngine.Random.Range(0, int.MaxValue);
        runReport = new RunReport(policy, policyJson, seed);
        
        // RUN_START 이벤트 추가
        runReport.AddEvent(TutorialEventType.RUN_START, new
        {
            appVersion = Application.version,
            policyVariant = policy.variant,
            tutorialVersion = policy.tutorialVersion,
            reason = "Policy button clicked"
        });

        // 버튼 클릭 시점에 즉시 파일 저장
        string reportsDirectory = System.IO.Path.Combine(Application.persistentDataPath, "Reports");
        runReport.summary.damageTaken = 0;
        runReport.FinalizeSummary("START", "Policy button clicked");
        runReport.AddEvent(TutorialEventType.RUN_END, new
        {
            result = "START",
            endReason = "Policy button clicked",
            durationSeconds = 0,
            failCount = 0,
            damageTaken = 0
        });
        runReport.SaveToFile(reportsDirectory);
    }
}
