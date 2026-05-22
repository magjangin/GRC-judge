# GRC Judge Mod - 통계 분석 가이드

이 문서는 **판정 데이터 분석, 시각화, 인사이트 도출** 방법을 다룹니다.

## 1) 기본 통계 지표

### 1.1 정확도 (Accuracy)

**정의**:
```
정확도 = (Perfect + Great) / Total × 100 [%]
```

**의미**:
- 좋은 판정(Perfect/Great)의 비율
- 플레이어의 기술 수준을 나타내는 핵심 지표

**해석**:
| 범위 | 평가 | 상황 |
|------|------|------|
| 90~100% | 최고수 | 곡을 매우 잘 숙달함 |
| 80~90% | 상급자 | 안정적인 플레이 |
| 70~80% | 중급자 | 기본 실력 이상 |
| 50~70% | 초급자 | 아직 학습 중 |
| <50% | 미숙련 | 충분한 연습 필요 |

### 1.2 Miss율

**정의**:
```
Miss율 = Miss / Total × 100 [%]
```

**의미**:
- 완전히 놓친 노트의 비율
- Miss가 많으면 적응 못 한 곡이거나 집중력 부족

**감소 방법**:
1. 곡 반복 연습
2. 배속 조정 (느린 속도부터 시작)
3. 컨디션 확인 (피로, 스트레스)

### 1.3 Good 비율

**정의**:
```
Good 비율 = Good / Total × 100 [%]
```

**의미**:
- 타이밍이 어중간한 판정의 비율
- Good이 높으면 타이밍 감이 부족할 수 있음

## 2) 타이밍 분석

### 2.1 얼리/레이트 편향 (Bias)

```csharp
public class TimingAnalysis
{
    public static void AnalyzeTimingBias(List<JudgeResult> results)
    {
        var earlyCount = results.Count(r => r.IsEarly);
        var lateCount = results.Count(r => !r.IsEarly);
        
        double earlyRatio = earlyCount / (double)(earlyCount + lateCount);
        
        MelonLogger.Msg("=== 타이밍 분석 ===");
        MelonLogger.Msg($"얼리: {earlyCount} ({earlyRatio*100:F1}%)");
        MelonLogger.Msg($"레이트: {lateCount} ({(1-earlyRatio)*100:F1}%)");
        
        if (earlyRatio > 0.6)
            MelonLogger.Msg("→ 얼리 편향: 입력을 조금 늦게");
        else if (earlyRatio < 0.4)
            MelonLogger.Msg("→ 레이트 편향: 입력을 조금 일찍");
        else
            MelonLogger.Msg("→ 균형잡힘: 타이밍 좋음");
    }
}
```

### 2.2 타이밍 오차 분포

```csharp
public class TimingDistribution
{
    public static void ShowDistribution(List<JudgeResult> results)
    {
        var histogram = results
            .GroupBy(r => r.TimeDifference / 5)  // 5샘플 단위로 그룹화
            .OrderBy(g => g.Key)
            .ToList();
        
        MelonLogger.Msg("=== 타이밍 오차 분포 ===");
        
        foreach (var group in histogram)
        {
            var timing = group.Key * 5;
            var count = group.Count();
            var bar = new string('█', count / 2);
            
            MelonLogger.Msg($"{timing,4}ms: {bar} ({count})");
        }
    }
}
```

## 3) 성능 추세 분석

### 3.1 구간별 정확도 변화

```csharp
public class PerformanceTrend
{
    /// <summary>100개 노트마다의 정확도 변화 추적</summary>
    public static void ShowProgressionTrend(List<JudgeResult> results)
    {
        const int WINDOW_SIZE = 100;
        
        MelonLogger.Msg("=== 구간별 정확도 ===");
        
        for (int i = 0; i < results.Count; i += WINDOW_SIZE)
        {
            var window = results.Skip(i).Take(WINDOW_SIZE).ToList();
            var accuracy = CalculateAccuracy(window);
            var position = i / WINDOW_SIZE + 1;
            
            MelonLogger.Msg($"구간 {position}: {accuracy:F1}%");
        }
    }
    
    private static double CalculateAccuracy(List<JudgeResult> judges)
    {
        if (judges.Count == 0) return 0;
        
        var goodCount = judges.Count(j => 
            j.Grade == JudgeGrade.Perfect || j.Grade == JudgeGrade.Great);
        
        return goodCount / (double)judges.Count * 100;
    }
}
```

### 3.2 연속 Perfect 추적

```csharp
public class StreakAnalysis
{
    public static int GetCurrentStreak(List<JudgeResult> results)
    {
        int streak = 0;
        
        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (results[i].Grade == JudgeGrade.Perfect)
                streak++;
            else
                break;
        }
        
        return streak;
    }
    
    public static int GetLongestStreak(List<JudgeResult> results)
    {
        int maxStreak = 0;
        int currentStreak = 0;
        
        foreach (var judge in results)
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
    
    public static void ShowStreakStats(List<JudgeResult> results)
    {
        var current = GetCurrentStreak(results);
        var longest = GetLongestStreak(results);
        
        MelonLogger.Msg("=== 연속 Perfect ===");
        MelonLogger.Msg($"현재: {current}");
        MelonLogger.Msg($"최고: {longest}");
    }
}
```

## 4) 곡별 비교 분석

### 4.1 난이도별 정확도

```csharp
public class DifficultyAnalysis
{
    public static void CompareDifficulties(Dictionary<string, JudgeStatistics> byDifficulty)
    {
        MelonLogger.Msg("=== 난이도별 정확도 ===");
        
        foreach (var kvp in byDifficulty.OrderBy(x => x.Key))
        {
            var accuracy = kvp.Value.GetAccuracy();
            var total = kvp.Value.GetTotalCount();
            
            MelonLogger.Msg($"{kvp.Key}: {accuracy:F1}% ({total}곡)");
        }
    }
}
```

### 4.2 곡별 성능 비교

```csharp
public class SongComparison
{
    public static void RankSongs(Dictionary<string, JudgeStatistics> bySong)
    {
        var ranked = bySong
            .OrderByDescending(x => x.Value.GetAccuracy())
            .ThenByDescending(x => x.Value.GetTotalCount())
            .ToList();
        
        MelonLogger.Msg("=== 곡별 정확도 랭킹 ===");
        
        for (int i = 0; i < ranked.Count; i++)
        {
            var song = ranked[i];
            var accuracy = song.Value.GetAccuracy();
            
            MelonLogger.Msg($"{i+1}. {song.Key}: {accuracy:F1}%");
        }
    }
}
```

## 5) 세션 분석

### 5.1 플레이 세션 정의

```csharp
public class PlaySession
{
    public string SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalJudges { get; set; }
    public double FinalAccuracy { get; set; }
    public List<string> SongsPlayed { get; set; }
}

public class SessionAnalyzer
{
    private List<PlaySession> sessions = new();
    private PlaySession currentSession = null;
    
    public void StartSession()
    {
        currentSession = new PlaySession
        {
            SessionId = Guid.NewGuid().ToString(),
            StartTime = DateTime.Now,
            SongsPlayed = new List<string>()
        };
        
        MelonLogger.Msg($"[SESSION] 시작: {currentSession.SessionId}");
    }
    
    public void EndSession()
    {
        if (currentSession == null) return;
        
        currentSession.EndTime = DateTime.Now;
        currentSession.TotalJudges = GRCJudgeMod.GetStatistics().GetTotalCount();
        currentSession.FinalAccuracy = GRCJudgeMod.GetStatistics().GetAccuracy();
        
        sessions.Add(currentSession);
        
        MelonLogger.Msg($"[SESSION] 종료: {currentSession.FinalAccuracy:F1}% " +
            $"({currentSession.TotalJudges}개)");
        
        currentSession = null;
    }
    
    public void PrintSessionStats()
    {
        if (sessions.Count == 0)
        {
            MelonLogger.Msg("기록된 세션이 없습니다");
            return;
        }
        
        var totalTime = sessions.Sum(s => (s.EndTime - s.StartTime).TotalSeconds);
        var avgAccuracy = sessions.Average(s => s.FinalAccuracy);
        var bestSession = sessions.MaxBy(s => s.FinalAccuracy);
        
        MelonLogger.Msg("=== 세션 통계 ===");
        MelonLogger.Msg($"세션 수: {sessions.Count}");
        MelonLogger.Msg($"총 플레이 시간: {totalTime / 60:F0}분");
        MelonLogger.Msg($"평균 정확도: {avgAccuracy:F1}%");
        MelonLogger.Msg($"최고 정확도: {bestSession.FinalAccuracy:F1}%");
    }
}
```

## 6) 개선 추적

### 6.1 시간대별 진전

```csharp
public class ProgressTracking
{
    private Dictionary<DateTime, JudgeStatistics> dailyStats = new();
    
    public void RecordDailyStats()
    {
        var today = DateTime.Now.Date;
        var stats = GRCJudgeMod.GetStatistics();
        
        if (!dailyStats.ContainsKey(today))
        {
            dailyStats[today] = new JudgeStatistics();
        }
        
        dailyStats[today] = stats;
    }
    
    public void ShowProgress()
    {
        MelonLogger.Msg("=== 일일 진전 ===");
        
        foreach (var kvp in dailyStats.OrderBy(x => x.Key))
        {
            var date = kvp.Key.ToString("yyyy-MM-dd");
            var accuracy = kvp.Value.GetAccuracy();
            
            MelonLogger.Msg($"{date}: {accuracy:F1}%");
        }
    }
    
    /// <summary>주간 평균 정확도</summary>
    public double GetWeeklyAverage()
    {
        var thisWeek = dailyStats
            .Where(x => (DateTime.Now - x.Key).TotalDays <= 7)
            .Average(x => x.Value.GetAccuracy());
        
        return thisWeek;
    }
}
```

### 6.2 목표 추적

```csharp
public class GoalTracking
{
    public class Goal
    {
        public string Name { get; set; }
        public double TargetAccuracy { get; set; }
        public string Song { get; set; }
        public string Difficulty { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AchievedAt { get; set; }
    }
    
    private List<Goal> goals = new();
    
    public void AddGoal(string name, double targetAccuracy, 
        string song = null, string difficulty = null)
    {
        var goal = new Goal
        {
            Name = name,
            TargetAccuracy = targetAccuracy,
            Song = song,
            Difficulty = difficulty,
            CreatedAt = DateTime.Now
        };
        
        goals.Add(goal);
        MelonLogger.Msg($"[GOAL] 목표 추가: {name} ({targetAccuracy:F1}%)");
    }
    
    public void CheckGoals(JudgeStatistics currentStats)
    {
        var currentAccuracy = currentStats.GetAccuracy();
        
        foreach (var goal in goals.Where(g => g.AchievedAt == null))
        {
            if (currentAccuracy >= goal.TargetAccuracy)
            {
                goal.AchievedAt = DateTime.Now;
                
                var daysToAchieve = (goal.AchievedAt.Value - goal.CreatedAt).Days;
                MelonLogger.Msg($"🎉 [GOAL] 달성: {goal.Name} ({daysToAchieve}일)");
            }
        }
    }
}
```

## 7) 데이터 내보내기

### 7.1 CSV 형식 내보내기

```csharp
public class DataExporter
{
    public static void ExportToCSV(List<JudgeResult> judges, string filename)
    {
        try
        {
            var lines = new List<string>();
            
            // 헤더
            lines.Add("Index,Grade,TimeDifference,IsEarly,Timestamp");
            
            // 데이터
            for (int i = 0; i < judges.Count; i++)
            {
                var j = judges[i];
                lines.Add($"{i},{j.Grade},{j.TimeDifference},{j.IsEarly},{j.Timestamp:O}");
            }
            
            File.WriteAllLines(filename, lines);
            MelonLogger.Msg($"[EXPORT] CSV 저장: {filename}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[EXPORT] 내보내기 실패: {ex.Message}");
        }
    }
}
```

### 7.2 JSON 형식 내보내기

```csharp
public class JsonExporter
{
    public static void ExportStats(JudgeStatistics stats, string filename)
    {
        var data = new
        {
            Perfect = stats.GetCount(JudgeGrade.Perfect),
            Great = stats.GetCount(JudgeGrade.Great),
            Good = stats.GetCount(JudgeGrade.Good),
            Miss = stats.GetCount(JudgeGrade.Miss),
            Total = stats.GetTotalCount(),
            Accuracy = stats.GetAccuracy(),
            ExportedAt = DateTime.Now
        };
        
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filename, json);
            
            MelonLogger.Msg($"[EXPORT] JSON 저장: {filename}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[EXPORT] JSON 저장 실패: {ex.Message}");
        }
    }
}
```

## 8) 고급 분석 예제

### 8.1 플레이 스타일 분석

```csharp
public class PlayStyleAnalysis
{
    public enum PlayStyle
    {
        Aggressive,      // 빠른 판정, 얼리 편향
        Conservative,    // 안정적, 약간 레이트
        Inconsistent,    // 불안정, Good 많음
        Perfect,         // 완벽함
    }
    
    public static PlayStyle AnalyzeStyle(JudgeStatistics stats, 
        TimingBias timing)
    {
        var accuracy = stats.GetAccuracy();
        var missRatio = stats.GetCount(JudgeGrade.Miss) / 
                       (double)stats.GetTotalCount();
        
        // 완벽함
        if (accuracy > 95 && missRatio < 0.02)
            return PlayStyle.Perfect;
        
        // 공격적
        if (timing.EarlyRatio > 0.65 && accuracy > 80)
            return PlayStyle.Aggressive;
        
        // 보수적
        if (timing.EarlyRatio < 0.35 && accuracy > 85)
            return PlayStyle.Conservative;
        
        // 불안정
        return PlayStyle.Inconsistent;
    }
}
```

### 8.2 난이도 추정

```csharp
public class DifficultyEstimation
{
    /// <summary>플레이어의 곡별 정확도 기반 난이도 등급 추정</summary>
    public static string EstimateSuitableDifficulty(double accuracy)
    {
        return accuracy switch
        {
            >= 95 => "EXTREME",
            >= 90 => "HARD",
            >= 80 => "NORMAL",
            >= 70 => "EASY",
            _ => "BEGINNER"
        };
    }
}
```

---

**통계 분석을 통해 플레이어의 약점을 파악하고 개선 방향을 설정하세요!**
