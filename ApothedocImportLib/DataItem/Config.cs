using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class Config
    {
        public required string ResourceApi { get; set; }
        public required string SourceAuthToken { get; set; }
        public required string SourceOrgId { get; set; }
        public required string SourceClinicId { get; set; }
        public required string TargetAuthToken { get; set; }
        public required string TargetOrgId { get; set;}
        public required string TargetClinicId { get;set; }
        public bool SkipCareSessionImport { get; set; }
        public required List<ProviderIdMapping> ProviderMappings { get; set; }
    }
}
