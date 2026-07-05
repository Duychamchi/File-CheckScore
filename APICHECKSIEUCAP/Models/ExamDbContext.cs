using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace APICHECKSIEUCAP.Models
{
    public class ExamDbContext : DbContext
    {
        public ExamDbContext() : base("name=ExamDbContext")
        {
        }

        public DbSet<ExamAnswerKey> ExamAnswerKeys { get; set; }
        public DbSet<UserSubmission> UserSubmissions { get; set; }
    }
}