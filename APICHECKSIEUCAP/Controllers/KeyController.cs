using APICHECKSIEUCAP.Models;
using APICHECKSIEUCAP.Services;
using Newtonsoft.Json.Linq;
using QuestionLib;
using QuestionLib.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace APICHECKSIEUCAP.Controllers
{
    [RoutePrefix("api/key")]
    public class KeyController : ApiController
    {
        private readonly ExamDbContext _db = new ExamDbContext();
        private readonly GradingService _gradingService = new GradingService();

        public class AnswerKeyDetail
        {
            public int QID { get; set; }
            public string QuestionText { get; set; }
            public double Mark { get; set; }
            public List<AnswerDetail> Answers { get; set; }
        }

        public class AnswerDetail
        {
            public int QAID { get; set; }
            public string AnswerText { get; set; }
            public bool IsCorrect { get; set; }
        }

        [HttpGet]
        [Route("get/{examCode}")]
        public IHttpActionResult GetAnswerKey(string examCode)
        {
            try
            {
                var answerKey = _db.ExamAnswerKeys.FirstOrDefault(k => k.ExamCode == examCode);

                if (answerKey == null)
                {
                    return Content(System.Net.HttpStatusCode.NotFound, $"Không tìm thấy đáp án gốc cho ExamCode: {examCode}.");
                }

                string tempKeyPath = System.IO.Path.GetTempFileName() + "." + answerKey.OriginalFileName.Split('.').Last();
                System.IO.File.WriteAllBytes(tempKeyPath, answerKey.FileData);

                Paper keyPaper;
                try
                {
                    keyPaper = _gradingService.LoadPaperFromFile(tempKeyPath);
                }
                finally
                {
                    if (System.IO.File.Exists(tempKeyPath))
                    {
                        System.IO.File.Delete(tempKeyPath);
                    }
                }

                if (keyPaper == null)
                {
                    return InternalServerError(new Exception("Không thể tải đối tượng Paper từ file đáp án."));
                }

                var allKeyQuestions = _gradingService.GetAllQuestionsFlat(keyPaper);

                var answerKeyDetails = new List<AnswerKeyDetail>();

                foreach (var q in allKeyQuestions)
                {
                    var details = new AnswerKeyDetail
                    {
                        QID = q.QID,
                        QuestionText = q.Text.Split('\n').LastOrDefault()?.Trim(),
                        Mark = q.Mark,
                        Answers = new List<AnswerDetail>()
                    };

                    foreach (var ansObj in q.QuestionAnswers)
                    {
                        var ans = (ansObj is QuestionAnswer qa)
                                        ? qa
                                        : (ansObj as JObject)?.ToObject<QuestionAnswer>();

                        if (ans != null)
                        {
                            details.Answers.Add(new AnswerDetail
                            {
                                QAID = ans.QAID,
                                AnswerText = ans.Text,
                                IsCorrect = ans.Chosen
                            });
                        }
                    }
                    answerKeyDetails.Add(details);
                }

                return Ok(new { ExamCode = keyPaper.ExamCode, TotalQuestions = allKeyQuestions.Count, KeyDetails = answerKeyDetails });
            }
            catch (Exception ex)
            {
                return Content(System.Net.HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}