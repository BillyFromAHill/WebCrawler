using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

        private CancellationTokenSource _internalCTS = new CancellationTokenSource();

        private DomainCrawlerConfiguration _configuration;

        private string _domainDirectory;

        private DomainCrawlStatistics _statistics;

        private int _statSavePeriodPages = 10;

        public DomainCrawler(Uri siteUri, DomainCrawlerConfiguration configuration)
        {
            if (siteUri == null)
            {
                throw new ArgumentNullException("siteUri");
            }

            _siteUri = siteUri;

            _pagesToCrawl.Enqueue(
                new CrawlQueueItem()
                {
                    Crawler = new PageCrawler(_siteUri),
                    PageLevel = 0,
                });

            _crawledUri.Add(_siteUri);

            _robotsReader = new RobotsReader(_siteUri);

            _configuration = configuration;
        }

        public async Task<DomainCrawlStatistics> CrawlDomain(CancellationToken cancellationToken)
        {
            _statistics = new DomainCrawlStatistics();

            CancellationToken resultToken =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _internalCTS.Token).Token;

            await Task.Factory.StartNew(
                async () =>
                {
                    _domainDirectory = _siteUri.Host.Replace("/", "-").Replace(":", "-");
                    Directory.CreateDirectory(_domainDirectory);

                    RobotsParams robotsParams = await _robotsReader.GetRobotsParams();

                    while ((_pagesToCrawl.Count > 0 || _currentTasks.Count > 0) &&
                           !resultToken.IsCancellationRequested)
                    {
                        _currentTasks.RemoveWhere(t => t.IsCompleted);

                        if (_currentTasks.Count >= _configuration.MaxTasks)
                        {
                            Task.WaitAny(_currentTasks.ToArray(), WaitForPageMS, resultToken);
                            continue;
                        }

                        if (_pagesToCrawl.Count > 0)
                        {
                            CrawlQueueItem currentCrawler = _pagesToCrawl.Dequeue();

                            Task task = CrawlPage(currentCrawler, resultToken);
                            _currentTasks.Add(task);

                            if (robotsParams.CrawlDelay > 0)
                            {
                                await Task.Delay(robotsParams.CrawlDelay, resultToken);
                            }
                        }
                    }

                    SaveStatistics();
                },
                TaskCreationOptions.LongRunning);

            return _statistics;
        }

        private async Task CrawlPage(CrawlQueueItem crawlItem, CancellationToken cancellationToken)
        {
            try
            {
                // Раскладываем в корень страницы.
                using (FileStream pageStream = new FileStream(
                    Path.Combine(
                        _domainDirectory,
                        crawlItem.Crawler.PageUri.ToString().Replace("/", "-").Replace(":", "-")
                            .Replace("?", "-") + ".html"),
                    FileMode.Create))
                {
                    CrawlPageResults results =
                        await crawlItem.Crawler.StartCrawling(pageStream, cancellationToken);

                    LoadContent(results.ContentUris);

                    UpdateStatistics(results);

                    // В прекрасном мире, здесь был бы стрим, который пропускал через себя страницу,
                    // которую по мере вычитывания ему давал PageCrawler и, тем самым проверялось
                    // бы и стоп условие и все шло бы без излишних проверок и прочего, но имеем то, 
                    // что имеем.
                    if (!string.IsNullOrEmpty(_configuration.StopString) &&
                        results.PageContent.Contains(_configuration.StopString))
                    {
                        Logger.Log(
                            LogLevel.Info,
                            $"Stop string found at {crawlItem.Crawler.PageUri}");

                        _internalCTS.Cancel();
                        return;
                    }

                    if (crawlItem.PageLevel < _configuration.MaxPageLevel)
                    {
                        EnqeueCrawlers(results.References, crawlItem.PageLevel);
                    }
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
                if ((_configuration.MaxPages < 0 || _crawledUri.Count < _configuration.MaxPages) &&
                    !_crawledUri.Contains(page))
                {
                    _crawledUri.Add(page);
                    _pagesToCrawl.Enqueue(
                        new CrawlQueueItem()
                        {
                            Crawler = new PageCrawler(page),
                            PageLevel = parentLevel + 1
                        });
                }
            }
        }

        private void LoadContent(IEnumerable<Uri> uris)
        {

        }

        private void UpdateStatistics(CrawlPageResults results)
        {
            _statistics.AppendPageResults(results);

            if (_crawledUri.Count % _statSavePeriodPages == 0)
            {
                SaveStatistics();
            }
        }

        private void SaveStatistics()
        {
            using (var statStream = new FileStream( Path.Combine(_domainDirectory, $"{_siteUri.Host}.stat.json"), FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(statStream))
                {
                    using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jsonWriter, _statistics);
                    }
                }
            }
        }
    }
}
