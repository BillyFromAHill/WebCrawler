using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Domain
{
    class DomainCrawlerState
    {
        public IEnumerable<Uri> CrawledUri { get; set; }

        public IEnumerable<CrawlQueueItem> CrawlQueue { get; set; }
    }
}
