////////////////////////////////////////////////////////////////////////////////// 
//                                                                              //
//      Copyright (C) 2005-2016 nzsjb                                           //
//                                                                              //
//  This Program is free software; you can redistribute it and/or modify        //
//  it under the terms of the GNU General Public License as published by        //
//  the Free Software Foundation; either version 2, or (at your option)         //
//  any later version.                                                          //
//                                                                              //
//  This Program is distributed in the hope that it will be useful,             //
//  but WITHOUT ANY WARRANTY; without even the implied warranty of              //
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the                //
//  GNU General Public License for more details.                                //
//                                                                              //
//  You should have received a copy of the GNU General Public License           //
//  along with GNU Make; see the file COPYING.  If not, write to                //
//  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.       //
//  http://www.gnu.org/copyleft/gpl.html                                        //
//                                                                              //  
//////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace WMCUtility
{
    internal class ExportCommand : ICommand
    {
        private const string OrganisationName = "Geekzone";
        private const string ProductName = "EPG Collector";

        private readonly ILogger<DisableGuideLoaderCommand> _logger;
        private object _objectStore;

        public ExportCommand(ILogger<DisableGuideLoaderCommand> logger)
        {
            _logger = logger;
        }

        public int Run()
        {
            _logger.LogInformation("Windows Media Center Utility exporting data");

            ReflectionServices.LoadLibraries();

            bool reply = false;
            if (Environment.OSVersion.Version.Minor != 0)
                reply = GetObjectStore();
            if (!reply)
                return 1;

            _logger.LogInformation("Opened Windows Media Center database " + ReflectionServices.GetPropertyValue(_objectStore.GetType(), _objectStore, "StoreName"));

            var channels = GetMergedChannels();
            var recordings = GetRecordings();

            var path = Path.Combine(GetDataDirectory(), "WMC Export.xml");

            _logger.LogInformation("Exporting Windows Media Center data to " + path);

            try
            {
                using (var writer = XmlWriter.Create(path))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("wmcdata");

                    writer.WriteAttributeString("generator-info-name", Assembly.GetExecutingAssembly().GetName().Name
                        + "/" + Assembly.GetExecutingAssembly().GetName().Version.ToString());

                    CreateChannelsSection(writer, channels);
                    CreateRecordingsSection(writer, recordings);

                    writer.WriteEndElement();
                    writer.WriteEndDocument();

                    writer.Flush();
                    writer.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "<E> An exception of type {ExceptionType} has been thrown", ex.GetType());
                return 1;
            }
            finally
            {
                ReflectionServices.InvokeMethod(_objectStore.GetType(), _objectStore, "Dispose", Array.Empty<object>());
            }

            ReflectionServices.InvokeMethod(_objectStore.GetType(), _objectStore, "Dispose", Array.Empty<object>());

            _logger.LogInformation(channels.Count + " channels exported from database");
            if (recordings != null)
                _logger.LogInformation(recordings.Count + " recordings exported from database");
            else
                _logger.LogInformation("0 recordings exported from database");

            _logger.LogInformation("Windows Media Center Utility finished - returning exit code 0");

            return 0;
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

        private bool GetObjectStore()
        {
            _logger.LogInformation("Opening Windows Media Center database");

            string s = "Unable upgrade recording state.";
            byte[] bytes = Convert.FromBase64String("FAAODBUITwADRicSARc=");
            byte[] buffer2 = Encoding.ASCII.GetBytes(s);

            for (int i = 0; i != bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ buffer2[i]);
            }

            var type = ReflectionServices.GetType("mcstore", "ObjectStore");

            var clientId = ReflectionServices.GetStaticValue(type, "GetClientId", new object[] { true }) as string;
            byte[] buffer = Encoding.Unicode.GetBytes(clientId);

            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(buffer);
                clientId = Convert.ToBase64String(hash);
            }
            string friendlyName = Encoding.ASCII.GetString(bytes);
            string displayName = clientId;

            try
            {
                _objectStore = ReflectionServices.InvokeMethod(type, null, "Open", new object[] { string.Empty, friendlyName, displayName, true });

                if (_objectStore == null)
                {
                    _logger.LogInformation("<e> Cannot get Windows Media Center information: database cannot be opened");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "<e> Cannot get Windows Media Center information: database cannot be opened");
                return false;
            }

            return true;
        }

        private IReadOnlyList<object> GetMergedChannels()
        {
            var result = new List<object>();

            object mergedChannelsObject = ReflectionServices.InvokeConstructor("mcepg", "MergedChannels", new Type[] { _objectStore.GetType() }, new object[] { _objectStore });
            object mergedChannels = ReflectionServices.InvokeMethod(mergedChannelsObject.GetType(), mergedChannelsObject, "ToList", Array.Empty<object>());

            foreach (object channel in mergedChannels as IList)
            {
                if (ReflectionServices.GetPropertyValue(channel.GetType(), channel, "Lineup") != null)
                {
                    object tuningInfosObject = ReflectionServices.GetPropertyValue(channel.GetType(), channel, "TuningInfos");
                    object tuningInfos = ReflectionServices.InvokeMethod(tuningInfosObject.GetType(), tuningInfosObject, "ToList", Array.Empty<object>());

                    foreach (object tuningInfo in tuningInfos as System.Collections.IList)
                    {
                        if (ReflectionServices.GetPropertyValue(tuningInfo.GetType(), tuningInfo, "Device") != null)
                        {
                            result.Add(channel);

                            /*if (!channel.HasUserMappedListings && channel.PrimaryChannel != null && channel.ChannelType != ChannelType.UserHidden)
                                channel.AddChannelListings(channel.PrimaryChannel);*/

                            break;
                        }
                    }
                }
            }

            return result;
        }

        private IReadOnlyList<object> GetRecordings()
        {
            var result = new List<object>();

            object library = ReflectionServices.InvokeConstructor("mcepg", "Library", new Type[] { _objectStore.GetType(), typeof(bool), typeof(bool) }, new object[] { _objectStore, false, false });

            object recordingsObject = ReflectionServices.GetPropertyValue(library.GetType(), library, "Recordings");
            if (recordingsObject is null)
            {
                _logger.LogInformation("No library found");
            }
            else
            {
                object recordingsList = ReflectionServices.InvokeMethod(recordingsObject.GetType(), recordingsObject, "ToList", Array.Empty<object>());

                foreach (object recording in recordingsList as IList)
                {
                    result.Add(recording);
                }
            }

            return result;
        }

        private void CreateChannelsSection(XmlWriter writer, IEnumerable<object> items)
        {
            if (items.Any())
            {
                writer.WriteStartElement("channels");

                var dvbProcessed = new Collection<object>();
                var atscProcessed = new Collection<object>();

                foreach (object item in items)
                {
                    writer.WriteStartElement("channel");

                    writer.WriteAttributeString("channelNumber", ReflectionServices.GetPropertyValue(item.GetType(), item, "ChannelNumber").ToString());
                    writer.WriteAttributeString("callSign", (string)ReflectionServices.GetPropertyValue(item.GetType(), item, "CallSign"));

                    object primaryChannel = ReflectionServices.GetPropertyValue(item.GetType(), item, "PrimaryChannel");
                    writer.WriteAttributeString("matchName", (string)ReflectionServices.GetPropertyValue(primaryChannel.GetType(), primaryChannel, "MatchName"));
                    writer.WriteAttributeString("uid", GetUniqueID(ReflectionServices.GetPropertyValue(primaryChannel.GetType(), primaryChannel, "UIds")));

                    object lineup = ReflectionServices.GetPropertyValue(item.GetType(), item, "Lineup");
                    writer.WriteAttributeString("lineup", (string)ReflectionServices.GetPropertyValue(lineup.GetType(), lineup, "Name"));

                    writer.WriteStartElement("tuningInfos");

                    object tuningInfosObject = ReflectionServices.GetPropertyValue(item.GetType(), item, "TuningInfos");
                    object tuningInfos = ReflectionServices.InvokeMethod(tuningInfosObject.GetType(), tuningInfosObject, "ToList", new object[] { });

                    foreach (object tuningInfo in tuningInfos as System.Collections.IList)
                    {
                        object tuneRequest = ReflectionServices.GetPropertyValue(tuningInfo.GetType(), tuningInfo, "TuneRequest");
                        if (tuneRequest.GetType().Name == "DVBTuneRequest")
                        {
                            bool alreadyDone = CheckDVBProcessed(dvbProcessed, tuneRequest);
                            if (!alreadyDone)
                            {
                                writer.WriteStartElement("dvbTuningInfo");

                                object locator = ReflectionServices.GetPropertyValue(tuneRequest.GetType(), tuneRequest, "Locator");
                                writer.WriteAttributeString("frequency", ((int)(ReflectionServices.GetPropertyValue(locator.GetType(), locator, "CarrierFrequency"))).ToString());
                                writer.WriteAttributeString("onid", ((int)(ReflectionServices.GetPropertyValue(tuneRequest.GetType(), tuneRequest, "ONID"))).ToString());
                                writer.WriteAttributeString("tsid", ((int)(ReflectionServices.GetPropertyValue(tuneRequest.GetType(), tuneRequest, "TSID"))).ToString());
                                writer.WriteAttributeString("sid", ((int)(ReflectionServices.GetPropertyValue(tuneRequest.GetType(), tuneRequest, "SID"))).ToString());

                                writer.WriteEndElement();

                                dvbProcessed.Add(tuneRequest);
                            }
                        }
                        else
                        {
                            if (tuneRequest.GetType().Name == "ATSCTuneRequest")
                            {
                                bool alreadyDone = CheckATSCProcessed(atscProcessed, tuneRequest);
                                if (!alreadyDone)
                                {
                                    writer.WriteStartElement("atscTuningInfo");

                                    object locator = ReflectionServices.GetPropertyValue(tuneRequest.GetType(), tuneRequest, "Locator");
                                    writer.WriteAttributeString("frequency", ((int)(ReflectionServices.GetPropertyValue(locator.GetType(), locator, "CarrierFrequency"))).ToString());

                                    writer.WriteAttributeString("majorChannel", ((int)(ReflectionServices.GetPropertyValue(tuneRequest.GetType(), tuneRequest, "Channel"))).ToString());
                                    writer.WriteAttributeString("minorChannel", ((int)(ReflectionServices.GetPropertyValue(tuneRequest.GetType(), tuneRequest, "MinorChannel"))).ToString());

                                    writer.WriteEndElement();

                                    atscProcessed.Add(tuneRequest);
                                }
                            }
                            else
                                _logger.LogInformation("Tune request type '" + tuneRequest.GetType().Name + "' ignored");
                        }
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }

        private bool CheckDVBProcessed(IEnumerable<object> items, object newRequest)
        {
            object newLocator = ReflectionServices.GetPropertyValue(newRequest.GetType(), newRequest, "Locator");
            int newFrequency = (int)ReflectionServices.GetPropertyValue(newLocator.GetType(), newLocator, "CarrierFrequency");

            int newONID = (int)ReflectionServices.GetPropertyValue(newRequest.GetType(), newRequest, "ONID");
            int newTSID = (int)ReflectionServices.GetPropertyValue(newRequest.GetType(), newRequest, "TSID");
            int newSID = (int)ReflectionServices.GetPropertyValue(newRequest.GetType(), newRequest, "SID");

            foreach (object item in items)
            {
                object existingLocator = ReflectionServices.GetPropertyValue(item.GetType(), item, "Locator");
                int existingFrequency = (int)ReflectionServices.GetPropertyValue(existingLocator.GetType(), existingLocator, "CarrierFrequency");

                int existingONID = (int)ReflectionServices.GetPropertyValue(item.GetType(), item, "ONID");
                int existingTSID = (int)ReflectionServices.GetPropertyValue(item.GetType(), item, "TSID");
                int existingSID = (int)ReflectionServices.GetPropertyValue(item.GetType(), item, "SID");

                if (existingFrequency == newFrequency && existingONID == newONID &&
                    existingTSID == newTSID && existingSID == newSID)
                    return true;
            }

            return false;
        }

        private bool CheckATSCProcessed(IEnumerable<object> atscProcessed, object newRequest)
        {
            object newLocator = ReflectionServices.GetPropertyValue(newRequest.GetType(), newRequest, "Locator");
            int newFrequency = (int)ReflectionServices.GetPropertyValue(newLocator.GetType(), newLocator, "CarrierFrequency");

            int newChannel = (int)ReflectionServices.GetPropertyValue(newRequest.GetType(), newRequest, "Channel");
            int newMinorChannel = (int)ReflectionServices.GetPropertyValue(newRequest.GetType(), newRequest, "MinorChannel");

            foreach (object existingRequest in atscProcessed)
            {
                object existingLocator = ReflectionServices.GetPropertyValue(existingRequest.GetType(), existingRequest, "Locator");
                int existingFrequency = (int)ReflectionServices.GetPropertyValue(existingLocator.GetType(), existingLocator, "CarrierFrequency");

                int existingChannel = (int)ReflectionServices.GetPropertyValue(existingRequest.GetType(), existingRequest, "Channel");
                int existingMinorChannel = (int)ReflectionServices.GetPropertyValue(existingRequest.GetType(), existingRequest, "MinorChannel");

                if (existingFrequency == newFrequency && existingChannel == newChannel && existingMinorChannel == newMinorChannel)
                    return true;
            }

            return false;
        }

        private string GetUniqueID(object uids)
        {
            if (uids == null)
                return null;

            var uidList = ReflectionServices.InvokeMethod(uids.GetType(), uids, "ToList", Array.Empty<object>()) as IList;
            if (uidList.Count == 0)
                return null;

            string firstName = null;

            foreach (object uid in uidList)
            {
                string fullName = ReflectionServices.InvokeMethod(uid.GetType(), uid, "GetFullName", Array.Empty<object>()) as string;

                if (firstName == null)
                    firstName = fullName;

                if (fullName.Contains("EPGCollector"))
                    return fullName;
            }

            return firstName;
        }

        private void CreateRecordingsSection(XmlWriter writer, IEnumerable<object> items)
        {
            if (items.Any())
            {
                writer.WriteStartElement("recordings");

                foreach (var item in items)
                {
                    writer.WriteStartElement("recording");

                    object program = ReflectionServices.GetPropertyValue(item.GetType(), item, "Program");

                    writer.WriteAttributeString("title", (string)ReflectionServices.GetPropertyValue(program.GetType(), program, "Title"));
                    writer.WriteAttributeString("description", (string)ReflectionServices.GetPropertyValue(program.GetType(), program, "ShortDescription"));
                    writer.WriteAttributeString("startTime", ((DateTime)(ReflectionServices.GetPropertyValue(item.GetType(), item, "ContentStartTime"))).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("seasonNumber", ((int)(ReflectionServices.GetPropertyValue(program.GetType(), program, "SeasonNumber"))).ToString());
                    writer.WriteAttributeString("episodeNumber", ((int)(ReflectionServices.GetPropertyValue(program.GetType(), program, "EpisodeNumber"))).ToString());

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }
    }
}
