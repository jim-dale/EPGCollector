using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.MediaCenter.Pvr;

namespace WMCUtility
{
    public static class IEnumerable_Recording_Extensions
    {
        public static XElement AsXElement(this IEnumerable<Recording> items)
        {
            XElement result = default;

            if (items.Any())
            {
                result = new XElement("recordings");

                foreach (var item in items)
                {
                    var element = Create(item);

                    result.Add(element);
                }
            }

            return result;
        }

        private static XElement Create(Recording item)
        {
            var program = item.Program;

            var result = new XElement("recording",
                new XAttribute("title", program.Title),
                new XAttribute("description", program.ShortDescription),
                new XAttribute("startTime", item.ContentStartTime), //.ToString(CultureInfo.InvariantCulture));
                new XAttribute("seasonNumber", program.SeasonNumber),
                new XAttribute("episodeNumber", program.EpisodeNumber)
            );

            return result;
        }
    }
}
