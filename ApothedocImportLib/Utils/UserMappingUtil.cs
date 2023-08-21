using ApothedocImportLib.DataItem;
using ApothedocImportLib.Logic;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApothedocImportLib.Utils
{
    public class UserMappingUtil
    {
        public UserMappingUtil()
        {

        }

        public List<CareSession> MapCareSessionProvidersAndSubmitters(List<CareSession> careSessions, List<Provider> targetProviders, List<User> targetSubmitters, UserIdMappingWrapper mappings)
        {
            try
            {
                careSessions.ForEach(c =>
                {
                    var sourceProviderId = c.PerformedBy?.Id;
                    var targetProviderId = mappings.ProviderMappings?.Find(u => u.SourceId == sourceProviderId)?.TargetId;
                    var targetProvider = targetProviders.Find(u => u.Id == targetProviderId);

                    if (targetProvider == null)
                    {
                        Log.Warning($">>> Mapping issue for care session of source ID: {c.Id}");
                        Log.Warning($">>> Unable to map provider {c.PerformedBy?.FirstName} {c.PerformedBy?.LastName} with provider ID of {c.PerformedBy?.Id} to new clinic.");
                        Log.Warning($">>> Setting provider for care session to null");
                        Log.Warning($">>> Please ensure mapping for provider exists and user is a provider in target clinic");
                    }

                    c.PerformedBy = targetProvider;

                    var sourceSubmitterId = c.SubmittedBy?.Id;
                    var targetSubmitterId = mappings.UserMappings?.Find(u => u.SourceId == sourceSubmitterId)?.TargetId;
                    var targetSubmitter = targetSubmitters.Find(u => u.Id == targetSubmitterId);

                    if (targetSubmitter == null)
                    {
                        Log.Warning($">>> Mapping issue for care session of source ID: {c.Id}");
                        Log.Warning($">>> Unable to map submitter {c.SubmittedBy?.FirstName} {c.SubmittedBy?.LastName} with user ID of {c.SubmittedBy?.Id} to new clinic.");
                        Log.Warning($">>> Setting submitter for care session to null");
                        Log.Warning($">>> Please ensure mapping for submitter exists");
                    }

                    c.SubmittedBy = targetSubmitter;
                });

            }
            catch(Exception ex) {
                Log.Error($">>> Error while mapping care session providers and submitters: {ex.Message}");
            }

            return careSessions;
        }

        public Enrollment MapEnrollmentUserInfo(Enrollment enrollment, string clinicId, List<User> targetUserList, List<Provider> targetProviders, UserIdMappingWrapper mappings)
        {

            var sourcePrimaryClinicianId = enrollment.PrimaryClinician?.Id;
            var targetPrimaryClinicianId = mappings.ProviderMappings.Find(u => u.SourceId == sourcePrimaryClinicianId)?.TargetId;
            var targetPrimaryClinician = targetProviders.Find(u => u.Id == targetPrimaryClinicianId);

            if (targetPrimaryClinician == null)
            {
                Log.Warning($">>> Mapping issue for enrollment");
                Log.Warning($">>> Unable to map primary clinician {enrollment.PrimaryClinician?.FirstName} {enrollment.PrimaryClinician?.LastName} with clinician ID of {enrollment.PrimaryClinician?.Id} to new clinic.");
                Log.Warning($">>> Setting primary clinician to null");
                Log.Warning($">>> Please ensure mapping for user exists and user is either a clinician or org admin  in target clinic");
            }

            enrollment.PrimaryClinician = targetPrimaryClinician;

            var sourceSpecialistId = enrollment.Specialist?.Id;
            
            if (sourceSpecialistId == null)
                return enrollment;  // This can be null, just break early if it is
            
            var targetSpecialistId = mappings.UserMappings.Find(u => u.SourceId == sourceSpecialistId)?.TargetId;
            var targetSpecialist = targetUserList.Find(u => u.Id == targetSpecialistId);

            if (targetSpecialist == null)
            {
                Log.Warning($">>> Mapping issue for enrollment");
                Log.Warning($">>> Unable to map specialist {enrollment.Specialist?.FirstName} {enrollment.Specialist?.LastName} with specialist ID of {enrollment.Specialist?.Id} to new clinic.");
                Log.Warning($">>> Setting specialist to null");
                Log.Warning($">>> Please ensure mapping for user exists");
            }

            enrollment.Specialist = targetSpecialist;

            return enrollment;
        }

    }
}
