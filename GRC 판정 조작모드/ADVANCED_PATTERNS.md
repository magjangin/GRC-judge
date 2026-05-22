# GRC Judge Mod - 고급 패턴 및 확장성 가이드

이 문서는 **고급 기술, 확장 패턴, 커스터마이제이션** 방법을 설명합니다.

## 1) 플러그인/모디파이어 아키텍처

### 1.1 판정 수정자(Modifier) 패턴

```csharp
/// <summary>판정 값을 수정하는 인터페이스</summary>
public interface IJudgeModifier
{
    /// <summary>
    /// 원본 판정을 수정합니다.
    /// </summary>
    /// <param name="original">원본 판정</param>
    /// <param name="context">판정 컨텍스트</param>
    /// <returns>수정된 판정</returns>
    JudgeGrade ModifyJudge(JudgeGrade original, JudgeResult context);
    
    /// <summary>수정자의 우선순위 (낮을수록 먼저 실행)</summary>
    int Priority { get; }
    
    /// <summary>수정자의 이름</summary>
    string Name { get; }
}
```

### 1.2 구체적 구현 예

#### PassthroughModifier (아무것도 안 함)

```csharp
public class PassthroughModifier : IJudgeModifier
{
    public int Priority => int.MaxValue;
    public string Name => "Passthrough";
    
    public JudgeGrade ModifyJudge(JudgeGrade original, JudgeResult context)
    {
        return original;  // 변경 없음
    }
}
```

#### PerfectForceModifier (Perfect 강제)

```csharp
public class PerfectForceModifier : IJudgeModifier
{
    public int Priority => 10;
    public string Name => "PerfectForce";
    
    public JudgeGrade ModifyJudge(JudgeGrade original, JudgeResult context)
    {
        // NUM 노트 제외
        if (context.NoteType?.Contains("NUM") ?? false)
            return original;
        
        return JudgeGrade.Perfect;
    }
}
```

#### TimingBasedModifier (타이밍 기반 조정)

```csharp
public class TimingBasedModifier : IJudgeModifier
{
    public int Priority => 20;
    public string Name => "TimingBased";
    
    public JudgeGrade ModifyJudge(JudgeGrade original, JudgeResult context)
    {
        // 타이밍 오차로 등급 결정
        int timeDiff = Math.Abs(context.TimeDifference);
        
        if (timeDiff <= 5)
            return JudgeGrade.Perfect;
        else if (timeDiff <= 15)
            return JudgeGrade.Great;
        else if (timeDiff <= 30)
            return JudgeGrade.Good;
        else
            return JudgeGrade.Miss;
    }
}
```

#### RandomModifier (무작위)

```csharp
public class RandomModifier : IJudgeModifier
{
    private Random random = new Random();
    
    public int Priority => 50;
    public string Name => "Random";
    
    public JudgeGrade ModifyJudge(JudgeGrade original, JudgeResult context)
    {
        // 50% Perfect, 30% Great, 20% Good
        int roll = random.Next(100);
        
        if (roll < 50)
            return JudgeGrade.Perfect;
        else if (roll < 80)
            return JudgeGrade.Great;
        else
            return JudgeGrade.Good;
    }
}
```

### 1.3 수정자 관리자

```csharp
public class ModifierManager
{
    private List<IJudgeModifier> modifiers = new();
    
    /// <summary>수정자 등록</summary>
    public void RegisterModifier(IJudgeModifier modifier)
    {
        modifiers.Add(modifier);
        modifiers = modifiers.OrderBy(m => m.Priority).ToList();
        
        MelonLogger.Msg($"[MODIFIER] {modifier.Name} 등록됨 (Priority: {modifier.Priority})");
    }
    
    /// <summary>수정자 제거</summary>
    public void UnregisterModifier(string name)
    {
        var modifier = modifiers.FirstOrDefault(m => m.Name == name);
        if (modifier != null)
        {
            modifiers.Remove(modifier);
            MelonLogger.Msg($"[MODIFIER] {name} 제거됨");
        }
    }
    
    /// <summary>모든 수정자 적용</summary>
    public JudgeGrade ApplyModifiers(JudgeGrade original, JudgeResult context)
    {
        var result = original;
        
        foreach (var modifier in modifiers)
        {
            result = modifier.ModifyJudge(result, context);
        }
        
        return result;
    }
}
```

### 1.4 통합

```csharp
public class EnhancedJudgeHooks
{
    private static ModifierManager modifierManager = new();
    
    [HarmonyPostfix]
    public static void OnJudgeMentPostfix(object onJudgeParam)
    {
        var original = ExtractJudgeGrade(onJudgeParam);
        var context = new JudgeResult { Grade = original };
        
        // 수정자 적용
        var modified = modifierManager.ApplyModifiers(original, context);
        
        // 수정되었으면 게임에 반영
        if (modified != original)
        {
            SetJudgeGrade(onJudgeParam, modified);
        }
    }
}
```

---

## 2) 이벤트 기반 아키텍처

### 2.1 이벤트 정의

```csharp
/// <summary>판정 처리 이벤트</summary>
public class JudgeProcessedEventArgs : EventArgs
{
    public JudgeResult Result { get; set; }
    public JudgeGrade OriginalGrade { get; set; }
    public JudgeGrade FinalGrade { get; set; }
    public DateTime ProcessedAt { get; set; }
    public bool Manipulated { get; set; }
}

public static class JudgeEvents
{
    /// <summary>판정이 처리될 때 발생</summary>
    public static event EventHandler<JudgeProcessedEventArgs> OnJudgeProcessed;
    
    /// <summary>통계가 업데이트될 때 발생</summary>
    public static event EventHandler<StatisticsUpdatedEventArgs> OnStatisticsUpdated;
    
    internal static void RaiseJudgeProcessed(JudgeProcessedEventArgs e)
    {
        OnJudgeProcessed?.Invoke(null, e);
    }
    
    internal static void RaiseStatisticsUpdated(StatisticsUpdatedEventArgs e)
    {
        OnStatisticsUpdated?.Invoke(null, e);
    }
}

public class StatisticsUpdatedEventArgs : EventArgs
{
    public int TotalCount { get; set; }
    public double CurrentAccuracy { get; set; }
    public JudgeGrade MostRecentGrade { get; set; }
}
```

### 2.2 이벤트 리스너 예

```csharp
public class JudgeEventListener
{
    public void Subscribe()
    {
        JudgeEvents.OnJudgeProcessed += OnJudgeProcessed_Handler;
        JudgeEvents.OnStatisticsUpdated += OnStatisticsUpdated_Handler;
    }
    
    public void Unsubscribe()
    {
        JudgeEvents.OnJudgeProcessed -= OnJudgeProcessed_Handler;
        JudgeEvents.OnStatisticsUpdated -= OnStatisticsUpdated_Handler;
    }
    
    private void OnJudgeProcessed_Handler(object sender, JudgeProcessedEventArgs e)
    {
        if (e.Manipulated)
        {
            MelonLogger.Msg($"[EVENT] 판정 수정: {e.OriginalGrade} → {e.FinalGrade}");
        }
    }
    
    private void OnStatisticsUpdated_Handler(object sender, StatisticsUpdatedEventArgs e)
    {
        if (e.TotalCount % 10 == 0)
        {
            MelonLogger.Msg($"[EVENT] 총 {e.TotalCount}개, 정확도: {e.CurrentAccuracy:F1}%");
        }
    }
}
```

---

## 3) 데이터 저장 및 로드

### 3.1 JSON 기반 설정

```csharp
public class JudgeModConfig
{
    public string ModeName { get; set; }
    public bool EnableManipulation { get; set; }
    public JudgeGrade ForcedGrade { get; set; }
    public bool EnableLogging { get; set; }
    public int LogLevel { get; set; }  // 0=Off, 1=Error, 2=Warning, 3=Info
    public Dictionary<string, bool> Modifiers { get; set; }
}
```

### 3.2 설정 관리자

```csharp
public class ConfigManager
{
    private const string CONFIG_PATH = "UserData/GRCJudgeMod/config.json";
    
    public static JudgeModConfig LoadConfig()
    {
        if (!File.Exists(CONFIG_PATH))
        {
            return CreateDefaultConfig();
        }
        
        try
        {
            string json = File.ReadAllText(CONFIG_PATH);
            var config = JsonUtility.FromJson<JudgeModConfig>(json);
            
            MelonLogger.Msg($"[CONFIG] 설정 로드: {CONFIG_PATH}");
            return config;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[CONFIG] 설정 로드 실패: {ex.Message}");
            return CreateDefaultConfig();
        }
    }
    
    public static void SaveConfig(JudgeModConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CONFIG_PATH));
            
            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(CONFIG_PATH, json);
            
            MelonLogger.Msg($"[CONFIG] 설정 저장: {CONFIG_PATH}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[CONFIG] 설정 저장 실패: {ex.Message}");
        }
    }
    
    private static JudgeModConfig CreateDefaultConfig()
    {
        return new JudgeModConfig
        {
            ModeName = "Default",
            EnableManipulation = true,
            ForcedGrade = JudgeGrade.Perfect,
            EnableLogging = true,
            LogLevel = 2,
            Modifiers = new()
            {
                ["PerfectForce"] = true,
                ["Random"] = false
            }
        };
    }
}
```

---

## 4) 통계 고급 분석

### 4.1 성능 추적

```csharp
public class PerformanceTracker
{
    private List<JudgeResult> judgeHistory = new();
    
    public void RecordJudge(JudgeResult result)
    {
        judgeHistory.Add(result);
    }
    
    /// <summary>시간대별 정확도</summary>
    public Dictionary<int, double> GetAccuracyBySecond()
    {
        var grouped = judgeHistory
            .GroupBy(r => (int)r.Timestamp.TotalSeconds)
            .ToDictionary(
                g => g.Key,
                g => g.Count(r => r.Grade == JudgeGrade.Perfect || 
                                   r.Grade == JudgeGrade.Great) 
                    / (double)g.Count() * 100
            );
        
        return grouped;
    }
    
    /// <summary>연속된 Perfect 개수의 최대값</summary>
    public int GetLongestPerfectStreak()
    {
        int maxStreak = 0;
        int currentStreak = 0;
        
        foreach (var judge in judgeHistory)
        {
            if (judge.Grade == JudgeGrade.Perfect)
            {
                currentStreak++;
                maxStreak = Math.Max(maxStreak, currentStreak);
            }
            else
            {
                currentStreak = 0;
            }
        }
        
        return maxStreak;
    }
    
    /// <summary>타이밍 편향 분석</summary>
    public TimingBias AnalyzeTimingBias()
    {
        var earlyCount = judgeHistory.Count(r => r.IsEarly);
        var lateCount = judgeHistory.Count(r => !r.IsEarly);
        
        return new TimingBias
        {
            EarlyRatio = earlyCount / (double)(earlyCount + lateCount),
            AverageEarlyTiming = judgeHistory
                .Where(r => r.IsEarly)
                .Average(r => r.TimeDifference),
            AverageLateTiming = judgeHistory
                .Where(r => !r.IsEarly)
                .Average(r => r.TimeDifference)
        };
    }
}

public class TimingBias
{
    public double EarlyRatio { get; set; }
    public double AverageEarlyTiming { get; set; }
    public double AverageLateTiming { get; set; }
}
```

### 4.2 곡별 통계

```csharp
public class SongStatistics
{
    public string SongName { get; set; }
    public string Difficulty { get; set; }
    public DateTime PlayedAt { get; set; }
    public JudgeStatistics JudgeStats { get; set; }
    public int HighScore { get; set; }
    public double BestAccuracy { get; set; }
}

public class SongStatsManager
{
    private Dictionary<string, List<SongStatistics>> songRecords = new();
    
    public void RecordPlay(string songName, string difficulty, SongStatistics stats)
    {
        var key = $"{songName}_{difficulty}";
        
        if (!songRecords.ContainsKey(key))
            songRecords[key] = new List<SongStatistics>();
        
        songRecords[key].Add(stats);
    }
    
    public SongStatistics GetBestRun(string songName, string difficulty)
    {
        var key = $"{songName}_{difficulty}";
        
        if (!songRecords.TryGetValue(key, out var runs))
            return null;
        
        return runs.OrderByDescending(r => r.BestAccuracy).FirstOrDefault();
    }
    
    public void PrintLeaderboard(string songName)
    {
        var diffStats = songRecords
            .Where(kvp => kvp.Key.StartsWith(songName))
            .OrderByDescending(kvp => kvp.Value.Max(r => r.BestAccuracy))
            .ToList();
        
        MelonLogger.Msg($"\n=== {songName} 랭킹 ===");
        foreach (var kvp in diffStats)
        {
            var best = kvp.Value.Max(r => r.BestAccuracy);
            MelonLogger.Msg($"{kvp.Key}: {best:F1}%");
        }
    }
}
```

---

## 5) 커스텀 UI/콘솔 명령어

### 5.1 콘솔 명령어 시스템

```csharp
public interface IConsoleCommand
{
    string Name { get; }
    string Description { get; }
    void Execute(string[] args);
}

public class SetModeCommand : IConsoleCommand
{
    public string Name => "setmode";
    public string Description => "판정 모드 설정: setmode <perfect|great|stats>";
    
    public void Execute(string[] args)
    {
        if (args.Length == 0)
        {
            MelonLogger.Msg("사용: setmode <perfect|great|stats>");
            return;
        }
        
        switch (args[0].ToLower())
        {
            case "perfect":
                JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Perfect);
                MelonLogger.Msg("Perfect 강제 모드 활성화");
                break;
                
            case "great":
                JudgeHooks.SetJudgeManipulation(true, JudgeGrade.Great);
                MelonLogger.Msg("Great 강제 모드 활성화");
                break;
                
            case "stats":
                JudgeHooks.SetJudgeManipulation(false);
                MelonLogger.Msg("통계 수집 모드 (조작 비활성화)");
                break;
                
            default:
                MelonLogger.Warning("알 수 없는 모드");
                break;
        }
    }
}

public class PrintStatsCommand : IConsoleCommand
{
    public string Name => "stats";
    public string Description => "현재 통계 출력";
    
    public void Execute(string[] args)
    {
        var stats = GRCJudgeMod.GetStatistics();
        
        MelonLogger.Msg("\n=== 판정 통계 ===");
        MelonLogger.Msg($"Perfect: {stats.GetCount(JudgeGrade.Perfect)}");
        MelonLogger.Msg($"Great:   {stats.GetCount(JudgeGrade.Great)}");
        MelonLogger.Msg($"Good:    {stats.GetCount(JudgeGrade.Good)}");
        MelonLogger.Msg($"Miss:    {stats.GetCount(JudgeGrade.Miss)}");
        MelonLogger.Msg($"합계:    {stats.GetTotalCount()}");
        MelonLogger.Msg($"정확도:  {stats.GetAccuracy():F1}%\n");
    }
}

public class ConsoleCommandManager
{
    private Dictionary<string, IConsoleCommand> commands = new();
    
    public ConsoleCommandManager()
    {
        RegisterCommand(new SetModeCommand());
        RegisterCommand(new PrintStatsCommand());
    }
    
    public void RegisterCommand(IConsoleCommand command)
    {
        commands[command.Name] = command;
        MelonLogger.Msg($"[CMD] '{command.Name}' 등록됨");
    }
    
    public bool ExecuteCommand(string input)
    {
        var parts = input.Split(' ');
        var commandName = parts[0].ToLower();
        var args = parts.Skip(1).ToArray();
        
        if (commands.TryGetValue(commandName, out var command))
        {
            command.Execute(args);
            return true;
        }
        
        return false;
    }
}
```

---

## 6) 성능 커스터마이제이션

### 6.1 동적 로그 레벨

```csharp
public enum LogLevel
{
    Off = 0,
    Error = 1,
    Warning = 2,
    Info = 3,
    Debug = 4
}

public class Logger
{
    private static LogLevel currentLevel = LogLevel.Info;
    
    public static void SetLogLevel(LogLevel level)
    {
        currentLevel = level;
        MelonLogger.Msg($"로그 레벨: {level}");
    }
    
    public static void LogError(string message)
    {
        if (currentLevel >= LogLevel.Error)
            MelonLogger.Error(message);
    }
    
    public static void LogWarning(string message)
    {
        if (currentLevel >= LogLevel.Warning)
            MelonLogger.Warning(message);
    }
    
    public static void LogInfo(string message)
    {
        if (currentLevel >= LogLevel.Info)
            MelonLogger.Msg(message);
    }
    
    public static void LogDebug(string message)
    {
        if (currentLevel >= LogLevel.Debug)
            MelonLogger.Msg($"[DEBUG] {message}");
    }
}
```

---

## 7) 향후 기능 개발 가이드

### 7.1 새로운 Modifier 개발

1. `IJudgeModifier` 인터페이스 구현
2. `Priority` 설정 (다른 Modifier와의 순서)
3. `ModifyJudge()` 로직 구현
4. `ModifierManager.RegisterModifier()` 호출

**템플릿**:
```csharp
public class MyCustomModifier : IJudgeModifier
{
    public int Priority => 25;  // 우선순위
    public string Name => "MyCustom";
    
    public JudgeGrade ModifyJudge(JudgeGrade original, JudgeResult context)
    {
        // 로직 구현
        return original;
    }
}
```

### 7.2 새로운 이벤트 리스너 개발

1. `JudgeEvents.OnJudgeProcessed` 구독
2. 이벤트 핸들러 구현
3. 필요 시 `Unsubscribe()`

---

**이 가이드는 GRC Judge Mod의 확장 개발을 위한 기초를 제공합니다. 더 복잡한 기능은 [ARCHITECTURE.md](ARCHITECTURE.md)를 참고하세요.**
