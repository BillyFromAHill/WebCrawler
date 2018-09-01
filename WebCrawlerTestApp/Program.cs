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
            CancellationTokenSource cts = new CancellationTokenSource();

            var pageCrawler = new PageCrawler(
                new Uri("https://ya.ru"),
                new Uri("ya/", UriKind.Relative),
                "ya.html");



            pageCrawler.StartCrawling(cts.Token).Wait();
        }
    }
}
