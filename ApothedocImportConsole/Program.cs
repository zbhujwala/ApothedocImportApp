using ApothedocImportLib.Logic;

class Program
{

    static async Task Main()
    {
        try
        {
            Console.WriteLine("Enter Resource API (ex: \"https://dev.apothedoc.com/api/\")");
            var resourceApi = Console.ReadLine();

            Console.WriteLine("Enter source Auth Token:");
            var sourceAuthToken = Console.ReadLine();

            Console.WriteLine("Enter source Organization ID:");
            var sourceOrgId = Console.ReadLine();

            Console.WriteLine("Enter source Clinic ID");
            var sourceClinicId = Console.ReadLine();

            Console.WriteLine("Enter destination Auth Token:");
            var destAuthToken = Console.ReadLine();

            Console.WriteLine("Enter destination Organization ID:");
            var destOrgId = Console.ReadLine();

            Console.WriteLine("Enter destination Clinic ID:");
            var destClinicId = Console.ReadLine();

            ImportApiLogic logic = new(resourceApi);

            _ = logic.TransferClinicDataAsync(sourceOrgId, sourceClinicId, sourceAuthToken, destOrgId, destClinicId, destAuthToken);

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            Console.ReadLine();
        }
    }

}
