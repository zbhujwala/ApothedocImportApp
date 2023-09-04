using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ApothedocImportLib.DataItem
{
    public class Patient
    {
        public int? Id { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Mrn { get; set; }
        public string? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Gender { get; set; }
        public string? PreferredName { get; set; }
        public string? MedicareId { get; set; }

    }

    public class PatientDetails
    {
        public string? DateOfBirth { get; set; }
        public string? FirstName { get; set; }
        public string? Gender { get; set; }
        public string? LastName { get; set; }
        public string? MedicareId { get; set; }
        public string? MiddleName { get; set; }
        public string? Mrn { get; set; }
        public string? NonHealthNote { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PreferredName { get; set; }
    }

    public class PatientListWrapper
    {
        public List<Patient>? Patients { get; set; }
    }

    public class PatientCreateResponse
    {
        public bool? Success { get; set; }

        public int? PatientId { get; set; }
    }

    public class PatientDetailsWrapper
    {
        public PatientDetails? PatientDetails { get; set; }
    }
}
