# GRC Judge Mod - 성능 최적화 가이드

이 문서는 로컬 모드 환경에서 **성능 최적화 기법, 병목 분석, 튜닝 전략**을 다룹니다.

## 1) 성능 병목 분석

### 1.1 주요 병목 지점

#### Reflection 호출 (가장 비쌈)

```csharp
// ❌ 비효율: 판정마다 타입 탐색
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    var assembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");  // 매번!
    var type = assembly.GetType("IntiCreates.cNotecWorkBase");         // 매번!
    var fields = type.GetFields();                                     // 매번!
}
```

**성능 영향**: ~10-50ms/호출 (1000판정 = 10~50초)

#### Enum.Parse() 호출 (비쌈)

```csharp
// ❌ 비효율: 매 판정마다 Parse
object result = Enum.Parse(enumType, "PERFECT");
```

**성능 영향**: ~1-5ms/호출 (1000판정 = 1~5초)

#### 문자열 검색 (중간 비용)

```csharp
// ❌ 비효율: 모든 필드 순회 + 부분일치
foreach (var field in fields)
{
    if (field.Name.ToLower().Contains("judge") && 
        field.Name.ToLower().Contains("type"))  // 문자열 연산
    {
        // ...
    }
}
```

**성능 영향**: ~0.1-1ms/호출

#### 로깅 (낮지만 누적)

```csharp
// ❌ 비효율: 판정마다 로그
MelonLogger.Msg($"판정: {grade} at {DateTime.Now}");  // 1000판정 = 1000회 로그
```

**성능 영향**: ~0.01-0.1ms/호출 하지만 누적 시 유의미함

### 1.2 벤치마크 추정치

```
초기화 (1회):
├─ Assembly 로드 대기: ~1~5초 (게임 시작 시)
├─ Type 탐색: ~50ms
└─ 패치 적용: ~100ms

게임 플레이 (1000판정):
├─ Prefix 호출: 1000 * 5ms = 5초
├─ Postfix 호출: 1000 * 10ms = 10초  
├─ 통계 누적: 1000 * 0.1ms = 0.1초
└─ 로깅: 1000 * 0.1ms = 0.1초
총: ~15초 오버헤드 (FPS 저하 관찰됨)
```

## 2) Reflection 캐싱 전략

### 2.1 어셈블리 캐싱

```csharp
public class ReflectionCache
{
    private static Assembly cachedAssembly = null;
    
    public static Assembly GetCSharpAssembly()
    {
        if (cachedAssembly != null)
            return cachedAssembly;
        
        cachedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        
        return cachedAssembly;
    }
}

// 사용: 매 호출마다 캐시 확인
var assembly = ReflectionCache.GetCSharpAssembly();
```

**성능 향상**: 10-50ms → 0.001ms (50000배 개선)

### 2.2 타입 캐싱

```csharp
private static Type cachedOnJudgeParamType = null;

public static Type GetOnJudgeParamType()
{
    if (cachedOnJudgeParamType != null)
        return cachedOnJudgeParamType;
    
    var assembly = ReflectionCache.GetCSharpAssembly();
    cachedOnJudgeParamType = assembly?.GetType("IntiCreates.cNotecWorkBase");
    
    return cachedOnJudgeParamType;
}
```

**성능 향상**: 10-20ms → 0.001ms

### 2.3 필드 정보 캐싱

```csharp
private static Dictionary<Type, Dictionary<string, FieldInfo>> fieldCache = 
    new Dictionary<Type, Dictionary<string, FieldInfo>>();

public static FieldInfo GetCachedField(Type type, string fieldName)
{
    // 1단계: Type별 캐시 확인
    if (!fieldCache.TryGetValue(type, out var typeCache))
    {
        typeCache = new Dictionary<string, FieldInfo>();
        fieldCache[type] = typeCache;
    }
    
    // 2단계: 필드명별 캐시 확인
    if (typeCache.TryGetValue(fieldName, out var field))
        return field;
    
    // 3단계: 탐색 (첫 호출에만)
    field = type.GetField(fieldName, 
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    
    if (field != null)
        typeCache[fieldName] = field;
    
    return field;
}
```

**사용 예**:
```csharp
var judgeTypeField = GetCachedField(onJudgeParamType, "judgeType");
var value = judgeTypeField?.GetValue(onJudgeParam);
```

**성능 향상**: 5-10ms → 0.01ms

### 2.4 Enum 값 사전 구축

```csharp
private static Dictionary<Type, Dictionary<string, object>> enumValueCache = 
    new Dictionary<Type, Dictionary<string, object>>();

public static void PreBuildEnumCache(Type enumType)
{
    if (enumValueCache.ContainsKey(enumType))
        return;
    
    var cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    
    foreach (var name in Enum.GetNames(enumType))
    {
        var value = Enum.Parse(enumType, name);
        cache[name] = value;
        
        // Perfect와 PERFECT 모두 같은 값으로 매핑
        cache[name.ToUpper()] = value;
        cache[name.ToLower()] = value;
    }
    
    enumValueCache[enumType] = cache;
}

public static object GetEnumValue(Type enumType, string searchName)
{
    if (!enumValueCache.TryGetValue(enumType, out var cache))
    {
        PreBuildEnumCache(enumType);
        cache = enumValueCache[enumType];
    }
    
    // 정확 일치
    if (cache.TryGetValue(searchName, out var result))
        return result;
    
    // 부분 일치
    var partialMatches = cache.Keys
        .Where(k => k.Contains(searchName, StringComparison.OrdinalIgnoreCase))
        .ToList();
    
    return partialMatches.Count > 0 ? cache[partialMatches[0]] : null;
}
```

**성능 향상**: 1-5ms → 0.01ms

## 3) 로깅 최적화

### 3.1 조건부 로깅

```csharp
// ❌ 비효율: 매번 포맷팅
[HarmonyPostfix]
public static void OnJudgeMentPostfix(object onJudgeParam)
{
    MelonLogger.Msg($"Judge: {GetGrade(onJudgeParam)}");  // 항상 실행
}

// ✅ 효율적: 조건 확인 후 로깅
[HarmonyPostfix]
public static void OnJudgeMentPostfix(object onJudgeParam)
{
    if (!debugmod.Value)  // 먼저 플래그 확인
        return;
    
    MelonLogger.Msg($"Judge: {GetGrade(onJudgeParam)}");
}
```

### 3.2 레이트 리미팅

```csharp
private static int judgeCounter = 0;
private const int LOG_INTERVAL = 100;

[HarmonyPostfix]
public static void OnJudgeMentPostfix(object onJudgeParam)
{
    judgeCounter++;
    
    // 100개마다 1번만 로그
    if (judgeCounter % LOG_INTERVAL == 0)
    {
        MelonLogger.Msg($"Processed {judgeCounter} judges");
    }
}
```

**효과**: 1000번 로그 → 10번 로그 (100배 감소)

### 3.3 버퍼링 로깅

```csharp
private static List<string> logBuffer = new List<string>(100);
private const int BUFFER_SIZE = 100;

[HarmonyPostfix]
public static void OnJudgeMentPostfix(JudgeResult result)
{
    logBuffer.Add($"{DateTime.Now:HH:mm:ss.fff}: {result.Grade}");
    
    // 버퍼가 꽉 찼을 때만 플러시
    if (logBuffer.Count >= BUFFER_SIZE)
    {
        FlushLogs();
    }
}

private static void FlushLogs()
{
    foreach (var log in logBuffer)
        MelonLogger.Msg(log);
    
    logBuffer.Clear();
}
```

## 4) 메모리 최적화

### 4.1 문자열 인턴링

```csharp
// ❌ 비효율: 매번 새로운 문자열 객체
private static string GetGradeStr(JudgeGrade grade)
{
    return grade switch
    {
        JudgeGrade.Perfect => "Perfect",      // 1000개 객체 생성
        JudgeGrade.Great => "Great",
        _ => "Unknown"
    };
}

// ✅ 효율적: 정적 문자열 참조
private static readonly string STR_PERFECT = "Perfect";
private static readonly string STR_GREAT = "Great";

private static string GetGradeStr(JudgeGrade grade)
{
    return grade switch
    {
        JudgeGrade.Perfect => STR_PERFECT,    // 문자열 재사용
        JudgeGrade.Great => STR_GREAT,
        _ => "Unknown"
    };
}
```

### 4.2 객체 풀링

```csharp
public class JudgeResultPool
{
    private static Stack<JudgeResult> pool = new Stack<JudgeResult>(1000);
    
    public static JudgeResult Rent()
    {
        return pool.Count > 0 ? pool.Pop() : new JudgeResult();
    }
    
    public static void Return(JudgeResult result)
    {
        result.Clear();
        pool.Push(result);
    }
}

// 사용
var result = JudgeResultPool.Rent();
result.Grade = judgeGrade;
// ... 처리 ...
JudgeResultPool.Return(result);
```

**효과**: GC 압력 감소 → 프레임 드롭 완화

### 4.3 배열 재사용

```csharp
// ❌ 비효율: 매번 새 배열 생성
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    var judgeParams = __args.Where(a => a != null).ToArray();  // 매번!
}

// ✅ 효율적: 정적 배열 재사용
private static object[] tempArgs = new object[16];

[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    Array.Copy(__args, tempArgs, Math.Min(__args.Length, 16));
}
```

## 5) 알고리즘 최적화

### 5.1 필드 탐색 최적화

```csharp
// ❌ 비효율: O(n) 모든 필드 순회
private static FieldInfo FindJudgeField(Type paramType)
{
    var fields = paramType.GetFields(flags);
    
    foreach (var field in fields)
    {
        if (field.Name.ToLower().Contains("judge"))
            return field;
    }
    return null;
}

// ✅ 효율적: O(1) 직접 접근
private static FieldInfo FindJudgeField(Type paramType)
{
    // 가능한 필드명 리스트 (미리 알려진 순서대로)
    var candidateNames = new[] { "judgeType", "type", "grade", "result" };
    
    foreach (var name in candidateNames)
    {
        var field = paramType.GetField(name, flags);
        if (field != null)
            return field;
    }
    
    return null;
}
```

### 5.2 조기 종료

```csharp
// ❌ 비효율: 모든 필드 다 확인
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    var onJudgeParam = FindOnJudgeParam(__args);
    
    // NUM 판정 확인
    if (IsNumJudgment(onJudgeParam))
        return;  // ← 여기서 이미 빠져나감
    
    // 조작
    ManipulateJudge(onJudgeParam);
}

private static bool IsNumJudgment(object onJudgeParam)
{
    // 조기 종료: 필드 1개 확인 후 바로 반환
    var field = GetCachedField(onJudgeParam.GetType(), "noteType");
    var value = field?.GetValue(onJudgeParam) as string;
    
    return value?.Contains("NUM") ?? false;
}
```

## 6) GC 최적화

### 6.1 GC 할당 추적

```csharp
// 성능 프로파일링: 각 작업의 GC 할당량 측정
private static long GetCurrentGCBytes()
{
    return GC.GetTotalMemory(false);
}

[HarmonyPostfix]
public static void OnJudgeMentPostfix(object onJudgeParam)
{
    var before = GetCurrentGCBytes();
    
    // 작업 수행
    var result = ExtractJudgeResult(onJudgeParam);
    
    var after = GetCurrentGCBytes();
    var allocated = after - before;
    
    if (allocated > 1024)  // 1KB 이상 할당?
    {
        MelonLogger.Warning($"GC 할당 과다: {allocated} bytes");
    }
}
```

### 6.2 GC 압박 감소

```csharp
// ❌ 비효율: 매번 새 객체 생성
private static JudgeResult BuildResult(object onJudgeParam)
{
    var result = new JudgeResult();  // ← GC 할당
    result.Grade = ExtractGrade(onJudgeParam);
    result.TimeDiff = ExtractTimeDiff(onJudgeParam);
    return result;
}

// ✅ 효율적: 객체 재사용
private static JudgeResult resultCache = new JudgeResult();

private static JudgeResult BuildResult(object onJudgeParam)
{
    resultCache.Grade = ExtractGrade(onJudgeParam);
    resultCache.TimeDiff = ExtractTimeDiff(onJudgeParam);
    return resultCache;
}
```

## 7) 프로파일링 및 측정

### 7.1 성능 측정 유틸리티

```csharp
public class PerformanceTimer
{
    private readonly string name;
    private readonly System.Diagnostics.Stopwatch sw;
    
    public PerformanceTimer(string name)
    {
        this.name = name;
        this.sw = System.Diagnostics.Stopwatch.StartNew();
    }
    
    public void Stop()
    {
        sw.Stop();
        MelonLogger.Msg($"{name}: {sw.ElapsedMilliseconds}ms");
    }
}

// 사용
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    var timer = new PerformanceTimer("OnJudgeMentPrefix");
    
    // ... 코드 ...
    
    timer.Stop();
}
```

### 7.2 주기적 성능 리포트

```csharp
private static class PerformanceMetrics
{
    public static int TotalJudges { get; set; }
    public static long TotalMilliseconds { get; set; }
    public static DateTime LastReportTime { get; set; }
    
    [HarmonyPostfix]
    public static void OnJudgeMentPostfix(object onJudgeParam)
    {
        TotalJudges++;
        
        // 10초마다 리포트
        if (DateTime.Now - LastReportTime > TimeSpan.FromSeconds(10))
        {
            var avgMs = (double)TotalMilliseconds / TotalJudges;
            MelonLogger.Msg($"Avg: {avgMs:F3}ms/judge, Total: {TotalJudges}");
            
            LastReportTime = DateTime.Now;
            TotalJudges = 0;
            TotalMilliseconds = 0;
        }
    }
}
```

## 8) 튜닝 체크리스트

```
성능 최적화 우선순위:

1순위: Reflection 캐싱
   □ Assembly 캐싱
   □ Type 캐싱
   □ FieldInfo 캐싱
   □ Enum 값 사전 구축

2순위: 로깅 최적화
   □ debugmod 조건부 확인
   □ 레이트 리미팅 적용
   □ 버퍼링 구현

3순위: 메모리 최적화
   □ 문자열 인턴링
   □ 객체 풀링
   □ 배열 재사용

4순위: 알고리즘 개선
   □ 필드 탐색 순서 최적화
   □ 조기 종료 추가
   □ 불필요한 연산 제거

5순위: GC 최적화
   □ 할당 추적
   □ GC 빈도 감소
   □ 메모리 누수 확인
```

---

**최적화 전략**: 측정 → 병목 파악 → 캐싱 → 로깅 최적화 순서로 진행하세요!
