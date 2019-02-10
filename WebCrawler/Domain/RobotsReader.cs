using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;

namespace WebCrawler
{
    class RobotsReader
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private Uri _robotsUri;

        private Uri _domain;

        public RobotsReader(Uri domain)
        {

            _domain = domain ?? throw new ArgumentNullException(nameof(domain));

            _robotsUri = new Uri(_domain, new Uri("robots.txt", UriKind.Relative));
        }

        public async Task<RobotsParams> GetRobotsParams()
        {
            string robotsString = String.Empty;
            try
            {
                var webRequest = WebRequest.Create(_robotsUri);
                using (WebResponse response = await webRequest.GetResponseAsync())
                {
                    using (Stream resopnseStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(resopnseStream))
                        {
                            robotsString = await reader.ReadToEndAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(
                    LogLevel.Error,
                    e,
                    $"Robots parse exception with uri {_robotsUri}");
            }

            if (!string.IsNullOrEmpty(robotsString))
            {
                return ParseRobots(robotsString);
            }

            return null;
        }

        public RobotsParams ParseRobots(string robotsString)
        {
            RobotsParams robotParams = new RobotsParams();
            using (var stringReader = new StringReader(robotsString))
            {
                string line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        int position = line.IndexOf("crawl-delay", StringComparison.OrdinalIgnoreCase);

                        if (position < 0)
                        {
                            continue;
                        }

                        int commentPosition = line.IndexOf("#", StringComparison.OrdinalIgnoreCase);

                        if (commentPosition > 0 && commentPosition < position)
                        {
                            continue;
                        }

                        Regex delayRegex = new Regex("crawl-delay\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);

                        Match match = delayRegex.Match(line);

                        if (match.Success)
                        {
                            int delay = 0;

                            if (int.TryParse(match.Groups[1].Value, out delay))
                            {
                                robotParams.CrawlDelay = delay;
                            }
                        }
                    }
                }
            }

            return robotParams;
        }

        public override string ToString()
        {
            return $"[Domain = {_domain}]";
        }
    }
}
