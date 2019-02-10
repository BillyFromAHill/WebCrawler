using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    class RobotsParams
    {
        public RobotsParams()
        {
            CrawlDelay = 0;
        }

        public int CrawlDelay { get; set; }

        public override string ToString()
        {
            return $"[{nameof(CrawlDelay)} = {CrawlDelay}]";
        }
    }
}
