using ApothedocImportLib.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class CareSession
    {
        public int? Id { get; set; }
        public string? CareType { get; set; }
        [JsonConverter(typeof(IntToBooleanConverter))]
        public int? UsingManualTimeEntry { get; set; }
        public int? DurationSeconds { get; set; }
        public string? PerformedOn { get; set; }
        public string? SubmittedAt { get; set; }
        public Provider? PerformedBy { get; set; }
        public Provider? SubmittedBy { get; set; }
        public string? CareNote { get; set; }
        [JsonConverter(typeof(IntToBooleanConverter))]
        public int? ComplexCare { get; set; }
        [JsonConverter(typeof(IntToBooleanConverter))]
        public int InteractedWithPatient { get; set; } = 0;
    }

    public class CareSessionWrapper
    {
        public List<CareSession>? CareSessions { get; set; }
    }
}
