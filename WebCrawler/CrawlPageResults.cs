using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class CrawlPageResults
    {
        public Uri CrawledUri { get; set; }

        public long ContentLength { get; set; }

        public long LoadTimeMS { get; set; }

        public IEnumerable<Uri> References { get; set; }

        public IEnumerable<Uri> ContentUris { get; set; }
    }
}
