using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 UI 컨트롤러
/// 
/// 정책 변화가 화면에서 보이게 하는 최소 UI:
/// - TMP 텍스트: Current State / FailCount / Policy Variant / HintDelay
/// - 화살표 GameObject: showArrow에 따라 On/Off
/// - 버튼 2개: Apply Policy A, Apply Policy B (런타임 정책 교체)
/// </summary>
public class TutorialUIController : MonoBehaviour
{
    [Header("정보 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("화살표 GameObject")]
    [SerializeField] private GameObject arrowObject;

    [Header("정책 교체 버튼")]
    [SerializeField] private Button applyPolicyAButton;
    [SerializeField] private Button applyPolicyBButton;

    [Header("의존성")]
    [SerializeField] private TutorialController tutorialController;
    [SerializeField] private PolicyApplier policyApplier;

    private void Start()
    {
        // 의존성 자동 찾기
        if (tutorialController == null)
        {
            tutorialController = FindObjectOfType<TutorialController>();
        }

        if (policyApplier == null)
        {
            policyApplier = FindObjectOfType<PolicyApplier>();
        }

        // 정책 변경 이벤트 구독
        if (policyApplier != null)
        {
            policyApplier.OnPolicyChanged += UpdateUI;
        }

        // 상태 변경 이벤트 구독
        if (tutorialController != null)
        {
            tutorialController.OnStateChanged += OnStateChanged;
        }

        // 버튼 이벤트 연결
        if (applyPolicyAButton != null)
        {
            applyPolicyAButton.onClick.AddListener(OnApplyPolicyAClicked);
        }

        if (applyPolicyBButton != null)
        {
            applyPolicyBButton.onClick.AddListener(OnApplyPolicyBClicked);
        }

        // 초기 UI 업데이트
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (policyApplier != null)
        {
            policyApplier.OnPolicyChanged -= UpdateUI;
        }

        if (tutorialController != null)
        {
            tutorialController.OnStateChanged -= OnStateChanged;
        }
    }

    /// <summary>
    /// 상태 변경 시 호출
    /// </summary>
    private void OnStateChanged(TutorialState newState)
    {
        UpdateUI();
    }

    /// <summary>
    /// UI 전체 업데이트
    /// </summary>
    public void UpdateUI()
    {
        UpdateInfoText();
        UpdateArrowVisibility();
    }

    /// <summary>
    /// 정보 텍스트 업데이트
    /// Current State / FailCount / Policy Variant / HintDelay
    /// </summary>
    private void UpdateInfoText()
    {
        if (infoText == null) return;

        string state = tutorialController?.GetCurrentState().ToString() ?? "Unknown";
        int failCount = tutorialController?.GetFailCount() ?? 0;
        string variant = policyApplier?.GetVariant() ?? "Unknown";
        float hintDelay = policyApplier?.GetHintDelaySeconds() ?? 0f;

        infoText.text = $"State: {state}\n" +
                       $"FailCount: {failCount}\n" +
                       $"Policy Variant: {variant}\n" +
                       $"HintDelay: {hintDelay:F1}s";
    }

    /// <summary>
    /// 화살표 표시/숨김 업데이트
    /// showArrow 정책에 따라 On/Off
    /// </summary>
    public void UpdateArrowVisibility()
    {
        if (arrowObject == null) return;

        bool shouldShow = policyApplier?.ShouldShowArrow() ?? false;
        arrowObject.SetActive(shouldShow);
    }

    /// <summary>
    /// Policy A 적용 버튼 클릭
    /// </summary>
    private void OnApplyPolicyAClicked()
    {
        ApplyPolicy(PolicyExamples.PolicyA, "A");
    }

    /// <summary>
    /// Policy B 적용 버튼 클릭
    /// </summary>
    private void OnApplyPolicyBClicked()
    {
        ApplyPolicy(PolicyExamples.PolicyB, "B");
    }

    /// <summary>
    /// 정책 적용 공통 로직
    /// </summary>
    private void ApplyPolicy(string policyJson, string policyName)
    {
        if (policyApplier == null || tutorialController == null) return;

        Debug.Log($"[TutorialUI] Policy {policyName} 버튼 클릭");
        
        // 1. 정책 적용
        policyApplier.ApplyPolicyFromServer(policyJson);
        
        // 2. 적용된 정책 가져오기
        var policy = policyApplier.GetCurrentPolicy();
        string currentPolicyJson = policyApplier.GetCurrentPolicyJson();
        
        if (policy == null)
        {
            Debug.LogError($"[TutorialUI] Policy {policyName}를 가져올 수 없습니다!");
            return;
        }
        
        Debug.Log($"[TutorialUI] Policy {policyName} 적용 완료 - variant: '{policy.variant}'");
        
        // 3. 새 Run 시작
        tutorialController.StartNewRunWithPolicy(policy, currentPolicyJson);
    }
}

