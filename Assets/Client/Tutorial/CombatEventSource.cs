using UnityEngine;
using System;

/// <summary>
/// 전투 이벤트 소스 (새 스크립트)
/// - Player/Enemy에서 사건이 터지면 이벤트를 발생시킴
/// - 이 스크립트가 ICombatEventSource를 구현해서 TutorialController에 연결됨
/// </summary>
public class CombatEventSource : MonoBehaviour, ICombatEventSource
{
    [Header("참조 (자동으로 찾거나 수동으로 연결)")]
    [Tooltip("Player 컴포넌트 참조. 비어있으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private Player player;
    
    [Tooltip("Enemy 컴포넌트 참조. 비어있으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private Enemy enemy;

    // ICombatEventSource 구현
    public event Action OnPlayerHit;
    public event Action OnPlayerDamaged;
    public event Action OnEnemyDefeated;
    public event Action OnPlayerDefeated;

    private bool isInitialized = false;

    private void Awake()
    {
        // Awake에서 초기화하여 다른 스크립트의 Start에서 참조 가능하도록 함
        Initialize();
    }

    private void Start()
    {
        // Start에서도 한 번 더 확인 (혹시 Awake가 안 불렸을 경우 대비)
        if (!isInitialized)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        // 이미 초기화되었으면 중복 구독 방지
        if (isInitialized)
        {
            Debug.LogWarning("[CombatEventSource] 이미 초기화되었습니다. 중복 초기화를 건너뜁니다.");
            return;
        }

        Debug.Log("[CombatEventSource] 초기화 시작...");

        // Player 자동 찾기
        if (player == null)
        {
            Debug.Log("[CombatEventSource] Player를 씬에서 찾는 중...");
            player = FindObjectOfType<Player>();
            if (player != null)
            {
                Debug.Log($"[CombatEventSource] Player를 찾았습니다: {player.gameObject.name}");
            }
        }
        else
        {
            Debug.Log($"[CombatEventSource] Player가 이미 연결되어 있습니다: {player.gameObject.name}");
        }

        // Enemy 자동 찾기
        if (enemy == null)
        {
            Debug.Log("[CombatEventSource] Enemy를 씬에서 찾는 중...");
            enemy = FindObjectOfType<Enemy>();
            if (enemy != null)
            {
                Debug.Log($"[CombatEventSource] Enemy를 찾았습니다: {enemy.gameObject.name}");
            }
        }
        else
        {
            Debug.Log($"[CombatEventSource] Enemy가 이미 연결되어 있습니다: {enemy.gameObject.name}");
        }

        if (player == null)
        {
            Debug.LogError("[CombatEventSource] Player를 찾을 수 없습니다! 씬에 Player 컴포넌트가 있는지 확인하세요.");
            return;
        }

        if (enemy == null)
        {
            Debug.LogError("[CombatEventSource] Enemy를 찾을 수 없습니다! 씬에 Enemy 컴포넌트가 있는지 확인하세요.");
            return;
        }

        // Player 이벤트 구독
        Debug.Log("[CombatEventSource] Player 이벤트 구독 중...");
        player.OnAttack += HandlePlayerAttack;
        player.OnDefeated += HandlePlayerDefeated;
        Debug.Log("[CombatEventSource] Player.OnAttack 구독 완료");
        Debug.Log("[CombatEventSource] Player.OnDefeated 구독 완료");

        // Enemy 이벤트 구독
        Debug.Log("[CombatEventSource] Enemy 이벤트 구독 중...");
        enemy.OnPlayerDamaged += HandlePlayerDamaged;
        Debug.Log("[CombatEventSource] Enemy.OnPlayerDamaged 구독 완료");

        // Enemy HP 체크를 위한 코루틴 시작
        StartCoroutine(CheckEnemyDefeated());

        isInitialized = true;
        Debug.Log("[CombatEventSource] ===== 초기화 완료 - Player와 Enemy 연결됨 =====");
        Debug.Log($"[CombatEventSource] Player: {player.gameObject.name}, Enemy: {enemy.gameObject.name}");
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (player != null)
        {
            player.OnAttack -= HandlePlayerAttack;
            player.OnDefeated -= HandlePlayerDefeated;
        }

        if (enemy != null)
        {
            enemy.OnPlayerDamaged -= HandlePlayerDamaged;
        }
    }

    /// <summary>
    /// 플레이어 공격 이벤트 처리
    /// </summary>
    private void HandlePlayerAttack()
    {
        // 플레이어가 사망했으면 공격 무시
        if (player != null && player.IsDefeated())
        {
            Debug.Log("[CombatEventSource] Player가 사망 상태라서 공격을 무시합니다.");
            return;
        }

        Debug.Log("[CombatEventSource] Player Hit 이벤트 발생");
        OnPlayerHit?.Invoke();

        // 공격 시 Enemy에게 데미지 주기 (간단한 구현)
        if (enemy != null && !enemy.IsDefeated())
        {
            enemy.TakeDamage(1);
        }
    }

    /// <summary>
    /// 플레이어 데미지 이벤트 처리
    /// </summary>
    private void HandlePlayerDamaged()
    {
        Debug.Log("[CombatEventSource] Player Damaged 이벤트 발생");
        OnPlayerDamaged?.Invoke();
    }

    /// <summary>
    /// 플레이어 사망 이벤트 처리
    /// </summary>
    private void HandlePlayerDefeated()
    {
        Debug.Log("[CombatEventSource] ===== HandlePlayerDefeated 호출됨 =====");
        Debug.Log("[CombatEventSource] Player.OnDefeated 이벤트 수신");
        
        // 이벤트 구독자 확인
        if (OnPlayerDefeated == null)
        {
            Debug.LogError("[CombatEventSource] OnPlayerDefeated 이벤트가 null입니다!");
            Debug.LogError("[CombatEventSource] TutorialController가 이벤트를 구독하지 않았을 수 있습니다.");
            Debug.LogError("[CombatEventSource] TutorialController의 Start()가 실행되었는지 확인하세요.");
            return;
        }
        
        int subscriberCount = OnPlayerDefeated.GetInvocationList().Length;
        Debug.Log($"[CombatEventSource] OnPlayerDefeated 구독자 수: {subscriberCount}");
        
        if (subscriberCount == 0)
        {
            Debug.LogError("[CombatEventSource] 구독자가 0명입니다! 이벤트가 전달되지 않습니다.");
            Debug.LogError("[CombatEventSource] TutorialController가 이벤트를 구독하지 않았습니다.");
            Debug.LogError("[CombatEventSource] TutorialController의 SubscribeToCombatEvents()가 호출되었는지 확인하세요.");
            return;
        }
        
        Debug.Log("[CombatEventSource] OnPlayerDefeated 이벤트 호출 중...");
        OnPlayerDefeated?.Invoke();
        Debug.Log("[CombatEventSource] OnPlayerDefeated 이벤트 호출 완료");
    }

    /// <summary>
    /// Enemy 처치 상태 체크 (주기적으로 확인)
    /// </summary>
    private System.Collections.IEnumerator CheckEnemyDefeated()
    {
        while (enemy != null)
        {
            yield return new WaitForSeconds(0.1f); // 0.1초마다 체크

            if (enemy.IsDefeated())
            {
                Debug.Log("[CombatEventSource] Enemy Defeated 이벤트 발생");
                OnEnemyDefeated?.Invoke();
                break;
            }
        }
    }
}

