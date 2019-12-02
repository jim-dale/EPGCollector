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
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;

namespace DomainObjects
{
    /// <summary>
    /// The class that creates an MXF file for import to 7MC.
    /// </summary>
    public static class OutputFileMXF
    {
        private static Collection<KeywordGroup> groups;
        private static Collection<string> people;
        private static Collection<string> series;
        private static Collection<int> stationImages;
        private static Collection<Guid> programImages;
        private static Collection<string> duplicateStationNames;

        private static bool isSpecial;
        private static bool isMovie;
        private static bool isSports;
        private static bool isKids;
        private static bool isNews;

        private static Collection<string> programIdentifiers;
        private static Collection<string> programIdentifierTitles;

        /// <summary>
        /// Create the MXF file
        /// </summary>
        /// <returns>An error message if the process fails; null otherwise</returns>
        public static string Process()
        {
            if (OptionEntry.IsDefined(RunParameters.Instance.Options, OptionName.NoDataNoFile))
            {
                if (TVStation.EPGCount(RunParameters.Instance.StationCollection) == 0)
                {
                    Logger.Instance.Write("No data collected - import process abandoned");
                    return null;
                }
            }

            var mcepgVersion = GetAssemblyVersionString("mcepg.dll");
            if (string.IsNullOrWhiteSpace(mcepgVersion))
                return ("Failed to get the assembly version for mcpeg.dll");

            var mcepgPublicKey = GetAssemblyPublicKey("mcepg.dll");
            if (string.IsNullOrWhiteSpace(mcepgPublicKey))
                return ("Failed to get the public key for mcpeg.dll");

            var mcstoreVersion = GetAssemblyVersionString("mcstore.dll");
            if (string.IsNullOrWhiteSpace(mcstoreVersion))
                return ("Failed to get the assembly version for mcstore.dll");

            var mcstorePublicKey = GetAssemblyPublicKey("mcstore.dll");
            if (string.IsNullOrWhiteSpace(mcstorePublicKey))
                return ("Failed to get the public key for mcstore.dll");

            var actualFileName = Path.Combine(RunParameters.DataDirectory, "TVGuide.mxf");

            try
            {
                if (File.Exists(actualFileName))
                {
                    Logger.Instance.Write("Deleting any existing version of output file");
                    File.SetAttributes(actualFileName, FileAttributes.Normal);
                    File.Delete(actualFileName);
                }
            }
            catch (IOException ex)
            {
                Logger.Instance.Write("File delete exception: " + ex.Message);
            }

            var importName = (string.IsNullOrWhiteSpace(RunParameters.Instance.WMCImportName)) ? "EPG Collector" : RunParameters.Instance.WMCImportName;
            var importReference = importName.Replace(" ", string.Empty);
            Logger.Instance.Write("Import name set to '" + importName + "'");

            duplicateStationNames = new Collection<string>();
            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    int occurrences = CountStationName(station);
                    if (occurrences > 1)
                    {
                        if (duplicateStationNames.Contains(station.Name) == false)
                            duplicateStationNames.Add(station.Name);
                    }
                }
            }

            Logger.Instance.Write("Creating output file: " + actualFileName);

            try
            {
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    NewLineOnAttributes = false,
                    Encoding = (OutputFile.UseUnicodeEncoding == false) ? Encoding.UTF8 : Encoding.Unicode,
                    CloseOutput = true
                };

                using (var writer = XmlWriter.Create(actualFileName, settings))
                {
                    writer.WriteStartDocument();

                    writer.WriteStartElement("MXF");
                    writer.WriteAttributeString("xmlns", "sql", null, "urn:schemas-microsoft-com:XML-sql");
                    writer.WriteAttributeString("xmlns", "xsi", null, @"http://www.w3.org/2001/XMLSchema-instance");

                    writer.WriteStartElement("Assembly");
                    writer.WriteAttributeString("name", "mcepg");
                    writer.WriteAttributeString("version", mcepgVersion);

                    writer.WriteAttributeString("cultureInfo", "");
                    writer.WriteAttributeString("publicKey", mcepgPublicKey);
                    writer.WriteStartElement("NameSpace");
                    writer.WriteAttributeString("name", "Microsoft.MediaCenter.Guide");

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Lineup");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Channel");
                    writer.WriteAttributeString("parentFieldName", "lineup");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Service");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "ScheduleEntry");
                    writer.WriteAttributeString("groupName", "ScheduleEntries");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Program");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Keyword");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "KeywordGroup");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Person");
                    writer.WriteAttributeString("groupName", "People");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "ActorRole");
                    writer.WriteAttributeString("parentFieldName", "program");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "DirectorRole");
                    writer.WriteAttributeString("parentFieldName", "program");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "WriterRole");
                    writer.WriteAttributeString("parentFieldName", "program");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "HostRole");
                    writer.WriteAttributeString("parentFieldName", "program");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "GuestActorRole");
                    writer.WriteAttributeString("parentFieldName", "program");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "ProducerRole");
                    writer.WriteAttributeString("parentFieldName", "program");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "GuideImage");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Affiliate");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "SeriesInfo");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Season");
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("Assembly");
                    writer.WriteAttributeString("name", "mcstore");
                    writer.WriteAttributeString("version", mcstoreVersion);

                    writer.WriteAttributeString("cultureInfo", "");
                    writer.WriteAttributeString("publicKey", mcstorePublicKey);
                    writer.WriteStartElement("NameSpace");
                    writer.WriteAttributeString("name", "Microsoft.MediaCenter.Store");

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "Provider");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("name", "UId");
                    writer.WriteAttributeString("parentFieldName", "target");
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("Providers");
                    writer.WriteStartElement("Provider");
                    writer.WriteAttributeString("id", "provider1");
                    writer.WriteAttributeString("name", importReference);
                    writer.WriteAttributeString("displayName", importName);
                    writer.WriteAttributeString("copyright", "");
                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    writer.WriteStartElement("With");
                    writer.WriteAttributeString("provider", "provider1");

                    writer.WriteStartElement("Keywords");
                    ProcessKeywords(writer);
                    writer.WriteEndElement();

                    writer.WriteStartElement("KeywordGroups");
                    ProcessKeywordGroups(writer);
                    writer.WriteEndElement();

                    writer.WriteStartElement("GuideImages");
                    ProcessGuideImages(writer);
                    writer.WriteEndElement();

                    writer.WriteStartElement("People");
                    ProcessPeople(writer);
                    writer.WriteEndElement();

                    writer.WriteStartElement("SeriesInfos");
                    ProcessSeries(writer);
                    writer.WriteEndElement();

                    writer.WriteStartElement("Seasons");
                    writer.WriteEndElement();

                    writer.WriteStartElement("Programs");
                    ProcessPrograms(writer);
                    writer.WriteEndElement();

                    writer.WriteStartElement("Affiliates");
                    ProcessAffiliates(writer, importName, importReference);
                    writer.WriteEndElement();

                    writer.WriteStartElement("Services");
                    ProcessServices(writer, importReference);
                    writer.WriteEndElement();

                    ProcessSchedules(writer);

                    writer.WriteStartElement("Lineups");
                    ProcessLineUps(writer, importName);
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                    writer.WriteEndDocument();

                    writer.Flush();
                    writer.Close();
                }
            }
            catch (XmlException ex)
            {
                return ex.Message;
            }
            catch (IOException ex)
            {
                return ex.Message;
            }

            var reply = RunImportUtility(actualFileName);
            if (reply != null)
            {
                return reply;
            }

            if (OptionEntry.IsDefined(OptionName.CreateBrChannels))
                OutputFileBladeRunner.Process(actualFileName);
            if (OptionEntry.IsDefined(OptionName.CreateArChannels))
                OutputFileAreaRegionChannels.Process(actualFileName);
            if (OptionEntry.IsDefined(OptionName.CreateSageTvFrq))
                OutputFileSageTVFrq.Process(actualFileName);

            return null;
        }

        private static int CountStationName(TVStation station)
        {
            int count = 0;

            foreach (TVStation existingStation in RunParameters.Instance.StationCollection)
            {
                if (existingStation.Included && station.Name == existingStation.Name)
                    count++;
            }

            return (count);
        }

        private static void ProcessKeywords(XmlWriter xmlWriter)
        {
            groups = new Collection<KeywordGroup>();
            groups.Add(new KeywordGroup("General"));

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    foreach (EPGEntry epgEntry in station.EPGCollection)
                    {
                        if (epgEntry.EventCategory != null)
                            ProcessCategory(xmlWriter, groups, epgEntry.EventCategory.GetDescription(EventCategoryMode.Wmc));
                    }
                }
            }

            foreach (KeywordGroup group in groups)
            {
                xmlWriter.WriteStartElement("Keyword");
                xmlWriter.WriteAttributeString("id", "k" + ((groups.IndexOf(group) + 1)));
                xmlWriter.WriteAttributeString("word", group.Name.Trim());
                xmlWriter.WriteEndElement();

                foreach (string keyword in group.Keywords)
                {
                    xmlWriter.WriteStartElement("Keyword");
                    xmlWriter.WriteAttributeString("id", "k" + (((groups.IndexOf(group) + 1) * 100) + group.Keywords.IndexOf(keyword)));
                    xmlWriter.WriteAttributeString("word", keyword.Trim());
                    xmlWriter.WriteEndElement();
                }
            }
        }

        private static void ProcessCategory(XmlWriter xmlWriter, Collection<KeywordGroup> groups, string category)
        {
            string[] parts = RemoveSpecialCategories(category);
            if (parts == null)
                return;

            if (parts.Length == 1)
            {
                foreach (KeywordGroup keywordGroup in groups)
                {
                    if (keywordGroup.Name == parts[0])
                        return;
                }

                KeywordGroup singleGroup = new KeywordGroup(parts[0]);
                singleGroup.Keywords.Add("All");
                singleGroup.Keywords.Add("General");
                groups.Add(singleGroup);
                return;
            }

            foreach (KeywordGroup group in groups)
            {
                if (group.Name == parts[0])
                {
                    for (int index = 1; index < parts.Length; index++)
                    {
                        bool keywordFound = false;

                        foreach (string keyword in group.Keywords)
                        {
                            if (keyword == parts[index])
                                keywordFound = true;
                        }

                        if (!keywordFound)
                        {
                            if (group.Keywords.Count == 0)
                                group.Keywords.Add("All");
                            group.Keywords.Add(parts[index]);
                        }
                    }

                    return;
                }
            }

            KeywordGroup newGroup = new KeywordGroup(parts[0]);
            newGroup.Keywords.Add("All");

            for (int partAddIndex = 1; partAddIndex < parts.Length; partAddIndex++)
                newGroup.Keywords.Add(parts[partAddIndex]);

            groups.Add(newGroup);
        }

        private static string[] RemoveSpecialCategories(string category)
        {
            var parts = category.Split(new string[] { "," }, StringSplitOptions.None);

            int specialCategoryCount = 0;

            foreach (string part in parts)
            {
                string specialCategory = GetSpecialCategory(part);
                if (specialCategory != null)
                    specialCategoryCount++;
            }

            if (specialCategoryCount == parts.Length)
                return (null);

            var editedParts = new string[parts.Length - specialCategoryCount];
            int index = 0;

            foreach (string part in parts)
            {
                string specialCategory = GetSpecialCategory(part);
                if (specialCategory == null)
                {
                    editedParts[index] = part;
                    index++;
                }

            }

            return editedParts;
        }

        private static void ProcessKeywordGroups(XmlWriter writer)
        {
            int groupNumber = 1;

            foreach (KeywordGroup group in groups)
            {
                writer.WriteStartElement("KeywordGroup");
                writer.WriteAttributeString("uid", "!KeywordGroup!k-" + group.Name.ToLowerInvariant().Replace(' ', '-'));
                writer.WriteAttributeString("groupName", "k" + groupNumber);

                StringBuilder keywordString = new StringBuilder();
                int keywordNumber = 0;

                foreach (string keyword in group.Keywords)
                {
                    if (keywordString.Length != 0)
                        keywordString.Append(",");
                    keywordString.Append("k" + ((groupNumber * 100) + keywordNumber));

                    keywordNumber++;
                }

                writer.WriteAttributeString("keywords", keywordString.ToString());
                writer.WriteEndElement();

                groupNumber++;
            }
        }

        private static void ProcessGuideImages(XmlWriter writer)
        {
            var stationDirectory = Path.Combine(RunParameters.DataDirectory, "Images");

            if (Directory.Exists(stationDirectory))
            {
                stationImages = new Collection<int>();

                var directoryInfo = new DirectoryInfo(stationDirectory);

                foreach (var item in directoryInfo.GetFiles())
                {
                    if (item.Extension.ToLowerInvariant() == ".png")
                    {
                        if (item.Name.Length > 4)
                        {
                            var str = item.Name.Remove(item.Name.Length - 4);

                            if (int.TryParse(str, out int serviceId))
                            {
                                stationImages.Add(serviceId);

                                writer.WriteStartElement("GuideImage");
                                writer.WriteAttributeString("id", "i" + stationImages.Count);
                                writer.WriteAttributeString("uid", "!Image!SID" + serviceId);
                                writer.WriteAttributeString("imageUrl", "file://" + item.FullName);
                                writer.WriteEndElement();
                            }
                        }
                    }
                }
            }

            if (RunParameters.Instance.LookupImagesInBase)
            {
                AddLookupImages(writer, Path.Combine(RunParameters.ImagePath));
            }
            else
            {
                AddLookupImages(writer, Path.Combine(RunParameters.ImagePath, "Movies"));
                AddLookupImages(writer, Path.Combine(RunParameters.ImagePath, "TV Series"));
            }
        }

        private static void AddLookupImages(XmlWriter writer, string directory)
        {
            if (Directory.Exists(directory))
            {
                if (programImages == null)
                    programImages = new Collection<Guid>();

                var directoryInfo = new DirectoryInfo(directory);

                foreach (var item in directoryInfo.GetFiles())
                {
                    if (item.Extension.ToLowerInvariant() == ".jpg")
                    {
                        if (item.Name.Length > 4)
                        {
                            var str = item.Name.Substring(0, item.Name.Length - 4);

                            if (Guid.TryParse(str, out Guid guid))
                            {
                                programImages.Add(guid);

                                writer.WriteStartElement("GuideImage");
                                writer.WriteAttributeString("id", "i-" + guid);
                                writer.WriteAttributeString("uid", "!Image!" + guid);
                                writer.WriteAttributeString("imageUrl", "file://" + item.FullName);
                                writer.WriteEndElement();
                            }
                        }
                    }
                }
            }
        }

        private static void ProcessPeople(XmlWriter writer)
        {
            people = new Collection<string>();

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    foreach (EPGEntry epgEntry in station.EPGCollection)
                    {
                        if (epgEntry.Cast != null)
                        {
                            foreach (string person in epgEntry.Cast)
                                ProcessPerson(writer, people, person);
                        }

                        if (epgEntry.Directors != null)
                        {
                            foreach (string person in epgEntry.Directors)
                                ProcessPerson(writer, people, person);
                        }

                        if (epgEntry.Producers != null)
                        {
                            foreach (string person in epgEntry.Producers)
                                ProcessPerson(writer, people, person);
                        }

                        if (epgEntry.Writers != null)
                        {
                            foreach (string person in epgEntry.Writers)
                                ProcessPerson(writer, people, person);
                        }

                        if (epgEntry.Presenters != null)
                        {
                            foreach (string person in epgEntry.Presenters)
                                ProcessPerson(writer, people, person);
                        }

                        if (epgEntry.GuestStars != null)
                        {
                            foreach (string person in epgEntry.GuestStars)
                                ProcessPerson(writer, people, person);
                        }
                    }
                }
            }
        }

        private static void ProcessPerson(XmlWriter writer, Collection<string> people, string newPerson)
        {
            string trimPerson = newPerson.Trim();

            foreach (string existingPerson in people)
            {
                if (existingPerson == trimPerson)
                    return;
            }

            people.Add(trimPerson);

            writer.WriteStartElement("Person");
            writer.WriteAttributeString("id", "prs" + people.Count);
            writer.WriteAttributeString("name", trimPerson);
            writer.WriteAttributeString("uid", "!Person!" + trimPerson);
            writer.WriteEndElement();
        }

        private static void ProcessSeries(XmlWriter writer)
        {
            series = new Collection<string>();

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    foreach (EPGEntry epgEntry in station.EPGCollection)
                    {
                        string seriesLink = ProcessEpisode(series, epgEntry);
                        if (seriesLink != null)
                        {
                            writer.WriteStartElement("SeriesInfo");
                            writer.WriteAttributeString("id", "si" + series.Count);
                            writer.WriteAttributeString("uid", "!Series!" + seriesLink);
                            writer.WriteAttributeString("title", epgEntry.EventName);
                            writer.WriteAttributeString("shortTitle", epgEntry.EventName);

                            if (epgEntry.SeriesDescription != null)
                            {
                                writer.WriteAttributeString("description", epgEntry.SeriesDescription);
                                writer.WriteAttributeString("shortDescription", epgEntry.SeriesDescription);
                            }
                            else
                            {
                                writer.WriteAttributeString("description", epgEntry.EventName);
                                writer.WriteAttributeString("shortDescription", epgEntry.EventName);
                            }

                            if (epgEntry.SeriesStartDate != null)
                                writer.WriteAttributeString("startAirdate", ConvertDateTimeToString(epgEntry.SeriesStartDate.Value, false));
                            else
                                writer.WriteAttributeString("startAirdate", ConvertDateTimeToString(DateTime.MinValue, false));

                            if (epgEntry.SeriesEndDate != null)
                                writer.WriteAttributeString("endAirdate", ConvertDateTimeToString(epgEntry.SeriesEndDate.Value, false));
                            else
                                writer.WriteAttributeString("endAirdate", ConvertDateTimeToString(DateTime.MinValue, false));

                            SetGuideImage(writer, epgEntry);

                            writer.WriteEndElement();
                        }
                    }
                }
            }
        }

        private static string ProcessEpisode(ICollection<string> items, EPGEntry entry)
        {
            string result = GetSeriesLink(entry);
            if (result != null)
            {
                foreach (var item in items)
                {
                    if (item == result)
                        return null;
                }

                items.Add(result);
            }

            return result;
        }

        private static void ProcessPrograms(XmlWriter writer)
        {
            if (OptionEntry.IsDefined(OptionName.UseWmcRepeatCheck) || OptionEntry.IsDefined(OptionName.UseWmcRepeatCheckBroadcast))
            {
                programIdentifiers = new Collection<string>();
                programIdentifierTitles = new Collection<string>();
            }

            int programNumber = 1;

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    foreach (EPGEntry epgEntry in station.EPGCollection)
                    {
                        if (ProcessProgram(writer, programNumber, epgEntry))
                            programNumber++;
                    }
                }
            }
        }

        private static bool ProcessProgram(XmlWriter writer, int programNumber, EPGEntry entry)
        {
            string uniqueID = GetProgramIdentifier(entry);
            if (uniqueID == null)
                return (false);

            isSpecial = false;
            isMovie = false;
            isSports = false;
            isKids = false;
            isNews = false;

            writer.WriteStartElement("Program");
            writer.WriteAttributeString("id", "prg" + programNumber);
            writer.WriteAttributeString("uid", "!Program!" + uniqueID);

            if (entry.EventName != null)
                writer.WriteAttributeString("title", entry.EventName);
            else
                writer.WriteAttributeString("title", "No Title");

            if (entry.ShortDescription != null)
                writer.WriteAttributeString("description", entry.ShortDescription);
            else
            {
                if (entry.EventName != null)
                    writer.WriteAttributeString("description", entry.EventName);
                else
                    writer.WriteAttributeString("description", "No Description");
            }

            if (entry.EventSubTitle != null)
                writer.WriteAttributeString("episodeTitle", entry.EventSubTitle);

            if (entry.HasAdult)
                writer.WriteAttributeString("hasAdult", "1");
            if (entry.HasGraphicLanguage)
                writer.WriteAttributeString("hasGraphicLanguage", "1");
            if (entry.HasGraphicViolence)
                writer.WriteAttributeString("hasGraphicViolence", "1");
            if (entry.HasNudity)
                writer.WriteAttributeString("hasNudity", "1");
            if (entry.HasStrongSexualContent)
                writer.WriteAttributeString("hasStrongSexualContent", "1");

            if (entry.MpaaParentalRating != null)
            {
                switch (entry.MpaaParentalRating)
                {
                    case "G":
                        writer.WriteAttributeString("mpaaRating", "1");
                        break;
                    case "PG":
                        writer.WriteAttributeString("mpaaRating", "2");
                        break;
                    case "PG13":
                        writer.WriteAttributeString("mpaaRating", "3");
                        break;
                    case "R":
                        writer.WriteAttributeString("mpaaRating", "4");
                        break;
                    case "NC17":
                        writer.WriteAttributeString("mpaaRating", "5");
                        break;
                    case "X":
                        writer.WriteAttributeString("mpaaRating", "6");
                        break;
                    case "NR":
                        writer.WriteAttributeString("mpaaRating", "7");
                        break;
                    case "AO":
                        writer.WriteAttributeString("mpaaRating", "8");
                        break;
                    default:
                        break;
                }
            }
            if (entry.EventCategory != null)
            {
                ProcessCategoryKeywords(writer, entry.EventCategory.GetDescription(EventCategoryMode.Wmc));
            }

            if (entry.Date != null)
                writer.WriteAttributeString("year", entry.Date);

            if (entry.SeasonNumber != -1)
                writer.WriteAttributeString("seasonNumber", entry.SeasonNumber.ToString());
            if (entry.EpisodeNumber != -1)
                writer.WriteAttributeString("episodeNumber", entry.EpisodeNumber.ToString());

            if (OptionEntry.IsDefined(OptionName.UseWmcRepeatCheck) == false && OptionEntry.IsDefined(OptionName.UseWmcRepeatCheckBroadcast) == false)
                writer.WriteAttributeString("originalAirdate", ConvertDateTimeToString(entry.PreviousPlayDate, false));
            else
            {
                if (entry.PreviousPlayDate == DateTime.MinValue)
                    writer.WriteAttributeString("originalAirdate", ConvertDateTimeToString(entry.StartTime, false));
                else
                    writer.WriteAttributeString("originalAirdate", ConvertDateTimeToString(entry.PreviousPlayDate, false));
            }

            ProcessSeries(writer, entry);

            if (entry.StarRating != null)
            {
                switch (entry.StarRating)
                {
                    case "+":
                        writer.WriteAttributeString("halfStars", "1");
                        break;
                    case "*":
                        writer.WriteAttributeString("halfStars", "2");
                        break;
                    case "*+":
                        writer.WriteAttributeString("halfStars", "3");
                        break;
                    case "**":
                        writer.WriteAttributeString("halfStars", "4");
                        break;
                    case "**+":
                        writer.WriteAttributeString("halfStars", "5");
                        break;
                    case "***":
                        writer.WriteAttributeString("halfStars", "6");
                        break;
                    case "***+":
                        writer.WriteAttributeString("halfStars", "7");
                        break;
                    case "****":
                        writer.WriteAttributeString("halfStars", "8");
                        if (OptionEntry.IsDefined(OptionName.WmcStarSpecial))
                            isSpecial = true;
                        break;
                    default:
                        break;
                }
            }

            ProcessCategoryAttributes(writer);
            SetGuideImage(writer, entry);

            if (entry.Cast != null && entry.Cast.Count != 0)
                ProcessCast(writer, entry.Cast);

            if (entry.Directors != null && entry.Directors.Count != 0)
                ProcessDirectors(writer, entry.Directors);

            if (entry.Producers != null && entry.Producers.Count != 0)
                ProcessProducers(writer, entry.Producers);

            if (entry.Writers != null && entry.Writers.Count != 0)
                ProcessWriters(writer, entry.Writers);

            if (entry.Presenters != null && entry.Presenters.Count != 0)
                ProcessPresenters(writer, entry.Presenters);

            if (entry.GuestStars != null && entry.GuestStars.Count != 0)
                ProcessGuestStars(writer, entry.GuestStars);

            writer.WriteEndElement();

            return (true);
        }

        private static string GetProgramIdentifier(EPGEntry entry)
        {
            if (OptionEntry.IsDefined(OptionName.UseWmcRepeatCheck) == false && OptionEntry.IsDefined(OptionName.UseWmcRepeatCheckBroadcast) == false)
            {
                return (entry.OriginalNetworkID + ":"
                    + entry.TransportStreamID + ":"
                    + entry.ServiceID
                    + GetUtcTime(entry.StartTime).ToString()).Replace(" ", "").Replace(":", "").Replace("/", "").Replace(".", "");
            }

            string crcString;
            string mode;

            if (OptionEntry.IsDefined(OptionName.UseWmcRepeatCheck))
            {
                crcString = GetWmcProgramIdentifier(entry);
                mode = "basic";
            }
            else
            {
                crcString = GetWmcProgramIdentifierBroadcast(entry);
                mode = "crids";
            }

            entry.UniqueIdentifier = Crc.CalculateCRC(crcString).ToString();

            if (programIdentifiers.Contains(entry.UniqueIdentifier))
            {
                string storedTitle = programIdentifierTitles[programIdentifiers.IndexOf(entry.UniqueIdentifier)];
                if (storedTitle != entry.EventName)
                {
                    Logger.Instance.Write("<e> Duplicate UID generated for '" + storedTitle + "' and '" +
                        entry.EventName + "'");
                }
                return (null);
            }

            if (DebugEntry.IsDefined(DebugName.LogPuids))
                Logger.Instance.Write("Program ID: mode: " + mode +
                    " uid: " + entry.UniqueIdentifier +
                    " crc string: " + crcString +
                    " title: " + (string.IsNullOrWhiteSpace(entry.EventName) ? "No title" : entry.EventName));

            programIdentifiers.Add(entry.UniqueIdentifier);
            programIdentifierTitles.Add(entry.EventName);

            return (entry.UniqueIdentifier);
        }

        private static string GetWmcProgramIdentifier(EPGEntry entry)
        {
            if (entry.SeasonNumber != -1 && entry.EpisodeNumber != -1)
                return (entry.EventName + " Season " + entry.SeasonNumber + " Episode " + entry.EpisodeNumber);
            else
                return (entry.EventName + " + " + entry.ShortDescription);
        }

        private static string GetWmcProgramIdentifierBroadcast(EPGEntry entry)
        {
            string crcString;

            if (!string.IsNullOrEmpty(entry.SeasonCrid) || !string.IsNullOrEmpty(entry.EpisodeCrid))
            {
                string seasonCrid = !string.IsNullOrEmpty(entry.SeasonCrid) ? entry.SeasonCrid : "n/a";
                string episodeCrid = !string.IsNullOrEmpty(entry.EpisodeCrid) ? entry.EpisodeCrid : "n/a";

                crcString = "Season CRID " + seasonCrid + " Episode CRID " + episodeCrid;
            }
            else
            {
                if (!string.IsNullOrEmpty(entry.SeriesId) || !string.IsNullOrEmpty(entry.EpisodeId))
                {
                    string seriesId = !string.IsNullOrEmpty(entry.SeriesId) ? entry.SeriesId : "n/a";
                    string episodeId = !string.IsNullOrEmpty(entry.EpisodeId) ? entry.EpisodeId : "n/a";

                    crcString = "Series ID " + seriesId + " Episode ID " + episodeId + " " +
                        (!string.IsNullOrEmpty(entry.ShortDescription) ? entry.ShortDescription : "No Description");
                }
                else
                {
                    if (entry.SeasonNumber != -1 || entry.EpisodeNumber != -1)
                        crcString = entry.EventName + " Season No. " + entry.SeasonNumber + " Episode No. " + entry.EpisodeNumber;
                    else
                        crcString = (!string.IsNullOrEmpty(entry.EventName) ? entry.EventName : "No Name") + " + " +
                            (!string.IsNullOrEmpty(entry.ShortDescription) ? entry.ShortDescription : "No Description") +
                            GetUtcTime(entry.StartTime).Date.ToString().Replace(" ", "").Replace(":", "").Replace("/", "").Replace(".", "");
                }
            }

            return (crcString);
        }

        private static void ProcessCategoryKeywords(XmlWriter writer, string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                writer.WriteAttributeString("keywords", "");
                return;
            }

            string[] parts = ProcessSpecialCategories(writer, category);
            if (parts == null)
            {
                writer.WriteAttributeString("keywords", "");
                return;
            }

            /*if (parts.Length < 2)
                return;*/

            var keywordString = new StringBuilder();

            int groupNumber = 1;

            foreach (KeywordGroup group in groups)
            {
                if (group.Name == parts[0])
                {
                    keywordString.Append("k" + groupNumber);

                    int keywordNumber = groupNumber * 100;

                    if (parts.Length < 2)
                        keywordString.Append(",k" + (keywordNumber + 1));
                    else
                    {
                        for (int keywordIndex = 1; keywordIndex < group.Keywords.Count; keywordIndex++)
                        {
                            keywordNumber++;

                            for (int partsIndex = 1; partsIndex < parts.Length; partsIndex++)
                            {
                                if (group.Keywords[keywordIndex] == parts[partsIndex])
                                    keywordString.Append(",k" + keywordNumber);
                            }
                        }
                    }

                    writer.WriteAttributeString("keywords", keywordString.ToString());
                    return;
                }
                groupNumber++;
            }

            writer.WriteAttributeString("keywords", "");
        }

        private static string[] ProcessSpecialCategories(XmlWriter xmlWriter, string category)
        {
            var specialCategories = new Collection<string>();

            string[] parts = category.Split(new string[] { "," }, StringSplitOptions.None);

            foreach (string part in parts)
            {
                string specialCategory = GetSpecialCategory(part);
                if (specialCategory != null)
                    specialCategories.Add(specialCategory);
            }

            if (specialCategories.Count == parts.Length)
                return (null);

            string[] editedParts = new string[parts.Length - specialCategories.Count];
            int index = 0;

            foreach (string part in parts)
            {
                string specialCategory = GetSpecialCategory(part);
                if (specialCategory == null)
                {
                    editedParts[index] = part;
                    index++;
                }

            }

            return (editedParts);
        }

        private static string GetSpecialCategory(string category)
        {
            switch (category.ToUpperInvariant())
            {
                case "ISMOVIE":
                    isMovie = true;
                    return ("isMovie");
                case "ISSPECIAL":
                    isSpecial = true;
                    return ("isSpecial");
                case "ISSPORTS":
                    isSports = true;
                    return ("isSports");
                case "ISNEWS":
                    isNews = true;
                    return ("isNews");
                case "ISKIDS":
                    isKids = true;
                    return ("isKids");
                default:
                    return (null);
            }
        }

        private static void AddSpecialCategory(ICollection<string> items, string newCategory)
        {
            foreach (var item in items)
            {
                if (item == newCategory)
                    return;
            }

            items.Add(newCategory);
        }

        private static void ProcessCast(XmlWriter writer, IEnumerable<string> items)
        {
            if (people == null)
                return;

            int rank = 1;

            foreach (var item in items)
            {
                writer.WriteStartElement("ActorRole");
                writer.WriteAttributeString("person", "prs" + (people.IndexOf(item.Trim()) + 1));
                writer.WriteAttributeString("rank", rank.ToString());
                writer.WriteEndElement();

                rank++;
            }
        }

        private static void ProcessDirectors(XmlWriter writer, IEnumerable<string> items)
        {
            if (people == null)
                return;

            int rank = 1;

            foreach (var item in items)
            {
                writer.WriteStartElement("DirectorRole");
                writer.WriteAttributeString("person", "prs" + (people.IndexOf(item.Trim()) + 1));
                writer.WriteAttributeString("rank", rank.ToString());
                writer.WriteEndElement();

                rank++;
            }
        }

        private static void ProcessProducers(XmlWriter writer, IEnumerable<string> items)
        {
            if (people == null)
                return;

            int rank = 1;

            foreach (var item in items)
            {
                writer.WriteStartElement("ProducerRole");
                writer.WriteAttributeString("person", "prs" + (people.IndexOf(item.Trim()) + 1));
                writer.WriteAttributeString("rank", rank.ToString());
                writer.WriteEndElement();

                rank++;
            }
        }

        private static void ProcessWriters(XmlWriter writer, IEnumerable<string> items)
        {
            if (people == null)
                return;

            int rank = 1;

            foreach (var item in items)
            {
                writer.WriteStartElement("WriterRole");
                writer.WriteAttributeString("person", "prs" + (people.IndexOf(item.Trim()) + 1));
                writer.WriteAttributeString("rank", rank.ToString());
                writer.WriteEndElement();

                rank++;
            }
        }

        private static void ProcessPresenters(XmlWriter writer, IEnumerable<string> items)
        {
            if (people == null)
                return;

            int rank = 1;

            foreach (var item in items)
            {
                writer.WriteStartElement("HostRole");
                writer.WriteAttributeString("person", "prs" + (people.IndexOf(item.Trim()) + 1));
                writer.WriteAttributeString("rank", rank.ToString());
                writer.WriteEndElement();

                rank++;
            }
        }

        private static void ProcessGuestStars(XmlWriter writer, IEnumerable<string> items)
        {
            if (people == null)
                return;

            int rank = 1;

            foreach (var item in items)
            {
                writer.WriteStartElement("GuestActorRole");
                writer.WriteAttributeString("person", "prs" + (people.IndexOf(item.Trim()) + 1));
                writer.WriteAttributeString("rank", rank.ToString());
                writer.WriteEndElement();

                rank++;
            }
        }

        private static void ProcessSeries(XmlWriter writer, EPGEntry entry)
        {
            var seriesLink = GetSeriesLink(entry);
            if (seriesLink == null)
                return;

            foreach (var oldSeriesLink in series)
            {
                if (oldSeriesLink == seriesLink)
                {
                    writer.WriteAttributeString("isSeries", "1");
                    writer.WriteAttributeString("series", "si" + (series.IndexOf(oldSeriesLink) + 1).ToString());

                    return;
                }
            }

            writer.WriteAttributeString("isSeries", "0");
        }

        private static void ProcessCategoryAttributes(XmlWriter writer)
        {
            if (isSpecial)
                writer.WriteAttributeString("isSpecial", "1");
            else
                writer.WriteAttributeString("isSpecial", "0");

            if (isMovie)
                writer.WriteAttributeString("isMovie", "1");
            else
                writer.WriteAttributeString("isMovie", "0");

            if (isSports)
                writer.WriteAttributeString("isSports", "1");
            else
                writer.WriteAttributeString("isSports", "0");

            if (isNews)
                writer.WriteAttributeString("isNews", "1");
            else
                writer.WriteAttributeString("isNews", "0");

            if (isKids)
                writer.WriteAttributeString("isKids", "1");
            else
                writer.WriteAttributeString("isKids", "0");
        }

        private static void SetGuideImage(XmlWriter writer, EPGEntry entry)
        {
            if (entry.Poster == null || programImages == null)
                return;

            foreach (Guid guid in programImages)
            {
                if (guid == entry.Poster)
                {
                    writer.WriteAttributeString("guideImage", "i-" + guid);
                    break;
                }
            }
        }

        private static void ProcessAffiliates(XmlWriter writer, string importName, string importReference)
        {
            writer.WriteStartElement("Affiliate");
            writer.WriteAttributeString("name", importName);
            writer.WriteAttributeString("uid", "!Affiliate!" + importReference);
            writer.WriteEndElement();

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included && duplicateStationNames.Contains(station.Name))
                {
                    writer.WriteStartElement("Affiliate");
                    writer.WriteAttributeString("name", importName + "-" + station.ServiceID);
                    writer.WriteAttributeString("uid", "!Affiliate!" + importReference + "-" + station.ServiceID);
                    writer.WriteEndElement();
                }
            }
        }

        private static void ProcessServices(XmlWriter writer, string importReference)
        {
            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    writer.WriteStartElement("Service");
                    writer.WriteAttributeString("id", "s" + (RunParameters.Instance.StationCollection.IndexOf(station) + 1));
                    writer.WriteAttributeString("uid", "!Service!" +
                        station.OriginalNetworkID + ":" +
                        station.TransportStreamID + ":" +
                        station.ServiceID);
                    writer.WriteAttributeString("name", string.IsNullOrWhiteSpace(station.NewName) ? station.Name : station.NewName);
                    writer.WriteAttributeString("callSign", string.IsNullOrWhiteSpace(station.NewName) ? station.Name : station.NewName);

                    if (!duplicateStationNames.Contains(station.Name))
                        writer.WriteAttributeString("affiliate", "!Affiliate!" + importReference);
                    else
                        writer.WriteAttributeString("affiliate", "!Affiliate!" + importReference + "-" + station.ServiceID);

                    if (stationImages != null)
                    {
                        int imageIndex = 1;

                        foreach (int imageServiceID in stationImages)
                        {
                            if (imageServiceID == station.ServiceID)
                            {
                                writer.WriteAttributeString("logoImage", "i" + imageIndex.ToString());
                                break;
                            }

                            imageIndex++;
                        }
                    }

                    writer.WriteEndElement();
                }
            }
        }

        private static void ProcessSchedules(XmlWriter writer)
        {
            int programNumber = 1;

            AdjustOldStartTimes();

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    writer.WriteStartElement("ScheduleEntries");
                    writer.WriteAttributeString("service", "s" + (RunParameters.Instance.StationCollection.IndexOf(station) + 1));

                    foreach (EPGEntry epgEntry in station.EPGCollection)
                    {
                        writer.WriteStartElement("ScheduleEntry");

                        if (!OptionEntry.IsDefined(OptionName.UseWmcRepeatCheck) && !OptionEntry.IsDefined(OptionName.UseWmcRepeatCheckBroadcast))
                            writer.WriteAttributeString("program", "prg" + programNumber);
                        else
                            writer.WriteAttributeString("program", "prg" + (programIdentifiers.IndexOf(epgEntry.UniqueIdentifier) + 1));

                        writer.WriteAttributeString("startTime", ConvertDateTimeToString(epgEntry.StartTime, true));
                        writer.WriteAttributeString("duration", epgEntry.Duration.TotalSeconds.ToString());

                        if (epgEntry.VideoQuality != null && epgEntry.VideoQuality.ToLowerInvariant() == "hdtv")
                            writer.WriteAttributeString("isHdtv", "true");

                        if (epgEntry.AudioQuality != null)
                        {
                            switch (epgEntry.AudioQuality.ToLowerInvariant())
                            {
                                case "mono":
                                    writer.WriteAttributeString("audioFormat", "1");
                                    break;
                                case "stereo":
                                    writer.WriteAttributeString("audioFormat", "2");
                                    break;
                                case "dolby":
                                case "surround":
                                    writer.WriteAttributeString("audioFormat", "3");
                                    break;
                                case "dolby digital":
                                    writer.WriteAttributeString("audioFormat", "4");
                                    break;
                                default:
                                    break;
                            }
                        }

                        writer.WriteEndElement();

                        programNumber++;
                    }

                    writer.WriteEndElement();
                }
            }
        }

        private static void ProcessLineUps(XmlWriter writer, string importName)
        {
            writer.WriteStartElement("Lineup");
            writer.WriteAttributeString("id", "l1");
            writer.WriteAttributeString("uid", "!Lineup!" + importName);
            writer.WriteAttributeString("name", importName);
            writer.WriteAttributeString("primaryProvider", "!MCLineup!MainLineup");

            writer.WriteStartElement("channels");

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included)
                {
                    writer.WriteStartElement("Channel");
                    if (!DebugEntry.IsDefined(DebugName.WmcNewChannels) && station.WMCUniqueID != null)
                        writer.WriteAttributeString("uid", station.WMCUniqueID);
                    else
                        writer.WriteAttributeString("uid", "!Channel!EPGCollector!" + station.OriginalNetworkID + ":" +
                            station.TransportStreamID + ":" +
                            station.ServiceID);
                    writer.WriteAttributeString("lineup", "l1");
                    writer.WriteAttributeString("service", "s" + (RunParameters.Instance.StationCollection.IndexOf(station) + 1));

                    if (OptionEntry.IsDefined(OptionName.AutoMapEpg))
                    {
                        if (station.WMCMatchName != null)
                            writer.WriteAttributeString("matchName", station.WMCMatchName);
                    }

                    if (station.LogicalChannelNumber != -1)
                        writer.WriteAttributeString("number", station.LogicalChannelNumber.ToString());

                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static string RunImportUtility(string fileName)
        {
            string result = default;

            string runDirectory = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot"), "ehome");
            Logger.Instance.Write("Running Windows Media Centre import utility LoadMXF from " + runDirectory);

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = Path.Combine(runDirectory, "LoadMXF.exe");
                    process.StartInfo.WorkingDirectory = runDirectory + Path.DirectorySeparatorChar;
                    process.StartInfo.Arguments = @"-v -i " + '"' + fileName + '"';
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.ErrorDialog = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.OutputDataReceived += (sender, eventArgs) =>
                    {
                        if (eventArgs.Data != null)
                        {
                            Logger.Instance.Write("LoadMXF message: " + eventArgs.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, eventArgs) =>
                    {
                        if (eventArgs.Data != null)
                        {
                            Logger.Instance.Write("<e> LoadMXF error: " + eventArgs.Data);
                        }
                    };

                    if (process.Start() == false)
                    {
                        Logger.Instance.Write($"Error starting Windows Media Centre import utility");
                    }
                    else
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        process.WaitForExit();
                    }

                    Logger.Instance.Write("Windows Media Centre import utility LoadMXF has completed: exit code " + process.ExitCode);
                    if (process.ExitCode != 0)
                    {
                        result = "Failed to load Windows Media Centre data: reply code " + process.ExitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Write("<e> Failed to run the Windows Media Centre import utility LoadMXF");
                Logger.Instance.Write("<e> " + ex.Message);
                result = "Failed to load Windows Media Centre data due to an exception";
            }

            return result;
        }

        private static string ConvertDateTimeToString(DateTime value, bool convertToUtc)
        {
            DateTime temp = (convertToUtc) ? GetUtcTime(value) : value;

            return (temp.Date.ToString("yyyy-MM-dd") + "T" +
                temp.Hour.ToString("00") + ":" +
                temp.Minute.ToString("00") + ":" +
                temp.Second.ToString("00"));
        }

        private static DateTime GetUtcTime(DateTime dateTime)
        {
            try
            {
                return (TimeZoneInfo.ConvertTimeToUtc(dateTime));
            }
            catch (ArgumentException e)
            {
                Logger.Instance.Write("<e> Local start date/time is invalid: " + dateTime);
                Logger.Instance.Write("<e> " + e.Message);
                Logger.Instance.Write("<e> Start time will be advanced by 1 hour");

                return (TimeZoneInfo.ConvertTimeToUtc(dateTime.AddHours(1)));
            }
        }

        private static void AdjustOldStartTimes()
        {
            if (!DebugEntry.IsDefined(DebugName.AdjustStartTimes))
                return;

            foreach (TVStation station in RunParameters.Instance.StationCollection)
            {
                if (station.Included && station.EPGCollection.Count > 0)
                {
                    TimeSpan offset = DateTime.Now - station.EPGCollection[0].StartTime;

                    foreach (EPGEntry epgEntry in station.EPGCollection)
                        epgEntry.StartTime = epgEntry.StartTime + offset;
                }
            }
        }

        private static string GetSeriesLink(EPGEntry entry)
        {
            if (!OptionEntry.IsDefined(OptionName.UseWmcRepeatCheckBroadcast))
                return (entry.EventName);

            string result = null;

            if (!string.IsNullOrEmpty(entry.SeasonCrid))
                result = GetNumber(entry.SeasonCrid);

            if (string.IsNullOrEmpty(result))
            {
                if (!string.IsNullOrEmpty(entry.SeriesId))
                    result = GetNumber(entry.SeriesId);
            }

            if (string.IsNullOrEmpty(result))
                result = entry.EventName;

            return result;
        }

        private static string GetNumber(string text)
        {
            if (text.Trim().Length == 0)
                return string.Empty;

            var builder = new StringBuilder();

            foreach (char cridChar in text)
            {
                if (cridChar >= '0' && cridChar <= '9')
                    builder.Append(cridChar);
            }

            if (builder.Length != 0)
                return builder.ToString();
            else
                return string.Empty;
        }

        private static string GetAssemblyVersionString(string fileName)
        {
            var result = string.Empty;

            var path = Path.Combine(Environment.GetEnvironmentVariable("windir"), "ehome", fileName);

            try
            {
                var version = AssemblyName.GetAssemblyName(path).Version;

                result = version.ToString();
            }
            catch (IOException ex)
            {
                Logger.Instance.Write("Failed to get assembly version for " + fileName);
                Logger.Instance.Write(ex.Message);
            }

            return result;
        }

        private static string GetAssemblyPublicKey(string fileName)
        {
            string path = Path.Combine(Environment.GetEnvironmentVariable("windir"), "ehome", fileName);

            try
            {
                AssemblyName assemblyName = AssemblyName.GetAssemblyName(path);

                byte[] publicKey = assemblyName.GetPublicKey();

                var builder = new StringBuilder();
                foreach (byte keyByte in publicKey)
                {
                    builder.Append(keyByte.ToString("x2"));
                }
                return builder.ToString();
            }
            catch (IOException ex)
            {
                Logger.Instance.Write("Failed to get assembly public key for " + fileName);
                Logger.Instance.Write(ex.Message);
                return string.Empty;
            }
        }

        internal class KeywordGroup
        {
            internal string Name { get; }
            internal Collection<string> Keywords { get; }

            internal KeywordGroup(string name)
            {
                Name = name;
                Keywords = new Collection<string>();
            }
        }
    }
}
