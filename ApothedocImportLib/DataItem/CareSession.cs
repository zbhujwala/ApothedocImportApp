using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class CareSession
    {
        public int Id { get; set; }
        public string? CareType { get; set; }
        public int? UsingManualTimeEntry { get; set; }
        public int? DurationSeconds { get; set; }
        public string? PerformedOn { get; set; }
        public string? SubmittedAt { get; set; }
        public User? PerformedBy { get; set; }
        public User? SubmittedBy { get; set; }
        public string? CareNote { get; set; }
        public int? ComplexCare { get; set; }
        public int? InteractedWithPatient { get; set; } = 0;
        public bool? MatchedSubmittedPerformedBy { get; set; }
        public bool? MatchedSubmittedPerformedAt { get; set; }
    }

    public class CareSessionWrapper
    {
        public List<CareSession>? CareSessions { get; set; }
    }
}
