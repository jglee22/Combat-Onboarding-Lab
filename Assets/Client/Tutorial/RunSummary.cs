/// <summary>
/// 런 요약 정보
/// </summary>
[System.Serializable]
public class RunSummary
{
    /// <summary>
    /// 결과 (CLEAR, FAIL, ABORT 등)
    /// </summary>
    public string result;

    /// <summary>
    /// 총 소요 시간 (초)
    /// </summary>
    public float durationSeconds;

    /// <summary>
    /// 종료 이유
    /// </summary>
    public string endReason;

    /// <summary>
    /// 단계 수
    /// </summary>
    public int stepCount;

    /// <summary>
    /// 실패 횟수
    /// </summary>
    public int failCount;

    /// <summary>
    /// 힌트 표시 횟수
    /// </summary>
    public int hintShownCount;

    /// <summary>
    /// 어시스트 발동 여부
    /// </summary>
    public bool assistTriggered;

    /// <summary>
    /// 받은 피해량
    /// </summary>
    public int damageTaken;
}

