using Microsoft.Extensions.Logging;

namespace WMCUtility
{
    internal class EnableGuideLoaderCommand : ICommand
    {
        private readonly ILogger<EnableGuideLoaderCommand> _logger;

        public EnableGuideLoaderCommand(ILogger<EnableGuideLoaderCommand> logger)
        {
            _logger = logger;
        }

        public int Run()
        {
            _logger.LogInformation("Enabling in-band guide loader");

            var success = RegistryHelpers.TrySetDWord(_logger, RegistryHelpers.MCEServicePath + "BackgroundScanner", "PeriodicScanEnabled", 1);
            if (success)
            {
                success = RegistryHelpers.TrySetDWord(_logger, RegistryHelpers.MCEServicePath + "GLID", "DisableInbandSchedule", 0);
            }
            if (success)
            {
                _logger.LogInformation("In-band guide loader enabled - the computer must be restarted for the settings to take effect");
            }
            else
            {
                _logger.LogWarning("In-band guide loader may not have been enabled - check the registry");
            }

            return 0;
        }
    }
}
