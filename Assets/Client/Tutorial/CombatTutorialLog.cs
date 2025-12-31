/// <summary>
/// 전투 튜토리얼 로그 DTO (Data Transfer Object)
/// 
/// 설계 원칙:
/// - 로그는 '보내는 코드' 말고 '스키마'부터 정의
/// - 아직 API 붙일 필요 없음
/// - "수집 → 판단 → 정책 변경" 흐름이 머릿속에 보이게
/// 
/// 각 필드가 왜 필요한지 설명 가능해야 함:
/// - timestamp: 로그 생성 시점 추적 (시간대별 분석)
/// - tutorialVersion: 정책 버전 추적
/// - variant: A/B 테스트 결과 분석
/// - failCount: 실패 패턴 분석
/// - clearTime: 튜토리얼 난이도 측정
/// - damageTaken: 전투 이해도 측정
/// </summary>
[System.Serializable]
public class CombatTutorialLog
{
    /// <summary>
    /// 로그 생성 시점 (ISO 8601 형식: "2024-12-22T22:45:30Z")
    /// </summary>
    public string timestamp;

    /// <summary>
    /// 튜토리얼 정책 버전 (어떤 정책으로 플레이했는지)
    /// </summary>
    public string tutorialVersion;

    /// <summary>
    /// A/B 테스트 변형 (어떤 변형으로 플레이했는지)
    /// </summary>
    public string variant;

    /// <summary>
    /// 실패 횟수 (유저가 몇 번 실패했는지)
    /// </summary>
    public int failCount;

    /// <summary>
    /// 클리어 시간 (초) (튜토리얼 완료까지 걸린 시간)
    /// </summary>
    public float clearTime;

    /// <summary>
    /// 받은 피해량 (전투 이해도 측정)
    /// </summary>
    public int damageTaken;
}

