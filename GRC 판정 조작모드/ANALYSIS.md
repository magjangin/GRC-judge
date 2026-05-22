# GRC judge 분석 문서

이 문서는 `GRC judge` 모드의 내부 동작을 **코드 기준으로 상세 분석**한 문서입니다.

## 0) 로컬 모드(Local Mode) 개요

### 로컬 모드란?

이 프로젝트는 **로컬 모드**를 기반으로 작동합니다. 로컬 모드는 다음을 의미합니다:

- **로컬 실행 환경**: MelonLoader를 통해 **게임 프로세스 내에서 직접 실행**되는 모드 형식
  - 온라인/클라우드 기반이 아닌, 사용자의 로컬 PC에서 동작
  - 게임 실행 파일과 동일 프로세스 메모리 공간에서 작동
- **직접 접근성**: 게임의 어셈블리, 메모리, 런타임 상태에 **직접 접근 가능**
  - Harmony를 통해 게임 메서드를 Hooking/Patching
  - Reflection으로 게임 객체의 필드/프로퍼티/메서드 조회 및 조작
  - 게임 씬, 객체 인스턴스에 대한 즉각적인 제어 가능
- **지속성과 실시간성**
  - 모드가 게임 런타임 내에서 항상 활성화되어 동작
  - 게임 이벤트(판정, 씬 전환 등)에 **즉시 반응** 가능
  - 게임 재시작 없이 설정/기능 변경 불가능 (재시작 필요)

### 로컬 모드의 장점

- **성능**: 네트워크 오버헤드 없음 → 지연 최소화
- **신뢰성**: 게임 프로세스가 보유한 정보를 직접 읽음 → 데이터 신뢰도 높음
- **자유도**: Reflection/Harmony의 범위 내에서 자유로운 패치/조작 가능

### 로컬 모드의 제약

- **포타빌리티**: MelonLoader 설치 필요 (온라인 플레이 불가능한 경우가 많음)
- **안정성**: 게임 버전/어셈블리 구조 변경 시 mod 호환성 깨짐
- **접근 제한**: 운영진의 정책/보안에 따라 mod 사용 불가능할 수 있음

## 1) 모드 개요

- **플랫폼/로더**: MelonLoader
- **패치 프레임워크**: Harmony
- **대상 게임**: `INTI CREATES / GUNVOLT RECORDS Cychronicle`
- **실행 모드**: 로컬 모드 (게임 프로세스 내 직접 실행)
- **핵심 목표**
  - 게임의 “판정 처리” 경로를 후킹해 판정 정보를 관찰(통계 수집)
  - 필요 시 판정 결과를 강제로 특정 값(기본: `Perfect`)으로 치환

## 2) 엔트리 포인트 및 초기화 흐름

### 2.1 MelonLoader 엔트리

- 어셈블리 속성:
  - `Properties/AssemblyInfo.cs`에 `MelonInfo`, `MelonGame`이 선언되어 로더가 게임/모드 매칭을 수행합니다.

### 2.2 초기화: `GRCJudgeMod.OnInitializeMelon()`

초기화에서 수행하는 것:

- MelonPreferences 설정 생성
  - 카테고리: `GRCJudge`
  - 엔트리: `debugmod` (bool)
- Harmony 인스턴스 생성: `"GRC.judge"`
- **게임 어셈블리(`Assembly-CSharp`) 로딩 대기 코루틴** 시작
- **판정 조작 자동 활성화**
  - `ForceAllPerfect()` 호출 → 내부적으로 `JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Perfect)`

중요 포인트:

- 이 모드는 “관찰”만 하는 것이 아니라, 초기화 시점에 **기본적으로 치환(Perfect 강제)** 기능이 켜집니다.

## 3) 후킹(패치) 지점

### 3.1 대상 어셈블리

로컬 모드에서는 **런타임 중 로드된 어셈블리**를 타겟합니다.

- `AppDomain.CurrentDomain.GetAssemblies()`에서 이름이 `Assembly-CSharp`인 어셈블리를 찾아 사용합니다.
  - `Assembly-CSharp.dll`은 Unity 게임의 C# 스크립트를 IL2CPP로 컴파일한 결과물
  - 게임이 실행되면서 메모리에 로드됨
- 어셈블리 탐색이 반복적으로 수행될 수 있으므로, **캐싱** 권장 (현재 코드 최적화 포인트)

### 3.2 대상 타입/메서드

Harmony Patching의 핵심 대상:

- **타입**: `IntiCreates.cNotecWorkBase`
  - 노트(Note) 처리의 기본 클래스로 추정
  - 모든 노트 판정은 이 클래스의 메서드를 거칠 가능성 높음
- **메서드**: `onJudgeMent` (instance, public/non-public 모두 탐색)
  - 판정 처리의 중심 메서드
  - Postfix 훅을 통해 판정 정보 관찰
  - Prefix 훅을 통해 판정 값 조작

### 3.3 Harmony Patching 메커니즘

```csharp
// 예상 패칭 코드 구조
var targetType = assembly.GetType("IntiCreates.cNotecWorkBase");
var targetMethod = targetType.GetMethod("onJudgeMent", 
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

harmony.Patch(
    targetMethod,
    prefix: new HarmonyMethod(typeof(JudgeHooks), nameof(JudgeHooks.OnJudgeMentPrefix)),
    postfix: new HarmonyMethod(typeof(JudgeHooks), nameof(JudgeHooks.OnJudgeMentPostfix))
);
```

**Prefix**의 역할:
- 원본 메서드 **실행 전** 호출
- 판정 값을 미리 조작하거나 조건에 따라 **실행 스킵** 가능 (`__runOriginal = false`)

**Postfix**의 역할:
- 원본 메서드 **실행 후** 호출
- 최종 판정 결과를 관찰/기록 가능
- 반환값 수정 불가능 (읽기 전용)

패치 방식:

- Prefix: `JudgeHooks.OnJudgeMentPrefix`
- Postfix: `JudgeHooks.OnJudgeMentPostfix`

설계 의도:

- `onJudgeMent`는 노트 판정 처리의 중심에 있을 가능성이 높고,
- 파라미터로 `OnJudgeParam`(또는 유사 구조체/클래스)이 전달될 것으로 가정하고 이를 조작/관찰합니다.

## 4) 판정 조작(Perfect 강제) 메커니즘

### 4.1 조작 스위치

- `JudgeHooks` 내부 정적 상태:
  - `enableJudgeManipulation` (bool)
  - `forcedJudgeGrade` (`JudgeGrade?`)

설정 API:

- `JudgeHooks.SetJudgeManipulation(bool enable, JudgeGrade? forcedGrade = null)`

### 4.2 조작 대상 제외: `num` 판정 스킵

판정 조작은 모든 판정에 적용되지 않습니다.

- `IsNumJudgment(onJudgeParam, onJudgeParamType)`가 `true`면 조작/모니터링을 건너뜁니다.
- 구현은 `OnJudgeParam` 객체에서 다음 후보 필드/프로퍼티를 탐색해 문자열에 `NUM`이 포함되는지 검사합니다:
  - `noteType`, `noteTypeId`, `type` 계열(대소문자/언더스코어 변형 포함)

의미(추정):

- “숫자 노트/숫자 판정”처럼 일반 판정과 다른 체계로 처리되는 케이스가 있어 예외 처리한 것으로 보입니다.

### 4.3 Prefix 조작: `OnJudgeMentPrefix`

전략:

1. `__args`에서 `OnJudgeParam`로 보이는 인자를 찾습니다.
   - 조건: `Name == "OnJudgeParam"`, `FullName`에 `"OnJudgeParam"` 포함, 또는 `Name`에 `"Judge"`와 `"Param"`이 포함
2. `num` 판정이면 즉시 리턴
3. `OnJudgeParam` 내부의 “판정 타입/결과”로 보이는 필드/프로퍼티를 폭넓게 탐색하여 강제 판정 값을 주입합니다.

탐색 후보(필드/프로퍼티 이름):

- `judgeType`, `type`, `result`, `judge`, `grade`, `judgeGrade`, `judgeResult` 등 다양한 변형

주입 방식:

- 대상이 **Enum**이면:
  - 강제 등급 문자열(예: `PERFECT`)과의 일치/부분일치로 enum 값을 찾아 세팅
  - `JUDGE_PERFECT`, `TYPE_PERFECT`, `E_PERFECT` 등 가능한 패턴까지 포함해 매칭 폭을 넓힘
- 대상이 **int**이면:
  - 내부 매핑을 통해 주입  
    - `Perfect=0`, `Great=1`, `Good=2`, `Miss=3`

의미:

- 게임 내부 판정 enum/정수 표현이 프로젝트의 `JudgeGrade`와 정확히 동일하지 않을 수 있어,
  - “이름 기반 매칭” + “부분일치” + “정수 매핑”의 3중 방어로 강제 판정을 시도합니다.

### 4.4 Postfix 조작: `OnJudgeMentPostfix`

Postfix에서도 두 가지를 합니다:

- **(A) 재조작**: Prefix에서 실패했을 경우를 대비해, 다시 한 번 `OnJudgeParam`의 모든 enum 필드를 훑으며 `PERFECT`(또는 강제값)과 유사한 enum을 찾아 세팅
- **(B) 모니터링**: `OnJudgeParam.judgeType`에서 판정 등급을 추출해 통계에 누적

## 5) 판정 모니터링(통계 누적) 구조

### 5.1 데이터 모델

- `JudgeGrade` (`Perfect/Great/Good/Miss`)
- `JudgeResult`
  - `Grade`
  - `TimeDifference` (샘플 단위 차이, 절대값)
  - `IsEarly` (일찍 눌렀는지)
- `JudgeStatistics`
  - `Dictionary<JudgeGrade, int> counts`
  - `AddJudge`, `GetCount`, `GetTotalCount`
  - `GetAccuracy()` = (Perfect + Great) / Total * 100

### 5.2 누적 흐름

- `GRCJudgeMod`가 정적 `JudgeStatistics`를 보유
  - `GRCJudgeMod.GetStatistics()`로 접근
- `JudgeHooks.OnJudgeMentPostfix`에서 판정을 파싱하면:
  - `new JudgeResult(...)`
  - `GRCJudgeMod.GetStatistics().AddJudge(result)`

현재 코드에서 시간/early 정보:

- `OnJudgeMentPostfix`는 `OnJudgeParam` 내부의 필드명을 훑어
  - `time/diff/delta` 키워드가 있으면 `timeDifference` 후보로 사용
  - `early/before` 키워드가 있으면 `isEarly` 후보로 사용
- 단, 필드명/타입이 게임 빌드마다 달라질 수 있어 “best-effort” 추출입니다.

## 6) 오토플레이(Reflection) 제어

### 6.1 인스턴스 탐색

- 씬 초기화 시점(`OnSceneWasInitialized`)에
  - 씬 이름에 `FairyMode`, `PlayMovie`, `Play`가 포함되면
  - `IntiCreates.cFairyModeNotesManager` 타입을 얻고
  - `UnityEngine.Object.FindObjectOfType<T>()`를 리플렉션으로 호출해 인스턴스를 찾습니다.

### 6.2 제어/조회

- 활성/비활성:
  - `setIsAutoPlay(bool)` 메서드를 리플렉션으로 찾아 호출
- 상태 조회:
  - `mIsCurrentAutoPlay` 필드를 리플렉션으로 조회

현상태 한계:

- API는 있지만, 현재 프로젝트에는 이를 호출하는 **핫키/커맨드/UI가 구현되어 있지 않음**

## 7) 로깅/디버그 모드 관찰 포인트

- `debugmod` 설정이 존재하지만,
  - `JudgeHooks.OnJudgeMentPostfix`는 일부 로그를 `DebugMod`로 제어합니다.
- 반면 `JudgeHooks.JudgePostfix`는 항상 `MelonLogger.Msg(...)`로 판정 로그를 찍습니다.

해석:

- 과거에 범용 후킹(`JudgePrefix/JudgePostfix`)을 쓰다가 `onJudgeMent` 전용(`OnJudgeMentPrefix/OnJudgeMentPostfix`)으로 옮기는 과정에서 로그 정책이 일관되지 않게 남은 흔적일 가능성이 있습니다.
- 실제로 현재 `GRCJudgeMod`는 `JudgePrefix/JudgePostfix`를 패치에 사용하지 않고 `OnJudgeMentPrefix/OnJudgeMentPostfix`만 패치합니다.

## 8) 깨질 수 있는 지점(리스크)

- **타입/메서드명 변경**: `IntiCreates.cNotecWorkBase.onJudgeMent`가 이름 변경/시그니처 변경되면 후킹 실패
- **OnJudgeParam 구조 변경**:
  - `judgeType` 필드가 없어지거나 다른 이름이면 추출 실패
  - enum 값 이름이 `PERFECT`와 전혀 다르면 이름 매칭 실패
- **정수 매핑 불일치**:
  - `Perfect=0` 등 매핑이 게임과 다르면 잘못된 치환 가능
- **성능/로그 과다**:
  - 판정은 매우 자주 발생하므로 로그가 과도하면 프레임 드랍/로그 파일 비대화 가능

## 9) 개선 제안(향후 작업 아이디어)

- **토글 기능 제공**
  - Perfect 강제 on/off, 강제 등급 선택(Perfect/Great/Good/Miss), 오토플레이 토글을 핫키나 콘솔 커맨드로 연결
- **로그 정책 통일**
  - 모든 판정 로그를 `debugmod`로 게이트하거나 rate-limit
- **판정 추출 안정화**
  - `OnJudgeParam`의 실제 필드명을 한 번 탐색/캐시하여 매번 전수조사 비용을 줄이기
  - 게임 버전별 매핑 테이블(타입명/필드명/enum명)을 분리해 유지보수 용이하게
- **치환 범위 제어**
  - `num` 외에도 특정 노트 타입/상황(예: 롱노트 틱 등)을 제외할 수 있도록 설정화

## 10) 참고 파일 맵

- 모드 엔트리/초기화: `GRC judge/GRCJudgeMod.cs`
- 후킹/조작/파싱 로직: `GRC judge/JudgeHooks.cs`
- 데이터 모델: `GRC judge/JudgeGrade.cs`, `GRC judge/JudgeResult.cs`, `GRC judge/JudgeStatistics.cs`
- MelonLoader 어셈블리 속성: `GRC judge/Properties/AssemblyInfo.cs`

## 11) 로컬 모드의 데이터 흐름

### 11.1 게임 실행 → 모드 초기화 → 후킹 적용

```
[게임 시작]
    ↓
[MelonLoader 초기화]
    ↓
[GRCJudgeMod.OnInitializeMelon() 호출]
    ├─ MelonPreferences 생성
    ├─ Harmony 인스턴스 생성
    └─ Assembly-CSharp 로드 대기 코루틴 시작
         ↓
    [Assembly-CSharp 로드됨]
         ↓
    [Harmony Patch 적용]
         └─ onJudgeMent에 Prefix/Postfix 후킹
              ↓
    [게임 플레이 시작]
```

### 11.2 게임 플레이 중 판정 처리

```
[플레이어가 노트를 판정]
    ↓
[onJudgeMent() 메서드 호출]
    ↓
[Prefix Hook 실행]
    ├─ OnJudgeParam 찾기
    ├─ NUM 판정 제외 확인
    └─ 조작 활성화 시 → 판정 값 강제 주입
         ↓
[원본 onJudgeMent() 실행]
    └─ 게임 로직이 (강제된) 판정 값으로 점수/이펙트 계산
         ↓
[Postfix Hook 실행]
    ├─ 재조작 (Fallback)
    └─ 판정 통계 누적
         ↓
[판정 결과 화면 출력]
```

### 11.3 로컬 모드의 장점과 단점 요약

**장점**:
- **실시간 반응**: 판정 직후 즉시 값 조작 → 게임 로직에 반영
- **통계 신뢰도**: 게임 메모리에서 직접 추출 → 정확한 데이터
- **자유도**: Reflection/Harmony로 필요한 거의 모든 요소 접근 가능
- **오프라인 가능**: 네트워크 없이 독립적으로 작동

**단점**:
- **안정성 취약**: 게임 업데이트로 필드명/메서드명 변경 시 모드 동작 불가
- **포타빌리티 제한**: MelonLoader 필요 (온라인 모드와 병행 불가 가능성)
- **정책 위반 가능**: 게임 운영사가 mod 사용을 금지할 수 있음
- **성능 오버헤드**: Reflection과 로그 처리로 프레임 저하 가능성

## 12) 로컬 모드 디버깅 및 로깅 전략

### 12.1 MelonLogger 활용

```csharp
// 기본 로그
MelonLogger.Msg("판정 감지: " + judgeGrade);

// 경고/에러
MelonLogger.Warning("조작 대상 필드를 찾을 수 없음");
MelonLogger.Error("OnJudgeParam 파싱 실패");
```

로컬 모드에서는 이 로그들이 **콘솔** 및 **MelonLoader 로그 파일**(`MelonLoader.log`)에 기록됩니다.

### 12.2 debugmod 설정 활용

```csharp
if (debugmod.Value)  // MelonPreferences에서 읽은 bool
{
    // 상세 디버그 로그 출력
    MelonLogger.Msg($"[DEBUG] OnJudgeParam 구조: {onJudgeParamType.FullName}");
    MelonLogger.Msg($"[DEBUG] 필드 목록: {string.Join(", ", fieldNames)}");
}
```

사용자가 MelonLoader 설정에서 `debugmod`를 켜면 상세 정보 출력.

### 12.3 로그 과다 문제 대응

로컬 모드는 게임 성능에 영향을 줄 수 있으므로:

- **판정마다 로그하지 않기**: 필요시에만 선택적 로그
- **rate-limiting**: 일정 프레임 간격으로 1회만 로그
- **요약 통계**: 라운드 끝나고 전체 통계만 출력

```csharp
// 예시: 100번마다 1번만 로그
if (judgeCount % 100 == 0)
    MelonLogger.Msg($"처리된 판정: {judgeCount}");
```

## 13) 타입/메서드 탐색의 안정성

### 13.1 Reflection 기반 탐색의 한계

로컬 모드는 Reflection으로 런타임 타입을 탐색합니다:

```csharp
var targetType = assembly.GetType("IntiCreates.cNotecWorkBase");
if (targetType == null)
{
    MelonLogger.Error("타입을 찾을 수 없음: 게임 버전이 변경되었을 가능성");
    return;
}
```

**깨지는 시나리오**:
- 게임 업데이트에서 `IntiCreates.cNotecWorkBase` 클래스 이름 변경
- `onJudgeMent` 메서드 제거 또는 이름 변경
- 메서드 시그니처 변경 (파라미터 추가/제거)

### 13.2 필드명 변경에 대한 대응

현재 코드는 **광범위한 필드 이름 탐색**으로 일부 대응합니다:

```csharp
// 탐색 후보: judgeType, type, result, judge, grade, judgeGrade, ...
foreach (var field in fields)
{
    if (field.Name.ToLower().Contains("judge") && 
        field.Name.ToLower().Contains("type"))
    {
        // 이 필드를 판정 대상으로 추정
        targetField = field;
        break;
    }
}
```

하지만 이는 **완전한 솔루션이 아니며**, 게임 아키텍처 대변화 시에는 한계가 있습니다.

### 13.3 버전 관리 권장

게임과 모드 버전 매핑을 관리하는 것이 좋습니다:

```
GRC judge Mod v1.0 → GUNVOLT RECORDS Cychronicle v1.0~1.2 호환
GRC judge Mod v2.0 → GUNVOLT RECORDS Cychronicle v1.3+ 호환
```

## 14) 로컬 모드 구현 시 주의점

### 14.1 메모리 안정성

- **Reflection 반복 호출**: 매 판정마다 필드를 탐색하면 GC 압력 증가 → **캐싱 필수**
- **필드 접근 권한**: private 필드는 `BindingFlags.NonPublic` 필요
- **인스턴스 생명주기**: 객체가 GC되면 참조 무효화 → 항상 null 체크

### 14.2 동시성/재진입(Reentrancy)

- Harmony Postfix에서 다른 Harmony Patch를 호출하면 문제 가능
- 판정 처리 중 게임 API 호출 시 안정성 고려

```csharp
// 위험: 무한 루프 가능
[HarmonyPostfix]
public static void Postfix(OnJudgeParam param)
{
    if (param.judgeType != JudgeGrade.Perfect)
    {
        param.judgeType = JudgeGrade.Perfect;  // → 다시 onJudgeMent 호출?
    }
}
```

### 14.3 예외 처리

Prefix/Postfix에서 발생한 예외는 게임 전체를 터뜨릴 수 있으므로 반드시 try-catch:

```csharp
[HarmonyPrefix]
public static void OnJudgeMentPrefix(object __instance, object[] __args)
{
    try
    {
        // 후킹 로직
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"후킹 중 오류: {ex.Message}\n{ex.StackTrace}");
    }
}
```

## 15) 로컬 모드 테스트 및 검증 전략

### 15.1 기능 테스트

**판정 조작 검증**:
- Perfect 강제 후 게임에서 "Perfect" 표시 확인
- 점수 계산이 올바른지 확인
- NUM 판정이 제외되는지 확인

**통계 수집 검증**:
- 라운드 종료 후 `GetStatistics()`로 통계 확인
- 손으로 센 판정 수와 일치하는지 확인

### 15.2 안정성 테스트

- **장시간 플레이**: 메모리 누수, GC 압력 모니터링
- **버전 호환성**: 게임 업데이트 후 모드 동작 확인
- **예외 처리**: 비정상 상황(null 필드, 예상치 못한 enum 값 등) 시뮬레이션

### 15.3 로그 분석

MelonLoader 로그에서 다음을 확인:

```
[로그 분석 체크리스트]
- onJudgeMent 호킹 성공 여부
- OnJudgeParam 파싱 성공 여부
- 필드/프로퍼티 탐색 결과
- 강제된 판정 값
- 통계 누적 확인
- 오류/예외 발생
```

---

이 문서는 **로컬 모드** 기반 GRC judge 모드의 기본 구조를 설명하는 핵심 분석 문서입니다. 더 상세한 내용은 다음 문서들을 참고하세요:

- [ARCHITECTURE.md](ARCHITECTURE.md) - 아키텍처 상세 분석
- [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md) - 성능 최적화 기법
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - 트러블슈팅 및 디버깅
- [API_REFERENCE.md](API_REFERENCE.md) - API 레퍼런스
- [ADVANCED_PATTERNS.md](ADVANCED_PATTERNS.md) - 고급 패턴 및 확장성
- [STATISTICS_GUIDE.md](STATISTICS_GUIDE.md) - 통계 분석 가이드
- [ROADMAP.md](ROADMAP.md) - 개선 로드맵 및 향후 방향


