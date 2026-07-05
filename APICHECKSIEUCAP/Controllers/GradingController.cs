using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using APICHECKSIEUCAP.Models;
using APICHECKSIEUCAP.Services;
using QuestionLib;
using QuestionLib.Entity;
using APICHECKSIEUCAP.Models;


namespace APICHECKSIEUCAP.Controllers
{
    [RoutePrefix("api/grading")]
    public class GradingController : ApiController
    {
        private readonly ExamDbContext _db = new ExamDbContext();
        private readonly GradingService _gradingService = new GradingService();

        [HttpPost]
        [Route("grade")]
        public async Task<IHttpActionResult> UploadAndGrade()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return BadRequest("Yêu cầu phải là multipart/form-data.");
            }

            var provider = await Request.Content.ReadAsMultipartAsync();

            var studentIdContent = provider.Contents
                .FirstOrDefault(c => c.Headers.ContentDisposition.Name.Trim('\"') == "studentIdentifier");

            var fileContent = provider.Contents
                .FirstOrDefault(c => c.Headers.ContentDisposition.FileName != null);

            if (fileContent == null)
            {
                return BadRequest("Không có file bài làm nào được upload.");
            }
            string studentIdentifier = null;


            var originalFileName = fileContent.Headers.ContentDisposition.FileName.Trim('\"');
            var submissionFileData = await fileContent.ReadAsByteArrayAsync();

            string tempSubmissionPath = null;
            string tempKeyPath = null;

            try
            {
                tempSubmissionPath = Path.GetTempFileName() + Path.GetExtension(originalFileName);
                File.WriteAllBytes(tempSubmissionPath, submissionFileData);

                Paper submissionPaper = _gradingService.LoadPaperFromFile(tempSubmissionPath);
                if (submissionPaper == null || string.IsNullOrEmpty(submissionPaper.ExamCode))
                {
                    return BadRequest("Không thể đọc file bài làm hoặc không tìm thấy ExamCode.");
                }

                string examCode = submissionPaper.ExamCode;

                var answerKey = _db.ExamAnswerKeys.FirstOrDefault(k => k.ExamCode == examCode);
                if (answerKey == null)
                {
                    return Content(HttpStatusCode.NotFound, $"Không tìm thấy đáp án gốc cho môn: {examCode}.");
                }

                tempKeyPath = Path.GetTempFileName() + Path.GetExtension(answerKey.OriginalFileName);
                File.WriteAllBytes(tempKeyPath, answerKey.FileData);

                Paper keyPaper = _gradingService.LoadPaperFromFile(tempKeyPath);

                GradingResult result = _gradingService.GradeSubmission(keyPaper, submissionPaper);
                if (result.Message != "Chấm điểm thành công.")
                {
                    return BadRequest(result.Message);
                }

                var submissionEntry = new UserSubmission
                {
                    StudentIdentifier = studentIdentifier,
                    ExamCode = examCode,
                    OriginalFileName = originalFileName,
                    FileData = submissionFileData,
                    Score = result.Score,
                    TotalQuestions = result.TotalQuestions,
                    FinalGrade = (decimal)result.FinalGrade,
                    UploadedAt = DateTime.Now
                };

                _db.UserSubmissions.Add(submissionEntry);
                await _db.SaveChangesAsync();
                return Ok(new
                {
                    ExamCode = examCode,
                    Score = result.Score,
                    TotalQuestions = result.TotalQuestions,
                    FinalGrade = result.FinalGrade,
                    Message = result.Message,
                    Details = result.QuestionDetails // Thêm dòng này để Client nhận được chi tiết câu hỏi
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
                if (tempKeyPath != null && File.Exists(tempKeyPath))
                {
                    File.Delete(tempKeyPath);
                }
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