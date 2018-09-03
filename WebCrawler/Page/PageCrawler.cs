using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler
{
    class PageCrawler
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private Uri _pageUri;

        public PageCrawler(Uri pageUri)
        {
            if (pageUri == null)
            {
                throw new ArgumentNullException("pageUri");
            }

            _pageUri = pageUri;
        }

        public Uri PageUri
        {
            get
            {
                return _pageUri;
            }
        }

        public async Task<CrawlPageResults> StartCrawling(
            Stream resultDestination,
            CancellationToken cancellationToken)
        {
            return await await Task.Factory.StartNew(
                async (args) =>
                {
                    return await CrawlWorker((CrawlWorkerArgs)args);
                },
                new CrawlWorkerArgs() { CancellationToken = cancellationToken, DestStream = resultDestination},
                TaskCreationOptions.LongRunning);
        }

        private async Task<CrawlPageResults> CrawlWorker(CrawlWorkerArgs args)
        {
            // Строка целиком - не слишком здорово.
            string responseString = string.Empty;

            var loadWatcher = new Stopwatch();

            HttpStatusCode statusCode;
            long contentLength = 0;

            loadWatcher.Start();

            var webRequest = WebRequest.Create(_pageUri);

            try
            {
                using (WebResponse response = await webRequest.GetResponseAsync())
                {
                    HttpWebResponse httpResponse = response as HttpWebResponse;
                    if (httpResponse == null)
                    {
                        // Здесь можно предпринять что-то еще, но мы хотим видеть именно страницы.
                        throw new ArgumentException("Specified Uri doesn't contain page");
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
            }
            catch (WebException ex)
            {
                HttpWebResponse httpResponse = ex.Response as HttpWebResponse;
                if (httpResponse == null)
                {
                    throw new ArgumentException("Specified Uri doesn't contain page");
                }

                statusCode = httpResponse.StatusCode;
                contentLength = httpResponse.ContentLength;
            }
            loadWatcher.Stop();


            if (!string.IsNullOrEmpty(responseString) && args.DestStream != null)
            {
                // С кодировкой могут быть проблемки.
                byte[] stringBytes = Encoding.UTF8.GetBytes(responseString);
                await args.DestStream.WriteAsync(stringBytes, 0, stringBytes.Length);
            }

            return new CrawlPageResults() {
                CrawledUri = _pageUri,
                LoadTimeMS = loadWatcher.ElapsedMilliseconds,
                ContentLength = contentLength,
                StatusCode = statusCode,
                References = FindAllReferences(responseString),
                ContentUris = FindContentUris(responseString),
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
                    string hrefValue = match.Groups[1].Value;

                    if (hrefValue == "#" || string.IsNullOrEmpty(hrefValue))
                    {
                        continue;
                    }

                    var currentUri = new Uri(match.Groups[1].Value, UriKind.RelativeOrAbsolute);

                    if (currentUri.IsAbsoluteUri && currentUri.Host != _pageUri.Host)
                    {
                        continue;
                    }

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
                    var currentUri = new Uri(match.Groups[1].Value, UriKind.RelativeOrAbsolute);

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