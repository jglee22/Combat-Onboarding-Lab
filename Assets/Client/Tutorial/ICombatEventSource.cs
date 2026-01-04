using System;

/// <summary>
/// 전투 이벤트 소스 인터페이스
/// 
/// 설계 목적:
/// - 튜토리얼이 전투 구현에 의존하지 않음을 증명
/// - "구조 설계자" 포지션을 확실히 보여주기
/// 
/// 전투를 만들 필요 없음
/// 전투에서 뭘 보고 판단할지만 정의
/// </summary>
public interface ICombatEventSource
{
    /// <summary>
    /// 플레이어가 공격했을 때 발생하는 이벤트
    /// </summary>
    event Action OnPlayerHit;

    /// <summary>
    /// 플레이어가 피해를 입었을 때 발생하는 이벤트
    /// </summary>
    event Action OnPlayerDamaged;

    /// <summary>
    /// 적이 패배했을 때 발생하는 이벤트
    /// </summary>
    event Action OnEnemyDefeated;

    /// <summary>
    /// 플레이어가 패배했을 때 발생하는 이벤트
    /// </summary>
    event Action OnPlayerDefeated;
}

