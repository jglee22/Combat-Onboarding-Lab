using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// RunReport (메모리 버퍼)
/// 
/// 역할: 실행 중 이벤트를 쌓아두고, 끝나면 한 번에 JSON으로 저장
/// 
/// 핵심 포인트:
/// - 이벤트는 "찍을 때마다" 파일 쓰지 말고 List에만 추가
/// - 저장은 RUN_END 시점에 한번
/// </summary>
[System.Serializable]
public class RunReport
{
    /// <summary>
    /// 스키마 버전
    /// </summary>
    public string schemaVersion = "1.0.0";

    /// <summary>
    /// 런 메타데이터 (시간, seed, variant, appVersion 등)
    /// </summary>
    public RunMetadata run;

    /// <summary>
    /// 정책 스냅샷 (이번 런에서 사용된 설정) - 객체 형태
    /// </summary>
    public TutorialPolicy policy;

    /// <summary>
    /// 정책 실제 로드값 (JSON 문자열, 원본 그대로)
    /// </summary>
    public string policyData;

    /// <summary>
    /// 런 요약 정보
    /// </summary>
    public RunSummary summary;

    /// <summary>
    /// 이벤트 리스트
    /// </summary>
    public List<TutorialEvent> events;

    /// <summary>
    /// 런 시작 시간 (초, Time.time 기준)
    /// </summary>
    private float runStartTime;

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="policySnapshot">정책 객체 스냅샷</param>
    /// <param name="policyJson">정책 실제 로드값 (JSON 문자열, 원본 그대로)</param>
    /// <param name="seed">시드 값</param>
    public RunReport(TutorialPolicy policySnapshot, string policyJson = null, int seed = 0)
    {
        this.schemaVersion = "1.0.0";
        
        // 정책이 null이거나 빈 값이면 기본값 사용
        if (policySnapshot == null || 
            (string.IsNullOrEmpty(policySnapshot.variant) && string.IsNullOrEmpty(policySnapshot.tutorialVersion)))
        {
            policySnapshot = TutorialPolicy.GetDefault();
        }
        
        // variant 설정 (policySnapshot.variant를 정확히 사용)
        string variantValue = string.IsNullOrEmpty(policySnapshot.variant) ? "A" : policySnapshot.variant;
        
        this.run = new RunMetadata
        {
            startTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            seed = seed,
            variant = variantValue,
            appVersion = Application.version,
            tutorialVersion = string.IsNullOrEmpty(policySnapshot.tutorialVersion) ? "1.0.0" : policySnapshot.tutorialVersion
        };
        this.policy = policySnapshot;
        
        // policyData 설정 (policyJson이 있으면 사용, 없으면 policySnapshot을 JSON으로 변환)
        if (!string.IsNullOrEmpty(policyJson))
        {
            this.policyData = policyJson;
            Debug.Log($"[RunReport] 생성자: policyJson 사용 (길이: {policyJson.Length})");
        }
        else
        {
            string snapshotJson = policySnapshot.ToJson();
            this.policyData = snapshotJson;
            Debug.Log($"[RunReport] 생성자: policySnapshot.ToJson() 사용 (길이: {snapshotJson?.Length ?? 0})");
        }
        
        // policyData가 여전히 비어있으면 기본 정책 JSON 사용
        if (string.IsNullOrEmpty(this.policyData))
        {
            Debug.LogError("[RunReport] 생성자: policyData가 비어있어 기본 정책 JSON을 사용합니다.");
            TutorialPolicy defaultPolicy = TutorialPolicy.GetDefault();
            this.policyData = defaultPolicy.ToJson();
            Debug.Log($"[RunReport] 생성자: 기본 정책 JSON 생성 (길이: {this.policyData?.Length ?? 0})");
        }
        
        // 최종 검증
        if (string.IsNullOrEmpty(this.policyData))
        {
            Debug.LogError("[RunReport] 생성자: ⚠️ policyData가 여전히 비어있습니다! 최소한 빈 객체라도 설정합니다.");
            this.policyData = "{}";
        }
        
        Debug.Log($"[RunReport] 생성자 완료 - policyData 최종 길이: {this.policyData?.Length ?? 0}");
        Debug.Log($"[RunReport] 생성자 - policyData 내용 (처음 100자): {this.policyData?.Substring(0, Math.Min(100, this.policyData.Length)) ?? "null"}");
        
        this.summary = new RunSummary();
        this.events = new List<TutorialEvent>();
        this.runStartTime = Time.time;
    }

    /// <summary>
    /// 이벤트 추가
    /// </summary>
    /// <param name="eventType">이벤트 타입</param>
    /// <param name="data">데이터 (object)</param>
    public void AddEvent(TutorialEventType eventType, object data = null)
    {
        float timestamp = Time.time - runStartTime;
        var evt = new TutorialEvent(eventType, timestamp);
        evt.data = data;
        events.Add(evt);
    }

    /// <summary>
    /// 요약 정보 최종화
    /// </summary>
    /// <param name="result">결과 (CLEAR, FAIL 등)</param>
    /// <param name="endReason">종료 이유</param>
    /// <param name="durationSeconds">소요 시간 (초). 지정하지 않으면 runStartTime 기준으로 계산</param>
    public void FinalizeSummary(string result, string endReason, float? durationSeconds = null)
    {
        summary.result = result;
        summary.durationSeconds = durationSeconds ?? (Time.time - runStartTime);
        summary.endReason = endReason;
    }

    /// <summary>
    /// 파일로 저장
    /// </summary>
    public void SaveToFile(string directoryPath)
    {
        try
        {
            // 디렉토리 생성
            if (!System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }

            // 파일명 구성: run_{timestamp}_{variant}_seed{seed}_{result}.json
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string variant = run.variant ?? "Unknown";
            string seedStr = run.seed > 0 ? $"seed{run.seed}" : "noseed";
            string result = summary.result ?? "UNKNOWN";
            string fileName = $"run_{timestamp}_{variant}_{seedStr}_{result}.json";

            string filePath = System.IO.Path.Combine(directoryPath, fileName);

            // 커스텀 JSON 직렬화 (data 필드를 객체로 변환)
            string json = ToJsonWithDataAsObject();

            // timestamp와 durationSeconds 필드의 소수점 자리수 제한 (정규식으로 처리)
            json = FormatFloatValues(json);

            // 파일 저장
            System.IO.File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
            Debug.Log($"[RunReport] 저장 완료: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RunReport] 리포트 저장 실패: {e.Message}");
        }
    }

    /// <summary>
    /// JSON 문자열로 변환 (디버그용)
    /// </summary>
    public string ToJson()
    {
        return ToJsonWithDataAsObject();
    }

    /// <summary>
    /// data 필드를 객체로 직렬화하는 커스텀 JSON 생성
    /// </summary>
    private string ToJsonWithDataAsObject()
    {
        // 기본 JSON 직렬화 (data 필드는 제외됨 - object 타입이므로)
        string baseJson = JsonUtility.ToJson(this, true);
        
        // policyData 필드를 객체로 변환
        baseJson = ConvertPolicyDataToObject(baseJson);
        
        // events 배열의 각 이벤트에서 data 필드를 객체로 추가
        System.Text.StringBuilder result = new System.Text.StringBuilder();
        int eventIndex = 0;
        int i = 0;
        
        while (i < baseJson.Length)
        {
            // "events": [ 패턴 찾기
            if (i + 8 < baseJson.Length && baseJson.Substring(i, 8) == "\"events\"")
            {
                // events 배열 시작 찾기
                int arrayStart = baseJson.IndexOf('[', i + 8);
                if (arrayStart == -1)
                {
                    result.Append(baseJson[i]);
                    i++;
                    continue;
                }
                
                result.Append(baseJson.Substring(i, arrayStart - i + 1));
                i = arrayStart + 1;
                
                // 각 이벤트 처리
                while (eventIndex < events.Count && i < baseJson.Length)
                {
                    // 이벤트 객체 시작 찾기
                    int objStart = baseJson.IndexOf('{', i);
                    if (objStart == -1) break;
                    
                    // 이벤트 객체 끝 찾기
                    int objEnd = FindMatchingBrace(baseJson, objStart);
                    if (objEnd == -1) break;
                    
                    // 이벤트 JSON 추출
                    string eventJson = baseJson.Substring(objStart, objEnd - objStart + 1);
                    
                    // data 필드 추가
                    var evt = events[eventIndex];
                    string dataJson = SerializeDataObject(evt.data);
                    
                    // 이벤트 JSON에 data 필드 추가
                    if (eventJson.EndsWith("}"))
                    {
                        eventJson = eventJson.Substring(0, eventJson.Length - 1);
                        if (!eventJson.EndsWith(",") && eventJson.TrimEnd().Length > 1)
                        {
                            eventJson += ",";
                        }
                        eventJson += $"\n        \"data\": {dataJson}\n    }}";
                    }
                    
                    result.Append(eventJson);
                    if (eventIndex < events.Count - 1)
                    {
                        result.Append(",");
                    }
                    
                    i = objEnd + 1;
                    eventIndex++;
                    
                    // 다음 이벤트로 이동
                    while (i < baseJson.Length && char.IsWhiteSpace(baseJson[i]))
                    {
                        i++;
                    }
                }
                
                // 나머지 JSON 추가
                result.Append(baseJson.Substring(i));
                break;
            }
            
            result.Append(baseJson[i]);
            i++;
        }
        
        return result.ToString();
    }

    /// <summary>
    /// policyData 필드를 문자열에서 객체로 변환
    /// </summary>
    private string ConvertPolicyDataToObject(string json)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(policyData)) return json;

        // "policyData": " 패턴 찾기
        int policyDataPos = json.IndexOf("\"policyData\"");
        if (policyDataPos == -1) return json;

        int colonPos = json.IndexOf(':', policyDataPos + 12);
        if (colonPos == -1) return json;

        // 공백 건너뛰기
        int quoteStart = colonPos + 1;
        while (quoteStart < json.Length && char.IsWhiteSpace(json[quoteStart]))
        {
            quoteStart++;
        }

        if (quoteStart >= json.Length || json[quoteStart] != '"') return json;

        // 닫는 따옴표 찾기
        int quoteEnd = quoteStart + 1;
        bool escaped = false;
        while (quoteEnd < json.Length)
        {
            if (escaped)
            {
                escaped = false;
                quoteEnd++;
                continue;
            }

            if (json[quoteEnd] == '\\')
            {
                escaped = true;
                quoteEnd++;
                continue;
            }

            if (json[quoteEnd] == '"')
            {
                quoteEnd++;
                break;
            }

            quoteEnd++;
        }

        // policyData JSON 문자열 추출 및 이스케이프 제거
        string escapedJson = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 2);
        string unescapedJson = UnescapeJsonString(escapedJson);

        // 변환
        string prefix = json.Substring(0, quoteStart);
        string suffix = json.Substring(quoteEnd);

        return prefix + unescapedJson + suffix;
    }

    /// <summary>
    /// JSON 문자열에서 이스케이프 문자를 제거
    /// </summary>
    private string UnescapeJsonString(string escapedJson)
    {
        if (string.IsNullOrEmpty(escapedJson)) return escapedJson;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool escaped = false;

        for (int i = 0; i < escapedJson.Length; i++)
        {
            char c = escapedJson[i];

            if (escaped)
            {
                // 이스케이프 문자 처리
                switch (c)
                {
                    case '"':
                        sb.Append('"');
                        break;
                    case '\\':
                        sb.Append('\\');
                        break;
                    case '/':
                        sb.Append('/');
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        // 유니코드 이스케이프 처리 (예: \u0041)
                        if (i + 4 < escapedJson.Length)
                        {
                            string hex = escapedJson.Substring(i + 1, 4);
                            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            else
                            {
                                sb.Append('\\');
                                sb.Append(c);
                            }
                        }
                        else
                        {
                            sb.Append('\\');
                            sb.Append(c);
                        }
                        break;
                    default:
                        // 알 수 없는 이스케이프는 그대로 유지
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                }
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// data 객체를 JSON 문자열로 직렬화
    /// </summary>
    private string SerializeDataObject(object data)
    {
        if (data == null) return "{}";
        
        // 이미 JSON 문자열인 경우
        if (data is string strData)
        {
            // 유효한 JSON인지 확인
            strData = strData.Trim();
            if (strData.StartsWith("{") && strData.EndsWith("}"))
            {
                return strData;
            }
            return "{}";
        }
        
        // 객체인 경우 JsonUtility로 직렬화
        try
        {
            string json = JsonUtility.ToJson(data);
            if (!string.IsNullOrEmpty(json) && json != "{}")
            {
                return json;
            }
        }
        catch
        {
            // JsonUtility 실패 시 리플렉션 사용
        }
        
        // 리플렉션으로 직렬화
        return SerializeObjectWithReflection(data);
    }

    /// <summary>
    /// 리플렉션을 사용하여 객체를 JSON으로 직렬화
    /// </summary>
    private string SerializeObjectWithReflection(object obj)
    {
        if (obj == null) return "{}";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("{");
        
        Type type = obj.GetType();
        System.Reflection.FieldInfo[] fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        System.Reflection.PropertyInfo[] properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        bool first = true;
        
        // Fields 처리
        foreach (var field in fields)
        {
            if (!first) sb.Append(",");
            first = false;
            
            object value = field.GetValue(obj);
            sb.Append($"\"{field.Name}\":{SerializeValue(value)}");
        }
        
        // Properties 처리
        foreach (var prop in properties)
        {
            if (!prop.CanRead) continue;
            if (!first) sb.Append(",");
            first = false;
            
            object value = prop.GetValue(obj);
            sb.Append($"\"{prop.Name}\":{SerializeValue(value)}");
        }
        
        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// 값을 JSON 형식으로 직렬화
    /// </summary>
    private string SerializeValue(object value)
    {
        if (value == null) return "null";
        
        if (value is string str)
        {
            return $"\"{str.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r")}\"";
        }
        else if (value is bool b)
        {
            return b ? "true" : "false";
        }
        else if (value is int || value is long || value is short || value is byte)
        {
            return value.ToString();
        }
        else if (value is float f)
        {
            return f.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (value is double d)
        {
            return d.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (value is decimal dec)
        {
            return dec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            return SerializeObjectWithReflection(value);
        }
    }

    /// <summary>
    /// 중괄호 매칭하여 객체 끝 찾기
    /// </summary>
    private int FindMatchingBrace(string json, int startPos)
    {
        int braceCount = 0;
        bool inString = false;
        bool escaped = false;
        
        for (int i = startPos; i < json.Length; i++)
        {
            char c = json[i];
            
            if (escaped)
            {
                escaped = false;
                continue;
            }
            
            if (c == '\\')
            {
                escaped = true;
                continue;
            }
            
            if (c == '"' && !escaped)
            {
                inString = !inString;
                continue;
            }
            
            if (!inString)
            {
                if (c == '{')
                {
                    braceCount++;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        return i;
                    }
                }
            }
        }
        
        return -1;
    }

    /// <summary>
    /// JSON 문자열에서 float 값의 소수점 자리수를 제한 (timestamp, durationSeconds 등)
    /// </summary>
    private string FormatFloatValues(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        // 정규식으로 float 값을 찾아서 소수점 3자리로 포맷팅
        // 패턴: 숫자.숫자 (예: 123.456789 -> 123.457)
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"(\d+\.\d{4,})");
        return regex.Replace(json, match =>
        {
            if (float.TryParse(match.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            }
            return match.Value;
        });
    }
}

