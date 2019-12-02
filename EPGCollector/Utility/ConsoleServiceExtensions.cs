using DomainObjects;

namespace EPGCollector
{
    public partial class ConsoleServiceExtensions
    {
        public static int ExportMediaCenterChannels()
        {
            Logger.Instance.Write("Running Windows Media Centre Utility to export data");

            var service = new ConsoleService();

            service.Run("WMCUtility.exe", "EXPORTDATA");

            Logger.Instance.Write("Windows Media Centre Utility has completed: exit code " + service.ExitCode);

            return service.ExitCode;
        }
    }
}
