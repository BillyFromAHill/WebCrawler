﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class DomainCrawlerConfiguration
    {
        public DomainCrawlerConfiguration()
        {
            MaxThreads = 100;
        }

        public int MaxThreads { get; set; }
    }
}
