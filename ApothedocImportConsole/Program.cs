using ApothedocImportLib.Logic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using ApothedocImportLib.Utils;

class Program
{

    static async Task Main()
    {
        try
        {
            var currentTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File($"debug-{currentTime}.log", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
                .WriteTo.File($"error-{currentTime}.log", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
                .CreateLogger();

            ConfigUtil configUtil = new();
            var config = configUtil.LoadConfig();

            Log.Debug($">>> Running import with the following config values: \nResource API: {config.ResourceApi}\nSource Auth Token: {config.SourceAuthToken}\n" +
                $"Source OrgId: {config.SourceOrgId}\nSource Clinic Id: {config.SourceClinicId}\nDestination Auth Token: {config.TargetAuthToken}" +
                $"\nTarget Org Id: {config.TargetOrgId}\nTarget Clinic Id: {config.TargetClinicId}");
            Log.Debug("Press <Enter> to import process...");
            while(Console.ReadKey().Key != ConsoleKey.Enter) {}

            ImportApiLogic logic = new(config.ResourceApi);

            _ = logic.TransferClinicDataAsync(config.SourceOrgId, config.SourceClinicId, config.SourceAuthToken, config.TargetOrgId, config.TargetClinicId, config.TargetAuthToken);

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
