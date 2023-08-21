using ApothedocImportLib.DataItem;
using ApothedocImportLib.Utils;
using Newtonsoft.Json;
using Serilog;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApothedocImportLib.Logic
{
    public class ImportApiLogic
    {
        #region Constructor and Members
        private static string _resourceApi = "";
        public ImportApiLogic(string resourceApi)
        {
            _resourceApi = resourceApi;
        }
        #endregion

        #region Main Procedure
        public async Task TransferClinicDataAsync(
            string sourceOrgId,
            string sourceClinicId,
            string sourceAuthToken,
            string destOrgId,
            string destClinicId,
            string destAuthToken,
            bool skipCareSessionImport = false)
        {
            try
            {
                UserMappingUtil userMappingUtil = new();
                ConfigUtil configUtil = new();

                Dictionary<Patient, Tuple<List<CareSession>, EnrollmentStatus>> patientInfoDictionary = new();

                Log.Information($">>> TransferClinicData called for OrgId: {sourceOrgId} and ClinicId: {sourceClinicId}");
                Log.Information($">>> Getting patient and user information from source clinic...");

                // Grab the patient list from both source and destination. If the patient is already in the destination, we will want the Patient Id so we can transfer over care sessions from source location
                var sourcePatientList = await GetPatientListForClinic(sourceOrgId, sourceClinicId, sourceAuthToken);
                var destPatientList = await GetPatientListForClinic(destOrgId, destClinicId, destAuthToken);

                var targetProvidersList = await GetProviderList(destOrgId, destClinicId, destAuthToken);
                var targetUserList = await GetUserList(destOrgId, destAuthToken);

                var mappings = configUtil.LoadConfig().Mappings;

                Log.Information($">>> Getting care sessions and enrollment status for patients...");
                // After we have retrieved all the patients from the source location, start grabbing their care sessions and enrollments and put them in a map
                foreach (var patient in sourcePatientList)
                {
                    var patientCareSessions = new List<CareSession>();
                    if (!skipCareSessionImport)
                    {
                        patientCareSessions = await GetPatientCareSessions(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    }
                    
                    var patientEnrollment = await GetPatientEnrollmentStatus(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);

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

                Log.Information($">>> Successfully retrieved {sourcePatientList.Count} patients, {careSessionCount} care sessions, and {enrollmentCount} unique enrollments from OrgId: {sourceOrgId} and ClinicId: {sourceClinicId}.");
                Log.Information($">>> Attempting to post data to OrgId: {destOrgId} and ClinicId: {destClinicId}");

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

                    //// Post enrollment status
                    if (enrollmentStatus.Rpm == true)
                    {
                        var rpmEnrollmentDetails = await GetPatientEnrollmentDetails("rpm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        rpmEnrollmentDetails = userMappingUtil.MapEnrollmentUserInfo(rpmEnrollmentDetails, destClinicId, targetUserList, targetProvidersList, mappings);
                        await PostEnrollmentsToClinic(rpmEnrollmentDetails, "rpm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                    if (enrollmentStatus.Ccm == true)
                    {
                        var ccmEnrollmentDetails = await GetPatientEnrollmentDetails("ccm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        ccmEnrollmentDetails = userMappingUtil.MapEnrollmentUserInfo(ccmEnrollmentDetails, destClinicId, targetUserList, targetProvidersList, mappings);
                        await PostEnrollmentsToClinic(ccmEnrollmentDetails, "ccm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                    if (enrollmentStatus.Bhi == true)
                    {
                        var bhiEnrollmentDetails = await GetPatientEnrollmentDetails("bhi", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        bhiEnrollmentDetails = userMappingUtil.MapEnrollmentUserInfo(bhiEnrollmentDetails, destClinicId, targetUserList, targetProvidersList, mappings);
                        await PostEnrollmentsToClinic(bhiEnrollmentDetails, "bhi", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                    if (enrollmentStatus.Pcm == true)
                    {
                        var pcmEnrollmentDetails = await GetPatientEnrollmentDetails("pcm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        pcmEnrollmentDetails = userMappingUtil.MapEnrollmentUserInfo(pcmEnrollmentDetails, destClinicId, targetUserList, targetProvidersList, mappings);
                        await PostEnrollmentsToClinic(pcmEnrollmentDetails, "pcm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    // Post the care sessions after we confirmed the patient ID in the destination clinic
                    careSessions = userMappingUtil.MapCareSessionProvidersAndSubmitters(careSessions, targetProvidersList, targetUserList, mappings);
                    foreach (var careSession in careSessions)
                    {
                        await PostCareSessionToClinic(careSession, newPatientId, destOrgId, destClinicId, destAuthToken);
                    }
                }

                Log.Information($">>> Successfully posted patients and care sessions to OrgId: {destOrgId}, ClinicId: {destClinicId}");

                Log.Information($">>> Transfer clinic data process complete.");
            }
            catch (Exception ex)
            {
                Log.Error($">>> TransferClinicData failed.");
                Log.Error(ex.Message);
            }
        }
        #endregion

        #region API Request Functions
        public async Task<List<Patient>> GetPatientListForClinic(string orgId, string clinicId, string authToken)
        {

            try
            {
                Log.Debug($">>> Attempting to retrieve patients from OrgId: {orgId} and ClinicId: {clinicId}");
                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
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

                    // Massage the data a bit...
                    wrapper.Patients.ForEach(patient =>
                    {
                        patient.DateOfBirth = DateTime.Parse(patient.DateOfBirth).ToString("MM/dd/yyyy");
                    });

                    Log.Debug($"Successfully retrieved list of patients");
                    return wrapper.Patients;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientList: {response.StatusCode}");
                }

            }
            catch (Exception)
            {
                Log.Error($">>> GetPatientListForClinic failed for orgId: {orgId}, clinicId: {clinicId}.");
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

                Log.Debug($">>> Attempting to retrieve care session for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
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

                    wrapper.CareSessions.ForEach(session =>
                    {
                        session.PerformedOn = DateTime.Parse(session.PerformedOn).ToString("MM/dd/yyyy");
                        session.SubmittedAt = DateTime.Parse(session.SubmittedAt).ToString("MM/dd/yyyy");
                    });

                    Log.Debug($"Successfully retrieved patient care sessions");

                    return wrapper.CareSessions;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientList: {response.StatusCode}");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetPatientCareSessions failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}.");
                return new List<CareSession>();
            }
        }

        public async Task<EnrollmentStatus> GetPatientEnrollmentStatus(string orgId, string clinicId, string patientId, string sourceAuthToken)
        {

            try
            {
                Log.Debug($">>> Attempting to retrieve patient enrollment status from orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
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

                    Log.Debug($">>> Successfully retrieved patient enrollment status");

                    return wrapper.CurrentEnrollments;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientEnrollmentStatus");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetPatientEnrollmentStatus failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}.");
                return new EnrollmentStatus();
            }

        }

        public async Task<Enrollment> GetPatientEnrollmentDetails(string enrollmentType, string orgId, string clinicId, string patientId, string sourceAuthToken)
        {
            try
            {
                Log.Debug($">>> Attempting to retrieve patient enrollment details from orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");
                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
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

                    Log.Debug($">>> Successfully retrieved patient enrollment details");

                    return wrapper.Enrollment;

                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientEnrollmentDetails");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetPatientEnrollmentDetails failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, enrollmentType: {enrollmentType}.");
                return new Enrollment();
            }
        }

        public async Task<List<User>> GetUserList(string orgId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to retrieve user list from orgId: {orgId}");
                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
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

                    Log.Debug($">>> Successfully retrieved user list");

                    return wrapper.Users;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetUserList");
                }

            }
            catch (Exception)
            {
                Log.Error($">>> GetUserList failed for orgId: {orgId}.");
                throw;
            }
        }

        public async Task<List<Provider>> GetProviderList(string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to retrieve provider list from orgId: {orgId}, clinicId: {clinicId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/provider/list?type=providers");

                if (response.IsSuccessStatusCode)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    ProviderListWrapper wrapper = System.Text.Json.JsonSerializer.Deserialize<ProviderListWrapper>(content, options);

                    Log.Debug($">>> Successfully retrieved provider list");

                    return wrapper.Providers;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetProviderList");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetProviderList failed for orgId: {orgId}, clinicId: {clinicId}.");
                throw;
            }

        }

        public async Task<string?> PostPatientToClinic(Patient patient, string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to post patient for orgId: {orgId}, clinicId: {clinicId}, patientId: {patient.Id}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                // Make sure Patient Id is null here when we post it up

                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                };

                var json = SerializePatientContent(patient);

                var requestBody = new StringContent(json, Encoding.UTF8, "application/json");
                requestBody.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/new", requestBody);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await resp.Content.ReadAsStringAsync();

                    Log.Debug($">>> Successfully posted patient to clinic");
                    PatientCreateResponse patientCreateResponse = System.Text.Json.JsonSerializer.Deserialize<PatientCreateResponse>(content, options);

                    return patientCreateResponse?.PatientId?.ToString();
                }
                else if (resp.StatusCode == HttpStatusCode.Conflict)
                {
                    // User has already been migrated, grab the ID and continue with the process so we can attempt to get the care sessions...
                    Log.Warning($">>> Patient with same MRN exists in destination clinic as the one being migrated.");
                    return null;
                }
                else
                {
                    Log.Error($">>> Failed to post patient for orgId: {orgId}, clinicId: {clinicId}, patientId: {patient.Id}");
                    Log.Error(resp.StatusCode.ToString());
                    Log.Error(resp.Content.ReadAsStringAsync().Result.ToString());
                    return null;
                }
            }
            catch (Exception)
            {
                Log.Error($">>> PostPatientToClinic failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patient.Id}.");
                return null;
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
                 Console.WriteLine($">>> Attempting to post patient care session to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, careSession: {careSession}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                var json = SerializeCareSessions(careSession);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/care-session", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                     Log.Debug($">>> Successfully posted care session for patient");
                }
                else
                {
                    Log.Error($">>> Failed to post care session for patientId: {patientId}, careSessionId: {careSession.Id}, orgId: {orgId}, and clinicId: {clinicId}.");
                    Log.Error(resp.StatusCode.ToString());
                    Log.Error(resp.Content.ReadAsStringAsync().Result.ToString());
                }
            }
            catch (Exception)
            {
                Log.Error($">>> PostCareSessionToClinic failed for patientId: {patientId}, careSessionId: {careSession.Id}, orgId: {orgId}, and clinicId: {clinicId}.");
            }
        }

        public async Task PostEnrollmentsToClinic(Enrollment enrollment, string enrollmentType, string patientId, string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to post enrollment information to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, enrollmentType: {enrollmentType}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                var json = SerializeEnrollment(enrollment);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/enrollment/{enrollmentType}", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    Log.Debug($">>> Successfully posted enrollment information for Patientid: {patientId}, Enrollment Type: {enrollmentType}, OrgId: {orgId}, and ClinicId: {clinicId}");
                }
                else
                {
                    Log.Error($">>> Failed to post enrollment information for Patientid: {patientId}, Enrollment Type: {enrollmentType}, OrgId: {orgId}, and ClinicId: {clinicId}");
                    Log.Error(resp.StatusCode.ToString());
                    Log.Error(resp.Content.ReadAsStringAsync().Result.ToString());
                }
            }
            catch (Exception)
            {
                Log.Error($">>> PostEnrollmentsToClinic failed for Patientid: {patientId}, Enrollment Type: {enrollmentType}, OrgId: {orgId}, and ClinicId: {clinicId}.");
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
                    serializedCareSession.Append($"\"performedBy\": {SerializedProvider(careSession.PerformedBy)},");
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
                if (!string.IsNullOrEmpty(user.Id.ToString()))
                    serializedUser.Append($"\"id\":\"{user.Id.ToString()}\",");
                if (!string.IsNullOrEmpty(user.FirstName))
                    serializedUser.Append($"\"firstName\":\"{user.FirstName}\",");
                if (!string.IsNullOrEmpty(user.LastName))
                    serializedUser.Append($"\"lastName\":\"{user.LastName}\",");

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

        private static string SerializedProvider(Provider provider)
        {
            try
            {
                var serializedProvider = new StringBuilder("{");
                if (!string.IsNullOrEmpty(provider.Id.ToString()))
                    serializedProvider.Append($"\"id\":\"{provider.Id.ToString()}\",");
                if (!string.IsNullOrEmpty(provider.FirstName))
                    serializedProvider.Append($"\"firstName\":\"{provider.FirstName}\",");
                if (!string.IsNullOrEmpty(provider.LastName))
                    serializedProvider.Append($"\"lastName\":\"{provider.LastName}\",");

                if (serializedProvider.Length > 1)
                    serializedProvider.Length--;

                serializedProvider.Append('}');

                return serializedProvider.ToString();
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
                    serializedEnrollment.Append($"\"primaryClinician\":{SerializedProvider(enrollment.PrimaryClinician)},");
                if (enrollment.Specialist != null)
                    serializedEnrollment.Append($"\"specialist\":{SerializeUser(enrollment.Specialist)},");
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
