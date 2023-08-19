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

    public class PatientWrapper
    {
        public List<Patient>? Patients { get; set; }
    }

    public class PatientGetWrapper
    {
        public Boolean? Success { get; set; }

        public int? PatientId { get; set; }
    }
}
