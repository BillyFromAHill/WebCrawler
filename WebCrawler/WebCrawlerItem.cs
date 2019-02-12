using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class WebCrawlerItem
    {
        public WebCrawlerItem(Uri domain, DomainCrawlerConfiguration configuration)
        {
            if (domain == null)
            {
                throw new ArgumentNullException("domain");
            }

            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Domain = domain;
        }

        public Uri Domain { get; private set; }

        public DomainCrawlerConfiguration Configuration { get; }

        public override string ToString()
        {
            return $"[{nameof(Domain)} = {Domain}, {nameof(Configuration)} = {Configuration}]";
        }
    }
}
