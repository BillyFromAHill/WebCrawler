using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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

        private HashSet<Task<Tuple<CrawlPageResults, int>>> _currentTasks = new HashSet<Task<Tuple<CrawlPageResults, int>>>();

        private HashSet<Uri> _queuedUri = new HashSet<Uri>();

        private HashSet<Uri> _crawledUri = new HashSet<Uri>();

        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private RobotsReader _robotsReader;

        private const int WaitForPageMS = 500;

        private CancellationTokenSource _internalCTS = new CancellationTokenSource();

        private DomainCrawlerConfiguration _configuration;

        private string _domainDirectory;

        private DomainCrawlStatistics _statistics;

        private int _saveProgressPeriod = 10;

        private bool _contentFound = false;

        public DomainCrawler(Uri siteUri, DomainCrawlerConfiguration configuration)
        {
            if (siteUri == null)
            {
                throw new ArgumentNullException("siteUri");
            }

            _siteUri = siteUri;

            _robotsReader = new RobotsReader(_siteUri);

            _configuration = configuration;

            _domainDirectory = _siteUri.Host.Replace("/", "-").Replace(":", "-");
        }


    public async Task<DomainCrawlStatistics> CrawlDomain(CancellationToken cancellationToken)
        {
            CancellationToken resultToken =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _internalCTS.Token).Token;

            _contentFound = false;

            await Task.Factory.StartNew(
                async () =>
                {
                    InitQueue();

                    Directory.CreateDirectory(_domainDirectory);

                    RobotsParams robotsParams = await _robotsReader.GetRobotsParams();

                        while ((_pagesToCrawl.Count > 0 && ShouldCrawlNextPages() || _currentTasks.Count > 0))
                        {
                            foreach (var task in _currentTasks.Where(t => t.IsCompleted).ToArray())
                            {
                                if (!task.IsFaulted && !task.IsCanceled)
                                {
                                    var results = task.Result;

                                    UpdateProgress(results.Item1);

                                    if (results.Item2 < _configuration.MaxPageLevel)
                                    {
                                        EnqeueCrawlers(results.Item1.References, results.Item2);
                                    }

                                    _crawledUri.Add(task.Result.Item1.CrawledUri);
                                    _queuedUri.Remove(task.Result.Item1.CrawledUri);
                                }

                                _currentTasks.Remove(task);
                            }

                            if (_currentTasks.Count >= _configuration.MaxTasks)
                            {
                                Task.WaitAny(_currentTasks.ToArray(), WaitForPageMS, resultToken);
                                continue;
                            }

                            if (_pagesToCrawl.Count > 0 &&
                                 ShouldCrawlNextPages())
                            {
                                CrawlQueueItem currentCrawler = _pagesToCrawl.Dequeue();

                                _currentTasks.Add(CrawlPage(currentCrawler, resultToken));

                                if (robotsParams.CrawlDelay > 0)
                                {
                                    await Task.Delay(robotsParams.CrawlDelay, resultToken);
                                }
                            }
                        }


                    SaveProgress();
                },
                TaskCreationOptions.LongRunning);

            return _statistics;
        }

        private bool ShouldCrawlNextPages()
        {
            CrawlQueueItem currentCrawler = null;

            if (_pagesToCrawl.Count > 0)
            {
                currentCrawler = _pagesToCrawl.Peek();
            }

            return !_contentFound &&
                    _crawledUri.Count + _currentTasks.Count < _configuration.MaxPages &&
                    (currentCrawler == null || currentCrawler.PageLevel < _configuration.MaxPageLevel);
        }

        private void InitQueue()
        {
            _statistics = new DomainCrawlStatistics();
            _queuedUri.Add(_siteUri);

            _pagesToCrawl.Enqueue(new CrawlQueueItem(){ PageLevel = 0, PageUri = _siteUri });

            if (File.Exists(GetStatisticsFileName()))
            {
                try
                {
                    using (var statStream = File.OpenRead(GetStatisticsFileName()))
                    {
                        using (var reader= new StreamReader(statStream))
                        {
                            using (var jsonReader = new JsonTextReader(reader))
                            {
                                var deserializer = new JsonSerializer();
                                _statistics = deserializer.Deserialize<DomainCrawlStatistics>(jsonReader);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(
                        LogLevel.Error,
                        e,
                        $"Statistics parse error:");
                }
            }

            if (File.Exists(GetStateFileName()))
            {
                try
                {
                    using (var stateStream = File.OpenRead(GetStateFileName()))
                    {
                        using (var reader = new StreamReader(stateStream))
                        {
                            using (var jsonReader = new JsonTextReader(reader))
                            {
                                var deserializer = new JsonSerializer();
                                var state = deserializer.Deserialize<DomainCrawlerState>(jsonReader);

                                foreach (var uri in state.CrawledUri)
                                {
                                    _crawledUri.Add(uri);
                                }

                                _pagesToCrawl.Clear();
                                foreach (var item in state.CrawlQueue)
                                {
                                    _queuedUri.Add(item.PageUri);
                                    _pagesToCrawl.Enqueue(item);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(
                        LogLevel.Error,
                        e,
                        $"State parse error:");
                }
            }
        }

        private async Task<Tuple<CrawlPageResults, int>> CrawlPage(CrawlQueueItem crawlItem, CancellationToken cancellationToken)
        {
            CrawlPageResults results = null;
            try
            {
                // Раскладываем в корень страницы.
                using (FileStream pageStream = new FileStream(
                    Path.Combine(
                        _domainDirectory,
                        crawlItem.PageUri.ToString().Replace("/", "-").Replace(":", "-")
                            .Replace("?", "-") + ".html"),
                    FileMode.Create))
                {
                    var pageCrawler = new PageCrawler(crawlItem.PageUri);

                    results =
                        await pageCrawler.StartCrawling(pageStream, cancellationToken);

                    await LoadContent(results);

                    // В прекрасном мире, здесь был бы стрим, который пропускал через себя страницу,
                    // которую по мере вычитывания ему давал PageCrawler и, тем самым проверялось
                    // бы и стоп условие и все шло бы без излишних проверок и прочего, но имеем то, 
                    // что имеем.
                    if (!string.IsNullOrEmpty(_configuration.StopString) &&
                        results.PageContent.Contains(_configuration.StopString))
                    {
                        Logger.Log(
                            LogLevel.Info,
                            $"Stop string found at {crawlItem.PageUri}");

                        _contentFound = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(
                    LogLevel.Error,
                    e,
                    $"Page crawl exception with uri {crawlItem.PageUri}");
            }
            finally
            {
            }

            return new Tuple<CrawlPageResults, int>(results, crawlItem.PageLevel);
        }

        private void EnqeueCrawlers(IEnumerable<Uri> nextPages, int parentLevel)
        {
            foreach (var page in nextPages)
            {
                if (!_queuedUri.Contains(page) && !_crawledUri.Contains(page))
                {
                    var item = new CrawlQueueItem()
                    {
                        PageUri = page,
                        PageLevel = parentLevel + 1
                    };

                    _queuedUri.Add(page);
                    _pagesToCrawl.Enqueue(item);
                }
            }
        }

        private async Task LoadContent(CrawlPageResults results)
        {
            if (string.IsNullOrEmpty(_configuration.ContentFileMask))
            {
                return;
            }

            string directoryName = Path.Combine(
                _domainDirectory,
                results.CrawledUri.ToString().Replace("/", "-").Replace(":", "-").Replace("?", "-"));

            Regex maskRegex = FileMaskToRegex(_configuration.ContentFileMask);
            foreach (var uri in results.ContentUris)
            {

                string fileName = System.IO.Path.GetFileName(uri.LocalPath);

                if (maskRegex.IsMatch(fileName))
                {
                    Directory.CreateDirectory(directoryName);

                    var webRequest = WebRequest.Create(uri);

                    try
                    {
                        using (WebResponse response = await webRequest.GetResponseAsync())
                        {
                            using (Stream resopnseStream = response.GetResponseStream())
                            {
                                using (var fileStream = File.OpenWrite(Path.Combine(directoryName, fileName)))
                                {
                                    resopnseStream.CopyTo(fileStream);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(
                            LogLevel.Error,
                            ex,
                            $"Saving content exception. {uri}");
                    }
                }
            }
        }

        private string GetFileNameByUri(Uri uri)
        {
            return uri.ToString().Replace("/", "-").Replace(":", "-").Replace("?", "-");
        }

        private static Regex FileMaskToRegex(string sFileMask)
        {
            String convertedMask = "^" + Regex.Escape(sFileMask).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(convertedMask, RegexOptions.IgnoreCase);
        }

        private void UpdateProgress(CrawlPageResults results)
        {
            _statistics.AppendPageResults(results);

            if (_crawledUri.Count % _saveProgressPeriod == 0)
            {
                SaveProgress();
            }
        }

        private string GetStatisticsFileName()
        {
            return Path.Combine(_domainDirectory, $"{_siteUri.Host}.statistics.json");
        }

        private string GetStateFileName()
        {
            return Path.Combine(_domainDirectory, $"{_siteUri.Host}.state.json");
        }

        private void SaveProgress()
        {
            // По-хорошему, статистику стоит объединить с прогрессом.

            using (var statStream = new FileStream(GetStatisticsFileName(), FileMode.Create))
            {
                using (var writer = new StreamWriter(statStream))
                {
                    using (var jsonWriter = new JsonTextWriter(writer))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(jsonWriter, _statistics);
                    }
                }
            }

            using (var statStream = new FileStream(GetStateFileName(), FileMode.Create))
            {
                using (var writer = new StreamWriter(statStream))
                {
                    using (var jsonWriter = new JsonTextWriter(writer))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(
                            jsonWriter,
                            new DomainCrawlerState()
                            {
                                CrawledUri = _crawledUri,
                                CrawlQueue = _pagesToCrawl
                            });
                    }
                }

            }
        }
    }
}
