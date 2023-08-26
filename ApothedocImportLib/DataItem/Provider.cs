using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.DataItem
{
    public class Provider
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class ProviderListWrapper
    {
        public List<Provider>? Providers { get; set; }
    }

    public class ProviderIdMapping
    {
        public int SourceId { get; set; }
        public int TargetId { get; set; }
    }
}
