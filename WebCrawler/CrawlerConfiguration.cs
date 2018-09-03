using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class CrawlerConfiguration
    {
        public CrawlerConfiguration()
        {
            MaxCrawlDomains = 20;
        }

        public int MaxCrawlDomains { get; set; }
    }
}
