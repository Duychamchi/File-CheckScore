using QuestionLib;
using QuestionLib.Entity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using APICHECKSIEUCAP.Models;
using EncryptData;

namespace APICHECKSIEUCAP.Services
{
    public class GradingService
    {
        private List<Question> GetQuestionsFromPaper(Paper paper)
        {
            var allQuestions = new List<Question>();

            if (paper?.GrammarQuestions != null)
            {
                var grammarQuestions = paper.GrammarQuestions.Cast<object>()
                                            .Select(item => (item is Question q) ? q : (item as JObject)?.ToObject<Question>())
                                            .Where(q => q != null);
                allQuestions.AddRange(grammarQuestions);
            }

            if (paper?.ReadingQuestions != null)
            {
                foreach (var passageObj in paper.ReadingQuestions)
                {
                    if (passageObj == null) continue;

                    try
                    {
                        JObject passageJObject = passageObj as JObject ?? JObject.FromObject(passageObj);

                        if (passageJObject != null && passageJObject["PassageQuestions"] is JArray passageQuestionsArray)
                        {
                            var passageQuestions = passageQuestionsArray.Cast<object>()
                                                    .Select(item => (item is Question q) ? q : (item as JObject)?.ToObject<Question>())
                                                    .Where(q => q != null);
                            allQuestions.AddRange(passageQuestions);
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }

            return allQuestions.OrderBy(q => q.QID).ToList();
        }

        public Paper LoadPaperFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Không tìm thấy file", filePath);
            }

            try
            {
                string extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".dat")
                {
                    SubmitPaper submitPaper = LoadSubmitPaper_FromDat(filePath);
                    return submitPaper?.SPaper;
                }
                else if (extension == ".json")
                {
                    return LoadPaper_FromJson(filePath);
                }
                else
                {
                    throw new NotSupportedException($"Định dạng file '{extension}' không được hỗ trợ.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi tải file '{Path.GetFileName(filePath)}': {ex.Message}", ex);
            }
        }

        private SubmitPaper LoadSubmitPaper_FromDat(string savedFile)
        {
            byte[] decryptedData = EncryptSupport.DecryptQuestions_FromFile(savedFile, "fpt-univ");
            string currentDllVersion = typeof(SubmitPaper).Assembly.FullName;
            string requiredDatVersion = ExtractRequiredVersion(decryptedData);

            if (string.IsNullOrEmpty(requiredDatVersion) || !currentDllVersion.Equals(requiredDatVersion, StringComparison.OrdinalIgnoreCase))
            {
                string errorMsg = $"Lỗi tương thích phiên bản. Yêu cầu: '{requiredDatVersion}', Hiện tại: '{currentDllVersion}'";
                throw new InvalidOperationException(errorMsg);
            }

            return (SubmitPaper)EncryptSupport.ByteArrayToObject(decryptedData);
        }

        private Paper LoadPaper_FromJson(string filePath)
        {
            string jsonContent = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Paper>(jsonContent, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });
        }

        public GradingResult GradeSubmission(Paper keyPaper, Paper submissionPaper)
        {
            var result = new GradingResult();

            if (keyPaper.ExamCode != submissionPaper.ExamCode)
            {
                result.Message = "LỖI: MÃ MÔN HỌC KHÔNG KHỚP!";
                return result;
            }

            var keyQuestions = GetQuestionsFromPaper(keyPaper);
            var submissionQuestions = GetQuestionsFromPaper(submissionPaper);
            int totalQuestions = submissionQuestions.Count;

            if (keyQuestions.Count != totalQuestions)
            {
                result.Message = $"LỖI: Số lượng câu hỏi không khớp. Key: {keyQuestions.Count}, Submission: {totalQuestions}";
                return result;
            }

            int correctCount = 0;

            for (int i = 0; i < totalQuestions; i++)
            {
                var keyQuestion = keyQuestions[i];
                var studentQuestion = submissionQuestions[i];

                // 1. Định nghĩa logic đánh dấu
                Func<QuestionAnswer, bool> isCorrectKey = (qa) => qa.Chosen || qa.Selected;
                Func<QuestionAnswer, bool> isStudentChosen = (qa) => qa.Selected;

                // 2. Hàm helper lấy ID (để chấm điểm)
                Func<Question, Func<QuestionAnswer, bool>, List<int>> GetMarkedQAIDs = (q, marker) =>
                {
                    return q.QuestionAnswers.Cast<object>()
                        .Select(item => (item is QuestionAnswer ans) ? ans : (item as JObject)?.ToObject<QuestionAnswer>())
                        .Where(qa => qa != null && marker(qa))
                        .Select(qa => qa.QAID)
                        .OrderBy(id => id)
                        .ToList();
                };

                // 3. Hàm helper lấy NỘI DUNG CHỮ (để hiển thị)
                // Chúng ta luôn lấy Text từ keyQuestion vì submissionPaper thường bị xóa Text để nhẹ file
                Func<Question, List<int>, List<string>> GetAnswerTextsByIds = (q, ids) =>
                {
                    return q.QuestionAnswers.Cast<object>()
                        .Select(item => (item is QuestionAnswer ans) ? ans : (item as JObject)?.ToObject<QuestionAnswer>())
                        .Where(qa => qa != null && ids.Contains(qa.QAID))
                        .Select(qa => qa.Text) // Lấy nội dung chữ
                        .ToList();
                };

                // Chấm điểm dựa trên ID
                var correctQAIDs = GetMarkedQAIDs(keyQuestion, isCorrectKey);
                var studentSelectedQAIDs = GetMarkedQAIDs(studentQuestion, isStudentChosen);

                // Lấy chữ dựa trên ID đã tìm được
                var correctTexts = GetAnswerTextsByIds(keyQuestion, correctQAIDs);
                var studentSelectedTexts = GetAnswerTextsByIds(keyQuestion, studentSelectedQAIDs);

                bool isCorrect = correctQAIDs.SequenceEqual(studentSelectedQAIDs);

                if (isCorrect)
                {
                    correctCount++;
                }

                // Lưu chi tiết vào danh sách trả về
                result.QuestionDetails.Add(new QuestionAnswerDetail
                {
                    QuestionId = keyQuestion.QID,
                    Content = keyQuestion.Text, //
                    Image = (keyQuestion.ImageData != null && keyQuestion.ImageData.Length > 0)
                            ? Convert.ToBase64String(keyQuestion.ImageData) //
                            : null,

                    // Đổ nội dung chữ vào đây để Postman hiển thị
                    StudentSelectedIds = studentSelectedTexts,
                    CorrectAnswerIds = correctTexts,
                    IsCorrect = isCorrect
                });
            }

            result.Score = correctCount;
            result.TotalQuestions = totalQuestions;
            result.FinalGrade = totalQuestions > 0 ? ((double)correctCount / totalQuestions) * 10.0 : 0;
            result.Message = "Chấm điểm thành công.";

            return result;
        }

        public string ExtractRequiredVersion(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                string textData = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 2048));
                var regex = new Regex(@"QuestionLib, Version=[\d\.]+, Culture=[\w-]+, PublicKeyToken=[\w\d]+|QuestionLib, Version=[\d\.]+, Culture=[\w-]+, PublicKeyToken=null");
                var match = regex.Match(textData);
                return match.Success ? match.Value : null;
            }
            catch { return null; }
        }


        public List<Question> GetAllQuestionsFlat(Paper paper)
        {
            var allQuestions = new List<Question>();

            if (paper?.GrammarQuestions != null)
            {
                var grammarQuestions = paper.GrammarQuestions.Cast<object>()
                                            .Select(item => (item is Question q) ? q : (item as JObject)?.ToObject<Question>())
                                            .Where(q => q != null);
                allQuestions.AddRange(grammarQuestions);
            }


            if (paper?.ReadingQuestions != null)
            {
                foreach (var passageObj in paper.ReadingQuestions)
                {
                    if (passageObj == null) continue;

                    try
                    {
                        JObject passageJObject = passageObj as JObject ?? JObject.FromObject(passageObj);

                        if (passageJObject != null && passageJObject["PassageQuestions"] is JArray passageQuestionsArray)
                        {
                            var passageQuestions = passageQuestionsArray.Cast<object>()
                                                    .Select(item => (item is Question q) ? q : (item as JObject)?.ToObject<Question>())
                                                    .Where(q => q != null);
                            allQuestions.AddRange(passageQuestions);
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }

            return allQuestions; 
        }
    }
}