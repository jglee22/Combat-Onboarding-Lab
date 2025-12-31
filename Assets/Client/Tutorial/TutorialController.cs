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
    [SerializeField] private ICombatEventSource combatEventSource;

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

    private void Start()
    {
        if (policyApplier == null)
        {
            policyApplier = GetComponent<PolicyApplier>();
            if (policyApplier == null)
            {
                Debug.LogError("PolicyApplier가 필요합니다!");
            }
        }

        // 전투 이벤트 소스 연결
        if (combatEventSource == null)
        {
            combatEventSource = GetComponent<ICombatEventSource>();
            if (combatEventSource == null)
            {
                combatEventSource = FindObjectOfType<MonoBehaviour>() as ICombatEventSource;
            }
        }

        // 전투 이벤트 구독
        if (combatEventSource != null)
        {
            combatEventSource.OnPlayerDamaged += HandlePlayerDamaged;
            combatEventSource.OnEnemyDefeated += HandleEnemyDefeated;
        }

        // 튜토리얼 시작 시간 기록
        tutorialStartTime = Time.time;

        // A. 튜토리얼(런) 시작 시점: RunReport 초기화
        // PolicyApplier가 초기화될 때까지 기다리기 위해 코루틴 사용
        StartCoroutine(InitializeRunReportWhenReady());

        // 초기 상태로 전환
        ChangeState(TutorialState.Init);
    }

    private void OnDestroy()
    {
        // 전투 이벤트 구독 해제
        if (combatEventSource != null)
        {
            combatEventSource.OnPlayerDamaged -= HandlePlayerDamaged;
            combatEventSource.OnEnemyDefeated -= HandleEnemyDefeated;
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
    /// 적 처치 이벤트 처리 → OnSuccess()
    /// </summary>
    private void HandleEnemyDefeated()
    {
        OnSuccess();
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
        failCount = 0; // 성공 시 실패 카운트 리셋
        ChangeState(TutorialState.Clear);
        
        // D. 튜토리얼 종료 시점: summary 최종화, RUN_END, SaveToFile
        FinalizeRunReport("CLEAR", "Enemy defeated");

        // 기존 로그 생성 및 출력 (하위 호환성)
        CreateAndLogTutorialData();
    }

    /// <summary>
    /// 튜토리얼 로그 생성 및 출력
    /// </summary>
    private void CreateAndLogTutorialData()
    {
        var policy = policyApplier?.GetCurrentPolicy();
        if (policy == null)
        {
            policy = TutorialPolicy.GetDefault();
        }

        var log = new CombatTutorialLog
        {
            timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), // ISO 8601 형식
            tutorialVersion = policy.tutorialVersion,
            variant = policy.variant,
            failCount = failCount,
            clearTime = Time.time - tutorialStartTime,
            damageTaken = damageTaken
        };

        string logJson = JsonUtility.ToJson(log, true);
        Debug.Log($"[TutorialLog] {logJson}");

        // 파일로 저장 (선택사항)
        SaveLogToFile(logJson);
    }

    /// <summary>
    /// 로그를 파일로 저장
    /// 저장 위치: {Application.persistentDataPath}/TutorialLogs/
    /// </summary>
    private void SaveLogToFile(string logJson)
    {
        try
        {
            // 로그 디렉토리 생성
            string logDirectory = System.IO.Path.Combine(Application.persistentDataPath, "TutorialLogs");
            if (!System.IO.Directory.Exists(logDirectory))
            {
                System.IO.Directory.CreateDirectory(logDirectory);
            }

            // 파일명: TutorialLog_YYYYMMDD_HHMMSS.json
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"TutorialLog_{timestamp}.json";
            string filePath = System.IO.Path.Combine(logDirectory, fileName);

            // 파일 저장
            System.IO.File.WriteAllText(filePath, logJson, System.Text.Encoding.UTF8);
            Debug.Log($"[TutorialLog] Saved to: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TutorialLog] Failed to save log file: {e.Message}");
        }
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
    /// A. 튜토리얼(런) 시작 시점: RunReport 초기화
    /// PolicyApplier가 준비될 때까지 기다림
    /// </summary>
    private System.Collections.IEnumerator InitializeRunReportWhenReady()
    {
        // PolicyApplier가 초기화될 때까지 최대 10프레임 대기
        int maxWaitFrames = 10;
        int waitFrames = 0;
        
        while (policyApplier == null || policyApplier.GetCurrentPolicy() == null)
        {
            if (waitFrames >= maxWaitFrames)
            {
                Debug.LogWarning("[RunReport] PolicyApplier not ready after waiting, using default policy");
                break;
            }
            waitFrames++;
            yield return null; // 한 프레임 대기
        }

        InitializeRunReport();
    }

    /// <summary>
    /// RunReport 초기화 (정책이 준비된 후 호출)
    /// </summary>
    private void InitializeRunReport()
    {
        // 실제 적용된 정책 객체 가져오기
        var appliedPolicy = policyApplier?.GetCurrentPolicy();
        if (appliedPolicy == null)
        {
            Debug.LogWarning("[RunReport] Policy is null, using default policy");
            appliedPolicy = TutorialPolicy.GetDefault();
        }

        // 정책 실제 로드값 가져오기 (JSON 문자열)
        string policyJson = policyApplier?.GetCurrentPolicyJson();
        
        // policyJson이 있으면 파싱하여 실제 적용된 정책인지 확인
        if (!string.IsNullOrEmpty(policyJson))
        {
            var parsedPolicy = TutorialPolicy.FromJson(policyJson);
            if (parsedPolicy != null)
            {
                // 파싱된 정책이 빈 값이 아닌 경우 실제 적용된 정책으로 사용
                if (!string.IsNullOrEmpty(parsedPolicy.variant) || !string.IsNullOrEmpty(parsedPolicy.tutorialVersion))
                {
                    appliedPolicy = parsedPolicy;
                    Debug.Log($"[RunReport] Using parsed policy from JSON: {appliedPolicy.variant} (v{appliedPolicy.tutorialVersion})");
                }
            }
        }
        
        // 적용된 정책이 여전히 빈 값이면 기본값으로 채우기
        if (string.IsNullOrEmpty(appliedPolicy.variant) && string.IsNullOrEmpty(appliedPolicy.tutorialVersion))
        {
            var defaultPolicy = TutorialPolicy.GetDefault();
            appliedPolicy.variant = defaultPolicy.variant;
            appliedPolicy.tutorialVersion = defaultPolicy.tutorialVersion;
            Debug.LogWarning("[RunReport] Policy has empty values, using default values");
        }
        
        // policyJson이 없거나 빈 값이면 적용된 정책 객체로부터 생성
        if (string.IsNullOrEmpty(policyJson))
        {
            policyJson = appliedPolicy.ToJson();
        }
        else
        {
            // policyJson이 있지만 파싱 결과가 빈 값이면 적용된 정책 객체로부터 생성
            var parsedCheck = TutorialPolicy.FromJson(policyJson);
            if (parsedCheck != null && string.IsNullOrEmpty(parsedCheck.variant) && string.IsNullOrEmpty(parsedCheck.tutorialVersion))
            {
                policyJson = appliedPolicy.ToJson();
                Debug.LogWarning("[RunReport] Policy JSON has empty values, using applied policy JSON");
            }
        }

        // RunReport 생성: 실제 적용된 정책 객체와 그 JSON을 넘기기
        int seed = UnityEngine.Random.Range(0, int.MaxValue); // 시드 생성
        runReport = new RunReport(appliedPolicy, policyJson, seed);

        // RUN_START 이벤트 추가
        runReport.AddEvent(TutorialEventType.RUN_START, new { 
            appVersion = Application.version,
            policyVariant = appliedPolicy.variant,
            tutorialVersion = appliedPolicy.tutorialVersion
        });

        Debug.Log($"[RunReport] Tutorial run started with policy: {appliedPolicy.variant} (v{appliedPolicy.tutorialVersion})");
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

        Debug.Log($"[RunReport] Tutorial run ended: {result}");
    }
}
