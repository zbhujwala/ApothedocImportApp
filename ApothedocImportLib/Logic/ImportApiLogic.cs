using ApothedocImportLib.DataItem;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApothedocImportLib.Logic
{
    public class ImportApiLogic : BaseLogic
    {
        private static string _resourceApi = "";
        public ImportApiLogic(string resourceApi)
        {
            _resourceApi = resourceApi;
        }

        public async Task TransferClinicDataAsync(
            string sourceOrgId,
            string sourceClinicId,
            string sourceAuthToken,
            string destOrgId,
            string destClinicId,
            string destAuthToken)
        {
            Thread loadingIndicator = new(ConsoleSpinner.StartLoadingIndicator);
            try
            {
                Dictionary<Patient, Tuple<List<CareSession>, EnrollmentStatus>> patientInfoDictionary = new();

                //Dictionary<Patient, List<CareSession>> patientCareSessionMap = new();
                //Dictionary<Patient, EnrollmentStatus> patientEnrollmentMap = new();

                LogDebug($">>> TransferClinicData called for OrgId: {sourceOrgId} and ClinicId: {sourceClinicId}");
                LogDebug($">>> Getting patient list for clinic...");
                loadingIndicator.Start();

                // Grab the patient list from both source and destination. If the patient is already in the destination, we will want the Patient Id so we can transfer over care sessions from source location
                var sourcePatientList = await GetPatientListForClinic(sourceOrgId, sourceClinicId, sourceAuthToken);
                var destPatientList = await GetPatientListForClinic(destOrgId, destClinicId, destAuthToken);

                LogDebug($">>> Successfully retrieved patient list");
                LogDebug($">>> Getting care sessions for patients...");

                // After we have retrieved all the patients from the source location, start grabbing their care sessions and enrollments and put them in a map
                foreach (var patient in sourcePatientList)
                {
                    var patientCareSessions = await GetPatientCareSessions(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    var patientEnrollment = await GetPatientEnrollmentStatus(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);

                    //patientCareSessionMap.Add(patient, patientCareSessions);
                    //patientEnrollmentMap.Add(patient, patientEnrollment);
                    patientInfoDictionary.Add(patient, Tuple.Create(patientCareSessions, patientEnrollment));
                }

                // Just counting for logging...
                int careSessionCount = 0;
                int enrollmentCount = 0;
                foreach (Tuple<List<CareSession>, EnrollmentStatus> patientInfoRecord in patientInfoDictionary.Values)
                {
                    careSessionCount += patientInfoRecord.Item1.Count;

                    if (patientInfoRecord.Item2.Rpm == true) enrollmentCount++;
                    if (patientInfoRecord.Item2.Ccm == true) enrollmentCount++;
                    if (patientInfoRecord.Item2.Bhi == true) enrollmentCount++;
                    if (patientInfoRecord.Item2.Pcm == true) enrollmentCount++;
                }

                LogDebug($">>> Successfully retrieved {sourcePatientList.Count} patients, {careSessionCount} care sessions, and {enrollmentCount} unique enrollments from OrgId: {sourceOrgId} and ClinicId: {sourceClinicId}.");
                LogDebug($">>> Attempting to post data to OrgId: {destOrgId} and ClinicId: {destClinicId}");

                // Time to start pushing up to destination location


                foreach (var patientInfoRecord in patientInfoDictionary)
                {
                    var patient = patientInfoRecord.Key;
                    var careSessions = patientInfoRecord.Value.Item1;
                    var enrollmentStatus = patientInfoRecord.Value.Item2;

                    // Start with posting up the patient
                    var newPatientId = await PostPatientToClinic(patient, destOrgId, destClinicId, destAuthToken);
                    if (string.IsNullOrEmpty(newPatientId))     // If we retreived null here, this means that there is a conflict and the patient with the MRN may already be in the destination clinic
                    {
                        var existingPatient = destPatientList.Find(p => p.Mrn == patient.Mrn);  // Try to find that patient, we will be moving over their care sessions from the source clinic

                        if (existingPatient != null)
                        {
                            newPatientId = existingPatient.Id.ToString();
                        }

                    }

                    // Post enrollment status
                    if (enrollmentStatus.Rpm == true)
                    {
                        var rpmEnrollmentDetails = await GetPatientEnrollmentDetails("rpm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        await PostEnrollmentsToClinic(rpmEnrollmentDetails, "rpm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                    if (enrollmentStatus.Ccm == true)
                    {
                        var ccmEnrollmentDetails = await GetPatientEnrollmentDetails("ccm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        await PostEnrollmentsToClinic(ccmEnrollmentDetails, "ccm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                    if (enrollmentStatus.Bhi == true)
                    {
                        var bhiEnrollmentDetails = await GetPatientEnrollmentDetails("bhi", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        await PostEnrollmentsToClinic(bhiEnrollmentDetails, "bhi", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                    if (enrollmentStatus.Pcm == true)
                    {
                        var pcmEnrollmentDetails = await GetPatientEnrollmentDetails("pcm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        await PostEnrollmentsToClinic(pcmEnrollmentDetails, "pcm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    // Post the care sessions after we confirmed the patient ID in the destination clinic
                    foreach (var careSession in careSessions)
                    {
                        await PostCareSessionToClinic(careSession, newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                }


                loadingIndicator.Interrupt();

                LogDebug($">>> Successfully posted patients and care sessions to OrgId: {destOrgId}, ClinicId: {destClinicId}");

                LogDebug($">>> Transfer clinic data process complete.");
            }
            catch (Exception ex)
            {
                loadingIndicator.Interrupt();
                LogError($">>> TransferClinicData failed.");
                LogError(ex.Message);
            }
        }

        #region API Request Functions
        public async Task<List<Patient>> GetPatientListForClinic(string orgId, string clinicId, string authToken)
        {

            try
            {
                // Console.WriteLine($">>> Attempting to retireve patients from OrgId: {orgId} and ClinicId: {clinicId}");
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/list");

                if (response.IsSuccessStatusCode)
                {

                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    PatientListWrapper wrapper = System.Text.Json.JsonSerializer.Deserialize<PatientListWrapper>(content, options);

                    // Console.WriteLine($">>> Successfully retrieved all patient data for OrgId: {orgId} and ClinicId: {clinicId}");

                    // Massage the data a bit...
                    wrapper.Patients.ForEach(patient =>
                    {
                        patient.DateOfBirth = DateTime.Parse(patient.DateOfBirth).ToString("MM/dd/yyyy");
                    });

                    return wrapper.Patients;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientList: {response.StatusCode}");
                }

            }
            catch (Exception)
            {
                Console.WriteLine(">>> GetPatientListForClinic failed.");
                throw;
            }
        }

        public async Task<List<CareSession>> GetPatientCareSessions(string orgId, string clinicId, string patientId, string sourceAuthToken)
        {
            try
            {
                if (string.IsNullOrEmpty(patientId))
                {
                    throw new Exception($"Cannot get care sessions of null Patient Id");
                }

                // Console.WriteLine($">>> Attempting to retireve care session from OrgId: {orgId}, ClinicId: {clinicId} and PatientId: {patientId}");
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sourceAuthToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/care-sessions");

                if (response.IsSuccessStatusCode)
                {

                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    CareSessionWrapper wrapper = System.Text.Json.JsonSerializer.Deserialize<CareSessionWrapper>(content, options);

                    // Console.WriteLine($">>> Successfully retrieved all care session data for OrgId: {orgId}, ClinicId: {clinicId} and PatientId: {patientId}");

                    wrapper.CareSessions.ForEach(session =>
                    {
                        session.PerformedOn = DateTime.Parse(session.PerformedOn).ToString("MM/dd/yyyy");
                        session.SubmittedAt = DateTime.Parse(session.SubmittedAt).ToString("MM/dd/yyyy");
                    });

                    return wrapper.CareSessions;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientList: {response.StatusCode}");
                }
            }
            catch (Exception)
            {
                LogError(">>> GetPatientCareSessions failed.");
                throw;
            }
        }

        public async Task<EnrollmentStatus> GetPatientEnrollmentStatus(string orgId, string clinicId, string patientId, string sourceAuthToken)
        {
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sourceAuthToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/enrollment/status");

                if (response.IsSuccessStatusCode)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    EnrollmentStatusWrapper wrapper = System.Text.Json.JsonSerializer.Deserialize<EnrollmentStatusWrapper>(content, options);

                    if (wrapper == null || wrapper.CurrentEnrollments == null)
                        throw new Exception($">>> No enrollment status found for user");

                    return wrapper.CurrentEnrollments;

                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientEnrollmentStatus");
                }
            }
            catch (Exception)
            {
                LogError(">>> GetPatientEnrollmentStatus faild.");
                throw;
            }
        }

        public async Task<Enrollment> GetPatientEnrollmentDetails(string enrollmentType, string orgId, string clinicId, string patientId, string sourceAuthToken)
        {
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sourceAuthToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/enrollment/{enrollmentType}");

                if (response.IsSuccessStatusCode)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    EnrollmentWrapper wrapper = System.Text.Json.JsonSerializer.Deserialize<EnrollmentWrapper>(content, options);

                    if (wrapper == null || wrapper.Enrollment == null)
                        throw new Exception($">>> No enrollment details found for user with enrollment type: {enrollmentType}");

                    if (wrapper.Enrollment.EnrollmentDate != null)
                        wrapper.Enrollment.EnrollmentDate = DateTime.Parse(wrapper.Enrollment.EnrollmentDate).ToString("MM/dd/yyyy");
                    if (wrapper.Enrollment.CancellationDate != null)
                        wrapper.Enrollment.CancellationDate = DateTime.Parse(wrapper.Enrollment.CancellationDate).ToString("MM/dd/yyyy");
                    if (wrapper.Enrollment.InformationSheet != null)
                        wrapper.Enrollment.InformationSheet = DateTime.Parse(wrapper.Enrollment.InformationSheet).ToString("MM/dd/yyyy");
                    if (wrapper.Enrollment.PatientAgreement != null)
                        wrapper.Enrollment.PatientAgreement = DateTime.Parse(wrapper.Enrollment.PatientAgreement).ToString("MM/dd/yyyy");

                    return wrapper.Enrollment;

                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientEnrollmentDetails");
                }
            }
            catch (Exception)
            {
                LogError(">>> GetPatientEnrollmentDetails faild.");
                throw;
            }
        }

        public async Task<List<User>> GetUserList(string orgId, string authToken)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/user/list");

            if (response.IsSuccessStatusCode)
            {
                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true
                };

                var content = await response.Content.ReadAsStringAsync();

                UserListWrapper wrapper = System.Text.Json.JsonSerializer.Deserialize<UserListWrapper>(content, options);

                return wrapper.Users;
            }
            else
            {
                throw new Exception($">>> Non-success HTTP Response code for TryGetProviderById");
            }
        }

        public async Task<string?> PostPatientToClinic(Patient patient, string destOrgId, string destClinicId, string destAuthToken)
        {
            try
            {
                // Console.WriteLine($">>> Attempting to post patient to OrgId: {destOrgId}, ClinicId: {destClinicId}");

                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", destAuthToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Make sure Patient Id is null here when we post it up
                patient.Id = null;

                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                };

                var json = SerializePatientContent(patient);

                var requestBody = new StringContent(json, Encoding.UTF8, "application/json");
                requestBody.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{destOrgId}/clinic-id/{destClinicId}/patient/new", requestBody);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await resp.Content.ReadAsStringAsync();

                    // Console.WriteLine($">>> Successfully posted PatientId {patient.Id} to OrgId: {destOrgId} and ClinicId: {destClinicId}");
                    PatientCreateResponse patientCreateResponse = System.Text.Json.JsonSerializer.Deserialize<PatientCreateResponse>(content, options);

                    return patientCreateResponse?.PatientId?.ToString();
                }
                else if (resp.StatusCode == HttpStatusCode.Conflict)
                {
                    // User has already been migrated, grab the ID and continue with the process so we can attempt to get the care sessions...
                    LogWarning($">>> Patient with same MRN exists in destination clinic as the one being migrated.");
                    return null;
                }
                else
                {
                    LogError($">>> Failed to post PatientId {patient.Id} to OrgId: {destOrgId} and ClinicId: {destClinicId}");
                    LogError(resp.StatusCode.ToString());
                    LogError(resp.Content.ToString());
                    return null;
                }
            }
            catch (Exception)
            {
                LogError(">>> PostPatientToClinic failed.");
                throw;
            }
        }

        public async Task PostCareSessionToClinic(CareSession careSession, string patientId, string orgId, string clinicId, string authToken)
        {
            try
            {
                if (string.IsNullOrEmpty(patientId))
                {
                    throw new Exception("PatientId is null, cannot upload care session");
                }
                // Console.WriteLine($">>> Attempting to post patient list to OrgId: {orgId}, ClinicId: {clinicId}");

                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var json = SerializeCareSessions(careSession);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/care-session", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    // Console.WriteLine($">>> Successfully posted care session for PatientId: {patientId}, CareSessionId: {careSession.Id}, OrgId: {orgId}, and ClinicId: {clinicId}");
                }
                else if (resp.StatusCode == HttpStatusCode.BadRequest)
                {
                    LogError(resp.Content.ToString());
                }
                else
                {
                    LogError($">>> Failed to post care session for PatientId: {patientId}, CareSessionId: {careSession.Id}, OrgId: {orgId}, and ClinicId: {clinicId}.");
                    LogError(resp.StatusCode.ToString());
                    LogError(resp.Content.ToString());
                }
            }
            catch (Exception)
            {
                LogError(">>> PostCareSessionToClinic failed.");
                throw;
            }
        }

        public async Task PostEnrollmentsToClinic(Enrollment enrollment, string enrollmentType, string patientId, string orgId, string clinicId, string authToken)
        {
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var json = SerializeEnrollment(enrollment);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/enrollment/{enrollmentType}", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine($">>> Successfully posted enrollment information for Patientid: {patientId}, Enrollment Type: {enrollmentType}, OrgId: {orgId}, and ClinicId: {clinicId}");
                }
                else if (resp.StatusCode == HttpStatusCode.BadRequest)
                {
                    LogError(resp.Content.ToString());
                }
                else
                {
                    Console.WriteLine($">>> Failed to post enrollment information for Patientid: {patientId}, Enrollment Type: {enrollmentType}, OrgId: {orgId}, and ClinicId: {clinicId}");
                    LogError(resp.StatusCode.ToString());
                    LogError(resp.Content.ToString());
                }
            }
            catch (Exception)
            {
                LogError(">>> PostEnrollmentsToClinic failed.");
                throw;
            }
        }
        #endregion

        #region Helper functions
        private static string SerializePatientContent(Patient patient)
        {
            try
            {
                var serializedPatient = new StringBuilder("{");
                if (!string.IsNullOrEmpty(patient.FirstName))
                    serializedPatient.Append($"\"firstName\":\"{patient.FirstName}\",");
                if (!string.IsNullOrEmpty(patient.MiddleName))
                    serializedPatient.Append($"\"middleName\":\"{patient.MiddleName}\",");
                if (!string.IsNullOrEmpty(patient.LastName))
                    serializedPatient.Append($"\"lastName\":\"{patient.LastName}\",");
                if (!string.IsNullOrEmpty(patient.Mrn))
                    serializedPatient.Append($"\"mrn\":\"{patient.Mrn}\",");
                if (!string.IsNullOrEmpty(patient.DateOfBirth))
                    serializedPatient.Append($"\"dateOfBirth\":\"{patient.DateOfBirth}\",");
                if (!string.IsNullOrEmpty(patient.PhoneNumber))
                    serializedPatient.Append($"\"phoneNumber\":\"{patient.PhoneNumber}\",");
                if (!string.IsNullOrEmpty(patient.Gender))
                    serializedPatient.Append($"\"gender\":\"{patient.Gender}\",");
                if (!string.IsNullOrEmpty(patient.PreferredName))
                    serializedPatient.Append($"\"preferredName\":\"{patient.PreferredName}\",");
                if (!string.IsNullOrEmpty(patient.MedicareId))
                    serializedPatient.Append($"\"medicareId\":\"{patient.MedicareId}\",");

                if (serializedPatient.Length > 1)
                    serializedPatient.Length--;

                serializedPatient.Append('}');

                return serializedPatient.ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static string SerializeCareSessions(CareSession careSession)
        {
            try
            {
                var serializedCareSession = new StringBuilder("{");
                if (!string.IsNullOrEmpty(careSession.CareType))
                    serializedCareSession.Append($"\"careType\":\"{careSession.CareType}\",");
                if (careSession.UsingManualTimeEntry != null)
                    serializedCareSession.Append($"\"usingManualTimeEntry\":{(careSession.UsingManualTimeEntry == 1).ToString().ToLower()},");
                if (careSession.DurationSeconds != null)
                    serializedCareSession.Append($"\"durationSeconds\":\"{careSession.DurationSeconds}\",");
                if (!string.IsNullOrEmpty(careSession.PerformedOn))
                    serializedCareSession.Append($"\"performedOn\":\"{careSession.PerformedOn}\",");
                if (careSession.PerformedBy != null)
                    serializedCareSession.Append($"\"performedBy\": {SerializeUser(careSession.PerformedBy)},");
                if (!string.IsNullOrEmpty(careSession.CareNote))
                    serializedCareSession.Append($"\"careNote\":\"{careSession.CareNote}\",");
                if (careSession.ComplexCare != null)
                    serializedCareSession.Append($"\"complexCare\":{(careSession.ComplexCare == 1).ToString().ToLower()},");
                if (careSession.InteractedWithPatient != null)
                    serializedCareSession.Append($"\"interactedWithPatient\":{(careSession.InteractedWithPatient == 1).ToString().ToLower()},");

                if (serializedCareSession.Length > 1)
                    serializedCareSession.Length--;

                serializedCareSession.Append('}');

                return serializedCareSession.ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static string SerializeUser(User user)
        {
            try
            {
                var serializedUser = new StringBuilder("{");
                if (!string.IsNullOrEmpty(user.id.ToString()))
                    serializedUser.Append($"\"id\":\"{user.id.ToString()}\",");
                if (!string.IsNullOrEmpty(user.firstName))
                    serializedUser.Append($"\"firstName\":\"{user.firstName}\",");
                if (!string.IsNullOrEmpty(user.lastName))
                    serializedUser.Append($"\"lastName\":\"{user.lastName}\",");

                if (serializedUser.Length > 1)
                    serializedUser.Length--;

                serializedUser.Append('}');

                return serializedUser.ToString();
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static string SerializeEnrollment(Enrollment enrollment)
        {
            try
            {
                var serializedEnrollment = new StringBuilder("{");
                if (!string.IsNullOrEmpty(enrollment.EnrollmentDate))
                    serializedEnrollment.Append($"\"enrollmentDate\":\"{enrollment.EnrollmentDate}\",");
                if (!string.IsNullOrEmpty(enrollment.CancellationDate))
                    serializedEnrollment.Append($"\"cancelationDate\":\"{enrollment.CancellationDate}\",");
                if (!string.IsNullOrEmpty(enrollment.InformationSheet))
                    serializedEnrollment.Append($"\"informationSheet\":\"{enrollment.InformationSheet}\",");
                if (!string.IsNullOrEmpty(enrollment.PatientAgreement))
                    serializedEnrollment.Append($"\"patientAgreement\":\"{enrollment.PatientAgreement}\",");
                if (enrollment.VerbalAgreement != null)
                    serializedEnrollment.Append($"\"verbalAgreement\":\"{enrollment.VerbalAgreement}\",");
                if (enrollment.PrimaryClinician != null)
                    serializedEnrollment.Append($"\"primaryClinician\":\"{SerializeUser(enrollment.PrimaryClinician)}\",");
                if (enrollment.Specialist != null)
                    serializedEnrollment.Append($"\"specialist\":\"{SerializeUser(enrollment.Specialist)}\",");
                if (!string.IsNullOrEmpty(enrollment.EquipmentSetupAndEducation))
                    serializedEnrollment.Append($"\"equipmentSetupAndEducation\":\"{enrollment.EquipmentSetupAndEducation}\",");
                if (enrollment.EnrolledSameDayOfficeVisit != null)
                    serializedEnrollment.Append($"\"enrolledSameDayOfficeVisit\":\"{(enrollment.EnrolledSameDayOfficeVisit == 1).ToString().ToLower()}\",");

                if (serializedEnrollment.Length > 1)
                    serializedEnrollment.Length--;

                serializedEnrollment.Append('}');

                return serializedEnrollment.ToString();

            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

    }
}
