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
        string targetAuthToken;
        ImportApiLogic logic;

        [TestInitialize] 
        public void Init() {
            resourceApi = "https://dev.apothedoc.com/api/";
            sourceOrgId = "1";
            sourceClinicid = "1";
            // Replace this for each session
            sourceAuthToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJvcmdJZCI6MSwidXNlciI6InphaWRAc3luZXJncnguY29tIiwiaWF0IjoxNjkyNDY4MzgxLCJleHAiOjE2OTI1MTE1ODF9.jV1RM8VrqiCtFQVvO4lInZeCcyXMhQ8Xw5PWGAIIbWI";

            targetOrgId = "2";
            targetAuthToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJvcmdJZCI6MiwidXNlciI6ImJodWp3YWxhLnphaWRAZ21haWwuY29tIiwiaWF0IjoxNjkyNDc2OTUzLCJleHAiOjE2OTI1MjAxNTN9.UUwFc3IJueir4vlnltetpPMlHYa7oYwSrxjv9PqmuKU";

            logic = new(resourceApi);
        }

        [TestMethod]
        public async Task TestGetPatientList()
        {
            var patientList = await logic.GetPatientListForClinic(sourceOrgId, sourceClinicid, sourceAuthToken);

            Assert.IsTrue(patientList.Count > 0);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(patientList)}");
        }

        [TestMethod]
        public async Task TestGetUsers()
        {
            var provider = await logic.GetUserList(sourceOrgId, sourceAuthToken);

            Assert.IsTrue(provider != null);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(provider)}");
        }

        [TestMethod]
        public async Task TestGetPatientCareSessions()
        {
            patientId = "1";
            var careSessions = await logic.GetPatientCareSessions(sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsTrue(careSessions.Count > 0);


            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(careSessions)}");
        }

        [TestMethod]
        public async Task TestGetPatientEnrollmentStatus()
        {
            var enrollmentStatus = await logic.GetPatientEnrollmentStatus(sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentStatus);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentStatus)}");
        }

        // Get CCM
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsCCM()
        {
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("ccm", sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails)}");
        }

        // Get BHI
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsBHI()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("bhi", sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails)}");
        }

        // Get RPM
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsRPM()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("rpm", sourceOrgId, sourceClinicid, patientId, sourceAuthToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails)}");
        }

        [TestMethod]
        public async Task TestReadUserMappingsFile()
        {
            var mappings = UserMappingUtil.LoadJsonFile();

            Assert.IsNotNull(mappings);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(mappings)}");

        }

        [TestMethod]
        public async Task TestMapCareSessionProviderAndSubmitter()
        {
            var mappings = UserMappingUtil.LoadJsonFile();

            var sourceCareSession = new CareSession()
            {
                Id = 169,
                CareType = "ccm",
                UsingManualTimeEntry = 1,
                DurationSeconds = 60,
                PerformedOn = "2023-06-11T00:00:00.000Z",
                SubmittedAt = "2023-07-02T07:30:42.000Z",
                PerformedBy = new User {
                    id = 1,
                    firstName = "Anish",
                    lastName = "Bhatt"
                },
                SubmittedBy = new User {
                    id = 32,
                    firstName = "Zaid",
                    lastName = "Bhujwala"
                },
                CareNote = "adding 1 more min of CCM time.",
                ComplexCare = 0,
                InteractedWithPatient = 0,
                MatchedSubmittedPerformedBy = false,
                MatchedSubmittedPerformedAt = false
            };

            List<CareSession> sourceCareSessionList = new();
            sourceCareSessionList.Add(sourceCareSession);

            var targetUserList = await logic.GetUserList(targetOrgId, targetAuthToken);

            var transformedList = UserMappingUtil.MapCareSessionUsers(sourceCareSessionList, targetUserList, mappings);

            Assert.IsNotNull(transformedList);


            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(transformedList)}");

        }
    }
}