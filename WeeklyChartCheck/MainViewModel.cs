using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WeeklyChartCheck.Models;
using WeeklyChartCheck.Services;

namespace WeeklyChartCheck
{
    public class MainViewModel
    {
        public string PatientId { get; set; }
        public ObservableCollection<AppointmentModel> Appointments { get; set; }
        public ObservableCollection<DocumentModel> Documents { get; set; }
        //inialize and set defaults 
        public MainViewModel(string patientId)
        {
            PatientId = patientId;
            Appointments = new ObservableCollection<AppointmentModel>();
            Documents = new ObservableCollection<DocumentModel>();

            CollectFHIRData();
        }

        private void CollectFHIRData()
        {
            ARIAServices.Initialize();
            string patientFHIRId = ARIAServices.FindFHIRId("Patient", PatientId);

            var appointments = ARIAServices.FindAppointments(patientFHIRId);
            foreach (var appointment in appointments)
            {
                Appointments.Add(appointment);
            }

            var documents = ARIAServices.FindDocuments(patientFHIRId);
            foreach (var document in documents)
            {
                Documents.Add(document);
            }
        }
    }
}
