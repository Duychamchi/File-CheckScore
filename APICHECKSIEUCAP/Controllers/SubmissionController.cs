using APICHECKSIEUCAP.Services;
using Newtonsoft.Json.Linq;
using QuestionLib;
using QuestionLib.Entity;
using sun.nio.ch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using APICHECKSIEUCAP.Services;
using Newtonsoft.Json.Linq;
using QuestionLib.Entity;

namespace APICHECKSIEUCAP.Controllers
{
    [RoutePrefix("api/submission")]
    public class SubmissionController : ApiController
    {
        private readonly GradingService _gradingService = new GradingService();

        public class StudentSubmissionDetail
        {
            public int QID { get; set; }
            public string QuestionText { get; set; }
            public List<StudentAnswerDetail> Answers { get; set; }
        }

        public class StudentAnswerDetail
        {
            public int QAID { get; set; }
            public string AnswerText { get; set; }
            public bool IsSelected { get; set; }
        }

        [HttpPost]
        [Route("upload_and_read")]
        public async Task<IHttpActionResult> UploadAndReadSubmission()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return BadRequest("Yêu cầu phải là multipart/form-data để upload file.");
            }

            string tempSubmissionPath = null;

            try
            {

                var provider = await Request.Content.ReadAsMultipartAsync();
                var fileContent = provider.Contents
                    .FirstOrDefault(c => c.Headers.ContentDisposition.FileName != null);

                if (fileContent == null)
                {
                    return BadRequest("Không có file bài làm nào được upload.");
                }


                var originalFileName = fileContent.Headers.ContentDisposition.FileName.Trim('\"');
                var submissionFileData = await fileContent.ReadAsByteArrayAsync();


                tempSubmissionPath = Path.GetTempFileName() + Path.GetExtension(originalFileName);
                File.WriteAllBytes(tempSubmissionPath, submissionFileData);

                Paper submissionPaper = _gradingService.LoadPaperFromFile(tempSubmissionPath);

                if (submissionPaper == null)
                {
                    return InternalServerError(new Exception("Không thể tải đối tượng Paper từ file bài làm."));
                }

                var allSubmittedQuestions = _gradingService.GetAllQuestionsFlat(submissionPaper);

                var submissionDetails = new List<StudentSubmissionDetail>();

                foreach (var q in allSubmittedQuestions)
                {
                    var questionText = (q.Text ?? "")
                        .Split('\n')
                        .LastOrDefault()?
                        .Trim();

                    var details = new StudentSubmissionDetail
                    {
                        QID = q.QID,
                        QuestionText = questionText, 
                        Answers = new List<StudentAnswerDetail>()
                    };

                    foreach (var ansObj in q.QuestionAnswers)
                    {
                        var ans = (ansObj is QuestionAnswer qa)
                                    ? qa
                                    : (ansObj as JObject)?.ToObject<QuestionAnswer>();

                        if (ans != null)
                        {
                            details.Answers.Add(new StudentAnswerDetail
                            {
                                QAID = ans.QAID,
                                AnswerText = ans.Text,
                                IsSelected = ans.Selected
                            });
                        }
                    }
                    submissionDetails.Add(details);
                }

                return Ok(new
                {
                    ExamCode = submissionPaper.ExamCode,
                    TotalQuestions = allSubmittedQuestions.Count,
                    SubmissionDetails = submissionDetails
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
            finally
            {
                if (tempSubmissionPath != null && File.Exists(tempSubmissionPath))
                {
                    File.Delete(tempSubmissionPath);
                }
            }
        }
    }
}