using System;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace WMCUtility
{
    internal static class RegistryHelpers
    {
        internal const string MCEServicePath = @"Software\Microsoft\Windows\CurrentVersion\Media Center\Service\";

        public static bool TrySetDWord(ILogger logger, string path, string name, int value)
        {
            bool result = false;

            try
            {
                using (var key = OpenOrCreateSubKey(logger, path))
                {
                    SetDWORD(logger, key, name, value);
                }

                result = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "<E> Failed to set the required registry entry");
            }

            return result;
        }

        private static RegistryKey OpenOrCreateSubKey(ILogger logger, string path)
        {
            logger.LogInformation("Opening registry key \"{KeyPath}\"", path);

            var result = Registry.LocalMachine.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (result is null)
            {
                logger.LogInformation("Creating registry key \"{KeyPath}\"", path);

                result = Registry.LocalMachine.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (result is null)
                {
                    logger.LogError("Failed to create registry key \"{KeyPath}\"", path);
                }
            }

            return result;
        }

        private static void SetDWORD(ILogger logger, RegistryKey key, string name, int value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var temp = (int?)key.GetValue(name);
            if (temp.HasValue == false)
            {
                logger.LogInformation("Adding name/value pair \"{Name}\"={Value}", name, value);
                key.SetValue(name, value, RegistryValueKind.DWord);
            }
            else
            {
                var currentValue = temp.Value;

                if (value != currentValue)
                {
                    logger.LogInformation("Setting name/value pair \"{Name}\"={Value}", name, value);
                    key.SetValue(name, value, RegistryValueKind.DWord);
                }
                else
                {
                    logger.LogInformation("\"{Name}\" is already set to {Value}", name, value);
                }
            }
        }
    }
}
