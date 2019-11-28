using System;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace WMCUtility
{
    internal class DisableGuideLoaderCommand : ICommand
    {
        private const string BaseKeyName = @"Software\Microsoft\Windows\CurrentVersion\Media Center\Service\";
        private readonly ILogger<DisableGuideLoaderCommand> _logger;

        public DisableGuideLoaderCommand(ILogger<DisableGuideLoaderCommand> logger)
        {
            _logger = logger;
        }

        public int Run()
        {
            _logger.LogInformation("Disabling in-band guide loader");

            bool reply = SetDWord(BaseKeyName, "BackgroundScanner", "PeriodicScanEnabled", 0);
            if (reply)
                reply = SetDWord(BaseKeyName, "GLID", "DisableInbandSchedule", 1);

            if (reply)
            {
                _logger.LogInformation("In-band guide loader disabled - the computer must be restarted for any new settings to take effect");
            }
            else
            {
                _logger.LogWarning("In-band guide loader may not have been disabled - check the registry");
            }

            return 0;
        }

        private bool SetDWord(string baseKey, string subKey, string name, int value)
        {
            bool result = false;

            var keyName = baseKey + subKey;

            RegistryKey key = default;
            try
            {
                _logger.LogInformation("Opening registry key " + keyName);

                key = Registry.LocalMachine.OpenSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (key is null)
                {
                    _logger.LogInformation("Creating registry key " + keyName);
                    key = Registry.LocalMachine.CreateSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                }
                if (key != null)
                {
                    object namedData = key.GetValue(name);
                    if (namedData is null)
                    {
                        _logger.LogInformation("Creating " + name + " with value " + value);
                        //key.SetValue(name, value);
                    }
                    else
                    {
                        if ((int)namedData != value)
                        {
                            _logger.LogInformation("Setting " + name + " to value " + value);
                            //key.SetValue(name, value);

                        }
                        else
                            _logger.LogInformation(name + " not changed - value already " + value);
                    }
                }

                key.Close();

                result = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "<E> An exception of type {ExceptionType} has been thrown", ex.GetType());
            }
            finally
            {
                key?.Dispose();
            }

            return result;
        }
    }
}
