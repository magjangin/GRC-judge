using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using HarmonyLib;

namespace GRC_judge
{
    /// <summary>
    /// 판정 메서드 후킹 클래스
    /// </summary>
    public static class JudgeHooks
    {
        // 판정 조작 설정
        private static bool enableJudgeManipulation = false;
        private static JudgeGrade? forcedJudgeGrade = null;

        public static bool IsJudgeManipulationEnabled => enableJudgeManipulation && forcedJudgeGrade.HasValue;

        /// <summary>
        /// 판정 조작 활성화/비활성화
        /// </summary>
        public static void SetJudgeManipulation(bool enable, JudgeGrade? forcedGrade = null)
        {
            enableJudgeManipulation = enable;
            forcedJudgeGrade = forcedGrade;
            MelonLogger.Msg($"판정 조작: {(enable ? "활성화" : "비활성화")} {(forcedGrade.HasValue ? $"(강제: {forcedGrade.Value})" : "")}");
        }

        // JudgeType Enum 타입 캐시 (현재 사용되지 않음)
        // private static Type judgeTypeEnum = null;
        // private static object perfectJudgeType = null;

        /// <summary>
        /// 판정 메서드 Prefix 후킹 (OnJudgeParam의 judgeType 조작)
        /// </summary>
        public static void JudgePrefix(MethodBase __originalMethod, object[] __args)
        {
            // 판정 조작이 활성화되어 있고 강제 판정이 설정된 경우
            if (enableJudgeManipulation && forcedJudgeGrade.HasValue)
            {
                try
                {
                    // 파라미터에서 OnJudgeParam 찾기
                    foreach (var arg in __args)
                    {
                        if (arg != null)
                        {
                            Type argType = arg.GetType();
                            
                            // OnJudgeParam 타입인지 확인
                            if (argType.Name == "OnJudgeParam" || argType.FullName.Contains("OnJudgeParam"))
                            {
                                // num 판정이면 조작하지 않음
                                if (IsNumJudgment(arg, argType))
                                {
                                    continue;
                                }
                                
                                // judgeType 필드 찾기
                                FieldInfo judgeTypeField = argType.GetField("judgeType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (judgeTypeField != null)
                                {
                                    // JudgeType Enum 타입 가져오기
                                    Type judgeTypeEnumType = judgeTypeField.FieldType;
                                    
                                    // PERFECT 값 찾기
                                    if (judgeTypeEnumType.IsEnum)
                                    {
                                        Array enumValues = Enum.GetValues(judgeTypeEnumType);
                                        string targetName = forcedJudgeGrade.Value.ToString().ToUpper();
                                        
                                        foreach (var enumVal in enumValues)
                                        {
                                            if (enumVal.ToString().ToUpper() == targetName)
                                            {
                                                judgeTypeField.SetValue(arg, enumVal);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"판정 조작 중 오류: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 판정 메서드 Postfix 후킹 (판정 조작 및 모니터링)
        /// </summary>
        public static void JudgePostfix(MethodBase __originalMethod, object[] __args, ref object __result)
        {
            try
            {
                // 판정 조작이 활성화되어 있고 강제 판정이 설정된 경우
                if (enableJudgeManipulation && forcedJudgeGrade.HasValue)
                {
                    MethodInfo method = __originalMethod as MethodInfo;
                    if (method != null)
                    {
                        Type returnType = method.ReturnType;
                        
                        // Enum 타입인 경우
                        if (returnType.IsEnum)
                        {
                            try
                            {
                                Array enumValues = Enum.GetValues(returnType);
                                string targetName = forcedJudgeGrade.Value.ToString().ToUpper();
                                
                                foreach (var enumVal in enumValues)
                                {
                                    string enumName = enumVal.ToString().ToUpper();
                                    if (enumName.Equals(targetName, StringComparison.OrdinalIgnoreCase) || 
                                        enumName.Contains(targetName) || 
                                        targetName.Contains(enumName))
                                    {
                                        __result = enumVal;
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"Enum 변환 실패: {ex.Message}");
                            }
                        }
                        // int 타입인 경우
                        else if (returnType == typeof(int))
                        {
                            int judgeValue = ConvertJudgeGradeToInt(forcedJudgeGrade.Value);
                            __result = judgeValue;
                        }
                        // 다른 타입인 경우도 시도
                        else if (__result != null)
                        {
                            Type resultObjType = __result.GetType();
                            FieldInfo[] fields = resultObjType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            
                            foreach (var field in fields)
                            {
                                if (field.FieldType.IsEnum || field.FieldType == typeof(int))
                                {
                                    try
                                    {
                                        if (field.FieldType.IsEnum)
                                        {
                                            Array enumValues = Enum.GetValues(field.FieldType);
                                            string targetName = forcedJudgeGrade.Value.ToString().ToUpper();
                                            foreach (var enumVal in enumValues)
                                            {
                                                if (enumVal.ToString().ToUpper().Contains(targetName))
                                                {
                                                    field.SetValue(__result, enumVal);
                                                    break;
                                                }
                                            }
                                        }
                                        else if (field.FieldType == typeof(int))
                                        {
                                            int judgeValue = ConvertJudgeGradeToInt(forcedJudgeGrade.Value);
                                            field.SetValue(__result, judgeValue);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                // 판정 모니터링
                if (__result == null) return;

                Type resultType = __result.GetType();
                
                // 판정 등급 추출
                JudgeGrade? grade = ExtractJudgeGrade(__result, resultType);
                
                if (grade.HasValue)
                {
                    int timeDifference = ExtractTimeDifference(__result, resultType);
                    bool isEarly = ExtractIsEarly(__result, resultType);
                    
                    JudgeResult result = new JudgeResult(grade.Value, timeDifference, isEarly);
                    GRCJudgeMod.GetStatistics().AddJudge(result);
                    
                    MelonLogger.Msg($"[판정] {grade.Value}, 차이: {timeDifference} 샘플, 일찍: {isEarly}");
                }
                else
                {
                    // 판정 등급을 직접 추출할 수 없는 경우, 메서드 이름이나 반환값으로 판단 시도
                    TryExtractFromMethod(__originalMethod, __result, resultType);
                }
            }
            catch
            {
                // 조용히 실패 (너무 많은 로그 방지)
            }
        }

        /// <summary>
        /// 메서드 정보로부터 판정 정보 추출 시도
        /// </summary>
        private static void TryExtractFromMethod(MethodBase method, object result, Type resultType)
        {
            // 반환값이 enum인 경우
            if (resultType.IsEnum)
            {
                string enumValue = result.ToString();
                if (Enum.TryParse<JudgeGrade>(enumValue, true, out JudgeGrade grade))
                {
                    JudgeResult judgeResult = new JudgeResult(grade, 0, false);
                    GRCJudgeMod.GetStatistics().AddJudge(judgeResult);
                    MelonLogger.Msg($"[판정] {grade} (메서드: {method.Name})");
                }
            }
            // 반환값이 정수인 경우 (0=Perfect, 1=Great, 2=Good, 3=Miss 등)
            else if (result is int intValue)
            {
                JudgeGrade? grade = ConvertIntToJudgeGrade(intValue);
                if (grade.HasValue)
                {
                    JudgeResult judgeResult = new JudgeResult(grade.Value, 0, false);
                    GRCJudgeMod.GetStatistics().AddJudge(judgeResult);
                    MelonLogger.Msg($"[판정] {grade.Value} (값: {intValue}, 메서드: {method.Name})");
                }
            }
        }

        /// <summary>
        /// 정수를 JudgeGrade로 변환
        /// </summary>
        private static JudgeGrade? ConvertIntToJudgeGrade(int value)
        {
            // 일반적인 매핑: 0=Perfect, 1=Great, 2=Good, 3=Miss
            switch (value)
            {
                case 0: return JudgeGrade.Perfect;
                case 1: return JudgeGrade.Great;
                case 2: return JudgeGrade.Good;
                case 3: return JudgeGrade.Miss;
                default: return null;
            }
        }

        /// <summary>
        /// 판정 등급 추출
        /// </summary>
        private static JudgeGrade? ExtractJudgeGrade(object result, Type resultType)
        {
            string[] possibleFields = { "Grade", "JudgeGrade", "Result", "JudgeResult", "Type", "JudgeType", "Judge" };
            
            // Field 검색
            foreach (var fieldName in possibleFields)
            {
                FieldInfo field = resultType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object value = field.GetValue(result);
                    if (value != null)
                    {
                        if (TryParseJudgeGrade(value, out JudgeGrade grade))
                            return grade;
                    }
                }
            }

            // Property 검색
            foreach (var propName in possibleFields)
            {
                PropertyInfo prop = resultType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    object value = prop.GetValue(result);
                    if (value != null)
                    {
                        if (TryParseJudgeGrade(value, out JudgeGrade grade))
                            return grade;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// JudgeGrade 파싱 시도
        /// </summary>
        private static bool TryParseJudgeGrade(object value, out JudgeGrade grade)
        {
            grade = JudgeGrade.Miss;

            if (value is JudgeGrade directGrade)
            {
                grade = directGrade;
                return true;
            }

            if (value is int intValue)
            {
                JudgeGrade? converted = ConvertIntToJudgeGrade(intValue);
                if (converted.HasValue)
                {
                    grade = converted.Value;
                    return true;
                }
            }

            string valueStr = value.ToString();
            if (Enum.TryParse<JudgeGrade>(valueStr, true, out grade))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 시간 차이 추출
        /// </summary>
        private static int ExtractTimeDifference(object result, Type resultType)
        {
            string[] possibleFields = { "TimeDifference", "Difference", "Delta", "Offset", "Diff", "TimeDiff" };
            
            foreach (var fieldName in possibleFields)
            {
                FieldInfo field = resultType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object value = field.GetValue(result);
                    if (value is int intValue) return intValue;
                    if (value is float floatValue) return (int)floatValue;
                }

                PropertyInfo prop = resultType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    object value = prop.GetValue(result);
                    if (value is int intValue) return intValue;
                    if (value is float floatValue) return (int)floatValue;
                }
            }

            return 0;
        }

        /// <summary>
        /// 일찍 눌렀는지 여부 추출
        /// </summary>
        private static bool ExtractIsEarly(object result, Type resultType)
        {
            string[] possibleFields = { "IsEarly", "Early", "IsBefore", "Before", "IsTooEarly" };
            
            foreach (var fieldName in possibleFields)
            {
                FieldInfo field = resultType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object value = field.GetValue(result);
                    if (value is bool boolValue) return boolValue;
                }

                PropertyInfo prop = resultType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    object value = prop.GetValue(result);
                    if (value is bool boolValue) return boolValue;
                }
            }

            return false;
        }

        /// <summary>
        /// num 판정인지 확인
        /// </summary>
        private static bool IsNumJudgment(object onJudgeParam, Type onJudgeParamType)
        {
            if (onJudgeParam == null || onJudgeParamType == null) return false;
            
            // noteType, noteTypeId, type 등의 필드에서 num 확인
            string[] possibleTypeFields = { "noteType", "NoteType", "noteTypeId", "NoteTypeId", "type", "Type", "note_type", "note_type_id" };
            
            foreach (var fieldName in possibleTypeFields)
            {
                FieldInfo field = onJudgeParamType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    try
                    {
                        object value = field.GetValue(onJudgeParam);
                        if (value != null)
                        {
                            string valueStr = value.ToString().ToUpper();
                            // num 판정인지 확인
                            if (valueStr.Contains("NUM") || valueStr == "NUM")
                            {
                                return true;
                            }
                        }
                    }
                    catch { }
                }
                
                PropertyInfo prop = onJudgeParamType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    try
                    {
                        object value = prop.GetValue(onJudgeParam);
                        if (value != null)
                        {
                            string valueStr = value.ToString().ToUpper();
                            if (valueStr.Contains("NUM") || valueStr == "NUM")
                            {
                                return true;
                            }
                        }
                    }
                    catch { }
                }
            }
            
            return false;
        }

        /// <summary>
        /// JudgeGrade를 정수로 변환
        /// </summary>
        private static int ConvertJudgeGradeToInt(JudgeGrade grade)
        {
            switch (grade)
            {
                case JudgeGrade.Perfect: return 0;
                case JudgeGrade.Great: return 1;
                case JudgeGrade.Good: return 2;
                case JudgeGrade.Miss: return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// onJudgeMent 메서드 Prefix 후킹 (OnJudgeParam의 judgeType 조작)
        /// </summary>
        public static void OnJudgeMentPrefix(MethodBase __originalMethod, object[] __args)
        {
            // 파라미터에서 OnJudgeParam 찾기
            object onJudgeParam = null;
            Type onJudgeParamType = null;
            
            if (__args != null)
            {
                foreach (var arg in __args)
                {
                    if (arg != null)
                    {
                        Type argType = arg.GetType();
                        
                        if (argType.Name == "OnJudgeParam" || 
                            argType.FullName != null && argType.FullName.Contains("OnJudgeParam") ||
                            argType.Name.Contains("Judge") && argType.Name.Contains("Param"))
                        {
                            onJudgeParam = arg;
                            onJudgeParamType = argType;
                            break;
                        }
                    }
                }
            }
            
            // 판정 조작이 활성화되어 있고 강제 판정이 설정된 경우
            if (enableJudgeManipulation && forcedJudgeGrade.HasValue && onJudgeParam != null)
            {
                // num 판정이면 조작하지 않음
                if (IsNumJudgment(onJudgeParam, onJudgeParamType))
                {
                    return;
                }
                
                try
                {
                    bool manipulationSuccess = false;
                    
                    // 1. 필드에서 조작 시도
                    string[] possibleFieldNames = { 
                        "judgeType", "JudgeType", "judge_type", "type", "Type", 
                        "result", "Result", "judge", "Judge", "grade", "Grade",
                        "judgeGrade", "JudgeGrade", "judgeResult", "JudgeResult"
                    };
                    
                    foreach (var fieldName in possibleFieldNames)
                    {
                        FieldInfo field = onJudgeParamType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            Type fieldType = field.FieldType;
                            
                            if (fieldType.IsEnum)
                            {
                                Array enumValues = Enum.GetValues(fieldType);
                                string targetName = forcedJudgeGrade.Value.ToString().ToUpper();
                                
                                string[] possibleNames = {
                                    targetName,
                                    targetName + "_JUDGE",
                                    "JUDGE_" + targetName,
                                    targetName + "_TYPE",
                                    "TYPE_" + targetName,
                                    "E_" + targetName,
                                    targetName + "_E",
                                    "ENUM_" + targetName,
                                    targetName + "_ENUM"
                                };
                                
                                foreach (var enumVal in enumValues)
                                {
                                    string enumName = enumVal.ToString().ToUpper();
                                    
                                    foreach (var possibleName in possibleNames)
                                    {
                                        if (enumName == possibleName || enumName.Contains(possibleName) || possibleName.Contains(enumName))
                                        {
                                            field.SetValue(onJudgeParam, enumVal);
                                            manipulationSuccess = true;
                                            break;
                                        }
                                    }
                                    if (manipulationSuccess) break;
                                }
                                
                                // 정확히 일치하지 않으면 부분 일치로 시도
                                if (!manipulationSuccess)
                                {
                                    foreach (var enumVal in enumValues)
                                    {
                                        string enumName = enumVal.ToString().ToUpper();
                                        if (enumName.Contains(targetName) || targetName.Contains(enumName))
                                        {
                                            field.SetValue(onJudgeParam, enumVal);
                                            manipulationSuccess = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (fieldType == typeof(int))
                            {
                                int judgeValue = ConvertJudgeGradeToInt(forcedJudgeGrade.Value);
                                field.SetValue(onJudgeParam, judgeValue);
                                manipulationSuccess = true;
                            }
                            
                            if (manipulationSuccess) break;
                        }
                    }
                    
                    // 2. 프로퍼티에서도 조작 시도
                    if (!manipulationSuccess)
                    {
                        foreach (var propName in possibleFieldNames)
                        {
                            PropertyInfo prop = onJudgeParamType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (prop != null && prop.CanWrite)
                            {
                                Type propType = prop.PropertyType;
                                
                                if (propType.IsEnum)
                                {
                                    Array enumValues = Enum.GetValues(propType);
                                    string targetName = forcedJudgeGrade.Value.ToString().ToUpper();
                                    
                                    foreach (var enumVal in enumValues)
                                    {
                                        string enumName = enumVal.ToString().ToUpper();
                                        if (enumName.Contains(targetName) || targetName.Contains(enumName))
                                        {
                                            prop.SetValue(onJudgeParam, enumVal);
                                            manipulationSuccess = true;
                                            break;
                                        }
                                    }
                                }
                                else if (propType == typeof(int))
                                {
                                    int judgeValue = ConvertJudgeGradeToInt(forcedJudgeGrade.Value);
                                    prop.SetValue(onJudgeParam, judgeValue);
                                    manipulationSuccess = true;
                                }
                                
                                if (manipulationSuccess) break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"onJudgeMent 판정 조작 중 오류: {ex}");
                }
            }
        }

        /// <summary>
        /// onJudgeMent 메서드 Postfix 후킹 (OnJudgeParam에서 판정 정보 추출 및 모니터링)
        /// </summary>
        public static void OnJudgeMentPostfix(MethodBase __originalMethod, object[] __args)
        {
            try
            {
                // 파라미터에서 OnJudgeParam 찾기
                object onJudgeParam = null;
                foreach (var arg in __args)
                {
                    if (arg != null)
                    {
                        Type argType = arg.GetType();
                        if (argType.Name == "OnJudgeParam" || argType.FullName.Contains("OnJudgeParam"))
                        {
                            onJudgeParam = arg;
                            break;
                        }
                    }
                }

                if (onJudgeParam == null) return;
                
                // num 판정이면 조작하지 않음
                Type paramObjType = onJudgeParam.GetType();
                if (IsNumJudgment(onJudgeParam, paramObjType))
                {
                    return;
                }
                
                // Postfix에서도 다시 조작 시도 (Prefix에서 실패했을 경우를 대비)
                if (enableJudgeManipulation && forcedJudgeGrade.HasValue)
                {
                    FieldInfo[] fields = paramObjType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    string targetName = forcedJudgeGrade.Value.ToString().ToUpper();
                    
                    foreach (var field in fields)
                    {
                        if (field.FieldType.IsEnum)
                        {
                            try
                            {
                                Array enumValues = Enum.GetValues(field.FieldType);
                                
                                foreach (var enumVal in enumValues)
                                {
                                    string enumName = enumVal.ToString().ToUpper();
                                    if (enumName.Contains(targetName) || targetName.Contains(enumName))
                                    {
                                        field.SetValue(onJudgeParam, enumVal);
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }

                Type paramType = onJudgeParam.GetType();
                
                // judgeType 필드에서 판정 등급 추출
                FieldInfo judgeTypeField = paramType.GetField("judgeType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (judgeTypeField != null)
                {
                    object judgeTypeValue = judgeTypeField.GetValue(onJudgeParam);
                    if (judgeTypeValue != null)
                    {
                        JudgeGrade? grade = TryParseJudgeGradeFromObject(judgeTypeValue);
                        
                        if (grade.HasValue)
                        {
                            // 시간 차이 추출 시도
                            int timeDifference = 0;
                            bool isEarly = false;
                            
                            // OnJudgeParam에서 시간 관련 필드 찾기
                            FieldInfo[] fields = paramType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (var field in fields)
                            {
                                string fieldName = field.Name.ToLower();
                                if (fieldName.Contains("time") || fieldName.Contains("diff") || fieldName.Contains("delta"))
                                {
                                    object value = field.GetValue(onJudgeParam);
                                    if (value is int intVal) timeDifference = Math.Abs(intVal);
                                    else if (value is float floatVal) timeDifference = (int)Math.Abs(floatVal);
                                }
                                else if (fieldName.Contains("early") || fieldName.Contains("before"))
                                {
                                    object value = field.GetValue(onJudgeParam);
                                    if (value is bool boolVal) isEarly = boolVal;
                                }
                            }
                            
                            JudgeResult result = new JudgeResult(grade.Value, timeDifference, isEarly);
                            GRCJudgeMod.GetStatistics().AddJudge(result);
                            
                            if (GRCJudgeMod.DebugMod)
                            {
                                MelonLogger.Msg($"[판정] onJudgeMent -> {grade.Value}, 차이: {timeDifference} 샘플, 일찍: {isEarly}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 조용히 실패 (너무 많은 로그 방지)
                MelonLogger.Warning($"onJudgeMent 후킹 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 객체에서 JudgeGrade 파싱 시도 (Enum, int, string 등 다양한 타입 지원)
        /// </summary>
        private static JudgeGrade? TryParseJudgeGradeFromObject(object value)
        {
            if (value == null) return null;

            // 직접 JudgeGrade인 경우
            if (value is JudgeGrade directGrade)
            {
                return directGrade;
            }

            // int인 경우
            if (value is int intValue)
            {
                return ConvertIntToJudgeGrade(intValue);
            }

            // Enum인 경우
            if (value.GetType().IsEnum)
            {
                string enumStr = value.ToString();
                if (Enum.TryParse<JudgeGrade>(enumStr, true, out JudgeGrade grade))
                {
                    return grade;
                }
                
                // Enum 값이 Perfect, Great, Good, Miss와 유사한지 확인
                string upperStr = enumStr.ToUpper();
                if (upperStr.Contains("PERFECT")) return JudgeGrade.Perfect;
                if (upperStr.Contains("GREAT")) return JudgeGrade.Great;
                if (upperStr.Contains("GOOD")) return JudgeGrade.Good;
                if (upperStr.Contains("MISS") || upperStr.Contains("BAD")) return JudgeGrade.Miss;
            }

            // 문자열인 경우
            string valueStr = value.ToString();
            if (Enum.TryParse<JudgeGrade>(valueStr, true, out JudgeGrade parsedGrade))
            {
                return parsedGrade;
            }

            return null;
        }
    }
}
