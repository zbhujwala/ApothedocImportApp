using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class ContactInformation
    {
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Apt { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Email { get; set; }
        public Dictionary<string, string>? ContactDays { get; set; }
        public Dictionary<string, string>? ContactTimes { get; set; }
        public List<AltPhoneNumber> altPhones { get; set; }
    }
    public class AltPhoneNumber
    {
        public string? Label { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ContactInformationWrapper
    {
        public ContactInformation ContactInformation { get; set; }
    }
}
