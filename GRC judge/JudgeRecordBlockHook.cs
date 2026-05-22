using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace GRC_judge
{
    internal static class JudgeRecordBlockHook
    {
        private const string ResultUpdaterTypeName = "IntiCreates.cRythmGameResultSceneUpdater";
        private const string ResultInitializeMethodName = "initializePreFade";
        private const string ResultAnimMethodName = "coUpdateResultAnim";
        private const string SaveDirectorTypeName = "IntiCreates.sSaveDataDirector";
        private const string SaveMethodName = "requestGameDataSaveToFile";

        private static bool _installed;
        private static volatile bool _recordBlockPending;

        internal static bool IsInstalled => _installed;

        internal static bool TryInstall(HarmonyLib.Harmony harmony, Assembly gameAssembly, out string error)
        {
            error = null;

            if (_installed)
                return true;

            if (harmony == null)
            {
                error = "Harmony 인스턴스가 없습니다.";
                return false;
            }

            if (gameAssembly == null)
            {
                error = "게임 어셈블리가 없습니다.";
                return false;
            }

            try
            {
                var patchCount = 0;
                var resultType = gameAssembly.GetType(ResultUpdaterTypeName);
                if (resultType != null)
                {
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var initialize = resultType.GetMethods(flags)
                        .FirstOrDefault(m => m.Name == ResultInitializeMethodName && m.GetParameters().Length == 1);

                    if (initialize != null)
                    {
                        harmony.Patch(
                            initialize,
                            prefix: new HarmonyMethod(typeof(JudgeRecordBlockHook), nameof(ResultInitializePrefix)),
                            postfix: new HarmonyMethod(typeof(JudgeRecordBlockHook), nameof(ResultInitializePostfix)));
                        patchCount++;
                    }

                    var resultAnim = resultType.GetMethods(flags)
                        .FirstOrDefault(m => m.Name == ResultAnimMethodName && m.GetParameters().Length == 0);

                    if (resultAnim != null)
                    {
                        harmony.Patch(
                            resultAnim,
                            prefix: new HarmonyMethod(typeof(JudgeRecordBlockHook), nameof(ResultAnimPrefix)),
                            postfix: new HarmonyMethod(typeof(JudgeRecordBlockHook), nameof(ResultAnimPostfix)));
                        patchCount++;
                    }
                }

                var saveType = gameAssembly.GetType(SaveDirectorTypeName);
                if (saveType != null)
                {
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var save = saveType.GetMethods(flags)
                        .FirstOrDefault(m => m.Name == SaveMethodName);

                    if (save != null)
                    {
                        harmony.Patch(
                            save,
                            prefix: new HarmonyMethod(typeof(JudgeRecordBlockHook), nameof(RequestSavePrefix)));
                        patchCount++;
                    }
                }

                if (patchCount == 0)
                {
                    error = "결과/저장 차단 대상 메서드를 찾지 못했습니다.";
                    return false;
                }

                _installed = true;
                MelonLogger.Msg($"[기록 차단] 판정 조작 결과 차단 훅 설치 완료 ({patchCount}개)");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static void ResultInitializePrefix(object __instance, object __0, ref ResultSnapshot __state)
        {
            __state = null;

            try
            {
                if (!JudgeHooks.IsJudgeManipulationEnabled)
                {
                    _recordBlockPending = false;
                    return;
                }

                _recordBlockPending = true;
                __state = ResultSnapshot.TryCapture(__instance, __0);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[기록 차단] 스냅샷 실패: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void ResultInitializePostfix(object __instance, ResultSnapshot __state)
        {
            try
            {
                if (!_recordBlockPending)
                    return;

                ForceResultHighScoreZero(__instance);

                if (__state == null)
                {
                    MelonLogger.Warning("[기록 차단] 판정 조작 결과 차단: 스냅샷 없음");
                    return;
                }

                if (__state.TryRestore())
                {
                    ClearSaveDirty(__instance);
                    MelonLogger.Msg("[기록 차단] 판정 조작 결과 기록 차단: 베스트/콤보/횟수/클리어/언락 원복, 표시 하이스코어 0");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[기록 차단] 원복 실패: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void ResultAnimPrefix(object __instance)
        {
            try
            {
                if (!_recordBlockPending)
                    return;

                ForceResultHighScoreZero(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[기록 차단] 결과 표시 하이스코어 차단 실패: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void ResultAnimPostfix(ref IEnumerator __result)
        {
            if (!_recordBlockPending || __result == null)
                return;

            __result = ClearRecordBlockWhenFinished(__result);
        }

        private static IEnumerator ClearRecordBlockWhenFinished(IEnumerator source)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!source.MoveNext())
                        break;

                    current = source.Current;
                }
                catch
                {
                    _recordBlockPending = false;
                    throw;
                }

                yield return current;
            }

            _recordBlockPending = false;
            MelonLogger.Msg("[기록 차단] 판정 조작 결과 차단 플래그 해제");
        }

        private static bool RequestSavePrefix(object __instance)
        {
            if (!_recordBlockPending)
                return true;

            ClearSaveDirty(__instance);
            _recordBlockPending = false;
            MelonLogger.Msg("[기록 차단] 판정 조작 결과 저장 요청 스킵");
            return false;
        }

        private static void ClearSaveDirty(object saveDirectorOrResultUpdater)
        {
            try
            {
                var saveDirector = saveDirectorOrResultUpdater;
                if (saveDirector != null && saveDirector.GetType().FullName == ResultUpdaterTypeName)
                    saveDirector = GetMemberValue(saveDirectorOrResultUpdater, "mSaveDirector");

                if (saveDirector == null)
                    return;

                SetMemberValue(saveDirector, "mIsDirtySaveData", false);
            }
            catch
            {
                // best effort only
            }
        }

        private static void ForceResultHighScoreZero(object resultUpdater)
        {
            SetMemberValue(resultUpdater, "mOldPlayHighScore", 0);
        }

        private sealed class ResultSnapshot
        {
            private Array _playerMusicData;
            private int _musicId;
            private object _playData;
            private Array _highScoreArray;
            private Array _maxComboArray;
            private Array _playCountArray;
            private Array _playFlagArray;
            private object _unlockPlayData;
            private int _unlockId;
            private bool _unlockWasUnlocked;
            private bool _unlockWasNew;

            internal static ResultSnapshot TryCapture(object resultUpdater, object sceneParam)
            {
                if (resultUpdater == null || sceneParam == null)
                    return null;

                var musicData = GetMemberValue(sceneParam, "musicData");
                if (musicData == null)
                    return null;

                var id = Convert.ToInt32(GetMemberValue(musicData, "id"));

                var saveDirector = GetMemberValue(resultUpdater, "mSaveDirector");
                if (saveDirector == null)
                    return null;

                var gameData = Invoke(saveDirector, "getCurrentActiveGameData");
                if (gameData == null)
                    return null;

                var playerMusicData = GetMemberValue(gameData, "playerMusicData") as Array;
                if (playerMusicData == null || id < 0 || id >= playerMusicData.Length)
                    return null;

                var playData = playerMusicData.GetValue(id);
                if (playData == null)
                    return null;

                var snapshot = new ResultSnapshot
                {
                    _playerMusicData = playerMusicData,
                    _musicId = id,
                    _playData = playData,
                    _highScoreArray = CloneArray(GetMemberValue(playData, "highScoreArray") as Array),
                    _maxComboArray = CloneArray(GetMemberValue(playData, "maxComboArray") as Array),
                    _playCountArray = CloneArray(GetMemberValue(playData, "playCountArray") as Array),
                    _playFlagArray = CloneArray(GetMemberValue(playData, "playFlagArray") as Array)
                };

                snapshot.TryCaptureUnlockTarget(musicData, playerMusicData);
                return snapshot;
            }

            internal bool TryRestore()
            {
                if (_playData == null)
                    return false;

                var playData = _playerMusicData != null && _musicId >= 0 && _musicId < _playerMusicData.Length
                    ? _playerMusicData.GetValue(_musicId)
                    : _playData;

                RestoreArray(playData, "highScoreArray", _highScoreArray);
                RestoreArray(playData, "maxComboArray", _maxComboArray);
                RestoreArray(playData, "playCountArray", _playCountArray);
                RestoreArray(playData, "playFlagArray", _playFlagArray);

                if (_playerMusicData != null && _musicId >= 0 && _musicId < _playerMusicData.Length)
                    _playerMusicData.SetValue(playData, _musicId);

                if (_unlockPlayData != null)
                {
                    var unlockPlayData = _playerMusicData != null && _unlockId >= 0 && _unlockId < _playerMusicData.Length
                        ? _playerMusicData.GetValue(_unlockId)
                        : _unlockPlayData;

                    SetMemberValue(unlockPlayData, "isUnlock", _unlockWasUnlocked);
                    SetMemberValue(unlockPlayData, "isNew", _unlockWasNew);

                    if (_playerMusicData != null && _unlockId >= 0 && _unlockId < _playerMusicData.Length)
                        _playerMusicData.SetValue(unlockPlayData, _unlockId);
                }

                return true;
            }

            private void TryCaptureUnlockTarget(object musicData, Array playerMusicData)
            {
                try
                {
                    var unlockTarget = GetMemberValue(musicData, "unlockTargetMusic");
                    if (unlockTarget == null)
                        return;

                    var unlockId = Convert.ToInt32(unlockTarget);
                    if (unlockId < 0 || unlockId >= playerMusicData.Length)
                        return;

                    _unlockId = unlockId;
                    _unlockPlayData = playerMusicData.GetValue(unlockId);
                    if (_unlockPlayData == null)
                        return;

                    _unlockWasUnlocked = Convert.ToBoolean(GetMemberValue(_unlockPlayData, "isUnlock"));
                    _unlockWasNew = Convert.ToBoolean(GetMemberValue(_unlockPlayData, "isNew"));
                }
                catch
                {
                    _unlockPlayData = null;
                }
            }
        }

        private static Array CloneArray(Array array)
        {
            return array == null ? null : (Array)array.Clone();
        }

        private static void RestoreArray(object obj, string arrayName, Array snapshot)
        {
            if (obj == null || snapshot == null)
                return;

            SetMemberValue(obj, arrayName, snapshot.Clone());
        }

        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null)
                return null;

            var t = obj.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = t.GetField(name, flags);
            if (field != null)
                return field.GetValue(obj);

            var prop = t.GetProperty(name, flags);
            return prop != null && prop.CanRead ? prop.GetValue(obj, null) : null;
        }

        private static void SetMemberValue(object obj, string name, object value)
        {
            if (obj == null)
                return;

            var t = obj.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = t.GetField(name, flags);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }

            var prop = t.GetProperty(name, flags);
            if (prop != null && prop.CanWrite)
                prop.SetValue(obj, value, null);
        }

        private static object Invoke(object obj, string methodName)
        {
            if (obj == null)
                return null;

            var method = obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return method == null ? null : method.Invoke(obj, null);
        }
    }
}
