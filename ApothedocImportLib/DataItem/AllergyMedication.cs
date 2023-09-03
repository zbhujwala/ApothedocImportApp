using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class AllergyMedication
    {
        public string? Allergies { get; set; }
        public string? Medications { get; set; }
    }

    public class AllergyWrapper
    {
        public AllergyMedication? AllergyMedication { get; set; }
    }
}
