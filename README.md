# GRC judge

`GUNVOLT RECORDS Cychronicle`용 **MelonLoader 모드**입니다.  
게임의 판정 처리 메서드를 **Harmony**로 후킹하여 판정 결과를 수집(통계)하고, 현재 구현에서는 **모든 판정을 Perfect로 강제**하는 기능이 기본으로 켜집니다.

## 주요 기능

- **판정 후킹**: `Assembly-CSharp`의 `IntiCreates.cNotecWorkBase.onJudgeMent`를 후킹
- **판정 강제(치환)**: `OnJudgeParam` 내부의 `judgeType`(또는 유사 필드/프로퍼티)를 찾아 **강제로 Perfect**로 변경
  - `num` 판정(숫자 관련 판정으로 추정)은 조작 대상에서 제외
- **판정 통계 누적**: `Perfect/Great/Good/Miss` 카운트 누적(정확도 = Perfect+Great 비율)
- **오토플레이 토글(리플렉션)**: `IntiCreates.cFairyModeNotesManager.setIsAutoPlay(bool)` 호출 및 `mIsCurrentAutoPlay` 필드 조회 기능 제공
  - 주의: 현재 코드에는 “토글을 호출하는 UI/핫키”는 없고, API만 존재합니다.

## 요구 사항

- Windows
- Visual Studio 2019/2022 (MSBuild 포함)
- 대상 게임: `GUNVOLT RECORDS Cychronicle`
- MelonLoader 설치(게임 폴더에 `MelonLoader` 폴더가 존재)

## 설치(사용자)

1. 게임에 MelonLoader를 설치합니다.
2. 빌드된 모드 DLL(`GRC judge.dll`)을 게임 폴더의 `Mods` 디렉터리에 넣습니다.
   - 예) `...\GUNVOLT RECORDS Cychronicle\Mods\GRC judge.dll`
3. 게임 실행 후 MelonLoader 콘솔/로그에서 모드 로딩을 확인합니다.

## 빌드(개발자)

이 프로젝트는 게임 설치 경로에 있는 DLL을 참조합니다. 따라서 **내 PC의 게임 설치 경로에 맞게 경로를 먼저 수정**해야 합니다.

- **`GRC judge/GRC judge.csproj`**
  - `HintPath`들이 기본값으로 `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle\...`를 가리킵니다.
  - 본인 환경의 게임 설치 경로로 수정하세요.
- **`build.bat`**
  - `GAME_PATH` 변수를 본인 환경에 맞게 수정하세요.

빌드/배포:

- `build.bat` 실행
  - Debug 빌드 후 결과 DLL을 `GAME_PATH\Mods\`로 복사합니다.

## 설정

- MelonPreferences 카테고리: `GRCJudge`
- 엔트리: `debugmod` (bool)
  - `true`: 디버그 로그(일부 판정 로그) 출력
  - `false`: 디버그 로그 최소화

설정 파일 위치는 MelonLoader 버전에 따라 다를 수 있으나 보통 `UserData` 아래에 저장됩니다(예: `UserData/MelonPreferences.cfg`).

## 동작 요약

- 모드 초기화(`OnInitializeMelon`) 시:
  - `Assembly-CSharp` 로딩을 기다린 뒤 `onJudgeMent`를 후킹합니다.
  - **판정 조작을 자동으로 활성화**하여 기본적으로 모든 판정을 `Perfect`로 강제합니다.
- 특정 플레이 관련 씬 로드시(`OnSceneWasInitialized`):
  - `cFairyModeNotesManager` 인스턴스를 찾아 오토플레이/판정 조작 관련 처리를 재시도합니다.

## 주의 사항 / 면책

- 이 모드는 게임 판정을 조작할 수 있어 **치트(부정행위) 성격**을 가집니다. 온라인/랭킹/공유 환경에서 사용 시 불이익이 발생할 수 있습니다.
- 게임 업데이트로 인해 타입/필드/메서드 이름이 바뀌면 후킹이 실패할 수 있습니다.
- 본 저장소는 교육/연구 목적의 리버스 엔지니어링 참고 자료로만 사용하세요.

## 문서

- 분석 문서: `docs/ANALYSIS.md`



