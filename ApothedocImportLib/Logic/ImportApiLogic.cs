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
                ProviderMappingUtil providerMappingUtil = new();
                ConfigUtil configUtil = new();

                Dictionary<Patient, Tuple<List<CareSession>, EnrollmentStatus>> patientInfoDictionary = new();

                Log.Information($">>> TransferClinicData called for OrgId: {sourceOrgId} and ClinicId: {sourceClinicId}");
                Log.Information($">>> Getting patient and provider information from source clinic...");

                // Grab the patient list from both source and destination. If the patient is already in the destination, we will want the Patient Id so we can transfer over care sessions from source location
                var sourcePatientList = await GetPatientListForClinic(sourceOrgId, sourceClinicId, sourceAuthToken);
                var destPatientList = await GetPatientListForClinic(destOrgId, destClinicId, destAuthToken);

                var targetProvidersList = await GetProviderList(destOrgId, destClinicId, destAuthToken);

                var mappings = configUtil.LoadConfig().ProviderMappings;

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
                        if (rpmEnrollmentDetails != null) 
                        {  
                            rpmEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(rpmEnrollmentDetails, targetProvidersList, mappings);
                            await PostEnrollmentsToClinic(rpmEnrollmentDetails, "rpm", newPatientId, destOrgId, destClinicId, destAuthToken);
                        }
                        else
                        {
                            Log.Error(">>> Skipping enrollment POST for RPM due to reported error");
                        }
                    }
                    if (enrollmentStatus.Ccm == true)
                    {
                        var ccmEnrollmentDetails = await GetPatientEnrollmentDetails("ccm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        if(ccmEnrollmentDetails != null)
                        {
                            ccmEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(ccmEnrollmentDetails, targetProvidersList, mappings);
                            await PostEnrollmentsToClinic(ccmEnrollmentDetails, "ccm", newPatientId, destOrgId, destClinicId, destAuthToken);
                        }
                        else
                        {
                            Log.Error(">>> Skipping enrollment POST for CCM due to reported error");
                        }
                    }
                    if (enrollmentStatus.Bhi == true)
                    {
                        var bhiEnrollmentDetails = await GetPatientEnrollmentDetails("bhi", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        if(bhiEnrollmentDetails != null)
                        {
                            bhiEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(bhiEnrollmentDetails, targetProvidersList, mappings);
                            await PostEnrollmentsToClinic(bhiEnrollmentDetails, "bhi", newPatientId, destOrgId, destClinicId, destAuthToken);
                        }
                        else
                        {
                            Log.Error(">>> Skipping enrollment POST for BHI due to reported error");
                        }
                    }
                    if (enrollmentStatus.Pcm == true)
                    {
                        var pcmEnrollmentDetails = await GetPatientEnrollmentDetails("pcm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                        if(pcmEnrollmentDetails != null)
                        {
                            pcmEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(pcmEnrollmentDetails, targetProvidersList, mappings);
                            await PostEnrollmentsToClinic(pcmEnrollmentDetails, "pcm", newPatientId, destOrgId, destClinicId, destAuthToken);
                        }
                        else
                        {
                            Log.Error(">>> Skipping enrollment POST for PCM due to reported error");
                        }
                    }

                    // Post the care sessions after we confirmed the patient ID in the destination clinic
                    careSessions = providerMappingUtil.MapCareSessionProvidersAndSubmitters(careSessions, targetProvidersList, mappings);
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
                        throw new Exception($">>> No enrollment status found for patient");

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

        public async Task<Enrollment?> GetPatientEnrollmentDetails(string enrollmentType, string orgId, string clinicId, string patientId, string sourceAuthToken)
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
                        throw new Exception($">>> No enrollment details found for patient with enrollment type: {enrollmentType}");

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
                return null;
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

                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                var json = JsonConvert.SerializeObject(patient, settings);

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
                    // Patient has already been migrated, grab the ID and continue with the process so we can attempt to get the care sessions...
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

                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                var json = JsonConvert.SerializeObject(careSession, settings);

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

                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                var json = JsonConvert.SerializeObject(enrollment, settings);

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
    }
}
