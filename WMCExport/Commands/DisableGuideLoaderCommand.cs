using Microsoft.Extensions.Logging;

namespace WMCUtility
{
    internal class DisableGuideLoaderCommand : ICommand
    {
        private readonly ILogger<DisableGuideLoaderCommand> _logger;

        public DisableGuideLoaderCommand(ILogger<DisableGuideLoaderCommand> logger)
        {
            _logger = logger;
        }

        public int Run()
        {
            _logger.LogInformation("Disabling in-band guide loader");

            var success = RegistryHelpers.TrySetDWord(_logger, RegistryHelpers.MCEServicePath + "BackgroundScanner", "PeriodicScanEnabled", 0);
            if (success)
            {
                success = RegistryHelpers.TrySetDWord(_logger, RegistryHelpers.MCEServicePath + "GLID", "DisableInbandSchedule", 1);
            }
            if (success)
            {
                _logger.LogInformation("In-band guide loader disabled - the computer must be restarted for any new settings to take effect");
            }
            else
            {
                _logger.LogWarning("In-band guide loader may not have been disabled - check the registry");
            }

            return 0;
        }
    }
}
