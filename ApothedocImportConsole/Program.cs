using ApothedocImportLib.Logic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

class Program
{

    static async Task Main()
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File("debug.log", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
                .WriteTo.File("error.log", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
                .CreateLogger();

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
