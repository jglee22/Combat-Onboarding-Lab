/// <summary>
/// 튜토리얼 상태 정의
/// 
/// 핵심 설계 원칙:
/// - "실패 → UX 변화"가 구조로 명확히 보여야 함
/// - 상태 수가 중요한 것이 아니라, 실패 처리 흐름이 명확해야 함
/// 
/// 이후 모든 시스템(UI, 전투, 로그)이 이 enum을 기준으로 연결됨
/// </summary>
public enum TutorialState
{
    /// <summary>
    /// 정책 로딩 직후
    /// </summary>
    Init,

    /// <summary>
    /// 유저 입력 대기
    /// </summary>
    WaitingForAction,

    /// <summary>
    /// 힌트 노출
    /// </summary>
    Hint,

    /// <summary>
    /// 재시도
    /// </summary>
    Retry,

    /// <summary>
    /// 강제 보조
    /// </summary>
    Assist,

    /// <summary>
    /// 튜토리얼 완료
    /// </summary>
    Clear
}

