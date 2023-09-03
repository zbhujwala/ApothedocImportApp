using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class EmergencyContact
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Relationship { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class EmergencyContactWrapper
    { 
        public List<EmergencyContact> EmergencyContacts { get; set; }
    }

}
