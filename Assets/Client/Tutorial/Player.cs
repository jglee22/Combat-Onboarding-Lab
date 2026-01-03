using UnityEngine;

/// <summary>
/// 플레이어 최소 구성
/// - HP만 있음 (예: 3)
/// - 입력: Space 누르면 "공격" (근접 판정 없이도 OK)
/// </summary>
public class Player : MonoBehaviour
{
    [Header("플레이어 설정")]
    [SerializeField] private int maxHP = 3;
    [SerializeField] private int currentHP;

    [Header("공격 설정")]
    [SerializeField] private KeyCode attackKey = KeyCode.Space;

    // 이벤트: 공격 시 발생
    public System.Action OnAttack;

    // 이벤트: 사망 시 발생
    public System.Action OnDefeated;

    private void Start()
    {
        currentHP = maxHP;
        Debug.Log($"[Player] 초기화 완료. HP: {currentHP}/{maxHP}");
    }

    private void Update()
    {
        // 사망했으면 공격 불가
        if (IsDefeated()) return;

        // Space 키 입력 감지
        if (Input.GetKeyDown(attackKey))
        {
            Attack();
        }
    }

    /// <summary>
    /// 공격 처리
    /// </summary>
    private void Attack()
    {
        // 사망했으면 공격 불가
        if (IsDefeated())
        {
            Debug.Log("[Player] 사망 상태라서 공격할 수 없습니다!");
            return;
        }

        Debug.Log("[Player] 공격!");
        OnAttack?.Invoke();
    }

    /// <summary>
    /// 현재 HP 반환
    /// </summary>
    public int GetCurrentHP()
    {
        return currentHP;
    }

    /// <summary>
    /// 최대 HP 반환
    /// </summary>
    public int GetMaxHP()
    {
        return maxHP;
    }

    /// <summary>
    /// 데미지 받기
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (currentHP <= 0) return; // 이미 사망했으면 무시

        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"[Player] 데미지 받음! HP: {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            Debug.Log("[Player] 사망!");
            OnDefeated?.Invoke();
        }
    }

    /// <summary>
    /// 처치되었는지 확인
    /// </summary>
    public bool IsDefeated()
    {
        return currentHP <= 0;
    }
}

