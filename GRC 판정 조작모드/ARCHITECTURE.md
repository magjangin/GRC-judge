# GRC Judge Mod - 아키텍처 상세 분석

이 문서는 GRC Judge 모드의 **시스템 아키텍처, 구성요소, 상호작용**을 상세히 분석합니다.

## 1) 시스템 레이어 아키텍처

```
┌─────────────────────────────────────┐
│   MelonLoader Runtime Layer         │
│   (게임 프로세스 내 실행)            │
└──────────────────┬──────────────────┘
                   │
┌──────────────────▼──────────────────┐
│   GRC Judge Mod Layer               │
│  ┌──────────────┐   ┌─────────────┐ │
│  │ GRCJudgeMod  │   │ JudgeHooks  │ │
│  │(초기화/통계)  │   │(후킹/조작)   │ │
│  └──────────────┘   └─────────────┘ │
└──────────────────┬──────────────────┘
                   │
┌──────────────────▼──────────────────┐
│   Harmony Patching Layer            │
│   (메서드 Prefix/Postfix)            │
└──────────────────┬──────────────────┘
                   │
┌──────────────────▼──────────────────┐
│   Assembly-CSharp Layer             │
│   (게임 어셈블리 런타임)             │
│  ┌──────────────┐   ┌─────────────┐ │
│  │cNotecWorkBase│   │OnJudgeParam │ │
│  │(노트 판정)    │   │(판정 데이터) │ │
│  └──────────────┘   └─────────────┘ │
└─────────────────────────────────────┘
```

## 2) 핵심 컴포넌트 상세

### 2.1 GRCJudgeMod (엔트리 포인트)

**책임**:
- MelonLoader 초기화 훅 처리
- 정적 상태 관리 (통계, 설정)
- 라이프사이클 이벤트 처리

**상태 변수**:
```csharp
public class GRCJudgeMod : MelonMod
{
    private static JudgeStatistics statistics;
    private static bool initialized = false;
    private static Harmony harmony = null;
    
    public static JudgeStatistics GetStatistics() => statistics;
}
```

**초기화 순서**:
1. MelonPreferences 등록
2. Harmony 인스턴스 생성
3. 어셈블리 로드 대기 (코루틴)
4. 후킹 적용
5. 판정 조작 자동 활성화

### 2.2 JudgeHooks (후킹 중추)

**책임**:
- Harmony Prefix/Postfix 정의
- 판정 값 조작 로직
- 판정 통계 누적

**핵심 메서드**:
```csharp
public class JudgeHooks
{
    // 판정 조작 스위치
    private static bool enableJudgeManipulation = false;
    private static JudgeGrade? forcedJudgeGrade = null;
    
    // Prefix: 원본 메서드 실행 전
    [HarmonyPrefix]
    public static void OnJudgeMentPrefix(object[] __args) { }
    
    // Postfix: 원본 메서드 실행 후
    [HarmonyPostfix]
    public static void OnJudgeMentPostfix(object[] __args) { }
}
```

### 2.3 데이터 모델

#### JudgeGrade (판정 등급)
```csharp
public enum JudgeGrade
{
    Perfect = 0,
    Great = 1,
    Good = 2,
    Miss = 3
}
```

#### JudgeResult (판정 결과)
```csharp
public class JudgeResult
{
    public JudgeGrade Grade { get; set; }        // 판정 등급
    public int TimeDifference { get; set; }      // 시간 오차 (샘플)
    public bool IsEarly { get; set; }            // 일찍 눌렀는지
    public DateTime Timestamp { get; set; }      // 판정 시간
    public string NoteType { get; set; }         // 노트 타입
}
```

#### JudgeStatistics (판정 통계)
```csharp
public class JudgeStatistics
{
    private Dictionary<JudgeGrade, int> counts = new();
    
    public void AddJudge(JudgeResult result) { }
    public int GetCount(JudgeGrade grade) { }
    public int GetTotalCount() { }
    public double GetAccuracy() { }  // (Perfect+Great)/Total*100
    public void Reset() { }
}
```

## 3) 데이터 흐름 (Data Flow)

### 3.1 판정 처리 전체 흐름

```
[플레이어 입력]
      ↓
[게임: onJudgeMent() 호출 결정]
      ↓
[Harmony Prefix 실행]
├─ OnJudgeParam 찾기
├─ 조작 활성화 확인
└─ 판정 값 강제 주입 (if enabled)
      ↓
[원본 onJudgeMent() 실행]
└─ 게임 로직: 점수/이펙트/UI 처리
      ↓
[Harmony Postfix 실행]
├─ 재조작 (fallback)
└─ 통계 누적
      ↓
[판정 결과 표시]
```

### 3.2 메모리 접근 경로

```
게임 인스턴스
 └─ cNotecWorkBase (노트 처리 클래스)
     └─ onJudgeMent() 메서드 호출
         └─ OnJudgeParam 객체 (메서드 인자)
             ├─ judgeType (enum)      [조작 대상]
             ├─ type (int)             [조작 대상]
             ├─ timeDifference (int)   [추출 대상]
             └─ isEarly (bool)         [추출 대상]
```

### 3.3 리플렉션 탐색 순서

```csharp
1. Assembly-CSharp 로드 확인
2. IntiCreates.cNotecWorkBase 타입 찾기
3. onJudgeMent 메서드 탐색
   ├─ Public 메서드 시도
   └─ NonPublic 메서드 시도
4. 메서드 파라미터 분석
   └─ OnJudgeParam 인자 식별
5. OnJudgeParam 내부 필드 탐색
   ├─ judgeType 필드/프로퍼티 찾기
   ├─ timeDifference 필드 찾기
   └─ isEarly 필드 찾기
```

## 4) Harmony 패칭 메커니즘

### 4.1 패치 적용 프로세스

```csharp
// 1. 대상 메서드 획득
var targetMethod = targetType.GetMethod("onJudgeMent", flags);

// 2. 패치 메서드 정의
var prefixMethod = new HarmonyMethod(
    typeof(JudgeHooks), 
    nameof(JudgeHooks.OnJudgeMentPrefix)
);

var postfixMethod = new HarmonyMethod(
    typeof(JudgeHooks), 
    nameof(JudgeHooks.OnJudgeMentPostfix)
);

// 3. 패치 적용
harmony.Patch(
    targetMethod,
    prefix: prefixMethod,
    postfix: postfixMethod
);
```

### 4.2 Prefix 실행 컨텍스트

```
[Harmony가 원본 메서드 호출 전]
    ↓
[파라미터 캡처]
├─ __instance: 메서드 소유 객체
├─ __args: 모든 메서드 인자
├─ __originalMethod: 원본 메서드 정보
└─ __runOriginal: 원본 실행 여부 (기본=true)
    ↓
[JudgeHooks.OnJudgeMentPrefix() 실행]
    ↓
[return 값]
├─ false: 원본 메서드 스킵
└─ true: 원본 메서드 실행 (기본)
```

### 4.3 Postfix 실행 컨텍스트

```
[원본 메서드 완료 후]
    ↓
[파라미터 캡처]
├─ __instance: 메서드 소유 객체
├─ __args: 메서드 인자
└─ __return: 원본 반환값 (ref)
    ↓
[JudgeHooks.OnJudgeMentPostfix() 실행]
    ↓
[반환값 수정 불가능]
└─ __return은 읽기 전용
```

## 5) 필드 조작 알고리즘

### 5.1 판정 타입 필드 찾기

```csharp
public static FieldInfo FindJudgeTypeField(Type paramType)
{
    var fields = paramType.GetFields(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );
    
    foreach (var field in fields)
    {
        // 1. 이름 기반 매칭
        if (field.Name.ToLower().Contains("judge") && 
            field.Name.ToLower().Contains("type"))
            return field;
        
        // 2. 타입 기반 매칭
        if (field.FieldType.IsEnum || field.FieldType == typeof(int))
        {
            // Enum 값이 판정과 유사한지 확인
            if (IsJudgeGradeEnum(field.FieldType))
                return field;
        }
    }
    
    return null;
}
```

### 5.2 Enum 값 매칭 알고리즘

```csharp
public static object FindMatchingEnumValue(Type enumType, JudgeGrade targetGrade)
{
    var targetName = targetGrade.ToString().ToUpper();  // "PERFECT"
    var enumNames = Enum.GetNames(enumType);
    
    // 전략 1: 정확 일치
    var exactMatch = enumNames.FirstOrDefault(n => 
        n.Equals(targetName, StringComparison.OrdinalIgnoreCase));
    if (exactMatch != null)
        return Enum.Parse(enumType, exactMatch);
    
    // 전략 2: 부분 일치 (JUDGE_PERFECT, TYPE_PERFECT 등)
    var partialMatches = enumNames.Where(n => 
        n.Contains(targetName, StringComparison.OrdinalIgnoreCase)).ToList();
    
    if (partialMatches.Count == 1)
        return Enum.Parse(enumType, partialMatches[0]);
    
    // 전략 3: 첫 번째 후보 선택 (ambiguous)
    if (partialMatches.Count > 1)
        return Enum.Parse(enumType, partialMatches[0]);
    
    return null;  // 찾을 수 없음
}
```

### 5.3 정수 매핑

```csharp
public static int ConvertJudgeGradeToInt(JudgeGrade grade)
{
    return grade switch
    {
        JudgeGrade.Perfect => 0,
        JudgeGrade.Great => 1,
        JudgeGrade.Good => 2,
        JudgeGrade.Miss => 3,
        _ => -1
    };
}
```

## 6) 초기화 라이프사이클

### 6.1 시간 흐름

```
[t=0] 게임 시작
     └─ MelonLoader가 모드 로드
         └─ GRCJudgeMod.cs 로드
             └─ GRCJudgeMod 클래스 인스턴스화
                 └─ MelonMod.OnInitializeMelon() 호출

[t=1] MelonPreferences 등록
     └─ GRCJudge 카테고리
         └─ debugmod 엔트리

[t=2] Harmony 인스턴스 생성
     └─ ID: "GRC.judge"

[t=3] Assembly-CSharp 로드 대기 코루틴 시작
     └─ while (!AssemblyLoaded("Assembly-CSharp"))
         yield return null;

[t=4~200] 게임 씬 로드, Assembly-CSharp 메모리 올라옴
     └─ 코루틴 계속 확인

[t=201] Assembly-CSharp 로드 감지
     └─ 코루틴 종료
         └─ JudgeHooks.ApplyPatch(harmony) 호출
             └─ Harmony 패치 적용

[t=202] 판정 조작 자동 활성화
     └─ ForceAllPerfect() 호출
         └─ JudgeHooks.SetJudgeManipulation(true, Perfect)

[t=203~] 게임 정상 플레이
     └─ 판정마다 Prefix/Postfix 호출
```

## 7) 정적 상태 관리 (Static State)

### 7.1 전역 상태 변수

```csharp
// GRCJudgeMod
public static class GRCJudgeMod
{
    private static JudgeStatistics statistics;           // 전역 통계
    private static MelonPreferences_Entry<bool> debugmod; // 디버그 모드
    private static bool initialized = false;             // 초기화 완료 플래그
}

// JudgeHooks
public class JudgeHooks
{
    private static bool enableJudgeManipulation = false;        // 조작 활성화
    private static JudgeGrade? forcedJudgeGrade = null;        // 강제 등급
    private static Type cachedOnJudgeParamType = null;         // 캐시
    private static Dictionary<Type, FieldInfo[]> fieldCache;   // 필드 캐시
}
```

### 7.2 상태 동기화 이슈

**문제**: 멀티스레드 환경에서 정적 상태 접근

```csharp
// ❌ 안전하지 않음
private static JudgeStatistics statistics;
public static void AddJudge(JudgeResult result)
{
    statistics.AddJudge(result);  // Race condition 가능
}

// ✅ 안전
private static readonly object lockObject = new();
public static void AddJudge(JudgeResult result)
{
    lock (lockObject)
    {
        statistics.AddJudge(result);
    }
}
```

## 8) 오류 처리 전략

### 8.1 계층별 오류 처리

```
[Reflection 오류]
├─ Type not found
├─ Method not found
├─ Field not found
└─ → Fallback: 광범위 탐색 후속 시도

[Prefix 오류]
├─ ArgumentException (enum 값 없음)
├─ FieldAccessException (접근 불가)
└─ → 계속 진행 (원본 메서드 실행)

[Postfix 오류]
├─ Null reference
├─ Type cast error
└─ → 로그만 남김 (게임에 영향 없음)

[통계 누적 오류]
├─ Enum 파싱 실패
└─ → 미분류 카테고리에 추가
```

### 8.2 예외 처리 골격

```csharp
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object[] __args)
{
    try
    {
        var onJudgeParam = FindOnJudgeParam(__args);
        if (onJudgeParam == null) return;
        
        if (IsNumJudgment(onJudgeParam)) return;  // 특수 케이스 제외
        
        if (enableJudgeManipulation)
        {
            ManipulateJudgeType(onJudgeParam);
        }
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"Prefix 오류: {ex.Message}");
        // 게임 진행은 계속됨
    }
}
```

## 9) 성능 고려사항

### 9.1 반복 호출 비용

**판정 빈도**: 보통 곡당 100~500개 판정
→ Prefix/Postfix 총 1000회 이상 호출

**비용이 높은 작업**:
- ❌ `Type.GetFields()` 매번 호출 (엄청 비쌈)
- ❌ `Enum.Parse()` 매번 호출 (느림)
- ❌ 문자열 검색 매번 수행 (경합)

### 9.2 캐싱 전략

```csharp
private static Dictionary<Type, FieldInfo> fieldCache = new();

public static FieldInfo GetJudgeTypeField(Type paramType)
{
    // 1차: 캐시 확인
    if (fieldCache.TryGetValue(paramType, out var cached))
        return cached;
    
    // 2차: 탐색 (첫 호출에만 수행)
    var field = SearchForField(paramType, "judgeType");
    
    // 3차: 캐싱
    if (field != null)
        fieldCache[paramType] = field;
    
    return field;
}
```

### 9.3 메모리 임팩트

```
통계 저장소:
- JudgeStatistics: ~100 bytes (int 4개)
- 판정 100개: 100 * JudgeResult 크기
- JudgeResult: ~50 bytes
- 총 메모리: ~5KB (곡당)

캐시:
- fieldCache: Type → FieldInfo 딕셔너리
- 최대 ~1KB (게임당 1-2개 Type)

로그 버퍼:
- 파일 디스크: ~1MB/시간 (high verbosity)
```

---

이 아키텍처는 **안정성, 확장성, 성능**의 균형을 맞추려 설계되었습니다.
