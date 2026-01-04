using UnityEngine;

/// <summary>
/// 가짜 전투 이벤트 소스 (테스트/시연용)
/// 
/// 실제 전투 구현 없이 이벤트를 발생시켜 튜토리얼 흐름을 시연
/// </summary>
public class MockCombatEventSource : MonoBehaviour, ICombatEventSource
{
    [Header("테스트 설정")]
    [SerializeField] private bool autoSimulate = false;
    [SerializeField] private float damageInterval = 3.0f;
    [SerializeField] private float enemyDefeatDelay = 10.0f;

    // ICombatEventSource 구현
    public event System.Action OnPlayerHit;
    public event System.Action OnPlayerDamaged;
    public event System.Action OnEnemyDefeated;
    public event System.Action OnPlayerDefeated;

    private void Start()
    {
        if (autoSimulate)
        {
            StartCoroutine(SimulateCombat());
        }
    }

    private System.Collections.IEnumerator SimulateCombat()
    {
        // 플레이어가 공격
        yield return new WaitForSeconds(1.0f);
        OnPlayerHit?.Invoke();
        Debug.Log("[MockCombat] Player Hit!");

        // 플레이어가 피해를 입음 (반복)
        for (int i = 0; i < 2; i++)
        {
            yield return new WaitForSeconds(damageInterval);
            OnPlayerDamaged?.Invoke();
            Debug.Log($"[MockCombat] Player Damaged! (Count: {i + 1})");
        }

        // 적 처치
        yield return new WaitForSeconds(enemyDefeatDelay);
        OnEnemyDefeated?.Invoke();
        Debug.Log("[MockCombat] Enemy Defeated!");
    }

    // ============================================
    // 수동 트리거 메서드들 (에디터에서 테스트용)
    // ============================================

    [ContextMenu("Trigger: Player Hit")]
    public void TriggerPlayerHit()
    {
        OnPlayerHit?.Invoke();
    }

    [ContextMenu("Trigger: Player Damaged")]
    public void TriggerPlayerDamaged()
    {
        OnPlayerDamaged?.Invoke();
    }

    [ContextMenu("Trigger: Enemy Defeated")]
    public void TriggerEnemyDefeated()
    {
        OnEnemyDefeated?.Invoke();
    }

    [ContextMenu("Trigger: Player Defeated")]
    public void TriggerPlayerDefeated()
    {
        OnPlayerDefeated?.Invoke();
    }
}

