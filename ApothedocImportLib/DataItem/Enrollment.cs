using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class Enrollment
    {
        public string? EnrollmentDate { get; set; }     // Date string
        public string? CancellationDate { get; set; }   // Date string
        public string? InformationSheet { get; set; }   // Date string
        public string? PatientAgreement { get; set; }   // Date string
        public bool? VerbalAgreement { get; set; }
        public Provider? PrimaryClinician { get; set; }
        public User? Specialist { get; set; }
        public string? EquipmentSetupAndEducation { get; set; }     // RPM Specfic
        public int? EnrolledSameDayOfficeVisit { get; set; }      // CCM specific, bool integer
    }

    public class EnrollmentStatus
    {
        public bool Ccm { get; set; } = false;
        public bool Bhi { get; set; } = false;
        public bool Rpm { get; set; } = false;
        public bool Pcm { get; set; } = false;
    }
    public class EnrollmentWrapper
    {
        public bool? Success { get; set; }
        public Enrollment? Enrollment { get; set; }
    }

    public class EnrollmentStatusWrapper
    {
        public bool? Success { get; set; }

        public EnrollmentStatus? CurrentEnrollments { get; set; }
    }

}
