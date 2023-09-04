﻿using ApothedocImportLib.DataItem;
using ApothedocImportLib.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };
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

                Dictionary<
                    Patient,
                    Tuple<
                        PatientDetails,
                        List<CareSession>,
                        EnrollmentStatus,
                        AllergyMedication,
                        EmergencyContactWrapper,
                        ContactInformation
                    >
                > patientInfoDictionary = new();

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
                        patientCareSessions = await GetAllPatientCareSessions(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    }

                    var patientDetails = await GetPatientInfo(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    var patientEnrollment = await GetPatientEnrollmentStatus(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    var patientAllergies = await GetAllergyMedication(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    var patientEmergencyContacts = await GetEmergencyContacts(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    var patientContactInformation = await GetContactInformation(sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);

                    patientInfoDictionary.Add(patient, Tuple.Create(patientDetails, patientCareSessions, patientEnrollment, patientAllergies, patientEmergencyContacts, patientContactInformation));
                }

                // Just counting for logging...
                int careSessionCount = 0;
                int enrollmentCount = 0;
                foreach (Tuple<PatientDetails, List<CareSession>, EnrollmentStatus, AllergyMedication, EmergencyContactWrapper, ContactInformation> patientInfoRecord in patientInfoDictionary.Values)
                {
                    careSessionCount += patientInfoRecord.Item2.Count;

                    if (patientInfoRecord.Item3?.Rpm == true) enrollmentCount++;
                    if (patientInfoRecord.Item3?.Ccm == true) enrollmentCount++;
                    if (patientInfoRecord.Item3?.Bhi == true) enrollmentCount++;
                    if (patientInfoRecord.Item3?.Pcm == true) enrollmentCount++;
                }

                Log.Information($">>> Successfully retrieved {sourcePatientList.Count} patients, {careSessionCount} care sessions, and {enrollmentCount} unique enrollments from OrgId: {sourceOrgId} and ClinicId: {sourceClinicId}.");
                Log.Information($">>> Attempting to post data to OrgId: {destOrgId} and ClinicId: {destClinicId}");

                // Time to start pushing up to destination location


                foreach (var patientInfoRecord in patientInfoDictionary)
                {
                    var patient = patientInfoRecord.Key;
                    var patientDetails = patientInfoRecord.Value?.Item1;
                    var careSessions = patientInfoRecord.Value?.Item2;
                    var enrollmentStatus = patientInfoRecord.Value?.Item3;
                    var allergies = patientInfoRecord.Value?.Item4;
                    var emergencyContacts = patientInfoRecord.Value?.Item5;
                    var contactInformation = patientInfoRecord.Value?.Item6;

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

                    // Post patient details
                    if (patientDetails != null)
                    {
                        await PostPatientDetailsToClinic(patientDetails, newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    // Post enrollment status
                    var rpmEnrollmentDetails = await GetPatientEnrollmentDetails("rpm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    if (rpmEnrollmentDetails?.EnrollmentDate != null)
                    {
                        rpmEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(rpmEnrollmentDetails, targetProvidersList, mappings);
                        await PostEnrollmentsToClinic(rpmEnrollmentDetails, "rpm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    var ccmEnrollmentDetails = await GetPatientEnrollmentDetails("ccm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    if (ccmEnrollmentDetails?.EnrollmentDate != null)
                    {
                        ccmEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(ccmEnrollmentDetails, targetProvidersList, mappings);
                        await PostEnrollmentsToClinic(ccmEnrollmentDetails, "ccm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    var bhiEnrollmentDetails = await GetPatientEnrollmentDetails("bhi", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    if (bhiEnrollmentDetails?.EnrollmentDate != null)
                    {
                        bhiEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(bhiEnrollmentDetails, targetProvidersList, mappings);
                        await PostEnrollmentsToClinic(bhiEnrollmentDetails, "bhi", newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    // PCM not supported yet
                    //var pcmEnrollmentDetails = await GetPatientEnrollmentDetails("pcm", sourceOrgId, sourceClinicId, patient.Id.ToString(), sourceAuthToken);
                    //if (pcmEnrollmentDetails?.EnrollmentDate != null)
                    //{
                    //    pcmEnrollmentDetails = providerMappingUtil.MapEnrollmentProviderInfo(pcmEnrollmentDetails, targetProvidersList, mappings);
                    //    await PostEnrollmentsToClinic(pcmEnrollmentDetails, "pcm", newPatientId, destOrgId, destClinicId, destAuthToken);
                    //}

                    // Post the allergy information
                    if (allergies != null)
                    {
                        await PostAllergiesToClinic(allergies, newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    // Post the emergency contact
                    if (emergencyContacts?.EmergencyContacts.Count > 0)
                    {
                        await PostEmergencyContactsToClinic(emergencyContacts, newPatientId, destOrgId, destClinicId, destAuthToken);
                    }

                    // Post contact information
                    if (contactInformation != null)
                    {
                        await PostContactInformationToClinic(contactInformation, newPatientId, destOrgId, destClinicId, destAuthToken);
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
        #region GET Requests
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

                    PatientListWrapper wrapper = JsonConvert.DeserializeObject<PatientListWrapper>(content);

                    // Massage the data a bit...
                    wrapper.Patients.ForEach(patient =>
                    {
                        patient.DateOfBirth = DateTime.Parse(patient.DateOfBirth).ToString("MM/dd/yyyy");
                    });

                    Log.Debug($">>> Successfully retrieved list of patients");
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

        public async Task<CareSessionWrapper> GetPatientCareSessions(string orgId, string clinicId, string patientId, int pageNumber, string authToken)
        {
            try
            {
                if (string.IsNullOrEmpty(patientId))
                {
                    throw new Exception($"Cannot get care sessions of null Patient Id");
                }

                Log.Debug($">>> Attempting to retrieve care session for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/care-sessions?page={pageNumber}&type=total");

                if (response.IsSuccessStatusCode)
                {

                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    CareSessionWrapper wrapper = JsonConvert.DeserializeObject<CareSessionWrapper>(content);

                    Log.Debug($">>> Successfully retrieved patient care sessions");

                    return wrapper;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientList: {response.StatusCode}");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetPatientCareSessions failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}.");
                return new();
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

                    EnrollmentStatusWrapper wrapper = JsonConvert.DeserializeObject<EnrollmentStatusWrapper>(content);

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

                    EnrollmentWrapper wrapper = JsonConvert.DeserializeObject<EnrollmentWrapper>(content);

                    if (wrapper == null || wrapper.Enrollment == null)
                        throw new Exception($">>> No enrollment details found for patient with enrollment type: {enrollmentType}");

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

                    ProviderListWrapper wrapper = JsonConvert.DeserializeObject<ProviderListWrapper>(content);

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

        public async Task<PatientDetails> GetPatientInfo(string orgId, string clinicId, string patientId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to retrieve patient information from orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/details");

                if (response.IsSuccessStatusCode)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    PatientDetailsWrapper wrapper = JsonConvert.DeserializeObject<PatientDetailsWrapper>(content);

                    Log.Debug($">>> Successfully retrieved patient information");

                    return wrapper.PatientDetails;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetPatientInfo");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetAllergyMedication failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}.");
                throw;
            }
        }
        public async Task<AllergyMedication> GetAllergyMedication(string orgId, string clinicId, string patientId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to retrieve patient allergy medication from orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/allergy-medication");

                if (response.IsSuccessStatusCode)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    AllergyMedicationWrapper wrapper = JsonConvert.DeserializeObject<AllergyMedicationWrapper>(content);

                    Log.Debug($">>> Successfully retrieved patient allergy medication");

                    return wrapper.AllergiesMedications;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetAllergyMedication");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetAllergyMedication failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}.");
                return new AllergyMedication();
            }

        }

        public async Task<EmergencyContactWrapper> GetEmergencyContacts(string orgId, string clinicId, string patientId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to retrieve patient emergency contact from orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/emergency-contact");

                if (response.IsSuccessStatusCode)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    EmergencyContactWrapper wrapper = JsonConvert.DeserializeObject<EmergencyContactWrapper>(content);

                    Log.Debug($">>> Successfully retrieved patient emergency contact");

                    return wrapper;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetEmergencyContact");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetEmergencyContact failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}.");
                return new();
            }

        }

        public async Task<ContactInformation> GetContactInformation(string orgId, string clinicId, string patientId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to retrieve patient contact information from orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                HttpResponseMessage response = await client.GetAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/contact-information");

                if (response.IsSuccessStatusCode)
                {
                    JsonSerializerOptions options = new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var content = await response.Content.ReadAsStringAsync();

                    ContactInformationWrapper wrapper = JsonConvert.DeserializeObject<ContactInformationWrapper>(content);

                    Log.Debug($">>> Successfully retrieved patient contact information");

                    return wrapper.ContactInformation;
                }
                else
                {
                    throw new Exception($">>> Non-success HTTP Response code for GetContactInformation");
                }
            }
            catch (Exception)
            {
                Log.Error($">>> GetContactInformation failed for orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}.");
                return new();
            }

        }
        #endregion

        #region POST Requests
        public async Task<string?> PostPatientToClinic(Patient patient, string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to post patient for orgId: {orgId}, clinicId: {clinicId}, patientId: {patient.Id}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                // Make sure Patient Id is null here when we post it up, make a deep copy
                Patient patientPostObject = JsonConvert.DeserializeObject<Patient>(JsonConvert.SerializeObject(patient));
                patientPostObject.Id = null;

                var json = JsonConvert.SerializeObject(patientPostObject);

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
                    PatientCreateResponse patientCreateResponse = JsonConvert.DeserializeObject<PatientCreateResponse>(content);

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
                Log.Debug($">>> Attempting to post patient care session to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, careSession: {careSession}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                // Make sure Patient Id is null here when we post it up, make a deep copy
                CareSession careSessionPostObject = JsonConvert.DeserializeObject<CareSession>(JsonConvert.SerializeObject(careSession));
                careSessionPostObject.Id = null;
                careSessionPostObject.SubmittedAt = null;
                careSessionPostObject.SubmittedBy = null;

                var json = JsonConvert.SerializeObject(careSessionPostObject);

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
                Log.Debug($">>> Attempting to post enrollment information to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, enrollment: {JsonConvert.SerializeObject(enrollment)}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                var json = JsonConvert.SerializeObject(enrollment);

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

        public async Task PostAllergiesToClinic(AllergyMedication allergies, string patientId, string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to post allergies to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, allergies: {JsonConvert.SerializeObject(allergies)}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                var json = JsonConvert.SerializeObject(allergies);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/allergy-medication", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    Log.Debug($">>> Successfully posted allergies for Patientid: {patientId}, Allergies: {allergies}, OrgId: {orgId}, and ClinicId: {clinicId}");
                }
                else
                {
                    Log.Error($">>> Failed to post allergies for Patientid: {patientId}, Allergies: {allergies}, OrgId: {orgId}, and ClinicId: {clinicId}");
                    Log.Error(resp.StatusCode.ToString());
                    Log.Error(resp.Content.ReadAsStringAsync().Result.ToString());
                }
            }
            catch (Exception)
            {
                Log.Error($">>> PostAllergiesToClinic failed for Patientid: {patientId}, Allergies: {JsonConvert.SerializeObject(allergies)}, OrgId: {orgId}, and ClinicId: {clinicId}.");
            }

        }

        public async Task PostEmergencyContactsToClinic(EmergencyContactWrapper emergencyContacts, string patientId, string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to post emergency contacts to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, emergency contacts: {JsonConvert.SerializeObject(emergencyContacts)}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                var json = JsonConvert.SerializeObject(emergencyContacts);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/emergency-contact", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    Log.Debug($">>> Successfully posted emergency contacts for Patientid: {patientId}, Emergency Contacts: {JsonConvert.SerializeObject(emergencyContacts)}, OrgId: {orgId}, and ClinicId: {clinicId}");
                }
                else
                {
                    Log.Error($">>> Failed to post emergency contacts for Patientid: {patientId}, Emergency Contacts: {JsonConvert.SerializeObject(emergencyContacts)}, OrgId: {orgId}, and ClinicId: {clinicId}");
                    Log.Error(resp.StatusCode.ToString());
                    Log.Error(resp.Content.ReadAsStringAsync().Result.ToString());
                }
            }
            catch (Exception)
            {
                Log.Error($">>> PostEmergencyContactToClinic failed for Patientid: {patientId}, Emergency Contacts: {JsonConvert.SerializeObject(emergencyContacts)}, OrgId: {orgId}, and ClinicId: {clinicId}.");
            }

        }

        public async Task PostContactInformationToClinic(ContactInformation contactInformation, string patientId, string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to post contact information to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, contact information: {JsonConvert.SerializeObject(contactInformation)}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                var json = JsonConvert.SerializeObject(contactInformation);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/contact-information", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    Log.Debug($">>> Successfully posted contact information for Patientid: {patientId}, Contact Information: {JsonConvert.SerializeObject(contactInformation)}, OrgId: {orgId}, and ClinicId: {clinicId}");
                }
                else
                {
                    Log.Error($">>> Failed to post contact information for Patientid: {patientId}, Contact Information: {JsonConvert.SerializeObject(contactInformation)}, OrgId: {orgId}, and ClinicId: {clinicId}");
                    Log.Error(resp.StatusCode.ToString());
                    Log.Error(resp.Content.ReadAsStringAsync().Result.ToString());
                }
            }
            catch (Exception)
            {
                Log.Error($">>> PostContactInformationToClinic failed for Patientid: {patientId}, Contact Information: {JsonConvert.SerializeObject(contactInformation)}, OrgId: {orgId}, and ClinicId: {clinicId}.");
            }

        }

        public async Task PostPatientDetailsToClinic(PatientDetails patientDetails, string patientId, string orgId, string clinicId, string authToken)
        {
            try
            {
                Log.Debug($">>> Attempting to post patient information to orgId: {orgId}, clinicId: {clinicId}, patientId: {patientId}, patient information: {JsonConvert.SerializeObject(patientDetails)}");

                using HttpClient client = new(new RetryHandler(new HttpClientHandler()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMinutes(2);

                var json = JsonConvert.SerializeObject(patientDetails);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await client.PostAsync(_resourceApi + $"org-id/{orgId}/clinic-id/{clinicId}/patient/{patientId}/details", content);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    Log.Debug($">>> Successfully posted patient information for Patientid: {patientId}, Patient Information: {JsonConvert.SerializeObject(patientDetails)}, OrgId: {orgId}, and ClinicId: {clinicId}");
                }
                else
                {
                    Log.Error($">>> Failed to post patient information for Patientid: {patientId}, Patient Information: {JsonConvert.SerializeObject(patientDetails)}, OrgId: {orgId}, and ClinicId: {clinicId}");
                    Log.Error(resp.StatusCode.ToString());
                    Log.Error(resp.Content.ReadAsStringAsync().Result.ToString());
                }
            }
            catch (Exception)
            {
                Log.Error($">>> PostPatientInformationToClinic failed for Patientid: {patientId}, Patient Information: {JsonConvert.SerializeObject(patientDetails)}, OrgId: {orgId}, and ClinicId: {clinicId}.");
            }

        }
        #endregion
        #endregion

        #region Helper Functions

        public async Task<List<CareSession>> GetAllPatientCareSessions(string orgId, string clinicId, string patientId, string authToken)
        {
            List<CareSession> allPatientCareSessions = new();
            try
            {
                var allCareSessionsRetreived = false;
                int pageNumber = 1;

                var careSessionWrapper = await GetPatientCareSessions(orgId, clinicId, patientId, pageNumber, authToken);

                var totalCareSessionCount = careSessionWrapper.CareMetaData.Counts.Total;
                allPatientCareSessions = allPatientCareSessions.Concat(careSessionWrapper.CareSessions).ToList();

                if (totalCareSessionCount == allPatientCareSessions.Count)
                    return allPatientCareSessions;

                while (!allCareSessionsRetreived)
                {
                    careSessionWrapper = await GetPatientCareSessions(orgId, clinicId, patientId, ++pageNumber, authToken);
                    allPatientCareSessions = allPatientCareSessions.Concat(careSessionWrapper.CareSessions).ToList();

                    if (allPatientCareSessions.Count == totalCareSessionCount)
                        allCareSessionsRetreived = true;

                }

                return allPatientCareSessions;
            }
            catch (Exception ex)
            {
                Log.Warning($">>> Unable to retrieve all patient care sessions");
                Log.Warning(ex.Message);
                Log.Warning($">>> Skipping Care Session import for patient id: {patientId}");
            }
            return new();
        }

        #endregion
    }
}
