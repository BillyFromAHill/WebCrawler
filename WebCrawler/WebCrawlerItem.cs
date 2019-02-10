using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class WebCrawlerItem
    {
        public WebCrawlerItem(Uri domain)
        {
            if (domain == null)
            {
                throw new ArgumentNullException("domain");
            }

            Domain = domain;
        }

        public Uri Domain { get; private set; }

        public DomainCrawlerConfiguration Configuration { get; set; }

        public override string ToString()
        {
            return $"[{nameof(Domain)} = {Domain}, {nameof(Configuration)} = {Configuration}]";
        }
    }
}
