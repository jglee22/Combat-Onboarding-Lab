using UnityEngine;
using System;

/// <summary>
/// 튜토리얼 이벤트 타입
/// </summary>
public enum TutorialEventType
{
    RUN_START,      // 튜토리얼(런) 시작
    RUN_END,        // 튜토리얼(런) 종료
    STEP_START,     // 단계 시작
    STEP_CLEAR,     // 단계 클리어
    FAIL,           // 실패
    HINT_SHOWN,     // 힌트 표시
    ASSIST_TRIGGERED // 어시스트 발동
}

/// <summary>
/// 튜토리얼 이벤트
/// </summary>
[System.Serializable]
public class TutorialEvent
{
    /// <summary>
    /// 이벤트 타입 (enum, 내부 사용)
    /// </summary>
    public TutorialEventType type;

    /// <summary>
    /// 이벤트 타입 이름 (문자열, JSON 저장용)
    /// </summary>
    public string typeName;

    /// <summary>
    /// 이벤트 발생 시점 (초, 튜토리얼 시작 기준)
    /// </summary>
    public float timestamp;

    /// <summary>
    /// 추가 데이터 (객체로 저장)
    /// </summary>
    public object data;

    public TutorialEvent(TutorialEventType eventType, float timestamp)
    {
        this.type = eventType;
        this.typeName = eventType.ToString(); // enum을 문자열로 변환
        this.timestamp = timestamp;
    }
}


