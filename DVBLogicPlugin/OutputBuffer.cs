﻿////////////////////////////////////////////////////////////////////////////////// 
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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using DomainObjects;

namespace DVBLogicPlugin
{
    /// <summary>
    /// The class that describes the xml DVBLogic output buffer.
    /// </summary>
    public sealed class OutputBuffer
    {
        private OutputBuffer() { }

        /// <summary>
        /// Create the file.
        /// </summary>
        /// <returns></returns>
        public static byte[] Process()
        {
            Logger.Instance.Write("Creating output buffer");

            MemoryStream memoryStream = new MemoryStream();

            XmlWriterSettings settings = new XmlWriterSettings();

            settings.Indent = true;
            settings.NewLineOnAttributes = false;
            settings.Encoding = new UTF8Encoding(false);
            settings.CloseOutput = false;

            using (XmlWriter xmlWriter = XmlWriter.Create(memoryStream, settings))
            {
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("EPGInfo");

                foreach (TVStation tvStation in RunParameters.Instance.StationCollection)
                {
                    if (tvStation.Included && tvStation.EPGCollection.Count != 0)
                    {
                        xmlWriter.WriteStartElement("Channel");

                        processStationHeader(xmlWriter, tvStation);
                        processStationEPG(xmlWriter, tvStation);

                        xmlWriter.WriteEndElement();
                    }
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();

                xmlWriter.Flush();
                xmlWriter.Close();
            }

            memoryStream.Position = memoryStream.Length;
            memoryStream.WriteByte(0x00);

            return (memoryStream.ToArray());
        }

        private static void processStationHeader(XmlWriter xmlWriter, TVStation tvStation)
        {
            xmlWriter.WriteAttributeString("Name", tvStation.Name.Trim());

            xmlWriter.WriteAttributeString("ID", tvStation.OriginalNetworkID.ToString() + ":" +
                tvStation.TransportStreamID.ToString() + ":" +
                tvStation.ServiceID.ToString());
            xmlWriter.WriteAttributeString("NID", tvStation.OriginalNetworkID.ToString());
            xmlWriter.WriteAttributeString("TID", tvStation.TransportStreamID.ToString());
            xmlWriter.WriteAttributeString("SID", tvStation.ServiceID.ToString());
            xmlWriter.WriteAttributeString("Count", tvStation.EPGCollection.Count.ToString());
            xmlWriter.WriteAttributeString("FirstStart", getStartTime(tvStation.EPGCollection[0]));
            xmlWriter.WriteAttributeString("LastStart", getStartTime(tvStation.EPGCollection[tvStation.EPGCollection.Count - 1]));
        }

        private static void processStationEPG(XmlWriter xmlWriter, TVStation tvStation)
        {
            xmlWriter.WriteStartElement("dvblink_epg");

            for (int index = 0; index < tvStation.EPGCollection.Count; index++)
            {
                EPGEntry epgEntry = tvStation.EPGCollection[index];

                if (TuningFrequency.HasUsedMHEG5Frequency(RunParameters.Instance.FrequencyCollection))
                    checkMidnightBreak(tvStation, epgEntry, index);                

                processEPGEntry(xmlWriter, epgEntry);
            }

            xmlWriter.WriteEndElement();
        }

        private static void checkMidnightBreak(TVStation tvStation, EPGEntry currentEntry, int index)
        {
            if (index == tvStation.EPGCollection.Count - 1)
                return;

            EPGEntry nextEntry = tvStation.EPGCollection[index + 1];

            if (currentEntry.EventName != nextEntry.EventName)
                return;

            bool combined = false;
            if (RunParameters.Instance.FrequencyCollection[0].AdvancedRunParamters.CountryCode == null)
                combined = checkNZLTimes(currentEntry, nextEntry);
            else
            {
                switch (RunParameters.Instance.FrequencyCollection[0].AdvancedRunParamters.CountryCode)
                {
                    case Country.NewZealand:
                        combined = checkNZLTimes(currentEntry, nextEntry);
                        break;
                    case Country.Australia:
                        combined = checkAUSTimes(currentEntry, nextEntry);
                        break;
                    default:
                        break;
                }
            }

            if (combined)
                tvStation.EPGCollection.RemoveAt(index + 1);
        }

        private static bool checkNZLTimes(EPGEntry currentEntry, EPGEntry nextEntry)
        {
            if (!currentEntry.EndsAtMidnight)
                return (false);

            if (!nextEntry.StartsAtMidnight)
                return (false);

            if (currentEntry.StartTime + currentEntry.Duration != nextEntry.StartTime)
                return (false);

            if (nextEntry.Duration > new TimeSpan(3, 0, 0))
                return (false);

            Logger.Instance.Write("Combining " + currentEntry.ScheduleDescription + " with " + nextEntry.ScheduleDescription);
            currentEntry.Duration = currentEntry.Duration + nextEntry.Duration;

            return (true);
        }

        private static bool checkAUSTimes(EPGEntry currentEntry, EPGEntry nextEntry)
        {
            if (!nextEntry.StartsAtMidnight)
                return (false);

            if (currentEntry.StartTime + currentEntry.Duration != nextEntry.StartTime + nextEntry.Duration)
                return (false);

            Logger.Instance.Write("Combining " + currentEntry.ScheduleDescription + " with " + nextEntry.ScheduleDescription);

            return (true);
        }

        private static void processEPGEntry(XmlWriter xmlWriter, EPGEntry epgEntry)
        {
            Regex whitespace = new Regex(@"\s+");

            xmlWriter.WriteStartElement("program");

            if (epgEntry.EventName != null)
                xmlWriter.WriteAttributeString("name", whitespace.Replace(epgEntry.EventName.Trim(), " "));
            else
                xmlWriter.WriteAttributeString("name", "No Title");

            /*TimeSpan timeSpan = epgEntry.StartTime - new DateTime(1970, 1, 1, 0, 0, 0, 0); 
            UInt32 seconds = Convert.ToUInt32(Math.Abs(timeSpan.TotalSeconds));*/ 
            xmlWriter.WriteElementString("start_time", getStartTime(epgEntry));
            xmlWriter.WriteElementString("duration", epgEntry.Duration.TotalSeconds.ToString());            

            if (epgEntry.EventSubTitle != null)
                xmlWriter.WriteElementString("subname", whitespace.Replace(epgEntry.EventSubTitle.Trim(), " "));

            if (epgEntry.ShortDescription != null)
                xmlWriter.WriteElementString("short_desc", whitespace.Replace(epgEntry.ShortDescription.Trim(), " "));
            else
            {
                if (epgEntry.EventName != null)
                    xmlWriter.WriteElementString("short_desc", whitespace.Replace(epgEntry.EventName.Trim(), " "));
                else
                    xmlWriter.WriteElementString("short_desc", "No Description");
            }

            if (epgEntry.EventCategory != null)
                xmlWriter.WriteElementString("categories", epgEntry.EventCategory.GetDescription(EventCategoryMode.DvbLogic));

           /* if (epgEntry.ParentalRating != null)
            {
                xmlWriter.WriteStartElement("rating");
                if (epgEntry.ParentalRatingSystem != null)
                    xmlWriter.WriteAttributeString("system", epgEntry.ParentalRatingSystem);
                xmlWriter.WriteElementString("value", epgEntry.ParentalRating);
                xmlWriter.WriteEndElement();
            }*/

            if (epgEntry.VideoQuality != null && epgEntry.VideoQuality.ToLowerInvariant() == "hdtv")
                xmlWriter.WriteElementString("hdtv", string.Empty);
            
            if (epgEntry.SeasonNumber != -1)
                xmlWriter.WriteElementString("season_num", epgEntry.SeasonNumber.ToString());
            if (epgEntry.EpisodeNumber != -1)
                xmlWriter.WriteElementString("episode_num", epgEntry.EpisodeNumber.ToString());

            if (epgEntry.Directors != null)
            {
                StringBuilder directorString = new StringBuilder();

                foreach (string director in epgEntry.Directors)
                {
                    if (directorString.Length != 0)
                        directorString.Append("/");
                    directorString.Append(director.Trim());
                }

                xmlWriter.WriteElementString("directors", directorString.ToString());
            }

            if (epgEntry.Cast != null)
            {
                StringBuilder castString = new StringBuilder();

                foreach (string castMember in epgEntry.Cast)
                {
                    if (castString.Length != 0)
                        castString.Append("/");
                    castString.Append(castMember.Trim());
                }

                xmlWriter.WriteElementString("actors", castString.ToString());
            }         

            if (epgEntry.Date != null)
                xmlWriter.WriteElementString("year", epgEntry.Date);

            xmlWriter.WriteEndElement();
        }

        private static string getStartTime(EPGEntry epgEntry)
        {
            DateTime gmtStartTime = TimeZoneInfo.ConvertTimeToUtc(epgEntry.StartTime);
            TimeSpan timeSpan = gmtStartTime - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            UInt32 seconds = Convert.ToUInt32(Math.Abs(timeSpan.TotalSeconds));

            return (seconds.ToString());
        }
    }
}
