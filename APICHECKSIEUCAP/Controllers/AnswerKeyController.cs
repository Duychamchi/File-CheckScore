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

namespace APICHECKSIEUCAP.Controllers
{
    [RoutePrefix("api/answerkey")]
    public class AnswerKeyController : ApiController
    {
        private readonly ExamDbContext _db = new ExamDbContext();
        private readonly GradingService _gradingService = new GradingService();

        [HttpPost]
        [Route("upload")]
        public async Task<IHttpActionResult> UploadAnswerKey()
        {
  
            if (!Request.Content.IsMimeMultipartContent())
            {
                return BadRequest("Yêu cầu phải là multipart/form-data.");
            }

            var provider = new MultipartMemoryStreamProvider();
            await Request.Content.ReadAsMultipartAsync(provider);

            if (provider.Contents.Count == 0)
            {
                return BadRequest("Không có file nào được upload.");
            }

            var fileContent = provider.Contents[0];
            var originalFileName = fileContent.Headers.ContentDisposition.FileName.Trim('\"');
            var fileData = await fileContent.ReadAsByteArrayAsync();

            if (fileData.Length == 0)
            {
                return BadRequest("File rỗng.");
            }

            string tempPath = null;
            try
            {

                tempPath = Path.GetTempFileName() + Path.GetExtension(originalFileName);
                File.WriteAllBytes(tempPath, fileData);


                Paper paper = _gradingService.LoadPaperFromFile(tempPath);
                if (paper == null || string.IsNullOrEmpty(paper.ExamCode))
                {
                    return BadRequest("Không thể đọc file hoặc không tìm thấy ExamCode trong file.");
                }

                string examCode = paper.ExamCode;


                var existingKey = _db.ExamAnswerKeys.FirstOrDefault(k => k.ExamCode == examCode);
                if (existingKey != null)
                {
                    existingKey.OriginalFileName = originalFileName;
                    existingKey.FileData = fileData;
                    existingKey.UploadedAt = DateTime.Now;
                }
                else
                {
                    var newKey = new ExamAnswerKey
                    {
                        ExamCode = examCode,
                        OriginalFileName = originalFileName,
                        FileData = fileData,
                        UploadedAt = DateTime.Now
                    };
                    _db.ExamAnswerKeys.Add(newKey);
                }

                await _db.SaveChangesAsync();

                return Ok($"Đã upload thành công đáp án cho môn: {examCode}");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
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