using ApothedocImportLib.DataItem;
using ApothedocImportLib.Logic;
using Newtonsoft.Json;
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
    public class ProviderMappingUtil
    {
        public ProviderMappingUtil()
        {

        }

        public List<CareSession> MapCareSessionProvidersAndSubmitters(List<CareSession> careSessions, List<Provider> targetProviders, List<ProviderIdMapping> mappings)
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
                        Log.Warning($">>> Mapping issue for care session. Unable to map \"performed by\" for care session for provider:" +
                            $"\n{JsonConvert.SerializeObject(c.PerformedBy, Formatting.Indented)}");
                        Log.Warning($">>> Please ensure either mapping for provider exists in configuration and role is set correctly in target clinic");
                    }

                    c.PerformedBy = targetProvider;

                });

            }
            catch(Exception ex) {
                Log.Error($">>> Error while mapping care session providers and submitters: {ex.Message}");
            }

            return careSessions;
        }

        public Enrollment MapEnrollmentProviderInfo(Enrollment enrollment, List<Provider> targetProviders, List<ProviderIdMapping> mappings)
        {

            var sourcePrimaryClinicianId = enrollment.PrimaryClinician.Id;
            var targetPrimaryClinicianId = mappings.Find(u => u.SourceId == sourcePrimaryClinicianId)?.TargetId;
            var targetPrimaryClinician = targetProviders.Find(u => u.Id == targetPrimaryClinicianId);

            if (targetPrimaryClinician == null)
            {
                Log.Warning($">>> Mapping issue for enrollment info. Unable to map primary clinician for enrollment information for primary clinician:" +
                    $"\n{JsonConvert.SerializeObject(enrollment.PrimaryClinician, Formatting.Indented)}");
                Log.Warning($">>> Please ensure either mapping for provider exists in configuration and role is set correctly in target clinic");
            }

            if(enrollment.PrimaryClinician != null)
                enrollment.PrimaryClinician = targetPrimaryClinician;

            var sourceSpecialistId = enrollment.Specialist?.Id;
            
            if (sourceSpecialistId == null)
                return enrollment;  // This can be null, just break early if it is
            
            var targetSpecialistId = mappings.Find(u => u.SourceId == sourceSpecialistId)?.TargetId;
            var targetSpecialist = targetProviders.Find(u => u.Id == targetSpecialistId);

            if (targetSpecialist == null)
            {
                Log.Warning($">>> Mapping issue for enrollment info. Unable to map specialist for enrollment information for specialist:" +
                    $"\n{JsonConvert.SerializeObject(enrollment.Specialist, Formatting.Indented)}");
                Log.Warning($">>> Please ensure either mapping for provider exists in configuration and role is set correctly in target clinic");
            }

            enrollment.Specialist = targetSpecialist;

            return enrollment;
        }

    }
}
