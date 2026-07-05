using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace APICHECKSIEUCAP.Models
{
    public class GradingResult
    {
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public double FinalGrade { get; set; }
        public string Message { get; set; }
        public List<QuestionAnswerDetail> QuestionDetails { get; set; } = new List<QuestionAnswerDetail>();
    }
    public class QuestionAnswerDetail
    {
        public int QuestionId { get; set; }
        public string Content { get; set; }
        public string Image { get; set; }
        public List<string> StudentSelectedIds { get; set; } // Đổi sang string
        public List<string> CorrectAnswerIds { get; set; }   // Đổi sang string
        public bool IsCorrect { get; set; }
    }
}