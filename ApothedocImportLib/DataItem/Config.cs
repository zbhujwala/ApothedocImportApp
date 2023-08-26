using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class Config
    {
        public string ResourceApi { get; set; }
        public string SourceAuthToken { get; set; }
        public string SourceOrgId { get; set; }
        public string SourceClinicId { get; set; }
        public string TargetAuthToken { get; set; }
        public string TargetOrgId { get; set;}
        public string TargetClinicId { get;set; }
        public bool SkipCareSessionImport { get; set; }
        public List<ProviderIdMapping> ProviderMappings { get; set; }
    }
}
