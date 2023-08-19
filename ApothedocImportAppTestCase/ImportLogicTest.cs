using ApothedocImportLib.DataItem;
using ApothedocImportLib.Logic;
using Newtonsoft.Json;

namespace ApothedocImportAppTestCase
{
    [TestClass]
    public class ImportLogicTest
    {
        string patientId = "1740";
        string resourceApi;
        string orgId;
        string clinicid;
        string authToken;
        ImportApiLogic logic;

        [TestInitialize] 
        public void Init() {
            resourceApi = "https://dev.apothedoc.com/api/";
            orgId = "1";
            clinicid = "1";
            // Replace this for each session
            authToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJvcmdJZCI6MSwidXNlciI6InphaWRAc3luZXJncnguY29tIiwiaWF0IjoxNjkyNDY4MzgxLCJleHAiOjE2OTI1MTE1ODF9.jV1RM8VrqiCtFQVvO4lInZeCcyXMhQ8Xw5PWGAIIbWI";
            logic = new(resourceApi);
        }

        [TestMethod]
        public async Task TestGetPatientList()
        {
            var patientList = await logic.GetPatientListForClinic(orgId, clinicid, authToken);

            Assert.IsTrue(patientList.Count > 0);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(patientList)}");
        }

        [TestMethod]
        public async Task TestGetPatientCareSessions()
        {
            patientId = "1";
            var careSessions = await logic.GetPatientCareSessions(orgId, clinicid, patientId, authToken);

            Assert.IsTrue(careSessions.Count > 0);


            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(careSessions)}");
        }

        [TestMethod]
        public async Task TestGetPatientEnrollmentStatus()
        {
            var enrollmentStatus = await logic.GetPatientEnrollmentStatus(orgId, clinicid, patientId, authToken);

            Assert.IsNotNull(enrollmentStatus);

            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentStatus)}");
        }

        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsCCM()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("ccm", orgId, clinicid, patientId, authToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails)}");
        }

        // Get BHI
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsBHI()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("bhi", orgId, clinicid, patientId, authToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails)}");
        }

        // Get RPM
        [TestMethod]
        public async Task TestGetPatientEnrollmentDetailsRPM()
        {
            // Get CCM
            var enrollmentDetails = await logic.GetPatientEnrollmentDetails("rpm", orgId, clinicid, patientId, authToken);

            Assert.IsNotNull(enrollmentDetails);
            Console.WriteLine($"Response:");
            Console.WriteLine($"{JsonConvert.SerializeObject(enrollmentDetails)}");
        }
    }
}