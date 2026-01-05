using UnityEngine;
using System.Collections;

/// <summary>
/// 튜토리얼 상태 전환 흐름 시연 드라이버
/// 
/// 역할: 이 상태들이 실제 플레이 흐름에서 어떻게 전환되는지 보여주는 샘플 드라이버
/// 
/// 포함:
/// - 흐름만 있음 (O)
/// 
/// 제외:
/// - 전투 구현 (X)
/// - UI 구현 (X)
/// </summary>
public class TutorialFlowDriver : MonoBehaviour
{
    [Header("의존성")]
    [SerializeField] private TutorialController tutorialController;
    [SerializeField] private PolicyApplier policyApplier;

    [Header("시뮬레이션 설정")]
    [SerializeField] private bool autoSimulate = false;
    [SerializeField] private float simulationDelay = 1.0f;

    private void Start()
    {
        if (tutorialController == null)
        {
            tutorialController = GetComponent<TutorialController>();
        }

        if (policyApplier == null)
        {
            policyApplier = GetComponent<PolicyApplier>();
        }

        // 상태 변경 이벤트 구독
        if (tutorialController != null)
        {
            tutorialController.OnStateChanged += OnStateChanged;
        }

        if (autoSimulate)
        {
            StartCoroutine(SimulateFlow());
        }
    }

    private void OnDestroy()
    {
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
        Debug.Log($"[TutorialFlowDriver] 상태 변경: {newState}");
    }

    /// <summary>
    /// 예시 흐름 시뮬레이션
    /// 
    /// Init
    ///   → WaitingForAction
    ///   → (시간 초과) Hint
    ///   → (피격 발생) Failure → Retry
    ///   → (반복 실패) Assist
    ///   → (적 처치) Clear
    /// </summary>
    private IEnumerator SimulateFlow()
    {
        if (tutorialController == null) yield break;

        Debug.Log("=== 튜토리얼 흐름 시뮬레이션 시작 ===");

        // 1. Init → WaitingForAction
        yield return new WaitForSeconds(simulationDelay);
        tutorialController.StartWaitingForAction();
        Debug.Log("→ WaitingForAction 상태로 전환");

        // 2. (시간 초과) → Hint
        float hintDelay = tutorialController.GetHintDelaySeconds();
        yield return new WaitForSeconds(hintDelay);
        tutorialController.ShowHint();
        Debug.Log($"→ (시간 초과 {hintDelay}초) Hint 상태로 전환");

        // 3. (피격 발생) → Failure → Retry
        yield return new WaitForSeconds(simulationDelay);
        tutorialController.OnFailure();
        Debug.Log("→ (피격 발생) Retry 상태로 전환");

        // 4. (반복 실패) → Assist (maxFailCount 초과 시)
        yield return new WaitForSeconds(simulationDelay);
        tutorialController.OnFailure();
        Debug.Log("→ (반복 실패) Assist 상태로 전환해야 하는지 확인 중...");

        // 5. (적 처치) → Clear
        yield return new WaitForSeconds(simulationDelay);
        tutorialController.OnSuccess();
        Debug.Log("→ (적 처치) Clear 상태로 전환");

        Debug.Log("=== 튜토리얼 흐름 시뮬레이션 종료 ===");
    }

    // ============================================
    // 수동 트리거 메서드들 (에디터에서 테스트용)
    // ============================================

    [ContextMenu("Start Flow Simulation")]
    public void StartFlowSimulation()
    {
        StartCoroutine(SimulateFlow());
    }

    [ContextMenu("Trigger: Start Waiting For Action")]
    public void TriggerStartWaitingForAction()
    {
        tutorialController?.StartWaitingForAction();
    }

    [ContextMenu("Trigger: Show Hint")]
    public void TriggerShowHint()
    {
        tutorialController?.ShowHint();
    }

    [ContextMenu("Trigger: On Failure")]
    public void TriggerOnFailure()
    {
        tutorialController?.OnFailure();
    }

    [ContextMenu("Trigger: On Success")]
    public void TriggerOnSuccess()
    {
        tutorialController?.OnSuccess();
    }
}

