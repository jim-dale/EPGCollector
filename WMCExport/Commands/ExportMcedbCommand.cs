using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.MediaCenter.Guide;
using Microsoft.MediaCenter.Pvr;
using Microsoft.MediaCenter.Store;

namespace WMCUtility
{
    internal class ExportMcedbCommand : ICommand
    {
        private const string OrganisationName = "Geekzone";
        private const string ProductName = "EPG Collector";

        private readonly ILogger<ExportMcedbCommand> _logger;

        public ExportMcedbCommand(ILogger<ExportMcedbCommand> logger)
        {
            _logger = logger;
        }

        public int Run()
        {
            _logger.LogInformation("Windows Media Center Utility exporting data");

            string generator = Assembly.GetExecutingAssembly().GetName().Name + "/" + Assembly.GetExecutingAssembly().GetName().Version;
            var clientId = ObjectStore.GetClientId(true);

            using (var objectStore = OpenObjectStore(clientId, isAdminRequested: true))
            {
                _logger.LogInformation("Opened Windows Media Center database \"{StoreName}\"", objectStore.StoreName);

                var document = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("wmcdata",
                        new XAttribute("generator-info-name", generator)));

                var mergedChannels = GetMergedChannels(objectStore);
                if (mergedChannels.Any())
                {
                    var node = mergedChannels.AsXElement();
                    document.Root.Add(node);
                }
                else
                {
                    _logger.LogInformation("No channels found");
                }

                var recordings = GetRecordings(objectStore);
                if (recordings.Any())
                {
                    var node = recordings.AsXElement();
                    document.Root.Add(node);
                }
                else
                {
                    _logger.LogInformation("No recordings found");
                }

                var outputPath = Path.Combine(GetDataDirectory(), "WMC Export.xml");
                _logger.LogInformation("Exporting Windows Media Center data to {OutputPath}", outputPath);

                using (var writer = XmlTextWriter.Create(outputPath, new XmlWriterSettings { /*Encoding = new UTF8Encoding(false),*/ Indent = true }))
                {
                    document.Save(writer);
                }
            }

            return 0;
        }

        private ObjectStore OpenObjectStore(string clientId, bool isAdminRequested)
        {
            ObjectStore result;

            if (isAdminRequested)
            {
                var providerName = GetProviderName();
                var password = GetPassword(clientId);

                result = ObjectStore.Open(string.Empty, providerName, password, isAdminRequested: true);
            }
            else
            {
                result = ObjectStore.Open();
            }

            return result;
        }

        private string GetProviderName()
        {
            var bytes = Convert.FromBase64String("FAAODBUITwADRicSARc=");
            var buffer = Encoding.ASCII.GetBytes("Unable upgrade recording state.");

            for (int i = 0; i != bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ buffer[i]);
            }

            return Encoding.ASCII.GetString(bytes);
        }

        private string GetPassword(string clientId)
        {
            string result = default;

            var buffer = Encoding.Unicode.GetBytes(clientId);
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(buffer);

                result = Convert.ToBase64String(hash);
            }

            return result;
        }

        private string GetDataDirectory()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            string parentFolder = (principal.IsInRole(WindowsBuiltInRole.Administrator))
                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string result = Path.Combine(parentFolder, OrganisationName, ProductName);

            if (Directory.Exists(result) == false)
            {
                Directory.CreateDirectory(result);
            }

            return result;
        }

        private IReadOnlyList<MergedChannel> GetMergedChannels(ObjectStore objectStore)
        {
            var result = new List<MergedChannel>();

            using (var mergedChannels = new MergedChannels(objectStore))
            {
                var channels = mergedChannels.ToList();

                foreach (var channel in channels)
                {
                    if (channel.Lineup != null)
                    {
                        var tuningInfos = channel.TuningInfos.ToList();

                        foreach (var tuningInfo in tuningInfos)
                        {
                            if (tuningInfo.Device != null)
                            {
                                result.Add(channel);
                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }

        private IReadOnlyList<Recording> GetRecordings(ObjectStore objectStore)
        {
            var result = new List<Recording>();

            using (var library = new Library(objectStore, false, false))
            {
                var temp = library.Recordings;
                if (temp != null)
                {
                    result = temp.ToList();
                }
            }

            return result;
        }
    }
}
