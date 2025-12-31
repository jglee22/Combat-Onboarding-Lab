using System;
using UnityEngine;

/// <summary>
/// 서버에서 제공하는 튜토리얼 정책 데이터 스키마
/// </summary>
[Serializable]
public class TutorialPolicy
{
    /// <summary>
    /// 튜토리얼 버전 (서버에서 관리하는 정책 버전)
    /// </summary>
    public string tutorialVersion;

    /// <summary>
    /// A/B 테스트용 변형 (예: "A", "B", "control")
    /// </summary>
    public string variant;

    /// <summary>
    /// 힌트 노출까지의 지연 시간 (초)
    /// </summary>
    public float hintDelaySeconds;

    /// <summary>
    /// 화살표 가이드 표시 여부
    /// </summary>
    public bool showArrow;

    /// <summary>
    /// 최대 실패 허용 횟수 (이 횟수를 초과하면 도움말 제공)
    /// </summary>
    public int maxFailCount;

    /// <summary>
    /// 자동 도움말(Assist) 활성화 여부
    /// </summary>
    public bool assistEnabled;

    /// <summary>
    /// JSON 문자열로부터 TutorialPolicy 객체를 생성
    /// </summary>
    public static TutorialPolicy FromJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<TutorialPolicy>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse TutorialPolicy JSON: {e.Message}");
            return GetDefault();
        }
    }

    /// <summary>
    /// 기본 정책 값 반환 (서버 연결 실패 시 사용)
    /// </summary>
    public static TutorialPolicy GetDefault()
    {
        return new TutorialPolicy
        {
            tutorialVersion = "1.0.0",
            variant = "A",
            hintDelaySeconds = 3.0f,
            showArrow = true,
            maxFailCount = 3,
            assistEnabled = false
        };
    }

    /// <summary>
    /// JSON 문자열로 직렬화
    /// </summary>
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }
}

