using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using HarmonyLib;
using UnityEngine;

namespace GRC_judge
{
    /// <summary>
    /// GRC judge MelonLoader 모드
    /// 게임의 판정 메서드를 후킹하여 판정 결과를 모니터링합니다.
    /// </summary>
    public class GRCJudgeMod : MelonMod
    {
        private static HarmonyLib.Harmony harmony;
        private static JudgeStatistics statistics = new JudgeStatistics();
        private static bool isInitialized = false;
        private static Assembly gameAssembly;
        private static Type fairyManagerType;
        private static object fairyManagerInstance;
        
        // 로그 레벨 제어 설정
        private static MelonPreferences_Category modCategory;
        private static MelonPreferences_Entry<bool> debugModEntry;
        public static bool DebugMod => debugModEntry?.Value ?? false;

        /// <summary>
        /// 모드 초기화
        /// </summary>
        public override void OnInitializeMelon()
        {
            // 설정 초기화
            modCategory = MelonPreferences.CreateCategory("GRCJudge");
            debugModEntry = modCategory.CreateEntry<bool>("debugmod", false, "디버그 모드 활성화 (판정 로그 출력)");
            
            MelonLogger.Msg("========================================");
            MelonLogger.Msg("GRC judge 모드 초기화 중...");
            MelonLogger.Msg($"디버그 모드: {(DebugMod ? "활성화" : "비활성화")}");
            MelonLogger.Msg("========================================");

            // Harmony 인스턴스 생성
            harmony = new HarmonyLib.Harmony("GRC.judge");

            // 게임 어셈블리 로드 대기 후 판정 메서드 후킹
            MelonCoroutines.Start(WaitForGameAssemblyAndHook());

            // 판정 조작 자동 활성화 (모든 판정을 Perfect로 강제)
            MelonLogger.Msg("========================================");
            MelonLogger.Msg("✨ 판정 조작 자동 활성화! ✨");
            MelonLogger.Msg("========================================");
            ForceAllPerfect();

            MelonLogger.Msg("GRC judge 모드 초기화 완료!");
        }


        /// <summary>
        /// 게임 어셈블리 로드를 기다린 후 판정 메서드 후킹
        /// </summary>
        private System.Collections.IEnumerator WaitForGameAssemblyAndHook()
        {
            // 게임 어셈블리가 로드될 때까지 대기
            while (true)
            {
                Assembly assembly = GetGameAssembly();
                if (assembly != null)
                {
                    gameAssembly = assembly;
                    MelonLogger.Msg("게임 어셈블리 발견!");
                    
                    // 판정 메서드 검색 및 후킹
                    MelonLogger.Msg("판정 메서드 검색 중...");
                    SearchAndHookJudgeMethods(gameAssembly);

                    if (!JudgeRecordBlockHook.TryInstall(harmony, gameAssembly, out var recordBlockError))
                    {
                        MelonLogger.Warning($"판정 조작 결과 차단 훅 설치 실패: {recordBlockError}");
                    }
                    break;
                }
                yield return null; // 다음 프레임까지 대기
            }
        }

        /// <summary>
        /// 씬이 초기화될 때 호출 (MelonLoader 내장 메서드)
        /// </summary>
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            // 플레이 씬인 경우 인스턴스 찾기
            if (sceneName.Contains("FairyMode") || sceneName.Contains("PlayMovie") || sceneName.Contains("Play"))
            {
                FindFairyManagerInstance();
                
                if (fairyManagerInstance != null)
                {
                    // 판정 조작 재활성화
                    ForceAllPerfect();
                }
            }
        }

        /// <summary>
        /// 게임 어셈블리 가져오기
        /// </summary>
        private Assembly GetGameAssembly()
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name == "Assembly-CSharp")
                    {
                        return assembly;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"어셈블리 검색 중 오류: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 판정 관련 메서드 검색 및 후킹
        /// onJudgeMent 메서드만 후킹
        /// </summary>
        private void SearchAndHookJudgeMethods(Assembly gameAssembly)
        {
            try
            {
                if (isInitialized)
                {
                    MelonLogger.Msg("이미 초기화되었습니다.");
                    return;
                }

                // cNotecWorkBase의 onJudgeMent 메서드 검색 및 후킹
                Type noteWorkBaseType = gameAssembly.GetType("IntiCreates.cNotecWorkBase");
                if (noteWorkBaseType != null)
                {
                    MethodInfo onJudgeMentMethod = noteWorkBaseType.GetMethod("onJudgeMent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (onJudgeMentMethod != null)
                    {
                        try
                        {
                            harmony.Patch(onJudgeMentMethod, 
                                prefix: new HarmonyMethod(typeof(JudgeHooks), nameof(JudgeHooks.OnJudgeMentPrefix)),
                                postfix: new HarmonyMethod(typeof(JudgeHooks), nameof(JudgeHooks.OnJudgeMentPostfix)));
                            
                            MelonLogger.Msg($"[후킹 성공] {noteWorkBaseType.Name}.onJudgeMent");
                            isInitialized = true;
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"onJudgeMent 메서드 후킹 실패: {ex.Message}");
                        }
                    }
                    else
                    {
                        MelonLogger.Warning("cNotecWorkBase.onJudgeMent 메서드를 찾을 수 없습니다.");
                    }
                }
                else
                {
                    MelonLogger.Warning("IntiCreates.cNotecWorkBase 타입을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"판정 메서드 검색 중 오류 발생: {ex}");
            }
        }

        /// <summary>
        /// 통계 정보 가져오기
        /// </summary>
        public static JudgeStatistics GetStatistics()
        {
            return statistics;
        }

        /// <summary>
        /// 통계 초기화
        /// </summary>
        public static void ResetStatistics()
        {
            statistics.Reset();
            MelonLogger.Msg("판정 통계 초기화됨");
        }

        /// <summary>
        /// cFairyModeNotesManager 인스턴스 찾기
        /// </summary>
        private void FindFairyManagerInstance()
        {
            try
            {
                if (gameAssembly == null)
                {
                    gameAssembly = GetGameAssembly();
                    if (gameAssembly == null)
                    {
                        MelonLogger.Warning("게임 어셈블리를 찾을 수 없습니다.");
                        return;
                    }
                }

                fairyManagerType = gameAssembly.GetType("IntiCreates.cFairyModeNotesManager");
                if (fairyManagerType == null)
                {
                    MelonLogger.Warning("cFairyModeNotesManager 타입을 찾을 수 없습니다.");
                    return;
                }

                // UnityEngine.Object.FindObjectOfType을 사용하여 인스턴스 찾기
                var findObjectOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { });
                if (findObjectOfTypeMethod != null)
                {
                    var genericMethod = findObjectOfTypeMethod.MakeGenericMethod(fairyManagerType);
                    fairyManagerInstance = genericMethod.Invoke(null, null);
                    
                    if (fairyManagerInstance != null)
                    {
                        MelonLogger.Msg("cFairyModeNotesManager 인스턴스 발견!");
                    }
                    else
                    {
                        MelonLogger.Warning("cFairyModeNotesManager 인스턴스를 찾을 수 없습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"인스턴스 찾기 중 오류: {ex}");
            }
        }

        /// <summary>
        /// 모든 판정을 Perfect로 강제
        /// </summary>
        public static void ForceAllPerfect()
        {
            JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Perfect);
            MelonLogger.Msg("========================================");
            MelonLogger.Msg("✨ 모든 판정을 Perfect로 강제합니다! ✨");
            MelonLogger.Msg("========================================");
        }

        /// <summary>
        /// 판정 조작 비활성화
        /// </summary>
        public static void DisableJudgeManipulation()
        {
            JudgeHooks.SetJudgeManipulation(false);
            MelonLogger.Msg("판정 조작 비활성화됨");
        }

        /// <summary>
        /// 오토 플레이 활성화/비활성화
        /// </summary>
        public static void SetAutoPlay(bool enable)
        {
            if (fairyManagerInstance == null || fairyManagerType == null)
            {
                MelonLogger.Warning("cFairyModeNotesManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            try
            {
                MethodInfo setIsAutoPlayMethod = fairyManagerType.GetMethod("setIsAutoPlay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (setIsAutoPlayMethod != null)
                {
                    setIsAutoPlayMethod.Invoke(fairyManagerInstance, new object[] { enable });
                    MelonLogger.Msg($"========================================");
                    MelonLogger.Msg($"🤖 오토 플레이: {(enable ? "활성화" : "비활성화")} 🤖");
                    MelonLogger.Msg($"========================================");
                }
                else
                {
                    MelonLogger.Warning("setIsAutoPlay 메서드를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"오토 플레이 설정 중 오류: {ex}");
            }
        }

        /// <summary>
        /// 오토 플레이 활성화
        /// </summary>
        public static void EnableAutoPlay()
        {
            SetAutoPlay(true);
        }

        /// <summary>
        /// 오토 플레이 비활성화
        /// </summary>
        public static void DisableAutoPlay()
        {
            SetAutoPlay(false);
        }

        /// <summary>
        /// 현재 오토 플레이 상태 확인
        /// </summary>
        public static bool GetAutoPlayState()
        {
            if (fairyManagerInstance == null || fairyManagerType == null)
            {
                MelonLogger.Warning("cFairyModeNotesManager 인스턴스를 찾을 수 없습니다.");
                return false;
            }

            try
            {
                FieldInfo autoPlayField = fairyManagerType.GetField("mIsCurrentAutoPlay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (autoPlayField != null)
                {
                    object value = autoPlayField.GetValue(fairyManagerInstance);
                    if (value is bool boolValue)
                    {
                        MelonLogger.Msg($"현재 오토 플레이 상태: {(boolValue ? "활성화" : "비활성화")}");
                        return boolValue;
                    }
                }
                else
                {
                    MelonLogger.Warning("mIsCurrentAutoPlay 필드를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"오토 플레이 상태 확인 중 오류: {ex}");
            }

            return false;
        }
    }
}
