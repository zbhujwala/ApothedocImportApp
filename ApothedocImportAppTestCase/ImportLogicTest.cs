using ApothedocImportLib.DataItem;
using ApothedocImportLib.Logic;
using ApothedocImportLib.Utils;
using Newtonsoft.Json;
using Serilog.Sinks.SystemConsole.Themes;
using Serilog;
using Newtonsoft.Json.Serialization;

namespace ApothedocImportAppTestCase
{
    [TestClass]
    public class ImportLogicTest
    {
        string patientId = "3";
        ImportApiLogic logic;
        ProviderMappingUtil providerMappingUtil;
        Config config;

        [TestInitialize] 
        public void Init() {

            ConfigUtil configUtil = new();
            config = configUtil.LoadConfig();

            logic = new(config.ResourceApi);
            providerMappingUtil = new();

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .CreateLogger();
        }

        [TestMethod]
        public async Task TestGetPatientList()
        {
            var patientList = await logic.GetPatientListForClinic(config.SourceOrgId, config.SourceClinicId, config.SourceAuthToken);

            Assert.IsTrue(patientList.Count > 0);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(patientList, Formatting.Indented)}");
        }

        [TestMethod]
        public async Task TestGetPatientCareSessions()
        {
            patientId = "3";
            var careSessions = await logic.GetPatientCareSessions(config.SourceOrgId, config.SourceClinicId, patientId, 1, config.SourceAuthToken);

            Assert.IsTrue(careSessions.CareSessions.Count > 0);


            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(careSessions)}");
            Console.WriteLine($"{JsonConvert.SerializeObject(careSessions)}");
        }

        [TestMethod]
        public async Task TestGetAllPatientCareSessions()
        {
            patientId = "1";
            var careSessions = await logic.GetAllPatientCareSessions(config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Console.WriteLine($"Care Session Count: {careSessions.Count}");
            Assert.IsTrue(careSessions.Count > 0);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(careSessions)}");
            Console.WriteLine($"{JsonConvert.SerializeObject(careSessions)}");
        }

        [TestMethod]
        public async Task TestGetPatientEnrollmentStatus()
        {
            var enrollmentStatus = await logic.GetPatientEnrollmentStatus(config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Assert.IsNotNull(enrollmentStatus);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentStatus, Formatting.Indented)}");
        }

        // Get CCM
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsCCM()
        {
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("ccm", config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails, Formatting.Indented)}");
        }

        // Get BHI
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsBHI()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("bhi", config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails, Formatting.Indented)}");
        }

        // Get RPM
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsRPM()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("rpm", config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails, Formatting.Indented)}");
        }

        [TestMethod]
        public void TestGetConfig()
        {
            Assert.IsNotNull(config);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(config, Formatting.Indented)}");

        }

        [TestMethod]
        public async Task TestMapCareSessionProvidersAndSubmitters()
        {
            var mappings = config.ProviderMappings;

            var sourceCareSession = new CareSession()
            {
                Id = 169,
                CareType = "ccm",
                UsingManualTimeEntry = 1,
                DurationSeconds = 60,
                PerformedOn = "2023-06-11T00:00:00.000Z",
                SubmittedAt = "2023-07-02T07:30:42.000Z",
                PerformedBy = new Provider {
                    Id = 4,
                    FirstName = "Naveed",
                    LastName = "Tharwani"
                },
                SubmittedBy = new Provider {
                    Id = 3,
                    FirstName = "Zaid",
                    LastName = "Bhujwala"
                },
                CareNote = "adding 1 more min of CCM time.",
                ComplexCare = 0,
                InteractedWithPatient = 0
            };

            List<CareSession> sourceCareSessionList = new() { sourceCareSession };

            var targetProvidersList = await logic.GetProviderList(config.TargetOrgId, config.TargetClinicId, config.TargetAuthToken);

            var transformedList = providerMappingUtil.MapCareSessionProvidersAndSubmitters(sourceCareSessionList, targetProvidersList, mappings);

            Assert.IsNotNull(transformedList[0].PerformedBy);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(transformedList, Formatting.Indented)}");

        }

        [TestMethod]
        public async Task TestMapEnrollmentProviderInfo()
        {
            var mappings = config.ProviderMappings;

            var sourceEnrollment = new Enrollment()
            {
              EnrollmentDate = "2022-10-27T19:00:00",
              CancellationDate = "2022-10-26T19:00:00",
              InformationSheet = "2022-10-27T19:00:00",
              PatientAgreement = "2022-10-26T19:00:00",
              VerbalAgreement = true,
              PrimaryClinician = new Provider(){
                Id = 1,
                FirstName = "Anish",
                LastName = "Bhatt"
              },
              Specialist = new Provider(){
                Id = 2,
                FirstName = "Dwyane",
                LastName = "The Rock"
              },
              EnrolledSameDayOfficeVisit = 0
            };

            var targetProviderList = await logic.GetProviderList(config.TargetOrgId, config.TargetClinicId, config.TargetAuthToken);

            var transformedEnrollment = providerMappingUtil.MapEnrollmentProviderInfo(sourceEnrollment, targetProviderList, mappings);

            Assert.IsNotNull(transformedEnrollment.PrimaryClinician);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(transformedEnrollment, Formatting.Indented)}");
        }

        [TestMethod]
        public async Task TestGetAllergyMedication()
        {
            patientId = "3";
            var allergyMedication = await logic.GetAllergyMedication(config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Assert.IsNotNull(allergyMedication);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(allergyMedication)}");
        }

        [TestMethod]
        public async Task TestGetEmergencyContact()
        {
            patientId = "3";
            var emergencyContacts = await logic.GetEmergencyContacts(config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Assert.IsNotNull(emergencyContacts);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(emergencyContacts)}");
        }

        [TestMethod]
        public async Task TestGetContactInformation()
        {
            patientId = "3";
            var contactInfo = await logic.GetContactInformation(config.SourceOrgId, config.SourceClinicId, patientId, config.SourceAuthToken);

            Assert.IsNotNull(contactInfo);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(contactInfo)}");
        }
    }
}