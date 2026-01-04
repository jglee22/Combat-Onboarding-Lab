using UnityEngine;

/// <summary>
/// 적 최소 구성
/// - HP만 있음 (예: 3)
/// - 일정 간격으로 Player에게 데미지 (예: 1초마다 -1) 또는 플레이어가 가까우면 -1
/// </summary>
public class Enemy : MonoBehaviour
{
    [Header("적 설정")]
    [SerializeField] private int maxHP = 3;
    [SerializeField] private int currentHP;

    [Header("데미지 설정")]
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float damageInterval = 1.0f; // 일정 간격으로 데미지
    [SerializeField] private bool useProximityDamage = false; // 플레이어가 가까우면 데미지
    [SerializeField] private float proximityDistance = 2.0f; // 근접 거리

    [Header("참조 (자동으로 찾거나 수동으로 연결)")]
    [Tooltip("Player 컴포넌트 참조. 비어있으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private Player targetPlayer;

    private float lastDamageTime = 0f;

    // 이벤트: 플레이어에게 데미지를 입혔을 때 발생
    public System.Action OnPlayerDamaged;

    private void Start()
    {
        currentHP = maxHP;
        
        // Player 자동 찾기
        if (targetPlayer == null)
        {
            targetPlayer = FindObjectOfType<Player>();
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning("[Enemy] Player를 찾을 수 없습니다!");
        }

        Debug.Log($"[Enemy] 초기화 완료. HP: {currentHP}/{maxHP}");
    }

    private void Update()
    {
        // 처치되었으면 공격 중단
        if (IsDefeated()) return;

        if (targetPlayer == null) return;

        // 플레이어가 사망했으면 공격 중단
        if (targetPlayer.IsDefeated()) return;

        // 일정 간격으로 데미지 주기
        if (Time.time - lastDamageTime >= damageInterval)
        {
            if (useProximityDamage)
            {
                // 플레이어가 가까우면 데미지
                float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
                if (distance <= proximityDistance)
                {
                    DealDamageToPlayer();
                    lastDamageTime = Time.time;
                }
            }
            else
            {
                // 일정 간격으로 무조건 데미지
                DealDamageToPlayer();
                lastDamageTime = Time.time;
            }
        }
    }

    /// <summary>
    /// 플레이어에게 데미지 주기
    /// </summary>
    private void DealDamageToPlayer()
    {
        // 처치되었으면 공격 중단
        if (IsDefeated()) return;

        if (targetPlayer == null) return;

        // 플레이어가 이미 사망했으면 공격 중단
        if (targetPlayer.IsDefeated())
        {
            Debug.Log("[Enemy] Player가 이미 사망했으므로 공격을 중단합니다.");
            return;
        }

        targetPlayer.TakeDamage(damageAmount);
        OnPlayerDamaged?.Invoke();
        Debug.Log($"[Enemy] Player에게 데미지 {damageAmount} 입힘!");
    }

    /// <summary>
    /// 데미지 받기
    /// </summary>
    public void TakeDamage(int damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"[Enemy] 데미지 받음! HP: {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            Debug.Log("[Enemy] 처치됨!");
        }
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
    /// 처치되었는지 확인
    /// </summary>
    public bool IsDefeated()
    {
        return currentHP <= 0;
    }
}

