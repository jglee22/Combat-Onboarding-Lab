using System.Collections;
using System;
using UnityEngine;

/// <summary>
/// 정책 해석기 (Policy Interpreter)
/// 
/// 책임:
/// - 서버 JSON → TutorialPolicy 변환
/// - 정책 값 해석 및 제공
/// - 정책 변경 시 "변화가 보이는 처리" (힌트 타이머 재설정, UI 토글 갱신)
/// 
/// 구조 의도:
/// [Server JSON] → TutorialPolicy → PolicyApplier (여기서만 정책 해석) → TutorialController (질문만 함)
/// 
/// TutorialController는 직접 정책을 해석하지 않고, PolicyApplier에게 질문만 함
/// </summary>
public class PolicyApplier : MonoBehaviour
{
    [Header("정책 설정")]
    [SerializeField] private TutorialPolicy currentPolicy;

    [Header("의존성")]
    [SerializeField] private TutorialController tutorialController;
    [SerializeField] private TutorialUIController uiController;

    [Header("디버그")]
    [SerializeField] private bool useDefaultPolicy = false;
    [SerializeField] private string testPolicyJson = "";

    // 정책 변경 이벤트
    public event Action OnPolicyChanged;

    // 실제 로드된 정책 JSON (원본 그대로)
    private string lastLoadedPolicyJson = "";

    private void Start()
    {
        if (tutorialController == null)
        {
            tutorialController = GetComponent<TutorialController>();
        }

        if (uiController == null)
        {
            uiController = FindObjectOfType<TutorialUIController>();
        }

        InitializePolicy();
    }

    /// <summary>
    /// 정책 초기화 (서버에서 받아오거나 기본값 사용)
    /// </summary>
    private void InitializePolicy()
    {
        if (useDefaultPolicy || string.IsNullOrEmpty(testPolicyJson))
        {
            currentPolicy = TutorialPolicy.GetDefault();
            lastLoadedPolicyJson = currentPolicy.ToJson(); // 기본값도 JSON으로 저장
            Debug.Log("Using default tutorial policy");
        }
        else
        {
            lastLoadedPolicyJson = testPolicyJson; // 실제 로드된 JSON 저장
            currentPolicy = TutorialPolicy.FromJson(testPolicyJson);
            Debug.Log($"Loaded tutorial policy: Version {currentPolicy.tutorialVersion}, Variant {currentPolicy.variant}");
        }

        // 초기 정책 로딩 시에도 변화가 보이는 처리 적용
        if (currentPolicy != null)
        {
            OnPolicyUpdated();
        }
    }

    /// <summary>
    /// 서버에서 정책을 받아와 적용
    /// </summary>
    public void ApplyPolicyFromServer(string jsonPolicy)
    {
        lastLoadedPolicyJson = jsonPolicy; // 실제 로드된 JSON 저장 (원본 그대로)
        currentPolicy = TutorialPolicy.FromJson(jsonPolicy);
        if (currentPolicy != null)
        {
            Debug.Log($"Policy applied: {currentPolicy.ToJson()}");
            OnPolicyUpdated();
        }
    }

    /// <summary>
    /// 현재 정책 반환
    /// </summary>
    public TutorialPolicy GetCurrentPolicy()
    {
        return currentPolicy;
    }

    /// <summary>
    /// 현재 정책의 JSON 문자열 반환 (실제 로드값, 원본 그대로)
    /// </summary>
    public string GetCurrentPolicyJson()
    {
        // 실제 로드된 원본 JSON이 있으면 그것을 반환, 없으면 직렬화
        return !string.IsNullOrEmpty(lastLoadedPolicyJson) 
            ? lastLoadedPolicyJson 
            : currentPolicy?.ToJson() ?? "";
    }

    /// <summary>
    /// 정책이 업데이트되었을 때 호출되는 이벤트
    /// 
    /// "변화가 보이는 처리"를 여기서 수행:
    /// - 힌트 타이머 재설정
    /// - UI 토글 갱신 (showArrow 등)
    /// </summary>
    private void OnPolicyUpdated()
    {
        if (currentPolicy == null) return;

        Debug.Log($"[PolicyApplier] Policy updated. Applying visible changes...");

        // 1. 힌트 타이머 재설정
        ResetHintTimer();

        // 2. UI 토글 갱신
        UpdateUIToggles();

        // 3. 정책 변경 이벤트 발생 (외부 시스템에 알림)
        OnPolicyChanged?.Invoke();
    }

    /// <summary>
    /// 힌트 타이머 재설정
    /// 
    /// 정책이 변경되면 현재 진행 중인 힌트 타이밍을 새로운 hintDelaySeconds로 재설정
    /// </summary>
    private void ResetHintTimer()
    {
        if (tutorialController == null) return;

        TutorialState currentState = tutorialController.GetCurrentState();

        // WaitingForAction 상태일 때만 힌트 타이머 재설정
        if (currentState == TutorialState.WaitingForAction)
        {
            float newHintDelay = GetHintDelaySeconds();
            Debug.Log($"[PolicyApplier] Hint timer reset to {newHintDelay} seconds (was in WaitingForAction state)");

            // 코루틴으로 힌트 타이밍 재시작
            StopAllCoroutines();
            StartCoroutine(DelayedHintCoroutine(newHintDelay));
        }
    }

    /// <summary>
    /// 지연된 힌트 표시 코루틴
    /// </summary>
    private IEnumerator DelayedHintCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (tutorialController != null && 
            tutorialController.GetCurrentState() == TutorialState.WaitingForAction)
        {
            tutorialController.ShowHint();
            Debug.Log($"[PolicyApplier] Hint shown after {delay} seconds delay");
        }
    }

    /// <summary>
    /// UI 토글 갱신
    /// 
    /// 정책의 showArrow 등 UI 관련 설정을 UI 시스템에 반영
    /// 이제 "로그만 찍는 함수"가 아니라 실제로 UI를 바꾸는 함수
    /// </summary>
    private void UpdateUIToggles()
    {
        bool shouldShowArrow = ShouldShowArrow();
        Debug.Log($"[PolicyApplier] UI toggles updated - showArrow: {shouldShowArrow}");

        // TutorialUIController를 통해 실제 UI 업데이트
        if (uiController != null)
        {
            uiController.UpdateArrowVisibility();
            uiController.UpdateUI();
        }
    }

    // ============================================
    // 정책 해석 메서드들 (TutorialController는 이 메서드들을 통해 정책을 참조)
    // ============================================

    /// <summary>
    /// 정책 해석: 튜토리얼 버전 반환
    /// </summary>
    public string GetTutorialVersion() => currentPolicy?.tutorialVersion ?? "unknown";

    /// <summary>
    /// 정책 해석: A/B 테스트 변형 반환
    /// </summary>
    public string GetVariant() => currentPolicy?.variant ?? "A";

    /// <summary>
    /// 정책 해석: 힌트 지연 시간 반환
    /// </summary>
    public float GetHintDelaySeconds() => currentPolicy?.hintDelaySeconds ?? 3.0f;

    /// <summary>
    /// 정책 해석: 화살표 표시 여부 반환
    /// </summary>
    public bool ShouldShowArrow() => currentPolicy?.showArrow ?? true;

    /// <summary>
    /// 정책 해석: 최대 실패 허용 횟수 반환
    /// </summary>
    public int GetMaxFailCount() => currentPolicy?.maxFailCount ?? 3;

    /// <summary>
    /// 정책 해석: 도움말 활성화 여부 반환
    /// </summary>
    public bool IsAssistEnabled() => currentPolicy?.assistEnabled ?? false;

    /// <summary>
    /// 정책 해석: 실패 횟수가 Assist 상태로 전환해야 하는지 판단
    /// </summary>
    public bool ShouldTransitionToAssist(int currentFailCount)
    {
        if (currentPolicy == null) return false;
        return currentFailCount > currentPolicy.maxFailCount && currentPolicy.assistEnabled;
    }
}
