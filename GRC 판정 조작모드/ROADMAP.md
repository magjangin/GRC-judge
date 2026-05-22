# GRC Judge Mod - 개선 로드맵 및 향후 방향

이 문서는 **프로젝트의 개발 로드맵, 기술 부채, 향후 계획**을 정리합니다.

## 1) 버전 히스토리

### v1.0.0 (현재)
- ✅ 기본 판정 후킹
- ✅ Perfect 강제
- ✅ 기본 통계 수집
- ✅ Harmony 기반 패칭
- ✅ MelonLoader 호환

**주요 한계**:
- ❌ 핫키/UI 없음
- ❌ 실시간 설정 변경 불가
- ❌ 로그 과다
- ❌ 캐싱 미흡

---

## 2) v1.1.0 (우선순위: HIGH)

### 2.1 예상 일정
- **계획**: 2026년 1월~2월
- **목표**: 성능 및 안정성 개선

### 2.2 핵심 기능

#### 2.2.1 Reflection 캐싱 강화

```csharp
// Before: 매 판정마다 Assembly 탐색
var assembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

// After: 첫 발견 시 캐시
private static Assembly cachedAssembly = null;

public static Assembly GetAssembly()
{
    return cachedAssembly ??= AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
}
```

**기대 효과**:
- 초당 처리량: 500 → 1000+ (2배)
- CPU 사용률: 15% → 5%
- FPS 개선: 50 → 55+

#### 2.2.2 로그 정책 통일

```csharp
// Before: 로그 흩어짐
MelonLogger.Msg("판정: " + grade);
DebugMod.Log("판정: " + grade);  // 다른 곳에서

// After: 통일된 인터페이스
Logger.Log(LogLevel.Info, "판정: " + grade);
Logger.Log(LogLevel.Debug, "상세 정보", debugmod: true);
```

#### 2.2.3 로그 버퍼링

```csharp
// 1000개 판정의 로그를 모아서 한 번에 기록
// 효과: 디스크 I/O 99% 감소
```

### 2.3 버그 수정

- [ ] NUM 판정 제외 로직 개선
- [ ] Enum 매칭 실패율 감소
- [ ] 메모리 누수 검토
- [ ] 예외 처리 강화

### 2.4 테스트

```
테스트 항목:
□ 100곡 연속 플레이 (메모리 누수 확인)
□ 다양한 곡 (호환성 확인)
□ 프레임 드롭 측정
□ 통계 정확도 검증
```

---

## 3) v1.2.0 (우선순위: HIGH)

### 3.1 예상 일정
- **계획**: 2026년 2월~3월
- **목표**: 기능 확장 및 UI 개선

### 3.2 핵심 기능

#### 3.2.1 핫키 시스템

```csharp
// 예상 핫키 바인딩
- Ctrl+J: 판정 조작 토글
- Ctrl+Shift+J: 모드 변경 (Perfect/Great/Stats)
- Ctrl+Alt+J: 통계 출력
- F2: 리셋
```

**구현 계획**:
```csharp
public class HotkeyManager
{
    [HarmonyPrefix]
    private static void OnKeyDown(KeyCode key)
    {
        if (Input.GetKeyDown(KeyCode.J) && Input.GetKey(KeyCode.LeftControl))
        {
            ToggleManipulation();
        }
    }
}
```

#### 3.2.2 간단한 UI 오버레이

```
게임 화면 우측 상단:
┌─────────────────────────┐
│ GRC Judge Mod v1.2      │
│ Mode: Perfect           │
│ Accuracy: 95.3%         │
│ Perfect: 100/105        │
└─────────────────────────┘
```

#### 3.2.3 곡별 통계 저장

```csharp
// UserData/GRCJudgeMod/songs/ 폴더
// - song_name_difficulty.json
// - 곡별 최고 정확도, 플레이 횟수, 최후 플레이 시간
```

### 3.3 개선 사항

- [ ] 더 정확한 Enum 매칭 알고리즘
- [ ] 콘솔 명령어 지원 (setmode, stats, reset)
- [ ] 게임 버전 자동 감지

---

## 4) v1.3.0 (우선순위: MEDIUM)

### 4.1 예상 일정
- **계획**: 2026년 3월~4월
- **목표**: 고급 기능 추가

### 4.2 핵심 기능

#### 4.2.1 다중 판정 모드

```csharp
public enum JudgeModePreset
{
    AllPerfect,      // 모든 판정 → Perfect
    GradeBased,      // 실제 판정 유지
    TimingBased,     // 타이밍 정도에 따라 결정
    Hybrid,          // Perfect 50%, 실제 판정 50%
    Custom           // 사용자 정의
}
```

#### 4.2.2 판정 분석 대시보드

```
주간 정확도 추이:
월 화 수 목 금 토 일
90 91 92 93 95 94 96 (%)

최근 Perfect 연속:
███████░░░ 7/10

타이밍 편향:
얼리 ████████░░ 80%
레이트 ██░░░░░░░░ 20%
```

#### 4.2.3 이벤트 시스템

```csharp
// 플러그인 개발자들이 이벤트를 구독할 수 있음
JudgeEvents.OnJudgeProcessed += MyHandler;
JudgeEvents.OnStatsUpdated += MyHandler;
```

### 4.4 이전 기능들

- [ ] 게임 버전별 설정 자동 로드
- [ ] 웹 기반 통계 열람 (로컬 서버)
- [ ] 스크린샷 자동 저장
- [ ] 판정 소리 피드백 (Optional)

---

## 5) v2.0.0 (우선순위: LOW, 장기계획)

### 5.1 예상 일정
- **계획**: 2026년 하반기~2027년
- **목표**: 아키텍처 재설계 및 범용화

### 5.2 핵심 변경

#### 5.2.1 게임 독립 아키텍처

**현재**:
```
GRC Judge Mod
  └─ GUNVOLT RECORDS Cychronicle만 지원
```

**목표**:
```
GRC Judge Framework
  ├─ GUNVOLT RECORDS Cychronicle 플러그인
  ├─ (다른 음악 게임) 플러그인
  └─ (다른 음악 게임) 플러그인
```

#### 5.2.2 설정 파일 기반 적응

```csharp
// config/gunvolt.json
{
  "game": "GUNVOLT RECORDS Cychronicle",
  "version": "1.0~1.2",
  "targetType": "IntiCreates.cNotecWorkBase",
  "targetMethod": "onJudgeMent",
  "judgeGradeField": "judgeType",
  "enumValueMap": {
    "PERFECT": 0,
    "GREAT": 1,
    ...
  }
}
```

#### 5.2.3 IL2CPP 지원

```csharp
// 현재: .NET Assembly 기반
// 목표: IL2CPP 게임도 지원
```

### 5.3 주요 기능

- [ ] 멀티게임 지원
- [ ] 플러그인 마켓플레이스
- [ ] 온라인 통계 동기화 (선택적)
- [ ] 모바일 앱 연동 (성적 열람)
- [ ] 커뮤니티 기반 설정 공유

---

## 6) 기술 부채 (Technical Debt)

### 6.1 즉시 해결 필요 (v1.1)

#### 6.1.1 하드코딩된 값들

```csharp
// ❌ 현재 (하드코딩)
private const string TARGET_TYPE = "IntiCreates.cNotecWorkBase";
private const string TARGET_METHOD = "onJudgeMent";

// ✅ 예정 (설정화)
private static string targetType;
private static string targetMethod;

// 설정 파일 또는 설정 API로 로드
```

#### 6.1.2 부족한 문서화

```csharp
// ❌ 현재
public void SetJudgeManipulation(bool enable, JudgeGrade? forcedGrade = null) { }

// ✅ 예정 (XML 문서)
/// <summary>판정 조작을 활성화/비활성화합니다.</summary>
/// <param name="enable">활성화 여부</param>
/// <param name="forcedGrade">강제 등급 (null=비활성화)</param>
public void SetJudgeManipulation(bool enable, JudgeGrade? forcedGrade = null) { }
```

### 6.2 장기 개선 (v1.3+)

#### 6.2.1 테스트 커버리지 부족
- 단위 테스트: 0%
- 통합 테스트: 10%
- **목표**: 70% 이상

#### 6.2.2 성능 최적화 미흡
- Reflection 반복 호출
- 불필요한 로그
- GC 압력 높음

#### 6.2.3 오류 처리 개선
- 일부 예외 미처리
- 실패 시 폴백 부족

---

## 7) 커뮤니티 피드백 통합 계획

### 7.1 피드백 수집 채널

```
1. GitHub Issues
   - 버그 리포트
   - 기능 요청
   
2. Discord 서버
   - 실시간 논의
   - 커뮤니티 투표
   
3. 설문조사
   - 분기별 사용자 만족도
   - 우선순위 결정
```

### 7.2 우선순위 결정 프로세스

```
1. 이슈 수집 (1개월)
2. 커뮤니티 투표 (1주)
3. 개발자 검토 (1주)
4. 로드맵 반영 (공식 공지)
5. 개발 시작
```

---

## 8) 리소스 요청

### 8.1 개발 인력

```
현황: 1명 (개인 프로젝트)
필요:
- 주요 개발자: 1명 (풀타임)
- 테스트 담당: 1명 (파트타임)
- 문서 작성: 1명 (파트타임)
- UI/UX 디자이너: 0.5명 (선택)
```

### 8.2 테스트 환경

```
필요:
- GUNVOLT RECORDS Cychronicle 게임
- 다양한 PC 사양 (노트북, 데스크톱)
- 다양한 OS (Windows 10, 11)
```

---

## 9) 성공 지표 (KPI)

### 9.1 v1.1.0 목표

| 지표 | 현재 | 목표 |
|------|------|------|
| FPS 개선 | -15% | -5% |
| 로그 크기/시간 | 1MB/시간 | 0.1MB/시간 |
| 버그 리포트 | - | <5건/월 |

### 9.2 v1.2.0 목표

| 지표 | 목표 |
|------|------|
| 다운로드 수 | 500+ |
| GitHub Star | 20+ |
| 활성 사용자 | 100+ |
| 커뮤니티 만족도 | 4.5/5.0 |

### 9.3 v1.3.0 목표

| 지표 | 목표 |
|------|------|
| 다운로드 수 | 2000+ |
| 플러그인 개수 | 5+ |
| 언어 지원 | 5+ |
| 유지보수성 점수 | 90+ |

---

## 10) 마이그레이션 가이드

### 10.1 v1.0 → v1.1

```
호환성: 100% 유지
마이그레이션 불필요
(자동 업그레이드 권장)
```

### 10.2 v1.1 → v1.2

```
호환성: 99% (핫키 바인딩 수동 설정 필요)
마이그레이션: 
- 설정 파일 백업
- MelonPreferences.cfg 재설정
```

### 10.3 v1.2 → v2.0

```
호환성: 70% (주요 변경 있음)
마이그레이션:
- 플러그인 구조 변경
- 네임스페이스 변경
- API 일부 제거/추가
```

---

## 11) 성과 측정 및 보고

### 11.1 월간 진행상황 리포트

```
매월 1일 공개:
- 완료된 기능
- 진행 중인 업무
- 알려진 이슈
- 다음 월 계획
```

### 11.2 버전 릴리스 공지

```
일정:
v1.1.0: 2026년 2월 15일 (예상)
v1.2.0: 2026년 3월 15일 (예상)
v1.3.0: 2026년 4월 15일 (예상)
v2.0.0: 2027년 1월 (예상)

각 버전마다:
- 변경 사항 (Change Log)
- 업그레이드 가이드
- 새로운 기능 튜토리얼
```

---

## 12) 질문과 답변

### Q1: 온라인 모드와의 충돌이 있을까?

**A**: 현재는 로컬 모드이므로 온라인 플레이 불가능. v2.0에서 개선 예정.

### Q2: 다른 게임도 지원할 예정인가?

**A**: v2.0에서 멀티게임 지원 계획 중. 플러그인 아키텍처로 변경 예정.

### Q3: 오픈소스 공개 계획이 있나?

**A**: 현재 비공개. v1.3 이후 GitHub에 공개 검토 중.

### Q4: 모바일 버전 계획이 있나?

**A**: 장기 계획에 포함되어 있지만 우선순위 낮음.

### Q5: 유지보수는 언제까지 지속되나?

**A**: 최소 2027년까지 보장. 커뮤니티 기여자 환영.

---

**이 로드맵은 상황에 따라 변경될 수 있습니다.**
**최신 정보는 GitHub Issues 또는 공식 Discord 서버를 참고하세요.**
