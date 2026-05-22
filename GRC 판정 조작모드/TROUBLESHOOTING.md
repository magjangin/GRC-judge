# GRC Judge Mod - 트러블슈팅 및 디버깅 가이드

이 문서는 **일반적인 문제, 원인 분석, 해결 방법**을 정리한 포괄적인 트러블슈팅 가이드입니다.

## 1) 초기화 관련 문제

### 1.1 "모드가 로드되지 않음"

**증상**: MelonLoader 콘솔에 GRC Judge 모드가 나타나지 않음

**원인 분석**:

| 원인 | 확인 방법 |
|------|----------|
| DLL 위치 잘못됨 | `MelonLoader/Mods/` 폴더에 있는지 확인 |
| DLL 이름 오류 | 파일명이 `GRC judge.dll` 정확히 일치하는지 확인 |
| .NET 버전 불일치 | 게임의 .NET 버전과 모드 타겟 버전 확인 |
| MelonInfo 누락 | `AssemblyInfo.cs`에 `[MelonGame]`, `[MelonInfo]` 있는지 확인 |
| 컴파일 오류 | 최근 빌드 후 DLL이 업데이트되었는지 확인 |

**해결책**:

```csharp
// 1. AssemblyInfo.cs 확인
[assembly: MelonInfo(typeof(GRCJudgeMod), "GRC Judge Mod", "1.0.0", "Author")]
[assembly: MelonGame("INTI CREATES", "GUNVOLT RECORDS Cychronicle")]

// 2. DLL 위치 확인: h:\MelonLoader\Mods\GRC judge.dll

// 3. MelonLoader 재시작
```

### 1.2 "Assembly-CSharp를 찾을 수 없음"

**증상**: "assembly not found" 또는 "type not found" 로그

**원인**:
- 게임이 아직 씬 로드 중
- IL2CPP 게임 (Assembly-CSharp 없음)
- 게임 버전 변경

**해결책**:

```csharp
// 현재 코드
private static IEnumerator WaitForAssembly()
{
    int timeout = 0;
    while (!IsAssemblyLoaded("Assembly-CSharp") && timeout < 1000)
    {
        timeout++;
        yield return null;
    }
    
    if (timeout >= 1000)
    {
        MelonLogger.Error("Assembly-CSharp 로드 타임아웃!");
        yield break;
    }
    
    MelonLogger.Msg("Assembly-CSharp 로드됨");
    // 패칭 진행
}

// IL2CPP 게임 대응
private static bool IsIL2CPPGame()
{
    return AppDomain.CurrentDomain.GetAssemblies()
        .All(a => a.GetName().Name != "Assembly-CSharp");
}
```

## 2) 후킹 관련 문제

### 2.1 "onJudgeMent 메서드를 찾을 수 없음"

**증상**: Harmony 패칭 실패, 판정 조작 작동 안 함

**원인 분석**:

```
[원인 체크리스트]
□ 타입명 변경: IntiCreates.cNotecWorkBase → 다른 이름
□ 메서드명 변경: onJudgeMent → 다른 이름
□ 메서드 삭제: 게임에서 리팩토링
□ 메서드 이동: 다른 클래스로 이동
□ 컴파일 최적화: 메서드 인라인됨
□ 오버로드: 같은 이름의 여러 메서드
```

**디버깅 프로세스**:

```csharp
public static void DebugFindTargetMethod()
{
    var assembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
    
    if (assembly == null)
    {
        MelonLogger.Error("Assembly-CSharp 없음");
        return;
    }
    
    // 1단계: IntiCreates 네임스페이스의 모든 타입 찾기
    var intiCreatesTypes = assembly.GetTypes()
        .Where(t => t.FullName?.StartsWith("IntiCreates") ?? false)
        .ToList();
    
    MelonLogger.Msg($"[DEBUG] IntiCreates 타입 {intiCreatesTypes.Count}개 발견:");
    foreach (var type in intiCreatesTypes)
        MelonLogger.Msg($"  - {type.FullName}");
    
    // 2단계: 각 타입에서 "Judge" 관련 메서드 찾기
    var judgeTypes = intiCreatesTypes
        .Where(t => t.GetMethods().Any(m => m.Name.Contains("Judge")))
        .ToList();
    
    MelonLogger.Msg($"[DEBUG] Judge 메서드를 가진 타입:");
    foreach (var type in judgeTypes)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name.Contains("Judge"))
            .ToList();
        
        MelonLogger.Msg($"  - {type.Name}:");
        foreach (var method in methods)
            MelonLogger.Msg($"    └─ {method.Name}");
    }
}

// 사용: OnInitializeMelon에서 호출
DebugFindTargetMethod();
```

**해결책**:

```csharp
// 방법 1: 유연한 이름 탐색
private static MethodInfo FindJudgeMethod(Type type)
{
    var methods = type.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );
    
    // 정확한 이름 먼저 시도
    var exact = methods.FirstOrDefault(m => m.Name == "onJudgeMent");
    if (exact != null) return exact;
    
    // 부분 일치
    var partial = methods.FirstOrDefault(m => 
        m.Name.Contains("Judge", StringComparison.OrdinalIgnoreCase) &&
        m.Name.Contains("Ment", StringComparison.OrdinalIgnoreCase)
    );
    if (partial != null) return partial;
    
    // 마지막 시도: Judge가 포함된 메서드
    return methods.FirstOrDefault(m => 
        m.Name.Contains("Judge", StringComparison.OrdinalIgnoreCase)
    );
}

// 방법 2: 여러 가능성 시도
private static void ApplyPatchWithFallback(Type type, Harmony harmony)
{
    var methodNames = new[] 
    { 
        "onJudgeMent",      // v1.0
        "OnJudgeMent",      // 다른 컨벤션
        "OnJudge",          // 약자 버전
        "JudgeNote",        // 역순
        "Judge"             // 최후의 수단
    };
    
    MethodInfo targetMethod = null;
    foreach (var methodName in methodNames)
    {
        targetMethod = type.GetMethod(methodName, 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (targetMethod != null)
        {
            MelonLogger.Msg($"[SUCCESS] {methodName} 메서드 발견!");
            break;
        }
    }
    
    if (targetMethod == null)
    {
        MelonLogger.Error("Judge 메서드를 찾을 수 없음");
        return;
    }
    
    // 패칭 진행
    harmony.Patch(targetMethod, ...);
}
```

### 2.2 "Prefix/Postfix가 호출되지 않음"

**증상**: 메서드는 찾았지만 후킹이 작동하지 않음

**원인**:

```csharp
// ❌ 잘못된 메서드 시그니처
[HarmonyPrefix]
public static void OnJudgeMentPrefix()  // 파라미터 없음!
{
    // ...
}

// ✅ 올바른 시그니처 (Harmony 호환)
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object __instance, object[] __args)
{
    // ...
}

// 또는 구체적인 파라미터
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object __instance, OnJudgeParam onJudgeParam)
{
    // ...
}
```

**확인 방법**:

```csharp
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    MelonLogger.Msg("[SUCCESS] Prefix 호출됨!");  // 이 로그가 나오나?
    
    if (__args == null || __args.Length == 0)
    {
        MelonLogger.Warning("인자 없음");
        return;
    }
    
    MelonLogger.Msg($"인자 개수: {__args.Length}");
    foreach (var i = 0; i < __args.Length; i++)
        MelonLogger.Msg($"  [{i}] {__args[i]?.GetType().Name ?? "null"}");
}
```

## 3) 판정 조작 관련 문제

### 3.1 "조작했는데 판정이 안 바뀜"

**증상**: Perfect 강제 설정 후에도 게임에서 다른 판정 표시

**원인 분석**:

```
[원인 체크리스트]
□ Prefix에서 필드를 찾을 수 없음
□ Enum 매칭 실패 (잘못된 값 주입)
□ 필드가 읽기 전용(readonly)
□ Postfix가 값을 다시 덮어씀
□ 게임이 다른 판정 소스를 우선함
```

**디버깅 단계**:

```csharp
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    var onJudgeParam = FindOnJudgeParam(__args);
    if (onJudgeParam == null)
    {
        MelonLogger.Error("[DEBUG] OnJudgeParam 찾을 수 없음");
        return;
    }
    
    var paramType = onJudgeParam.GetType();
    MelonLogger.Msg($"[DEBUG] OnJudgeParam 타입: {paramType.FullName}");
    
    // 1단계: 모든 필드 나열
    var fields = paramType.GetFields(BindingFlags.Public | BindingFlags.NonPublic);
    MelonLogger.Msg($"[DEBUG] 필드 개수: {fields.Length}");
    foreach (var field in fields)
    {
        var value = field.GetValue(onJudgeParam);
        MelonLogger.Msg($"  - {field.Name} ({field.FieldType.Name}): {value}");
    }
    
    // 2단계: 판정 필드 찾기
    var judgeFields = fields.Where(f => 
        f.Name.ToLower().Contains("judge") || 
        f.Name.ToLower().Contains("grade") ||
        f.Name.ToLower().Contains("type")
    ).ToList();
    
    MelonLogger.Msg($"[DEBUG] 판정 관련 필드:");
    foreach (var field in judgeFields)
    {
        var value = field.GetValue(onJudgeParam);
        MelonLogger.Msg($"  - {field.Name}: {value} ({field.FieldType.Name})");
        
        // 3단계: 값 변경 시도
        if (field.FieldType.IsEnum)
        {
            try
            {
                var enumValues = Enum.GetValues(field.FieldType);
                var firstValue = enumValues.GetValue(0);
                field.SetValue(onJudgeParam, firstValue);
                
                var newValue = field.GetValue(onJudgeParam);
                MelonLogger.Msg($"  ✓ 설정 후: {newValue}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  ✗ 설정 실패: {ex.Message}");
            }
        }
    }
}
```

### 3.2 "Enum 값 일치 실패"

**증상**: 필드는 찾았지만 Enum 값을 설정할 수 없음

**원인**:

```csharp
// 게임 Enum 값이 예상과 다름
// 게임: enum Judge { Perfect=10, Great=20, Good=30, Miss=40 }
// 모드: enum JudgeGrade { Perfect=0, Great=1, Good=2, Miss=3 }

// ❌ 이름 기반 매칭만으로는 부족
var targetValue = Enum.Parse(gameEnumType, "Perfect");  // 10일 수도?
```

**해결책**:

```csharp
public static object FindMatchingEnumValue(Type enumType, JudgeGrade target)
{
    var targetName = target.ToString();
    var names = Enum.GetNames(enumType);
    
    MelonLogger.Msg($"[DEBUG] Enum 타입: {enumType.FullName}");
    MelonLogger.Msg($"[DEBUG] 타겟: {targetName}, 찾을 이름들: {string.Join(", ", names)}");
    
    // 전략 1: 정확 일치
    var exactMatch = names.FirstOrDefault(n => 
        n.Equals(targetName, StringComparison.OrdinalIgnoreCase));
    
    if (exactMatch != null)
    {
        var value = Enum.Parse(enumType, exactMatch);
        MelonLogger.Msg($"[DEBUG] 정확 일치: {exactMatch} = {value}");
        return value;
    }
    
    // 전략 2: 부분 일치
    var partialMatches = names.Where(n => 
        n.Contains(targetName, StringComparison.OrdinalIgnoreCase) ||
        n.Contains(targetName, StringComparison.OrdinalIgnoreCase)
    ).ToList();
    
    if (partialMatches.Count == 1)
    {
        var value = Enum.Parse(enumType, partialMatches[0]);
        MelonLogger.Msg($"[DEBUG] 부분 일치: {partialMatches[0]} = {value}");
        return value;
    }
    
    // 전략 3: 위치 기반 (Perfect=첫 번째, Great=두 번째, ...)
    var targetIndex = (int)target;
    if (targetIndex < names.Length)
    {
        var value = Enum.Parse(enumType, names[targetIndex]);
        MelonLogger.Msg($"[DEBUG] 위치 기반: {names[targetIndex]} (index {targetIndex}) = {value}");
        return value;
    }
    
    MelonLogger.Error($"[DEBUG] 일치하는 Enum 값 없음!");
    return null;
}
```

## 4) 통계 관련 문제

### 4.1 "통계가 누적되지 않음"

**증상**: `GetStatistics().GetTotalCount()` = 0

**원인**:

```
□ Postfix가 호출되지 않음 (→ 2.2 참조)
□ IsNumJudgment() 때문에 모두 스킵
□ 통계 누적 로직 오류
□ 통계 초기화 실수
```

**확인 코드**:

```csharp
[HarmonyPostfix]
public static void OnJudgeMentPostfix(object onJudgeParam)
{
    MelonLogger.Msg("[DEBUG] Postfix 호출 - 통계 추가 중...");
    
    if (IsNumJudgment(onJudgeParam))
    {
        MelonLogger.Msg("[DEBUG] NUM 판정 - 스킵");
        return;
    }
    
    try
    {
        var result = ExtractJudgeResult(onJudgeParam);
        if (result == null)
        {
            MelonLogger.Warning("[DEBUG] JudgeResult null");
            return;
        }
        
        GRCJudgeMod.GetStatistics().AddJudge(result);
        
        var stats = GRCJudgeMod.GetStatistics();
        MelonLogger.Msg($"[DEBUG] 통계 추가됨 - 총 {stats.GetTotalCount()}개");
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"[DEBUG] 통계 추가 실패: {ex.Message}\n{ex.StackTrace}");
    }
}
```

### 4.2 "통계 집계 오류"

**증상**: GetAccuracy()가 NaN 또는 무한대

**해결책**:

```csharp
public double GetAccuracy()
{
    int total = GetTotalCount();
    
    // ❌ 위험: division by zero
    return (perfectCount + greatCount) / (double)total * 100;
    
    // ✅ 안전
    if (total == 0)
        return 0.0;
    
    return (perfectCount + greatCount) / (double)total * 100;
}
```

## 5) 성능 문제

### 5.1 "프레임 드롭 발생"

**증상**: 게임 FPS 급락 (60→20)

**원인 분석**:

```
우선순위별 원인:
1. Reflection 반복 호출 (Assembly, Type, Field 매번)
2. 과도한 로깅 (판정마다 로그 출력)
3. 문자열 연산 (매번 ToLower() 호출)
4. GC 압력 증가 (과도한 객체 할당)
```

**해결책** → [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md) 참조

### 5.2 "메모리 누수"

**증상**: 게임 진행할수록 메모리 사용량 증가

**디버깅**:

```csharp
private static class MemoryMonitor
{
    private static long lastMemory = 0;
    private static int callCount = 0;
    
    [HarmonyPostfix]
    public static void OnJudgeMentPostfix(object onJudgeParam)
    {
        callCount++;
        
        // 1000번마다 메모리 확인
        if (callCount % 1000 == 0)
        {
            var currentMemory = GC.GetTotalMemory(false);
            var delta = currentMemory - lastMemory;
            
            MelonLogger.Msg($"[MEMORY] {currentMemory / 1024 / 1024}MB " +
                $"(+{delta / 1024}KB since last check)");
            
            lastMemory = currentMemory;
        }
    }
}
```

**흔한 원인 및 해결책**:

| 원인 | 해결책 |
|------|--------|
| 캐시 무한 증가 | 캐시 크기 제한, 정기적 정리 |
| 이벤트 리스너 미제거 | `OnUnload()`에서 정리 |
| 문자열 누적 | `logBuffer`에 크기 제한 |
| 객체 참조 순환 | 약한 참조(WeakReference) 사용 |

## 6) 게임 버전 호환성

### 6.1 "버전 업데이트 후 모드 작동 안 함"

**대응 전략**:

```csharp
// 1단계: 게임 버전 감지
private static string DetectGameVersion()
{
    return UnityEngine.Application.version;  // "1.2.3"
}

// 2단계: 버전별 설정 분리
private static Dictionary<string, GameConfig> versionConfigs = new()
{
    ["1.0"] = new GameConfig 
    { 
        TargetTypeName = "IntiCreates.cNotecWorkBase",
        TargetMethod = "onJudgeMent",
        JudgeFieldName = "judgeType"
    },
    ["1.1"] = new GameConfig 
    { 
        TargetTypeName = "IntiCreates.Note.JudgeHandler",
        TargetMethod = "OnJudge",
        JudgeFieldName = "grade"
    }
};

// 3단계: 버전 기반 로드
public static GameConfig GetConfig()
{
    var version = DetectGameVersion();
    var majorVersion = version.Substring(0, version.LastIndexOf('.'));
    
    if (versionConfigs.TryGetValue(majorVersion, out var config))
        return config;
    
    MelonLogger.Warning($"알 수 없는 버전: {version}");
    return versionConfigs.Values.Last();  // 최신 버전으로 폴백
}
```

## 7) 로그 분석 가이드

### 7.1 정상 로그 시그니처

```
[정상 작동 시나리오]

1. 모드 초기화:
   ✓ GRC judge Mod v1.0.0 loaded
   ✓ Assembly-CSharp found
   ✓ Harmony patch applied successfully

2. 게임 플레이:
   ✓ Judge: Perfect (확인된 사항)
   ✓ [STATS] 100 judges processed, accuracy: 95.5%

3. 모드 언로드:
   ✓ GRC judge Mod unloading...
```

### 7.2 오류 로그 해석

| 오류 메시지 | 의미 | 해결책 |
|------------|------|--------|
| "Assembly-CSharp not found" | 게임 씬 로드 전 | 기다림 또는 재시작 |
| "Type not found" | 버전 불일치 | 게임 버전 확인 |
| "Method not found" | 메서드 이름 변경 | 수동으로 찾고 설정 |
| "Field access failed" | 권한 부족 | `BindingFlags.NonPublic` 추가 |
| "Enum parse error" | 이름 불일치 | enum 값 확인 후 매핑 |

---

**일반적인 흐름**: 로그 확인 → 원인 파악 → [ARCHITECTURE.md](ARCHITECTURE.md) 검토 → 수정 → 재테스트
