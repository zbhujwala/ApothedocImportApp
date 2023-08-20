using ApothedocImportLib.DataItem;
using ApothedocImportLib.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApothedocImportLib.Utils
{
    public class UserMappingUtil : BaseLogic
    {
        public UserMappingUtil()
        {

        }

        public List<UserIdMapping> LoadJsonFile()
        {
            string resourceName = "ApothedocImportLib.conf.user-mapping.json";

            string json = null;
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }
            }

            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };

            UserIdMappingWrapper userMappings = System.Text.Json.JsonSerializer.Deserialize<UserIdMappingWrapper>(json, options);

            return userMappings.Mappings;
        }

        public List<CareSession> MapCareSessionProvidersAndSubmitters(List<CareSession> careSessions, List<Provider> targetProviders, List<User> targetSubmitters, List<UserIdMapping> mappings)
        {
            try
            {
                careSessions.ForEach(c =>
                {
                    var sourceProviderId = c.PerformedBy?.Id;
                    var targetProviderId = mappings.Find(u => u.SourceId == sourceProviderId)?.TargetId;
                    var targetProvider = targetProviders.Find(u => u.Id == targetProviderId);

                    if (targetProvider == null)
                    {
                        LogWarning($">>> Mapping issue for care session of source ID: {c.Id}");
                        LogWarning($">>> Unable to map provider {c.PerformedBy?.FirstName} {c.PerformedBy?.LastName} with provider ID of {c.PerformedBy?.Id} to new clinic.");
                        LogWarning($">>> Setting provider for care session to null");
                        LogWarning($">>> Please ensure mapping for provider exists and user is a provider in target clinic");
                    }

                    c.PerformedBy = targetProvider;

                    var sourceSubmitterId = c.SubmittedBy?.Id;
                    var targetSubmitterId = mappings.Find(u => u.SourceId == sourceSubmitterId)?.TargetId;
                    var targetSubmitter = targetSubmitters.Find(u => u.Id == targetSubmitterId);

                    if (targetSubmitter == null)
                    {
                        LogWarning($">>> Mapping issue for care session of source ID: {c.Id}");
                        LogWarning($">>> Unable to map submitter {c.SubmittedBy?.FirstName} {c.SubmittedBy?.LastName} with user ID of {c.SubmittedBy?.Id} to new clinic.");
                        LogWarning($">>> Setting submitter for care session to null");
                        LogWarning($">>> Please ensure mapping for submitter exists");
                    }

                    c.SubmittedBy = targetSubmitter;
                });

            }
            catch(Exception ex) {
                LogError($">>> Error while mapping care session providers and submitters: {ex.Message}");
            }

            return careSessions;
        }

        public Enrollment MapEnrollmentUserInfo(Enrollment enrollment, string clinicId, List<User> targetUserList, List<UserIdMapping> mappings)
        {

            var sourcePrimaryClinicianId = enrollment.PrimaryClinician?.Id;
            var targetPrimaryClinicianId = mappings.Find(u => u.SourceId == sourcePrimaryClinicianId)?.TargetId;
            var targetPrimaryClinician = targetUserList.Find(u => u.Id == targetPrimaryClinicianId && (u.ClinicLevelAccess.ContainsKey(int.Parse(clinicId)) || u.OrgAdmin == true));

            if (targetPrimaryClinician == null)
            {
                LogWarning($">>> Mapping issue for enrollment");
                LogWarning($">>> Unable to map primary clinician {enrollment.PrimaryClinician?.FirstName} {enrollment.PrimaryClinician?.LastName} with clinician ID of {enrollment.PrimaryClinician?.Id} to new clinic.");
                LogWarning($">>> Setting primary clinician to null");
                LogWarning($">>> Please ensure mapping for user exists and user is either a clinician or org admin  in target clinic");
            }

            enrollment.PrimaryClinician = targetPrimaryClinician;

            var sourceSpecialistId = enrollment.Specialist?.Id;
            var targetSpecialistId = mappings.Find(u => u.SourceId == sourceSpecialistId)?.TargetId;
            var targetSpecialist = targetUserList.Find(u => u.Id == targetSpecialistId);

            if (targetSpecialist == null)
            {
                LogWarning($">>> Mapping issue for enrollment");
                LogWarning($">>> Unable to map specialist {enrollment.Specialist?.FirstName} {enrollment.Specialist?.LastName} with specialist ID of {enrollment.Specialist?.Id} to new clinic.");
                LogWarning($">>> Setting specialist to null");
                LogWarning($">>> Please ensure mapping for user exists");
            }

            enrollment.Specialist = targetSpecialist;

            return enrollment;
        }

    }
}
