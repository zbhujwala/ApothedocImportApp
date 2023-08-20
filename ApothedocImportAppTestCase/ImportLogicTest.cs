using ApothedocImportLib.DataItem;
using ApothedocImportLib.Logic;
using ApothedocImportLib.Utils;
using Newtonsoft.Json;

namespace ApothedocImportAppTestCase
{
    [TestClass]
    public class ImportLogicTest
    {
        string patientId = "1740";
        string resourceApi;
        string sourceOrgId;
        string sourceClinicid;
        string sourceAuthToken;
        string targetOrgId;
        string targetClinicId;
        string targetAuthToken;
        ImportApiLogic logic;
        UserMappingUtil userMappingUtil;

        [TestInitialize] 
        public void Init() {
            resourceApi = "https://dev.apothedoc.com/api/";
            sourceOrgId = "1";
            sourceClinicid = "1";
            // Replace this for each session
            sourceAuthToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJvcmdJZCI6MSwidXNlciI6InphaWRAc3luZXJncnguY29tIiwiaWF0IjoxNjkyNTUzODk5LCJleHAiOjE2OTI1OTcwOTl9.luBFA7hNtLvVAYoiUzS0iVqpdY8Ptv_StpneyjqJMxM";

            targetOrgId = "2";
            targetClinicId = "13";
            targetAuthToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJvcmdJZCI6MiwidXNlciI6ImJodWp3YWxhLnphaWRAZ21haWwuY29tIiwiaWF0IjoxNjkyNTUzOTMzLCJleHAiOjE2OTI1OTcxMzN9.V8N-cFzOKWu7GHoNTVN6BPrLcNpGp5aORMLkdLSo1A0";

            logic = new(resourceApi);
            userMappingUtil = new();
        }

        [TestMethod]
        public async Task TestGetPatientList()
        {
            var patientList = await logic.GetPatientListForClinic(sourceOrgId, sourceClinicid, sourceAuthToken);

            Assert.IsTrue(patientList.Count > 0);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(patientList, Formatting.Indented)}");
        }

        [TestMethod]
        public async Task TestGetUsers()
        {
            var provider = await logic.GetUserList(sourceOrgId, sourceAuthToken);

            Assert.IsTrue(provider != null);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(provider, Formatting.Indented)}");
        }

        [TestMethod]
        public async Task TestGetPatientCareSessions()
        {
            patientId = "1";
            var careSessions = await logic.GetPatientCareSessions(sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsTrue(careSessions.Count > 0);


            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(careSessions, Formatting.Indented)}");
        }

        [TestMethod]
        public async Task TestGetPatientEnrollmentStatus()
        {
            var enrollmentStatus = await logic.GetPatientEnrollmentStatus(sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentStatus);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentStatus, Formatting.Indented)}");
        }

        // Get CCM
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsCCM()
        {
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("ccm", sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails, Formatting.Indented)}");
        }

        // Get BHI
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsBHI()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("bhi", sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails, Formatting.Indented)}");
        }

        // Get RPM
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsRPM()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("rpm", sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails, Formatting.Indented)}");
        }

        [TestMethod]
        public async Task TestReadUserMappingsFile()
        {
            var mappings = userMappingUtil.LoadJsonFile();

            Assert.IsNotNull(mappings);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(mappings, Formatting.Indented)}");

        }

        [TestMethod]
        public async Task TestMapCareSessionProvidersAndSubmitters()
        {
            var mappings = userMappingUtil.LoadJsonFile();

            var sourceCareSession = new CareSession()
            {
                Id = 169,
                CareType = "ccm",
                UsingManualTimeEntry = 1,
                DurationSeconds = 60,
                PerformedOn = "2023-06-11T00:00:00.000Z",
                SubmittedAt = "2023-07-02T07:30:42.000Z",
                PerformedBy = new Provider {
                    Id = 1,
                    FirstName = "Anish",
                    LastName = "Bhatt"
                },
                SubmittedBy = new User {
                    Id = 32,
                    FirstName = "Zaid",
                    LastName = "Bhujwala"
                },
                CareNote = "adding 1 more min of CCM time.",
                ComplexCare = 0,
                InteractedWithPatient = 0,
                MatchedSubmittedPerformedBy = false,
                MatchedSubmittedPerformedAt = false
            };

            List<CareSession> sourceCareSessionList = new() { sourceCareSession };

            var targetProvidersList = await logic.GetProviderList(targetOrgId, targetClinicId, targetAuthToken);
            var targetUserList = await logic.GetUserList(targetOrgId, targetAuthToken);

            var transformedList = userMappingUtil.MapCareSessionProvidersAndSubmitters(sourceCareSessionList, targetProvidersList, targetUserList, mappings);

            Assert.IsNotNull(transformedList);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(transformedList, Formatting.Indented)}");

        }

        [TestMethod]
        public async Task TestMapEnrollmentUserInfo()
        {
            var mappings = userMappingUtil.LoadJsonFile();

            var sourceEnrollment = new Enrollment()
            {
                EnrollmentDate = "2023-02-06T00:00:00.000Z",
                CancellationDate = null,
                InformationSheet = "2023-02-06T00:00:00.000Z",
                PatientAgreement = "2023-02-06T00:00:00.000Z",
                VerbalAgreement = true,
                PrimaryClinician = new User()
                {
                    Id = 32,
                    FirstName = "Zaid",
                    LastName = "Bhujwala"
                },
                EnrolledSameDayOfficeVisit = 0,
                Specialist = new User()
                {
                    Id = 23,
                    FirstName = "anish devtest",
                    LastName = "b"
                }
            };

            var targetUserList = await logic.GetUserList(targetOrgId, targetAuthToken);

            var transformedEnrollment = userMappingUtil.MapEnrollmentUserInfo(sourceEnrollment, targetClinicId, targetUserList, mappings);

            Assert.IsNotNull(transformedEnrollment);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(transformedEnrollment, Formatting.Indented)}");
        }
    }
}