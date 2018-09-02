using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebCrawler;

namespace WebCrawlerTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            DomainCrawlerConfiguration configuration = new DomainCrawlerConfiguration();



            var domainCrawler = new DomainCrawler(new Uri("https://www.rbc.ru/"), configuration);

            domainCrawler.CrawlDomain();

            Console.ReadLine();
        }
    }
}
