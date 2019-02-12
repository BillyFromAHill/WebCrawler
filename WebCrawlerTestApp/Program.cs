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
            List<Uri> uris = new List<Uri>()
            {
                new Uri("https://www.rbc.ru/"),
                new Uri("https://habrahabr.ru/"),
                new Uri("https://zr.ru/"),
                new Uri("https://youtube.com/"),
                new Uri("https://rp5.ru/"),
            };

            List<WebCrawlerItem> crawlerItems = new List<WebCrawlerItem>();

            foreach (var uri in uris)
            {
                crawlerItems.Add(new WebCrawlerItem(uri, new DomainCrawlerConfiguration()));
            }

            var crawler = new WebCrawler.WebCrawler(new CrawlerConfiguration());

            crawler.StartCrawlingAsync(crawlerItems);

            Console.ReadLine();
        }
    }
}
