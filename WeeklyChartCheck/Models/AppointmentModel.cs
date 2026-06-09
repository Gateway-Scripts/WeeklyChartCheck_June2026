using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeeklyChartCheck.Models
{
    public class AppointmentModel
    {
        public string Department { get; set; }
        public string TreatmentMachine { get; set; }
        public string StartTime { get; set; }
        public string AppointmentStatus { get; set; }
        public string MD { get; set; }
        public string Comment { get; set; }
        public string ActivityName { get; internal set; }
        public  bool Pass { get; set; }
        public string PassMessage { get; set; }
    }
}
