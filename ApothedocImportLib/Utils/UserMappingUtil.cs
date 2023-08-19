using ApothedocImportLib.DataItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApothedocImportLib.Utils
{
    public static class UserMappingUtil
    {
        public static List<UserIdMapping> LoadJsonFile()
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

        public static List<CareSession> MapCareSessionUserInfo(List<CareSession> careSessions, List<User> targetUserList, List<UserIdMapping> mappings)
        {
            careSessions.ForEach(c =>
            {
                var sourceProviderId = c.PerformedBy.Id;
                var targetProviderId = mappings.Find(u => u.SourceId == sourceProviderId).TargetId;
                var targetProvider = targetUserList.Find(u => u.Id == targetProviderId);

                c.PerformedBy = targetProvider;

                var sourceSubmitterId = c.SubmittedBy.Id;
                var targetSubmitterId = mappings.Find(u => u.SourceId == sourceSubmitterId).TargetId;
                var targetSubmitter = targetUserList.Find(u => u.Id == targetSubmitterId);

                c.SubmittedBy = targetSubmitter;

            });

            return careSessions;
        }

        public static Enrollment MapEnrollmentUserInfo(Enrollment enrollment, List<User> targetUserList, List<UserIdMapping> mappings)
        {

            var sourcePrimaryClinicianId = enrollment.PrimaryClinician.Id;
            var targetPrimaryClinicianId = mappings.Find(u => u.SourceId == sourcePrimaryClinicianId).TargetId;
            var targetPrimaryClinician = targetUserList.Find(u => u.Id == targetPrimaryClinicianId);

            enrollment.PrimaryClinician = targetPrimaryClinician;

            var sourceSpecialistId = enrollment.Specialist.Id;
            var targetSpecialistId = mappings.Find(u => u.SourceId == sourceSpecialistId).TargetId;
            var targetSpecialist = targetUserList.Find(u => u.Id == targetSpecialistId);

            enrollment.Specialist = targetSpecialist;

            return enrollment;
        }

    }
}
