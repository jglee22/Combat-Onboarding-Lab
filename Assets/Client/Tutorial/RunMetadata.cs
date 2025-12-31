/// <summary>
/// 런 메타데이터 (시간, seed, variant, appVersion 등)
/// </summary>
[System.Serializable]
public class RunMetadata
{
    /// <summary>
    /// 런 시작 시간 (ISO 8601 형식)
    /// </summary>
    public string startTime;

    /// <summary>
    /// 시드 값 (재현 가능성을 위해)
    /// </summary>
    public int seed;

    /// <summary>
    /// 정책 variant (A/B 테스트)
    /// </summary>
    public string variant;

    /// <summary>
    /// 앱 버전
    /// </summary>
    public string appVersion;

    /// <summary>
    /// 튜토리얼 정책 버전
    /// </summary>
    public string tutorialVersion;
}

