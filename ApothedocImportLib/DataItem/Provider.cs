
namespace ApothedocImportLib.DataItem
{
    public class Provider
    {
        public int id { get; set; }
        public string firstName { get; set;}
        public string lastName { get; set; }

    }

    public class ProviderListWrapper
    {
        public List<Provider>? Providers { get; set; }
    }
}