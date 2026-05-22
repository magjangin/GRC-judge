namespace GRC_judge
{
    /// <summary>
    /// 판정 결과 클래스
    /// </summary>
    public class JudgeResult
    {
        public JudgeGrade Grade { get; set; }
        public int TimeDifference { get; set; }  // 샘플 단위 차이 (절대값)
        public bool IsEarly { get; set; }        // 일찍 눌렀는지 (true: 일찍, false: 늦게)

        public JudgeResult(JudgeGrade grade, int timeDifference, bool isEarly)
        {
            Grade = grade;
            TimeDifference = timeDifference;
            IsEarly = isEarly;
        }
    }
}



