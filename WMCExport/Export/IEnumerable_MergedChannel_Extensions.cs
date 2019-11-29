using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.MediaCenter.Guide;
using Microsoft.MediaCenter.Store;
using Microsoft.MediaCenter.TV.Tuning;

namespace WMCUtility
{
    public static partial class IEnumerable_MergedChannel_Extensions
    {
        public static XElement AsXElement(this IEnumerable<MergedChannel> items)
        {
            XElement result = new XElement("channels");

            foreach (var item in items)
            {
                var element = Create(item);

                result.Add(element);
            }

            return result;
        }

        private static XElement Create(MergedChannel item)
        {
            var primaryChannel = item.PrimaryChannel;
            var lineup = item.Lineup;
            var elements = Create(item.TuningInfos);

            var result = new XElement("channel",
                new XAttribute("channelNumber", item.ChannelNumber),
                new XAttribute("callSign", item.CallSign),
                new XAttribute("matchName", primaryChannel.MatchName),
                new XAttribute("uid", GetUniqueID(primaryChannel.UIds)),
                new XAttribute("lineup", lineup.Name),
                elements
            );

            return result;
        }

        private static XElement Create(TuningInfos items)
        {
            return Create(items.ToList());
        }

        private static XElement Create(IList<TuningInfo> items)
        {
            XElement result = new XElement("tuningInfos");

            var distinct = GetDistinctTuningRequests(items);

            foreach (var item in distinct)
            {
                var element = Create(item);
                if (element != null)
                {
                    result.Add(element);
                }
            }

            return result;
        }

        private static IReadOnlyList<TuningInfo> GetDistinctTuningRequests(IEnumerable<TuningInfo> items)
        {
            var result = new List<TuningInfo>();

            foreach (var item1 in items)
            {
                var request1 = item1.TuneRequest;

                var duplicate = false;
                foreach (var item2 in result)
                {
                    var request2 = item2.TuneRequest;

                    if (request1.CompareEquivalent(request2, BDA_Comp_Flags.BDACOMP_INCLUDE_LOCATOR_IN_TR) == 0)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate == false)
                {
                    result.Add(item1);
                }
            }

            return result;
        }

        private static XElement Create(TuningInfo item)
        {
            XElement result = default;

            var tuneRequest = item.TuneRequest;
            switch (tuneRequest)
            {
                case DVBTuneRequest request:
                    result = Create(request);
                    break;
                case ATSCChannelTuneRequest request:
                    result = Create(request);
                    break;
                default:
                    break;
            }

            return result;
        }

        private static XElement Create(DVBTuneRequest item)
        {
            var locator = item.Locator;

            var result = new XElement("dvbTuningInfo",
                new XAttribute("frequency", locator.CarrierFrequency),
                new XAttribute("onid", item.ONID),
                new XAttribute("tsid", item.TSID),
                new XAttribute("sid", item.SID)
            );

            return result;
        }

        private static XElement Create(ATSCChannelTuneRequest item)
        {
            var locator = item.Locator;

            var result = new XElement("atscTuningInfo",
                new XAttribute("frequency", locator.CarrierFrequency),
                new XAttribute("majorChannel", item.Channel),
                new XAttribute("minorChannel", item.MinorChannel)
            );

            return result;
        }

        private static string GetUniqueID(UIds uids)
        {
            string result = default;

            if (uids != null)
            {
                var items = uids.ToList();
                if (items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        string fullName = item.GetFullName();

                        if (result is null)
                        {
                            result = fullName;
                        }

                        if (fullName.Contains("EPGCollector"))
                        {
                            result = fullName;
                            break;
                        }
                    }
                }
            }

            return result;
        }
    }
}
