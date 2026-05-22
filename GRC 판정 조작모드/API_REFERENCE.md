# GRC Judge Mod - API 레퍼런스

이 문서는 **GRC Judge Mod의 공개 API, 클래스, 메서드**를 완전히 정리한 API 레퍼런스입니다.

## 1) GRCJudgeMod 클래스

### 설명
MelonLoader 모드의 메인 엔트리 포인트. 모드 초기화, 통계 관리, 전역 상태 제어.

### 주요 메서드

#### `GetStatistics()`

```csharp
/// <summary>
/// 현재 판정 통계를 반환합니다.
/// </summary>
/// <returns>JudgeStatistics 객체</returns>
public static JudgeStatistics GetStatistics() { }
```

**사용 예시**:
```csharp
var stats = GRCJudgeMod.GetStatistics();
int perfectCount = stats.GetCount(JudgeGrade.Perfect);
int total = stats.GetTotalCount();
double accuracy = stats.GetAccuracy();  // (Perfect+Great)/Total*100
```

**주의사항**:
- `GetStatistics()` 반환 객체는 항상 동일한 인스턴스 (싱글톤)
- 멀티스레드 환경에서 동시 접근 시 예외 가능
- 게임 언로드 후 접근하면 null 가능

---

#### `ForceAllPerfect()`

```csharp
/// <summary>
/// 모든 판정을 Perfect로 강제합니다.
/// </summary>
public static void ForceAllPerfect() { }
```

**동작**:
- `JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Perfect)` 호출
- 이후의 모든 판정이 Perfect로 치환됨
- NUM 판정은 제외

**사용 예시**:
```csharp
// OnInitializeMelon에서 자동 호출됨
GRCJudgeMod.ForceAllPerfect();

// 이후 게임에서:
// - 모든 노트 판정 → Perfect 표시
// - 스코어 계산 → Perfect 기준
```

---

### 주요 필드

#### `debugmod` (MelonPreferences_Entry<bool>)

```csharp
private static MelonPreferences_Entry<bool> debugmod;
```

**설명**: 디버그 모드 활성화 여부

**기본값**: `false`

**수정 방법**:
- MelonLoader 설정 파일: `MelonPreferences.cfg` 수정
- 또는 프로그래매틱으로:
  ```csharp
  // (현재 코드에서는 프로그래매틱 수정이 노출되지 않음)
  // MelonPreferences에서 직접 수정 필요
  ```

**용도**: 로그 필터링
```csharp
if (debugmod.Value)
{
    MelonLogger.Msg("[DEBUG] 상세 정보");
}
```

---

## 2) JudgeHooks 클래스

### 설명
Harmony를 통해 게임의 판정 메서드를 후킹하고, 판정 값을 조작 또는 모니터링하는 핵심 클래스.

### 공개 메서드

#### `SetJudgeManipulation()`

```csharp
/// <summary>
/// 판정 조작 기능을 활성화/비활성화합니다.
/// </summary>
/// <param name="enable">활성화 여부</param>
/// <param name="forcedGrade">강제할 판정 등급 (null = 비활성화)</param>
public static void SetJudgeManipulation(bool enable, JudgeGrade? forcedGrade = null) { }
```

**사용 예시**:

```csharp
// Perfect 강제 (통계 수집만)
JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Perfect);

// Great 강제
JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Great);

// Good 강제
JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Good);

// 조작 비활성화 (통계만 수집)
JudgeHooks.SetJudgeManipulation(false);
```

**매개변수 상세**:

| 파라미터 | 타입 | 설명 |
|---------|------|------|
| enable | bool | true = 활성화, false = 비활성화 |
| forcedGrade | JudgeGrade? | 강제 등급, null = 비활성화 |

**부작용**:
- enable=false로 설정 후에도 통계 수집 계속됨
- 게임 플레이 중에는 호출 불가능 (재시작 필요)

---

#### `IsJudgeManipulationEnabled()`

```csharp
/// <summary>
/// 판정 조작이 현재 활성화되어 있는지 확인합니다.
/// </summary>
/// <returns>조작 활성화 여부</returns>
public static bool IsJudgeManipulationEnabled() { }
```

**사용 예시**:
```csharp
if (JudgeHooks.IsJudgeManipulationEnabled())
{
    MelonLogger.Msg("판정 조작 활성화 중");
}
```

---

#### `GetForcedJudgeGrade()`

```csharp
/// <summary>
/// 현재 강제되는 판정 등급을 반환합니다.
/// </summary>
/// <returns>강제 등급, 비활성화 시 null</returns>
public static JudgeGrade? GetForcedJudgeGrade() { }
```

**사용 예시**:
```csharp
var forced = JudgeHooks.GetForcedJudgeGrade();
if (forced.HasValue)
{
    MelonLogger.Msg($"강제 판정: {forced.Value}");
}
```

---

### 내부 메서드 (후킹용)

#### `OnJudgeMentPrefix()`

```csharp
/// <summary>
/// 판정 처리 메서드 실행 전 호출되는 Harmony Prefix.
/// 판정 값을 조작합니다.
/// </summary>
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object __instance, object[] __args) { }
```

**역할**:
1. OnJudgeParam 객체 찾기
2. NUM 판정 확인
3. 조작 활성화 시 판정 값 강제 주입

**호출 빈도**: 곡당 100~500회

---

#### `OnJudgeMentPostfix()`

```csharp
/// <summary>
/// 판정 처리 메서드 실행 후 호출되는 Harmony Postfix.
/// 재조작 및 통계 누적을 수행합니다.
/// </summary>
[HarmonyPostfix]
public static void OnJudgeMentPostfix(object __instance, object[] __args) { }
```

**역할**:
1. Prefix 재확인 (Fallback)
2. 판정 정보 추출
3. 통계에 누적

**호출 빈도**: 곡당 100~500회

---

## 3) JudgeGrade 열거형

### 정의

```csharp
public enum JudgeGrade
{
    /// <summary>완벽한 타이밍의 판정</summary>
    Perfect = 0,
    
    /// <summary>좋은 타이밍의 판정</summary>
    Great = 1,
    
    /// <summary>평범한 타이밍의 판정</summary>
    Good = 2,
    
    /// <summary>놓친 판정</summary>
    Miss = 3
}
```

### 사용 예시

```csharp
// 각 등급 비교
JudgeGrade grade = JudgeGrade.Perfect;

if (grade == JudgeGrade.Perfect)
    MelonLogger.Msg("Perfect!");
else if (grade == JudgeGrade.Great)
    MelonLogger.Msg("Great!");
```

### 값 변환

```csharp
// int → JudgeGrade
int value = 0;
var grade = (JudgeGrade)value;  // Perfect

// JudgeGrade → int
int intValue = (int)JudgeGrade.Perfect;  // 0

// 문자열 → JudgeGrade
var gradeFromString = Enum.Parse<JudgeGrade>("Perfect");  // JudgeGrade.Perfect
```

---

## 4) JudgeResult 클래스

### 정의

```csharp
public class JudgeResult
{
    /// <summary>판정 등급</summary>
    public JudgeGrade Grade { get; set; }
    
    /// <summary>입력 타이밍 오차 (샘플 단위)</summary>
    public int TimeDifference { get; set; }
    
    /// <summary>일찍 눌렀는지 여부</summary>
    public bool IsEarly { get; set; }
    
    /// <summary>판정 발생 시간</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>노트 타입 (예: "NORMAL", "NUM")</summary>
    public string NoteType { get; set; }
    
    /// <summary>노트 인덱스 (곡 내에서의 순번)</summary>
    public int NoteIndex { get; set; }
}
```

### 속성 상세

#### Grade

```csharp
public JudgeGrade Grade { get; set; }
```

- **타입**: `JudgeGrade`
- **범위**: Perfect, Great, Good, Miss
- **초기값**: Perfect (기본값)

#### TimeDifference

```csharp
public int TimeDifference { get; set; }
```

- **타입**: `int`
- **의미**: 입력이 정확한 타이밍으로부터 얼마나 벗어났는지 (샘플 단위)
- **범위**: 일반적으로 -50~+50 (게임마다 다름)
- **부호**: 음수 = 일찍 입력, 양수 = 늦게 입력

**예시**:
```csharp
if (result.TimeDifference == 0)
    MelonLogger.Msg("정확한 타이밍!");
else if (Math.Abs(result.TimeDifference) < 10)
    MelonLogger.Msg("거의 정확함");
```

#### IsEarly

```csharp
public bool IsEarly { get; set; }
```

- **타입**: `bool`
- **의미**: 플레이어가 정확한 타이밍보다 일찍 입력했는지
- **예시**:
  ```csharp
  if (result.IsEarly)
      MelonLogger.Msg("일찍 눌렀음");
  else
      MelonLogger.Msg("제때 또는 늦게 눌렀음");
  ```

#### Timestamp

```csharp
public DateTime Timestamp { get; set; }
```

- **타입**: `DateTime`
- **의미**: 판정이 발생한 시각 (게임 시간 또는 시스템 시간)
- **용도**: 판정 시간대별 분석

**예시**:
```csharp
var timestamp = result.Timestamp;
MelonLogger.Msg($"판정 시각: {timestamp:HH:mm:ss.fff}");
```

---

## 5) JudgeStatistics 클래스

### 주요 메서드

#### `AddJudge()`

```csharp
/// <summary>
/// 판정 결과를 통계에 추가합니다.
/// </summary>
/// <param name="result">추가할 판정 결과</param>
public void AddJudge(JudgeResult result) { }
```

**사용 예시**:
```csharp
var result = new JudgeResult 
{ 
    Grade = JudgeGrade.Perfect, 
    TimeDifference = 0 
};
GRCJudgeMod.GetStatistics().AddJudge(result);
```

**주의**:
- NUM 판정은 자동으로 제외됨
- 같은 결과를 여러 번 추가하면 누적됨

---

#### `GetCount()`

```csharp
/// <summary>
/// 특정 등급의 판정 개수를 반환합니다.
/// </summary>
/// <param name="grade">조회할 등급</param>
/// <returns>판정 개수</returns>
public int GetCount(JudgeGrade grade) { }
```

**사용 예시**:
```csharp
var stats = GRCJudgeMod.GetStatistics();
int perfects = stats.GetCount(JudgeGrade.Perfect);      // 95
int greats = stats.GetCount(JudgeGrade.Great);          // 5
int goods = stats.GetCount(JudgeGrade.Good);            // 0
int misses = stats.GetCount(JudgeGrade.Miss);           // 0
```

**반환값**: 0 이상의 정수

---

#### `GetTotalCount()`

```csharp
/// <summary>
/// 전체 판정 개수를 반환합니다.
/// </summary>
/// <returns>누적된 모든 판정의 개수</returns>
public int GetTotalCount() { }
```

**사용 예시**:
```csharp
int total = stats.GetTotalCount();  // 100
MelonLogger.Msg($"총 판정: {total}");
```

**공식**:
```
GetTotalCount() = GetCount(Perfect) + GetCount(Great) 
                + GetCount(Good) + GetCount(Miss)
```

---

#### `GetAccuracy()`

```csharp
/// <summary>
/// 정확도를 계산합니다. (Perfect + Great) / Total * 100
/// </summary>
/// <returns>정확도 (0~100)</returns>
public double GetAccuracy() { }
```

**사용 예시**:
```csharp
double accuracy = stats.GetAccuracy();  // 95.0
MelonLogger.Msg($"정확도: {accuracy:F1}%");
```

**공식**:
```
GetAccuracy() = (Perfect + Great) / Total * 100
```

**엣지 케이스**:
```csharp
// Total = 0일 때
GetAccuracy();  // 0.0 반환 (또는 NaN 가능)

// 모두 Perfect일 때
GetAccuracy();  // 100.0 반환
```

---

#### `Reset()`

```csharp
/// <summary>
/// 모든 통계를 초기화합니다.
/// </summary>
public void Reset() { }
```

**사용 예시**:
```csharp
var stats = GRCJudgeMod.GetStatistics();
stats.Reset();

// 초기화 후
stats.GetTotalCount();  // 0
stats.GetAccuracy();    // 0.0
```

**주의**: 취소 불가능

---

## 6) 확장 인터페이스 (향후)

### 계획된 API (v1.3.0 이상)

```csharp
// 아직 구현되지 않음
public interface IJudgeModifier
{
    JudgeGrade ModifyJudge(JudgeGrade original, JudgeResult context);
}

public static class JudgeHooks
{
    public static event EventHandler<JudgeResultEventArgs> OnJudgeProcessed;
}

public class JudgeResultEventArgs : EventArgs
{
    public JudgeResult Result { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

---

## 7) 사용 시나리오별 가이드

### 시나리오 1: 전체 통계 출력

```csharp
public static void PrintStatistics()
{
    var stats = GRCJudgeMod.GetStatistics();
    
    int perfectCount = stats.GetCount(JudgeGrade.Perfect);
    int greatCount = stats.GetCount(JudgeGrade.Great);
    int goodCount = stats.GetCount(JudgeGrade.Good);
    int missCount = stats.GetCount(JudgeGrade.Miss);
    int total = stats.GetTotalCount();
    
    MelonLogger.Msg("=== 판정 통계 ===");
    MelonLogger.Msg($"Perfect: {perfectCount} ({perfectCount*100.0/total:F1}%)");
    MelonLogger.Msg($"Great:   {greatCount} ({greatCount*100.0/total:F1}%)");
    MelonLogger.Msg($"Good:    {goodCount} ({goodCount*100.0/total:F1}%)");
    MelonLogger.Msg($"Miss:    {missCount} ({missCount*100.0/total:F1}%)");
    MelonLogger.Msg($"정확도:  {stats.GetAccuracy():F1}%");
}
```

### 시나리오 2: 특정 등급만 강제

```csharp
public static void SetJudgeMode(string mode)
{
    switch (mode.ToLower())
    {
        case "perfect":
            JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Perfect);
            MelonLogger.Msg("Perfect 강제 모드 활성화");
            break;
            
        case "great":
            JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Great);
            MelonLogger.Msg("Great 강제 모드 활성화");
            break;
            
        case "stats_only":
            JudgeHooks.SetJudgeManipulation(false);
            MelonLogger.Msg("통계 수집 모드 (조작 비활성화)");
            break;
    }
}
```

### 시나리오 3: 통계 리셋 및 재시작

```csharp
public static void ResetAndPlayAgain()
{
    GRCJudgeMod.GetStatistics().Reset();
    JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Perfect);
    
    MelonLogger.Msg("통계 초기화 및 Perfect 강제 모드 시작");
}
```

---

## 8) 에러 처리

### 일반적인 예외

| 예외 | 원인 | 해결책 |
|------|------|--------|
| `NullReferenceException` | 모드 미초기화 | 게임 재시작 |
| `InvalidCastException` | 타입 불일치 | 타입 확인 |
| `ArgumentOutOfRangeException` | 잘못된 Grade 값 | Enum 값 사용 |
| `FieldAccessException` | 필드 접근 불가 | 권한 확인 |

### 안전한 호출 패턴

```csharp
// ✅ 권장: null 체크
var stats = GRCJudgeMod.GetStatistics();
if (stats != null)
{
    stats.AddJudge(result);
}

// ❌ 위험: null 체크 없음
GRCJudgeMod.GetStatistics().AddJudge(result);  // null exception 가능
```

---

**마지막 업데이트**: 2026년 1월 5일  
**API 버전**: 1.0.0  
**호환 게임**: GUNVOLT RECORDS Cychronicle v1.0~v1.2
