# GRC judge — 인수인계 문서 (AGENTS.md)

> 작성일 2026-11-15. 폴더의 코드·프로젝트 파일·기존 문서와 실제 게임 설치 폴더(Steam `.acf`, `UnityPlayer.dll`, `MelonLoader.dll`)를 읽어 정리했다.
> 이 문서 작성 시점에 인게임 재검증은 하지 않았다. **"실측"** 표시는 디스크에서 직접 읽은 값, **"미확인"**은 다음 세션에서 확인할 항목이다.

## 0. 프로젝트 개요

- **무엇인가:** GUNVOLT RECORDS Cychronicle 판정 모드. `IntiCreates.cNotecWorkBase.onJudgeMent`를 후킹해 판정 통계를 모으고 기본값으로 모든 판정을 Perfect로 강제, 기록 저장은 차단.
- **종류:** 게임 모드
- **최근 수정일(파일 기준):** 2026-11-05
- **git 원격:** `H:\local-repos\GRC`
- **최근 커밋:**
  - `0c92c84 Add GitHub Sponsors funding configuration`
  - `ae5a5df Initial commit`
- **먼저 읽을 기존 문서:**
  - [GRC 판정 조작모드\ADVANCED_PATTERNS.md](GRC%20판정%20조작모드/ADVANCED_PATTERNS.md)
  - [GRC 판정 조작모드\ANALYSIS.md](GRC%20판정%20조작모드/ANALYSIS.md)
  - [GRC 판정 조작모드\API_REFERENCE.md](GRC%20판정%20조작모드/API_REFERENCE.md)
  - [GRC 판정 조작모드\ARCHITECTURE.md](GRC%20판정%20조작모드/ARCHITECTURE.md)
  - [GRC 판정 조작모드\PERFORMANCE_OPTIMIZATION.md](GRC%20판정%20조작모드/PERFORMANCE_OPTIMIZATION.md)
  - [GRC 판정 조작모드\ROADMAP.md](GRC%20판정%20조작모드/ROADMAP.md)
  - [GRC 판정 조작모드\STATISTICS_GUIDE.md](GRC%20판정%20조작모드/STATISTICS_GUIDE.md)
  - [GRC 판정 조작모드\TROUBLESHOOTING.md](GRC%20판정%20조작모드/TROUBLESHOOTING.md)
  - [README.md](README.md)

## 1. 게임 버전 및 고정 여부

| 게임 | 설치 경로 | AppID | Steam Build ID (실측) | AutoUpdateBehavior | .acf 읽기 전용 |
| --- | --- | --- | --- | --- | --- |
| GUNVOLT RECORDS Cychronicle | `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle` | 2585040 | **13530823** | 0 | 아니오 |

- **고정 여부:** 고정 안 됨 — `AutoUpdateBehavior`가 `0`(항상 자동 업데이트)이고 `.acf`가 읽기 전용이 아니면 스팀이 게임을 업데이트할 수 있다. 업데이트되면 위 Build ID와 달라지므로 먼저 Build ID를 비교할 것.
- **DepotDownloader 다운그레이드:** 이 폴더에서 다운그레이드 기록은 찾지 못했다(**미확인**). 버전을 고정하려면 DepotDownloader로 위 Build ID의 매니페스트를 받아 두고, `.acf`를 읽기 전용으로 바꿔 업데이트를 막는다.

## 2. 런타임 백엔드 및 디컴파일 경로

- **GUNVOLT RECORDS Cychronicle:** Unity / **Mono** / Unity `2021.3.31f1` / x64 (실측)
  - 설치된 MelonLoader: `0.7.3.0 (net6)` (실측)
  - 참조 어셈블리: `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle\GUNVOLT_RECORDS_Cychronicle_Data\Managed\`
- 모드/앱 프로젝트 `GRC judge\GRC judge.csproj` 타깃 프레임워크: `v4.7.2` (구형 csproj)
- 디컴파일본은 이 폴더에 없다. Mono는 `ilspycmd`로 `<Game>_Data/Managed/Assembly-CSharp.dll`을, Il2Cpp는 `MelonLoader/Il2CppAssemblies/`를 SignatureDumper로 덤프해서 본다.

## 3. 설계 의도와 우회 사유 (Rationale)

코드 주석과 기존 문서에서 작성자가 남긴 의도·주의 문장을 그대로 옮겼다(파일:줄).

- `GRC 판정 조작모드\ANALYSIS.md:81` — Harmony Patching의 핵심 대상:
- `GRC 판정 조작모드\ANALYSIS.md:83` — 타입**: `IntiCreates.cNotecWorkBase`
- `GRC 판정 조작모드\ANALYSIS.md:84` — 노트(Note) 처리의 기본 클래스로 추정
- `GRC 판정 조작모드\ANALYSIS.md:143` — `noteType`, `noteTypeId`, `type` 계열(대소문자/언더스코어 변형 포함)
- `GRC 판정 조작모드\ANALYSIS.md:218` — `IntiCreates.cFairyModeNotesManager` 타입을 얻고
- `GRC 판정 조작모드\ANALYSIS.md:389` — 게임 업데이트에서 `IntiCreates.cNotecWorkBase` 클래스 이름 변경
- `GRC 판정 조작모드\ANALYSIS.md:422` — ) 로컬 모드 구현 시 주의점
- `GRC 판정 조작모드\ANALYSIS.md:501` — 이 문서는 **로컬 모드** 기반 GRC judge 모드의 기본 구조를 설명하는 핵심 분석 문서입니다. 더 상세한 내용은 다음 문서들을 참고하세요:
- `GRC judge\GRCJudgeMod.cs:142` — cNotecWorkBase의 onJudgeMent 메서드 검색 및 후킹
- `GRC judge\GRCJudgeMod.cs:197` — cFairyModeNotesManager 인스턴스 찾기
- `GRC judge\JudgeHooks.cs:388` — noteType, noteTypeId, type 등의 필드에서 num 확인

## 4. 핵심 훅 시그니처와 주입 파이프라인

- **Melon 특성:**
  - `"INTI CREATES", "GUNVOLT RECORDS Cychronicle"`
  - `typeof(GRC_judge.GRCJudgeMod), "GRC judge", "1.0.0", "GRC2"`
- **Harmony 대상(소스에서 수집):**
  - `Manual: UnityEngine.Object"FindObjectOfType", new Type[] { })`
- **주요 소스 파일(큰 순):**
  - `GRC judge\JudgeHooks.cs`
  - `GRC judge\JudgeRecordBlockHook.cs`
  - `GRC judge\GRCJudgeMod.cs`
  - `GRC judge\JudgeStatistics.cs`
  - `GRC judge\Properties\AssemblyInfo.cs`
  - `GRC judge\JudgeResult.cs`
  - `GRC judge\JudgeGrade.cs`

## 5. 시도했다가 실패한 것들 (Dead Ends)

코드 주석과 기존 문서에서 실패·크래시·금지 관련 문장을 그대로 옮겼다(파일:줄). 같은 방법을 다시 시도하기 전에 해당 위치를 읽을 것.

- `GRC 판정 조작모드\ANALYSIS.md:58` — 엔트리: `debugmod` (bool)
- `GRC 판정 조작모드\ANALYSIS.md:147` — “숫자 노트/숫자 판정”처럼 일반 판정과 다른 체계로 처리되는 케이스가 있어 예외 처리한 것으로 보입니다.
- `GRC 판정 조작모드\ANALYSIS.md:180` — (A) 재조작**: Prefix에서 실패했을 경우를 대비해, 다시 한 번 `OnJudgeParam`의 모든 enum 필드를 훑으며 `PERFECT`(또는 강제값)과 유사한 enum을 찾아 세팅
- `GRC 판정 조작모드\ANALYSIS.md:232` — ) 로깅/디버그 모드 관찰 포인트
- `GRC 판정 조작모드\ANALYSIS.md:234` — `debugmod` 설정이 존재하지만,
- `GRC 판정 조작모드\ANALYSIS.md:235` — `JudgeHooks.OnJudgeMentPostfix`는 일부 로그를 `DebugMod`로 제어합니다.
- `GRC 판정 조작모드\ANALYSIS.md:245` — 타입/메서드명 변경**: `IntiCreates.cNotecWorkBase.onJudgeMent`가 이름 변경/시그니처 변경되면 후킹 실패
- `GRC 판정 조작모드\ANALYSIS.md:247` — `judgeType` 필드가 없어지거나 다른 이름이면 추출 실패
- `GRC judge\JudgeHooks.cs:204` — 조용히 실패 (너무 많은 로그 방지)
- `GRC judge\JudgeHooks.cs:640` — Postfix에서도 다시 조작 시도 (Prefix에서 실패했을 경우를 대비)
- `GRC judge\JudgeHooks.cs:717` — 조용히 실패 (너무 많은 로그 방지)

## 6. 빌드 명령 및 배포 경로

- 솔루션: `GRC judge.sln`
- `GRC judge\GRC judge.csproj` (구형 csproj → Visual Studio MSBuild 필요):
  ```powershell
  msbuild "GRC judge\GRC judge.csproj" /p:Configuration=Release
  ```
  - 게임 경로 속성: `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle` (`-p:` 로 덮어쓸 수 있으면 다른 설치 경로로 빌드 가능)
- 빌드/배포 스크립트: `build.bat`
- 배포는 스크립트가 처리한다: `build.bat` (내용에 Mods 경로 복사 포함)
- 배포 위치: `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle\Mods\`

## 7. 다음 작업자에게

- 작업 전 1절의 Build ID와 현재 설치본 `.acf`의 `buildid`를 비교한다. 다르면 훅 대상 시그니처부터 다시 확인한다.
- 인게임 동작을 확인하면 3·5절의 **미확인** 항목을 실제 내용으로 바꾼다.
