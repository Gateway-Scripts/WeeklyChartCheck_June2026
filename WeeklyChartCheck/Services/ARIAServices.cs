using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using WeeklyChartCheck.Models;

namespace WeeklyChartCheck.Services
{
    //in C#, when you create a class, you're creating an instance of that object. 
    //in this example we do not want to create instances of ARIAServices.
    //As a static class, we can use methods and properties within the class without having multiple instances of the same class.
    //(i.e. we wouldn't want to connect to the ARIA API multiplie times.
    public static class ARIAServices
    {
        //Configure ARIA API URLs
        static string tokenUrl = "https://master-ae.vic.com:44333/tokenservice/connect/token";
        static string baseUrl = "https://master-ae:55370/fhir/r4";
        //TODO Replace here with your own client id and secret
        //===================================
        static string clientId = "c8f6cdaf-b492-4958-a75a-9a3730c05e01";
        static string clientSecret = "GatewayScripts_Varian!2026";
        static string scopes = "system/ActivityDefinition.rs system/AllergyIntolerance.cruds system/AllergyIntolerance.rs system/Appointment.cruds system/Appointment.rs system/AuditEvent.c system/AuditEvent.cruds system/BodyStructure.rs system/CarePlan.rs system/CareTeam.cruds system/CareTeam.rs system/ChargeItem.cruds system/ChargeItem.rs system/Condition.cruds system/Condition.rs system/Device.rs system/DocumentReference.cruds system/DocumentReference.rs system/Group.rs system/HealthcareService.rs system/Location.rs system/Observation.rs system/Organization.rs system/Patient.cruds system/Patient.rs system/Practitioner.cruds system/Practitioner.rs system/Procedure.rs system/ServiceRequest.rs system/Task.cruds system/Task.rs system/ValueSet.rs user/ActivityDefinition.rs user/AllergyIntolerance.cruds user/AllergyIntolerance.rs user/Appointment.cruds user/Appointment.rs user/AuditEvent.c user/AuditEvent.cruds user/BodyStructure.rs user/CarePlan.rs user/CareTeam.cruds user/CareTeam.rs user/ChargeItem.cruds user/ChargeItem.rs user/Condition.cruds user/Condition.rs user/Device.rs user/DocumentReference.cruds user/DocumentReference.rs user/Group.rs user/HealthcareService.rs user/Location.rs user/Observation.rs user/Organization.rs user/Patient.cruds user/Patient.rs user/Practitioner.cruds user/Practitioner.rs user/Procedure.rs user/ServiceRequest.rs user/Task.cruds user/Task.rs user/ValueSet.rs";
        private static HttpClient _client;
        public static void Initialize()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _client = new HttpClient(handler);

            Dictionary<string, string> credentials = new Dictionary<string, string>();
            credentials.Add("grant_type", "client_credentials");
            credentials.Add("client_id", clientId);
            credentials.Add("client_secret", clientSecret);
            credentials.Add("scope", scopes);

            Console.WriteLine("Requesting bearer token");
            var response = _client.PostAsync(tokenUrl, new FormUrlEncodedContent(credentials));
            var result = response.Result.Content.ReadAsStringAsync();
            var tokenJson = JObject.Parse(result.Result);
            var token = tokenJson["access_token"].ToString();
            Console.WriteLine($"Bearer token acquired: {token}");
            //now that we have the bearer token, this is the authentication mechanism.
            //set the authorization of the client.
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            _client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");
        }
        public static List<DocumentModel> FindDocuments(string patientId)
        {
            List<DocumentModel> documents = new List<DocumentModel>();

            Console.WriteLine("Fetching valid document types from ValueSet...");
            var documentTypeMapping = GetValidDocumentTypes(baseUrl, _client, "Varian");

            string url = $"{baseUrl}/DocumentReference?patient={patientId}&_pretty=true";
            Console.WriteLine($"Requesting documents for patient: {patientId}");
            while (!String.IsNullOrEmpty(url))
            {

                var response = _client.GetAsync(url);
                var result = response.Result.Content.ReadAsStringAsync().Result;

                var bundle = JObject.Parse(result);

                if (bundle["entry"] != null)
                {
                    foreach (var entry in bundle["entry"])
                    {
                        var resource = entry["resource"];
                        var document = new DocumentModel();

                        document.PatientId = patientId;
                        document.ApprovalStatus = resource["docStatus"]?.ToString();

                        if (resource["type"] != null)
                        {
                            if (resource["type"]["coding"] != null && resource["type"]["coding"].Any())
                            {
                                string documentTypeCode = resource["type"]["coding"][0]["code"]?.ToString();
                                if (!string.IsNullOrEmpty(documentTypeCode) && documentTypeMapping.ContainsKey(documentTypeCode))
                                {
                                    document.DocumentType = documentTypeMapping[documentTypeCode];
                                }
                                else
                                {
                                    document.DocumentType = resource["type"]["coding"][0]["display"]?.ToString();
                                }
                            }
                        }

                        if (resource["category"] != null && resource["category"].Any())
                        {
                            var category = resource["category"][0];
                            if (category["coding"] != null && category["coding"].Any())
                            {
                                document.DocumentTemplate = category["coding"][0]["display"]?.ToString();
                            }
                        }

                        if (resource["author"] != null && resource["author"].Any())
                        {
                            string authorRef = resource["author"][0]["reference"]?.ToString();
                            if (!string.IsNullOrEmpty(authorRef) && authorRef.StartsWith("Practitioner/"))
                            {
                                string fhirId = authorRef.Substring("Practitioner/".Length);
                                document.Author = GetClientValueFromFHIRId("Practitioner", fhirId);
                            }
                        }

                        if (resource["extension"] != null)
                        {
                            foreach (var extension in resource["extension"])
                            {
                                string extensionUrl = extension["url"]?.ToString();

                                if (extensionUrl != null && extensionUrl.Contains("supervisor"))
                                {
                                    var supervisorRef = extension["valueReference"];
                                    if (supervisorRef != null)
                                    {
                                        string reference = supervisorRef["reference"]?.ToString();
                                        if (!string.IsNullOrEmpty(reference) && reference.StartsWith("Practitioner/"))
                                        {
                                            string fhirId = reference.Substring("Practitioner/".Length);
                                            document.SupervisedBy = GetClientValueFromFHIRId("Practitioner", fhirId);
                                        }
                                    }
                                }
                                else if (extensionUrl != null && extensionUrl.Contains("signature"))
                                {
                                    var signerRef = extension["extension"]?.FirstOrDefault(e => e["url"].ToString().Contains("signer"));
                                    if (signerRef != null)
                                    {
                                        string reference = signerRef["valueReference"]?["display"]?.ToString();
                                        if (!string.IsNullOrEmpty(reference) && reference.StartsWith("Practitioner/"))
                                        {
                                            string fhirId = reference.Substring("Practitioner/".Length);
                                            document.SignedBy = GetClientValueFromFHIRId("Practitioner", fhirId);
                                        }
                                        else { document.SignedBy = reference; }
                                    }
                                }
                                else if (extensionUrl != null && extensionUrl.Contains("approvedBy"))
                                {
                                    var approverRef = extension["valueReference"];
                                    if (approverRef != null)
                                    {
                                        string reference = approverRef["reference"]?.ToString();
                                        if (!string.IsNullOrEmpty(reference) && reference.StartsWith("Practitioner/"))
                                        {
                                            string fhirId = reference.Substring("Practitioner/".Length);
                                            document.ApprovedBy = GetClientValueFromFHIRId("Practitioner", fhirId);
                                        }
                                    }
                                }
                                else if (extensionUrl != null && extensionUrl.Contains("authenticated"))
                                {
                                    document.ApprovalDate = extension["valueDateTime"]?.ToString();
                                }
                            }
                        }
                        //if (resource["authenticated"] != null)
                        //{
                        //    document.ApprovalDate = resource["authenticated"]?.ToString();
                        //}



                        if (resource["authenticator"] != null)
                        {
                            string authenticatorRef = resource["authenticator"]["reference"]?.ToString();
                            if (!string.IsNullOrEmpty(authenticatorRef) && authenticatorRef.StartsWith("Practitioner/"))
                            {
                                string fhirId = authenticatorRef.Substring("Practitioner/".Length);
                                if (string.IsNullOrEmpty(document.ApprovedBy))
                                {
                                    document.ApprovedBy = GetClientValueFromFHIRId("Practitioner", fhirId);
                                }
                            }
                        }
                        if (resource["signature"] != null)
                        {
                            string signatureRef = resource["signature"]["reference"]?.ToString();
                            if (!string.IsNullOrEmpty(signatureRef) && signatureRef.StartsWith("Practitioner/"))
                            {
                                string fhirId = signatureRef.Substring("Practitioner/".Length);
                                if (string.IsNullOrEmpty(document.SignedBy))
                                {
                                    document.SignedBy = GetClientValueFromFHIRId("Practitioner", fhirId);
                                }
                            }
                        }
                        if (String.IsNullOrEmpty(document.ApprovalStatus) ||
                            !document.ApprovalStatus.Equals("final", StringComparison.OrdinalIgnoreCase))
                        {
                            document.Pass = false;
                            document.PassMessage = "Document status is not approved (final)";
                        }
                        else
                        {
                            document.Pass = true;
                        }
                        documents.Add(document);
                    }
                }
                url = null;
                //prepare for nex page
                var links = bundle["link"];
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        if (link["relation"]?.ToString() == "next")
                        {
                            url = link["url"]?.ToString();
                            break;
                        }
                    }
                }
            }

            Console.WriteLine($"Found {documents.Count} documents");
            return documents;
        }
        public static string FindFHIRId(string profile, string appId)
        {
            //string searchParams = String.Join("&", search.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string patientUrl = $"{baseUrl}/{profile}?identifier={appId}&_pretty=true";
            var patientResponse = _client.GetAsync(patientUrl);
            var patientResult = patientResponse.Result.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"{profile} Idenfitied: {patientResult}");
            return JObject.Parse(patientResult)["entry"][0]["resource"]["id"].ToString();
        }
        private static Dictionary<string, string> GetValidDocumentTypes(string baseUrl, HttpClient client, string publisher)
        {
            var documentTypes = new Dictionary<string, string>();
            try
            {
                string valueSetUrl = $"{baseUrl}/ValueSet/$expand?url=http://varian.com/fhir/ValueSet/documentreference-type&publisher={publisher}";
                Console.WriteLine($"Querying: {valueSetUrl}");
                var response = client.GetAsync(valueSetUrl);
                var result = response.Result.Content.ReadAsStringAsync().Result;

                if (response.Result.IsSuccessStatusCode)
                {

                    var valueSetJson = JObject.Parse(result);
                    foreach (var entry in valueSetJson["entry"])
                    {
                        var expansion = entry["resource"]["expansion"];
                        if (expansion != null && expansion["contains"] != null)
                        {
                            foreach (var item in expansion["contains"])
                            {
                                documentTypes.Add(item["code"]?.ToString(), item["display"]?.ToString());
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Could not fetch document types. Status: {response.Result.StatusCode}");
                    Console.WriteLine($"Response: {result}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error fetching document types: {ex.Message}");
            }

            return documentTypes;
        }
        public static string GetClientValueFromFHIRId(string resourceType, string fhirId)
        {
            try
            {
                string url = $"{baseUrl}/{resourceType}/{fhirId}?_pretty=true";
                Console.WriteLine($"Resolving {resourceType} with ID: {fhirId}");

                var response = _client.GetAsync(url);
                var result = response.Result.Content.ReadAsStringAsync().Result;
                var resource = JObject.Parse(result);

                if (resourceType == "HealthcareService")
                {
                    return resource["name"]?.ToString();
                }
                else if (resourceType == "Device")
                {
                    if (resource["identifier"] != null)
                    {
                        return resource["identifier"]?[0]?["value"]?.ToString();
                    }

                }
                else if (resourceType == "Practitioner")
                {
                    if (resource["name"] != null && resource["name"].Any())
                    {
                        var name = resource["name"][0];
                        string given = name["given"]?[0]?.ToString();
                        string family = name["family"]?.ToString();

                        if (!string.IsNullOrEmpty(given) && !string.IsNullOrEmpty(family))
                        {
                            return $"{given} {family}";
                        }
                        else if (!string.IsNullOrEmpty(family))
                        {
                            return family;
                        }
                        else if (!string.IsNullOrEmpty(given))
                        {
                            return given;
                        }
                    }
                    return resource["identifier"]?[0]?["value"]?.ToString();
                }

                return resource["identifier"]?[0]?["value"]?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resolving {resourceType}/{fhirId}: {ex.Message}");
                return null;
            }
        }
        public static List<AppointmentModel> FindAppointments(string patientId)
        {
            List<AppointmentModel> appointments = new List<AppointmentModel>();

            string url = $"{baseUrl}/Appointment?patient={patientId}&_pretty=true";
            int pageCount = 0;
            Console.WriteLine($"Requesting appointments for patient: {patientId}");
            while (!String.IsNullOrEmpty(url))
            {
                pageCount++;
                var response = _client.GetAsync(url);
                var result = response.Result.Content.ReadAsStringAsync().Result;

                var bundle = JObject.Parse(result);

                if (bundle["entry"] != null)
                {
                    foreach (var entry in bundle["entry"])
                    {
                        var resource = entry["resource"];
                        var appointment = new AppointmentModel();

                        appointment.StartTime = resource["start"]?.ToString();
                        appointment.Comment = resource["comment"]?.ToString();

                        if (resource["extension"] != null)
                        {
                            foreach (var extension in resource["extension"])
                            {
                                string extensionUrl = extension["url"]?.ToString();
                                if (extensionUrl != null && extensionUrl.Contains("appointment-ariaStatus"))
                                {
                                    var valueCodeableConcept = extension["valueCodeableConcept"];
                                    if (valueCodeableConcept != null && valueCodeableConcept["coding"] != null && valueCodeableConcept["coding"].Any())
                                    {
                                        appointment.AppointmentStatus = valueCodeableConcept["coding"][0]["display"]?.ToString();
                                    }
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(appointment.AppointmentStatus))
                        {
                            appointment.AppointmentStatus = resource["status"]?.ToString();
                        }

                        if (resource["serviceType"] != null && resource["serviceType"].Any())
                        {
                            var serviceType = resource["serviceType"][0];
                            if (serviceType["coding"] != null && serviceType["coding"].Any())
                            {
                                appointment.ActivityName = serviceType["coding"][0]["display"]?.ToString();
                            }
                        }

                        if (resource["participant"] != null)
                        {
                            foreach (var participant in resource["participant"])
                            {
                                var actor = participant["actor"];
                                if (actor != null)
                                {
                                    string reference = actor["reference"]?.ToString();

                                    if (reference != null)
                                    {
                                        if (reference.StartsWith("HealthcareService/"))
                                        {
                                            string fhirId = reference.Substring("HealthcareService/".Length);
                                            appointment.Department = GetClientValueFromFHIRId("HealthcareService", fhirId);
                                        }
                                        else if (reference.StartsWith("Device/"))
                                        {
                                            string fhirId = reference.Substring("Device/".Length);
                                            appointment.TreatmentMachine = GetClientValueFromFHIRId("Device", fhirId);
                                        }
                                        else if (reference.StartsWith("Practitioner/"))
                                        {
                                            string fhirId = reference.Substring("Practitioner/".Length);
                                            appointment.MD = GetClientValueFromFHIRId("Practitioner", fhirId);
                                        }
                                    }
                                }
                            }
                        }
                        appointment.Pass = true;
                        if (!String.IsNullOrEmpty(appointment.StartTime))
                        {

                            DateTime startTime;
                            if (DateTime.TryParse(appointment.StartTime, out startTime))
                            {
                                if (DateTime.Now > startTime.AddHours(12) && appointment.AppointmentStatus == "Open")
                                {
                                    appointment.Pass = false;
                                    appointment.PassMessage = "Appointment cannot be open after 12 hours past start time";
                                }
                            }
                        }
                        appointments.Add(appointment);
                    }
                }
                url = null;
                //prepare for nex page
                var links = bundle["link"];
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        if (link["relation"]?.ToString() == "next")
                        {
                            url = link["url"]?.ToString();
                            break;
                        }
                    }
                }
            }
            Console.WriteLine($"Found {appointments.Count} appointments");
            return appointments;
        }
    }
}
