﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Domain
{
    class CrawlQueueItem
    {
        public Uri PageUri { get; set; }

        public int PageLevel { get; set; }
    }
}
