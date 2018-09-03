using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Domain
{
    class CrawlQueueItem
    {
        public PageCrawler Crawler { get; set; }

        public int PageLevel { get; set; }
    }
}
