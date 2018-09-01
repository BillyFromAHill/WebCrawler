using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class PageCrawler
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private Uri _pageUri;

        private Uri _resultsDirectory;

        private string _pageFileName;

        public PageCrawler(Uri pageUri, Uri resultsDirectory, string pageFileName)
        {
            if (pageUri == null)
            {
                throw new ArgumentNullException("pageUri");
            }

            if (resultsDirectory == null)
            {
                throw new ArgumentNullException("resultsDirecotry");
            }

            if (string.IsNullOrEmpty(pageFileName))
            {
                throw new ArgumentNullException("pageFileName");
            }

            _pageUri = pageUri;
            _resultsDirectory = resultsDirectory;
            _pageFileName = pageFileName;
        }

        public async Task<CrawlPageResults> StartCrawling(CancellationToken cancellationToken)
        {
            return await await Task.Factory.StartNew(
                async (token) =>
                {
                    return await CrawlWorker((CancellationToken)token);
                },
                cancellationToken,
                TaskCreationOptions.LongRunning);
        }

        private async Task<CrawlPageResults> CrawlWorker(CancellationToken token)
        {
            var webRequest = WebRequest.Create(_pageUri);

            // Строка целиком - не слишком здорово.
            string responseString = string.Empty;

            var loadWatcher = new Stopwatch();

            HttpStatusCode statusCode;
            long contentLength = 0;

            loadWatcher.Start();
            using (WebResponse response = await webRequest.GetResponseAsync())
            {
                HttpWebResponse httpResponse = response as HttpWebResponse;
                if (httpResponse == null)
                {
                    // Здесь можно предпринять что-то еще, но мы хотим видеть именно страницы.
                    throw new ArgumentException("Specified Uri deoesn't contain page");
                }

                statusCode = httpResponse.StatusCode;
                contentLength = httpResponse.ContentLength;

                using (Stream resopnseStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(resopnseStream))
                    {
                        responseString = await reader.ReadToEndAsync();
                    }
                }
            }
            loadWatcher.Stop();

            string directoryName = Path.GetDirectoryName(_resultsDirectory.OriginalString);
            if (!string.IsNullOrEmpty(responseString))
            {
                Directory.CreateDirectory(directoryName);
                File.WriteAllText(Path.Combine(directoryName, _pageFileName), responseString);
            }

            return new CrawlPageResults() {
                CrawledUri = _pageUri,
                LoadTimeMS = loadWatcher.ElapsedMilliseconds,
                ContentLength = contentLength,
                References = FindAllReferences(responseString),
                ContentUris = FindContentUris(responseString)
            };
        }

        private Uri GetBaseUri(string page)
        {
            string baseURL = "";
            string basePattern = "<base\\s+(?:[^>]*?\\s+)?href\\s*=\\s*(?:[\"'](?<1>[^\"']*)[\"']|(?<1>\\S+))";
            Match baseMatch = Regex.Match(page, basePattern);
            if (baseMatch.Success)
            {
                baseURL = baseMatch.Value;
            }

            return new Uri(_pageUri, new Uri(baseURL, UriKind.RelativeOrAbsolute));
        }

        private IEnumerable<Uri> FindAllReferences(string page)
        {
            // Здесь надо идти по скриптам во всякие onClick
            // Но, полагаю, это не вписывается в данное задание.
            // Ищем c помощью regex a.
            string linkPattern = "<(a|area)\\s+(?:[^>]*?\\s+)?href\\s*=\\s*(?:[\"'](?<1>[^\"']*)[\"']|(?<1>\\S+))";

            Uri baseUri = GetBaseUri(page);
            
            MatchCollection matches = Regex.Matches(page, linkPattern, RegexOptions.IgnoreCase);
            var references = new List<Uri>();

            foreach(Match match in matches)
            {
                try
                {
                    var currentUri = new Uri(match.Groups[1].Value);

                    if (currentUri.IsAbsoluteUri)
                    {
                        references.Add(currentUri);
                    }
                    else
                    {
                        references.Add(new Uri(baseUri, currentUri));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(
                        LogLevel.Error,
                        $"Link parse exception with uri {match.Groups[1].Value}",
                        ex);
                }
            }

            return references;
        }

        private IEnumerable<Uri> FindContentUris(string page)
        {
            var contentUris = new List<Uri>();
            string linkPattern = "<(link|img)\\s+(?:[^>]*?\\s+)?(src|href)\\s*=\\s*(?:[\"'](?<1>[^\"']*)[\"']|(?<1>\\S+))";

            Uri baseUri = GetBaseUri(page);

            MatchCollection matches = Regex.Matches(page, linkPattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                try
                {
                    var currentUri = new Uri(match.Groups[1].Value);

                    if (currentUri.IsAbsoluteUri)
                    {
                        contentUris.Add(currentUri);
                    }
                    else
                    {
                        contentUris.Add(new Uri(baseUri, currentUri));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(
                        LogLevel.Error,
                        $"Content parse exception with uri {match.Groups[1].Value}",
                        ex);
                }
            }

            return contentUris;
        }
    }
}