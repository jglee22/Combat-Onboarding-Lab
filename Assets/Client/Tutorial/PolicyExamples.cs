/// <summary>
/// 정책 예시 JSON
/// 
/// 같은 튜토리얼 흐름이지만, 서버 정책에 따라 유저가 경험하는 UX는 달라집니다.
/// 코드는 안 바뀌고, JSON만 바뀜.
/// </summary>
public static class PolicyExamples
{
    /// <summary>
    /// Policy A: 빠른 힌트, 도움말 비활성화
    /// </summary>
    public const string PolicyA = @"{
  ""tutorialVersion"": ""1.0.0"",
  ""variant"": ""A"",
  ""hintDelaySeconds"": 2.0,
  ""showArrow"": true,
  ""maxFailCount"": 3,
  ""assistEnabled"": false
}";

    /// <summary>
    /// Policy B: 느린 힌트, 도움말 활성화, 화살표 비활성화
    /// </summary>
    public const string PolicyB = @"{
  ""tutorialVersion"": ""1.0.0"",
  ""variant"": ""B"",
  ""hintDelaySeconds"": 6.0,
  ""showArrow"": false,
  ""maxFailCount"": 3,
  ""assistEnabled"": true
}";

}

