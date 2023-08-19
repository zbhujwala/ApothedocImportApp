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
        public Specialist? Specialist { get; set; }
        public string? EquipmentSetupAndEducation { get; set; }     // RPM Specfic
        public int? EnrolledSameDayOfficeVisit { get; set; }      // CCM specific, bool integer
    }

    public class EnrollmentStatus
    {
        public bool? Ccm { get; set; }
        public bool? Bhi { get; set; }
        public bool? Rpm { get; set; }
        public bool? Pcm { get; set; }
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
