using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using WebCrawler.Domain;

namespace WebCrawler
{
    class DomainCrawler
    {
        private Uri _siteUri;

        private Queue<CrawlQueueItem> _pagesToCrawl = new Queue<CrawlQueueItem>();

        private HashSet<Task> _currentTasks = new HashSet<Task>();

        private HashSet<Uri> _crawledUri = new HashSet<Uri>();

        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private RobotsReader _robotsReader;

        private const int WaitForPageMS = 500;


        private DomainCrawlerConfiguration _configuration;

        private string _domainDirectory;

        public DomainCrawler(Uri siteUri, DomainCrawlerConfiguration configuration)
        {
            if (siteUri == null)
            {
                throw new ArgumentNullException("siteUri");
            }

            _siteUri = siteUri;

            _pagesToCrawl.Enqueue( new CrawlQueueItem()
            {
                Crawler = new PageCrawler(_siteUri),
                PageLevel = 0,
            });

            _crawledUri.Add(_siteUri);

            _robotsReader = new RobotsReader(_siteUri);

            _configuration = configuration;
        }

        public async Task CrawlDomain(CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    _domainDirectory = _siteUri.Host.Replace("/", "-").Replace(":", "-");
                    Directory.CreateDirectory(_domainDirectory);

                    RobotsParams robotsParams = await _robotsReader.GetRobotsParams();

                    while ((_pagesToCrawl.Count > 0 || _currentTasks.Count > 0) && !cancellationToken.IsCancellationRequested)
                    {
                        if (_currentTasks.Count >= _configuration.MaxTasks)
                        {
                            _currentTasks.RemoveWhere(t => t.IsCompleted);

                            if (_currentTasks.Count >= _configuration.MaxTasks)
                            {
                                Task.WaitAny(_currentTasks.ToArray(), WaitForPageMS, cancellationToken);
                            }

                            continue;
                        }

                        if (_pagesToCrawl.Count > 0)
                        {
                            CrawlQueueItem currentCrawler = _pagesToCrawl.Dequeue();

                            Task task = CrawlPage(currentCrawler, cancellationToken);
                            _currentTasks.Add(task);

                            if (robotsParams.CrawlDelay > 0)
                            {
                                await Task.Delay(robotsParams.CrawlDelay, cancellationToken);
                            }
                        }
                    }

                }, TaskCreationOptions.LongRunning);
        }

        private async Task CrawlPage(CrawlQueueItem crawlItem, CancellationToken cancellationToken)
        {
            try
            {
                // Раскладываем в корень страницы.
                using (FileStream pageStream = new FileStream(
                    Path.Combine(_domainDirectory, crawlItem.Crawler.PageUri.ToString().Replace("/", "-").Replace(":", "-").Replace("?", "-") + ".html"),
                    FileMode.Create))
                {
                    CrawlPageResults results = await crawlItem.Crawler.StartCrawling(pageStream, cancellationToken);

                    EnqeueCrawlers(results.References, crawlItem.PageLevel);

                    LoadContent(results.ContentUris);

                    UpdateStatistics(results);
                }
            }
            catch (Exception e)
            {
                UpdateStatistics(null);
                Logger.Log(
                    LogLevel.Error,
                    $"Page crawl exception with uri {crawlItem.Crawler.PageUri}",
                    e);
            }
            finally
            {
            }
        }

        private void EnqeueCrawlers(IEnumerable<Uri> nextPages, int parentLevel)
        {
            foreach (var page in nextPages)
            {
                if (!_crawledUri.Contains(page))
                {
                    _crawledUri.Add(page);
                    _pagesToCrawl.Enqueue(new CrawlQueueItem() { Crawler = new PageCrawler(page), PageLevel = parentLevel + 1});
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
