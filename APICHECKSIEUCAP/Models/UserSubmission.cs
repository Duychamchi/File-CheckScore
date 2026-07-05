using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace APICHECKSIEUCAP.Models
{
    [Table("UserSubmissions")]
    public class UserSubmission
    {
        [Key]
        public int Id { get; set; }

     
        [StringLength(100)]
        public string StudentIdentifier { get; set; }

        [Required]
        [StringLength(100)]
        public string ExamCode { get; set; }

        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; }

        [Required]
        public byte[] FileData { get; set; }

        public int Score { get; set; }

        public int TotalQuestions { get; set; }

        public decimal FinalGrade { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}