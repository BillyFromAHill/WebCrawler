using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace WebCrawler
{
    public class DomainCrawler
    {
        private Uri _siteUri;

        private Queue<PageCrawler> _pagesToCrawl = new Queue<PageCrawler>();

        private HashSet<Task> _currentTasks = new HashSet<Task>();

        private HashSet<Uri> _crawledUri = new HashSet<Uri>();

        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private RobotsReader _robotsReader;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private DomainCrawlerConfiguration _configuration;

        private string _domainDirectory;

        public DomainCrawler(Uri siteUri, DomainCrawlerConfiguration configuration)
        {
            if (siteUri == null)
            {
                throw new ArgumentNullException("siteUri");
            }

            _siteUri = siteUri;

            _pagesToCrawl.Enqueue(new PageCrawler(_siteUri));
            _crawledUri.Add(_siteUri);

            _robotsReader = new RobotsReader(_siteUri);

            _configuration = configuration;
        }

        public async void CrawlDomain()
        {
            await Task.Factory.StartNew(
                async () =>
                {

                    _domainDirectory = _siteUri.Host.Replace("/", "-").Replace(":", "-");
                    Directory.CreateDirectory(_domainDirectory);

                    RobotsParams robotsParams = await _robotsReader.GetRobotsParams();

                    while (_pagesToCrawl.Count > 0 || _currentTasks.Count > 0)
                    {
                        if (_currentTasks.Count >= _configuration.MaxThreads)
                        {
                            Task.WaitAny(_currentTasks.ToArray(), 500, _cts.Token);

                            _currentTasks.RemoveWhere(t => t.IsCompleted);
                            continue;
                        }

                        if (_pagesToCrawl.Count > 0)
                        {
                            PageCrawler currentCrawler = _pagesToCrawl.Dequeue();

                            Task task = CrawlPage(currentCrawler);
                            _currentTasks.Add(task);

                            if (robotsParams.CrawlDelay > 0)
                            {
                                await Task.Delay(robotsParams.CrawlDelay);
                            }
                        }
                    }

                }, TaskCreationOptions.LongRunning);
        }

        private async Task CrawlPage(PageCrawler crawler)
        {
            try
            {
                // Раскладываем в корень страницы.
                using (FileStream pageStream = new FileStream(
                    Path.Combine(_domainDirectory, crawler.PageUri.ToString().Replace("/", "-").Replace(":", "-").Replace("?", "-") + ".html"),
                    FileMode.Create))
                {
                    CrawlPageResults results = await crawler.StartCrawling(pageStream, _cts.Token);

                    EnqeueCrawlers(results.References);

                    LoadContent(results.ContentUris);

                    UpdateStatistics(results);
                }
            }
            catch (Exception e)
            {
                UpdateStatistics(null);
                Logger.Log(
                    LogLevel.Error,
                    $"Page crawl exception with uri {crawler.PageUri}",
                    e);
            }
            finally
            {
            }
        }

        private void EnqeueCrawlers(IEnumerable<Uri> nextPages)
        {
            foreach (var page in nextPages)
            {
                if (!_crawledUri.Contains(page))
                {
                    _crawledUri.Add(page);
                    _pagesToCrawl.Enqueue(new PageCrawler(page));
                }
            }
        }

        private void LoadContent(IEnumerable<Uri> uris)
        {

        }

        private void UpdateStatistics(CrawlPageResults results)
        {

        }
    }
}
