﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace WebCrawler
{
    public class WebCrawler
    {
        private Queue<DomainCrawler> _domainsToCrawl = new Queue<DomainCrawler>();

        private HashSet<Task> _crawlingDomains = new HashSet<Task>();

        private const int WaitForDomainMS = 10000;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        static WebCrawler()
        {
            var config = new NLog.Config.LoggingConfiguration();

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "log.txt" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            NLog.LogManager.Configuration = config;
        }

        private CrawlerConfiguration _configuration;

        public WebCrawler(CrawlerConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            _configuration = configuration;
        }

        public async void StartCrawling(IEnumerable<WebCrawlerItem> items)
        {
            await Task.Factory.StartNew(
                () =>
                {
                    _crawlingDomains.Clear();

                    foreach (var webCrawlerItem in items)
                    {
                        DomainCrawlerConfiguration config = webCrawlerItem.Configuration;
                        _domainsToCrawl.Enqueue(new DomainCrawler(webCrawlerItem.Domain, config ?? new DomainCrawlerConfiguration()));
                    }

                    while (_domainsToCrawl.Count > 0)
                    {
                        // Вырисовывается копипаста из краулера домена.
                        if (_crawlingDomains.Count >= _configuration.MaxCrawlDomains)
                        {
                            _crawlingDomains.RemoveWhere(d => d.IsCompleted);

                            if (_crawlingDomains.Count >= _configuration.MaxCrawlDomains)
                            {
                                Task.WaitAny(_crawlingDomains.ToArray(), WaitForDomainMS);
                            }

                            continue;
                        }

                        DomainCrawler crawler = _domainsToCrawl.Dequeue();
                        _crawlingDomains.Add(crawler.CrawlDomain(_cts.Token));
                    }

                    Task.WaitAll(_crawlingDomains.ToArray());

                }, TaskCreationOptions.LongRunning);
        }


    }
}
