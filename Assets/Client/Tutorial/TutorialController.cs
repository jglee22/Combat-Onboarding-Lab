using UnityEngine;
using System;
using System.Collections;

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
    
    // 힌트 타이머 관련
    private float currentHintDelay = 0f; // 현재 사용 중인 힌트 지연 시간
    private float hintTimerStartTime = 0f; // 힌트 타이머 시작 시간

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
                policyApplier = FindObjectOfType<PolicyApplier>();
            }
        }

        // 전투 이벤트 소스 연결 및 구독
        TryConnectCombatEventSource();
        SubscribeToCombatEvents();

        // 튜토리얼 시작 시간 기록
        tutorialStartTime = Time.time;

        // RunReport 생성 (PolicyApplier 초기화를 기다린 후 생성)
        StartCoroutine(InitializeRunReportCoroutine());

        // 초기 상태로 전환
        ChangeState(TutorialState.Init);
        
        // Init → WaitingForAction 전환은 InitializeRunReportCoroutine() 내에서 처리됨
    }

    /// <summary>
    /// RunReport 초기화 코루틴 (PolicyApplier 초기화 대기)
    /// </summary>
    private IEnumerator InitializeRunReportCoroutine()
    {
        // PolicyApplier가 초기화될 때까지 최대 10프레임 대기
        int maxWaitFrames = 10;
        int waitFrame = 0;
        
        while (waitFrame < maxWaitFrames)
        {
            if (policyApplier == null)
            {
                policyApplier = FindObjectOfType<PolicyApplier>();
            }
            
            if (policyApplier != null)
            {
                // PolicyApplier가 정책을 로드했는지 확인
                TutorialPolicy policy = policyApplier.GetCurrentPolicy();
                if (policy != null)
                {
                    string policyJson = policyApplier.GetCurrentPolicyJson();
                    // policyJson이 비어있어도 policy가 있으면 생성 가능 (policy.ToJson()으로 생성)
                    // 정책이 준비되었으므로 RunReport 생성
                    CreateRunReport(policy, policyJson);
                    
                    // RunReport 생성 후 Init → WaitingForAction 전환
                    if (currentState == TutorialState.Init)
                    {
                        yield return null; // 한 프레임 대기하여 RunReport 생성 완료 보장
                        StartWaitingForAction();
                    }
                    
                    yield break;
                }
            }
            
            waitFrame++;
            yield return null; // 다음 프레임까지 대기
        }
        
        // 대기 시간 초과 시 기본 정책으로 생성
        Debug.LogWarning("[TutorialController] PolicyApplier 초기화 대기 시간 초과. 기본 정책을 사용합니다.");
        TutorialPolicy defaultPolicy = TutorialPolicy.GetDefault();
        CreateRunReport(defaultPolicy, defaultPolicy.ToJson());
        
        // RunReport 생성 후 Init → WaitingForAction 전환
        if (currentState == TutorialState.Init)
        {
            yield return null; // 한 프레임 대기하여 RunReport 생성 완료 보장
            StartWaitingForAction();
        }
    }

    /// <summary>
    /// RunReport 생성
    /// </summary>
    private void CreateRunReport(TutorialPolicy policy, string policyJson)
    {
        if (runReport != null) return;

        // 정책이 없거나 필드가 모두 비어있으면 기본 정책 사용
        if (policy == null || 
            (string.IsNullOrEmpty(policy.variant) && 
             string.IsNullOrEmpty(policy.tutorialVersion) && 
             policy.hintDelaySeconds == 0 && 
             policy.maxFailCount == 0))
        {
            Debug.LogWarning("[TutorialController] CreateRunReport: policy가 null이거나 비어있어 기본 정책을 사용합니다.");
            policy = TutorialPolicy.GetDefault();
            policyJson = null; // 기본 정책으로 재생성
        }
        
        // variant와 tutorialVersion이 비어있으면 기본값 보장
        if (string.IsNullOrEmpty(policy.variant))
        {
            Debug.LogWarning("[TutorialController] CreateRunReport: policy.variant가 비어있어 'A'로 설정합니다.");
            policy.variant = "A";
        }
        if (string.IsNullOrEmpty(policy.tutorialVersion))
        {
            Debug.LogWarning("[TutorialController] CreateRunReport: policy.tutorialVersion이 비어있어 '1.0.0'으로 설정합니다.");
            policy.tutorialVersion = "1.0.0";
        }
        
        // hintDelaySeconds가 0이면 기본값 설정
        if (policy.hintDelaySeconds == 0)
        {
            Debug.LogWarning("[TutorialController] CreateRunReport: policy.hintDelaySeconds가 0이어서 3.0으로 설정합니다.");
            policy.hintDelaySeconds = 3.0f;
        }
        
        // maxFailCount가 0이면 기본값 설정
        if (policy.maxFailCount == 0)
        {
            Debug.LogWarning("[TutorialController] CreateRunReport: policy.maxFailCount가 0이어서 3으로 설정합니다.");
            policy.maxFailCount = 3;
        }
        
        // policyJson이 비어있으면 policy를 JSON으로 생성
        // 정책 필드가 수정되었어도 원본 policyJson은 보존 (원본 정책 데이터 유지)
        if (string.IsNullOrEmpty(policyJson))
        {
            Debug.LogWarning("[TutorialController] CreateRunReport: policyJson이 비어있어 policy.ToJson()을 사용합니다.");
            policyJson = policy.ToJson();
        }
        // policyJson이 있으면 원본 그대로 사용 (정책 객체는 수정되었지만 원본 JSON 보존)
        else
        {
            Debug.Log("[TutorialController] CreateRunReport: 원본 policyJson을 사용합니다 (정책 객체는 수정되었지만 원본 JSON 보존).");
        }
        
        // policyJson이 여전히 비어있으면 에러
        if (string.IsNullOrEmpty(policyJson))
        {
            Debug.LogError("[TutorialController] CreateRunReport: policyJson이 여전히 비어있습니다! 기본 정책 JSON을 강제 생성합니다.");
            policy = TutorialPolicy.GetDefault();
            policyJson = policy.ToJson();
        }
        
        Debug.Log($"[TutorialController] CreateRunReport - 정책 정보:");
        Debug.Log($"[TutorialController] - variant: '{policy.variant}'");
        Debug.Log($"[TutorialController] - tutorialVersion: '{policy.tutorialVersion}'");
        Debug.Log($"[TutorialController] - hintDelaySeconds: {policy.hintDelaySeconds}");
        Debug.Log($"[TutorialController] - maxFailCount: {policy.maxFailCount}");
        Debug.Log($"[TutorialController] - policyJson 길이: {policyJson?.Length ?? 0}");
        Debug.Log($"[TutorialController] - policyJson 내용 (처음 200자): {policyJson?.Substring(0, Math.Min(200, policyJson.Length)) ?? "null"}");
        
        // RunReport 생성
        int seed = UnityEngine.Random.Range(0, int.MaxValue);
        runReport = new RunReport(policy, policyJson, seed);
        
        // RUN_START 이벤트 추가
        runReport.AddEvent(TutorialEventType.RUN_START, new
        {
            appVersion = Application.version,
            policyVariant = string.IsNullOrEmpty(policy.variant) ? "A" : policy.variant,
            tutorialVersion = string.IsNullOrEmpty(policy.tutorialVersion) ? "1.0.0" : policy.tutorialVersion,
            reason = "튜토리얼 시작"
        });
        
        Debug.Log($"[TutorialController] RunReport 초기화 완료");
        Debug.Log($"[TutorialController] - policyData 길이: {runReport.policyData?.Length ?? 0}");
        
        // policyData가 비어있으면 에러
        if (string.IsNullOrEmpty(runReport.policyData))
        {
            Debug.LogError("[TutorialController] ⚠️ RunReport 생성 후에도 policyData가 비어있습니다!");
        }
        else
        {
            Debug.Log($"[TutorialController] - policyData 내용 (처음 200자): {runReport.policyData.Substring(0, Math.Min(200, runReport.policyData.Length))}");
        }
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
        
        // PolicyApplier가 없으면 찾기
        if (policyApplier == null)
        {
            policyApplier = FindObjectOfType<PolicyApplier>();
        }
        
        // PolicyApplier에서 현재 정책 가져오기
        if (policyApplier != null)
        {
            policy = policyApplier.GetCurrentPolicy();
            policyJson = policyApplier.GetCurrentPolicyJson();
        }
        
        // policy가 null이거나 policyJson이 비어있으면 기본값 사용
        if (policy == null)
        {
            policy = TutorialPolicy.GetDefault();
        }
        
        // policyJson이 비어있으면 policy를 JSON으로 변환
        if (string.IsNullOrEmpty(policyJson))
        {
            policyJson = policy.ToJson();
        }
        
        // variant와 tutorialVersion이 비어있으면 기본값 보장
        if (string.IsNullOrEmpty(policy.variant))
        {
            policy.variant = "A";
        }
        if (string.IsNullOrEmpty(policy.tutorialVersion))
        {
            policy.tutorialVersion = "1.0.0";
        }
        
        // RunReport 생성
        int seed = UnityEngine.Random.Range(0, int.MaxValue);
        runReport = new RunReport(policy, policyJson, seed);
        
        // RUN_START 이벤트 추가 (늦은 시작, variant와 version 보장)
        runReport.AddEvent(TutorialEventType.RUN_START, new
        {
            appVersion = Application.version,
            policyVariant = string.IsNullOrEmpty(policy.variant) ? "A" : policy.variant,
            tutorialVersion = string.IsNullOrEmpty(policy.tutorialVersion) ? "1.0.0" : policy.tutorialVersion,
            reason = autoCreateReason
        });
        
        Debug.Log($"[TutorialController] RunReport 자동 생성 완료 (variant: {policy.variant}, policyJson 길이: {policyJson?.Length ?? 0})");
    }

    /// <summary>
    /// 적 처치 이벤트 처리 → OnSuccess()
    /// </summary>
    private void HandleEnemyDefeated()
    {
        Debug.Log("[TutorialController] ===== HandleEnemyDefeated 호출됨 =====");
        Debug.Log($"[TutorialController] runReport null 여부: {runReport == null}");
        
        EnsureRunReportExists("에너미 처치 시 자동 생성");
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

        EnsureRunReportExists("플레이어 사망 시 자동 생성");

        Debug.Log("[TutorialController] 플레이어 사망 - RunReport 저장 시작");

        // 실패 이벤트 로깅
        damageTaken++; // 플레이어 사망도 데미지로 간주
        failCount++;
        var failData = new { failCount = failCount, state = currentState.ToString(), reason = "플레이어 사망" };
        runReport.AddEvent(TutorialEventType.FAIL, failData);
        runReport.summary.failCount = failCount;

        // 튜토리얼 종료 상태로 변경
        ChangeState(TutorialState.Clear);
        
        // 로그 저장
        FinalizeRunReport("FAIL", "플레이어 사망");
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

        // runReport가 없으면 자동 생성 (늦은 시작)
        if (runReport == null)
        {
            EnsureRunReportExists("상태 변경 시 자동 생성");
        }

        // B. 단계 시작/클리어: STEP_START, STEP_CLEAR 이벤트 추가
        if (runReport != null)
        {
            // 이전 상태가 Init이 아니면 STEP_CLEAR
            if (currentState != TutorialState.Init)
            {
                runReport.AddEvent(TutorialEventType.STEP_CLEAR, new { previousState = currentState.ToString() });
            }

            // 새 상태로 STEP_START (Init과 Clear 제외하지 않고 모두 로깅)
            // 단, Clear는 최종 상태이므로 STEP_START 대신 RUN_END에서 처리
            if (newState != TutorialState.Clear)
            {
                runReport.AddEvent(TutorialEventType.STEP_START, new { stepName = newState.ToString() });
                // Init은 시작 상태이므로 stepCount에 포함하지 않음
                if (newState != TutorialState.Init)
                {
                    runReport.summary.stepCount++;
                }
            }
        }

        currentState = newState;
        OnStateChanged?.Invoke(currentState);
        Debug.Log($"튜토리얼 상태 변경: {currentState}");

        // WaitingForAction 상태로 전환 시 힌트 타이머 시작
        if (newState == TutorialState.WaitingForAction && policyApplier != null)
        {
            StartHintTimer();
        }
    }

    /// <summary>
    /// 사용자 행동 대기 상태로 전환
    /// </summary>
    public void StartWaitingForAction()
    {
        ChangeState(TutorialState.WaitingForAction);
    }

    /// <summary>
    /// 힌트 타이머 시작 (WaitingForAction 상태에서 자동 호출)
    /// </summary>
    private void StartHintTimer()
    {
        if (policyApplier == null) return;

        float hintDelay = policyApplier.GetHintDelaySeconds();
        currentHintDelay = hintDelay; // 실제 사용할 지연 시간 저장
        hintTimerStartTime = Time.time; // 타이머 시작 시간 기록
        Debug.Log($"[TutorialController] 힌트 타이머 시작 - hintDelay: {hintDelay}초 (정책 값)");
        StartCoroutine(HintTimerCoroutine(hintDelay));
    }

    /// <summary>
    /// 힌트 타이머 재시작 (정책 변경 시 호출)
    /// </summary>
    public void RestartHintTimer()
    {
        if (currentState != TutorialState.WaitingForAction) return;
        if (policyApplier == null) return;

        // 기존 코루틴 중지
        StopAllCoroutines();

        // 새 힌트 타이머 시작
        StartHintTimer();
    }

    /// <summary>
    /// 힌트 타이머 코루틴
    /// </summary>
    private System.Collections.IEnumerator HintTimerCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 여전히 WaitingForAction 상태이면 힌트 표시 (runReport는 ShowHint() 내부에서 자동 생성)
        if (currentState == TutorialState.WaitingForAction)
        {
            ShowHint();
        }
    }

    /// <summary>
    /// 힌트 상태로 전환 (hintDelaySeconds 후 자동 호출 예정)
    /// </summary>
    public void ShowHint()
    {
        // runReport가 없으면 자동 생성
        if (runReport == null)
        {
            EnsureRunReportExists("힌트 표시 시 자동 생성");
        }

        // C. 힌트 표시: HINT_SHOWN 이벤트 + summary.hintShownCount++
        if (runReport != null)
        {
            // 실제 경과 시간 계산 (정책 값과 실제 경과 시간 모두 기록)
            float actualElapsedTime = hintTimerStartTime > 0 ? (Time.time - hintTimerStartTime) : 0f;
            float policyHintDelay = currentHintDelay > 0 ? currentHintDelay : (policyApplier?.GetHintDelaySeconds() ?? 3.0f);
            
            // 정책 값과 실제 경과 시간 중 정책 값을 사용 (정책 값이 정확함)
            float hintDelay = policyHintDelay;
            
            runReport.AddEvent(TutorialEventType.HINT_SHOWN, new { 
                hintDelay = hintDelay,
                actualElapsedTime = actualElapsedTime > 0 ? actualElapsedTime : hintDelay
            });
            runReport.summary.hintShownCount++;
            Debug.Log($"[TutorialController] HINT_SHOWN 이벤트 로깅 - hintDelay: {hintDelay}초 (정책 값), 실제 경과: {actualElapsedTime:F3}초");
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
        Debug.Log($"실패 발생. 실패 횟수: {failCount}");

        // runReport가 없으면 자동 생성
        if (runReport == null)
        {
            EnsureRunReportExists("첫 번째 실패 시 자동 생성");
        }

        // C. 실패: FAIL 이벤트 + summary.failCount++
        if (runReport != null)
        {
            // data를 object로 전달 (더 유연함)
            var failData = new { failCount = failCount, state = currentState.ToString() };
            runReport.AddEvent(TutorialEventType.FAIL, failData);
            runReport.summary.failCount = failCount;
            Debug.Log($"[TutorialController] FAIL 이벤트 로깅 완료 (failCount: {failCount})");
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
        FinalizeRunReport("CLEAR", "에너미 처치");
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

        // 실제 duration 계산 (tutorialStartTime 기준)
        float actualDuration = Time.time - tutorialStartTime;

        // summary 최종화 (실제 duration 전달)
        runReport.summary.damageTaken = damageTaken;
        runReport.FinalizeSummary(result, endReason, actualDuration);

        // RUN_END 이벤트 추가 (summary.failCount 사용 - 리셋 전 값)
        runReport.AddEvent(TutorialEventType.RUN_END, new { 
            result = result, 
            endReason = endReason,
            durationSeconds = actualDuration,
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

        // policyJson이 비어있으면 policy를 JSON으로 변환
        if (string.IsNullOrEmpty(policyJson))
        {
            policyJson = policy.ToJson();
        }

        // variant와 tutorialVersion이 비어있으면 기본값 보장
        if (string.IsNullOrEmpty(policy.variant))
        {
            policy.variant = "A";
        }
        if (string.IsNullOrEmpty(policy.tutorialVersion))
        {
            policy.tutorialVersion = "1.0.0";
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
        
        // RUN_START 이벤트 추가 (variant와 version 보장)
        runReport.AddEvent(TutorialEventType.RUN_START, new
        {
            appVersion = Application.version,
            policyVariant = string.IsNullOrEmpty(policy.variant) ? "A" : policy.variant,
            tutorialVersion = string.IsNullOrEmpty(policy.tutorialVersion) ? "1.0.0" : policy.tutorialVersion,
            reason = "정책 버튼 클릭"
        });

        // 버튼 클릭 시점에 즉시 파일 저장
        string reportsDirectory = System.IO.Path.Combine(Application.persistentDataPath, "Reports");
        runReport.summary.damageTaken = 0;
        float buttonClickDuration = Time.time - tutorialStartTime;
        runReport.FinalizeSummary("START", "정책 버튼 클릭", buttonClickDuration);
        runReport.AddEvent(TutorialEventType.RUN_END, new
        {
            result = "START",
            endReason = "정책 버튼 클릭",
            durationSeconds = buttonClickDuration,
            failCount = 0,
            damageTaken = 0
        });
        runReport.SaveToFile(reportsDirectory);
    }
}

