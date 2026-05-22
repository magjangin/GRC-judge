using System;
using System.Collections.Generic;
using System.Linq;

namespace GRC_judge
{
    /// <summary>
    /// 판정 통계 클래스
    /// </summary>
    public class JudgeStatistics
    {
        private Dictionary<JudgeGrade, int> counts;

        public JudgeStatistics()
        {
            counts = new Dictionary<JudgeGrade, int>();
            foreach (JudgeGrade grade in Enum.GetValues(typeof(JudgeGrade)))
            {
                counts[grade] = 0;
            }
        }

        /// <summary>
        /// 판정 결과 추가
        /// </summary>
        public void AddJudge(JudgeResult result)
        {
            counts[result.Grade]++;
        }

        /// <summary>
        /// 특정 등급의 개수 가져오기
        /// </summary>
        public int GetCount(JudgeGrade grade)
        {
            return counts.ContainsKey(grade) ? counts[grade] : 0;
        }

        /// <summary>
        /// 전체 판정 개수 가져오기
        /// </summary>
        public int GetTotalCount()
        {
            return counts.Values.Sum();
        }

        /// <summary>
        /// 정확도 계산 (Perfect + Great 비율)
        /// </summary>
        public float GetAccuracy()
        {
            int total = GetTotalCount();
            if (total == 0) return 0f;

            int accurate = GetCount(JudgeGrade.Perfect) + GetCount(JudgeGrade.Great);
            return (float)accurate / total * 100f;
        }

        /// <summary>
        /// 통계 초기화
        /// </summary>
        public void Reset()
        {
            foreach (JudgeGrade grade in Enum.GetValues(typeof(JudgeGrade)))
            {
                counts[grade] = 0;
            }
        }

        /// <summary>
        /// 통계 정보 문자열로 반환
        /// </summary>
        public override string ToString()
        {
            int total = GetTotalCount();
            if (total == 0) return "판정 없음";

            return $"Perfect: {GetCount(JudgeGrade.Perfect)}, " +
                   $"Great: {GetCount(JudgeGrade.Great)}, " +
                   $"Good: {GetCount(JudgeGrade.Good)}, " +
                   $"Miss: {GetCount(JudgeGrade.Miss)}, " +
                   $"정확도: {GetAccuracy():F2}%";
        }
    }
}



