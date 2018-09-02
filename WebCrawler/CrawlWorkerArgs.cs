using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler
{
    class CrawlWorkerArgs
    {
        public CancellationToken CancellationToken { get; set; }

        public Stream DestStream { get; set; }

    }
}
