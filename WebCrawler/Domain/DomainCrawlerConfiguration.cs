using System;
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
            MaxTasks = 100;
        }

        public int MaxTasks { get; set; }

    }
}
