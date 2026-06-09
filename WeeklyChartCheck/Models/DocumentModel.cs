using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeeklyChartCheck.Models
{
    public class DocumentModel
    {
        public string PatientId { get; set; }
        public string DocumentType { get; set; }
        public string DocumentTemplate { get; set; }
        public string Author { get; set; }
        public string SupervisedBy { get; set; }
        public string SignedBy { get; set; }
        public string ApprovedBy { get; set; }
        public string ApprovalDate { get; set; }
        public string ApprovalStatus { get; set; }
        public bool Pass { get; set; }
        public string PassMessage { get; set; }

    }
}
