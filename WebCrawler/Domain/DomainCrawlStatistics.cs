using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebCrawler.Domain
{
    [Serializable]
    public class DomainCrawlStatistics
    {
        [JsonProperty]
        private Dictionary<HttpStatusCode, long> _statuses = new Dictionary<HttpStatusCode, long>();

        internal void AppendPageResults(CrawlPageResults results)
        {
            PageCount++;

            TotalSizeBytes += results.ContentLength;

            TotalLoadTimeMS += results.LoadTimeMS;

            if (!_statuses.ContainsKey(results.StatusCode))
            {
                _statuses.Add(results.StatusCode, 0);
            }

            _statuses[results.StatusCode]++;
        }

        [JsonProperty]
        public long TotalSizeBytes { get; private set; }

        [JsonProperty]
        public long PageCount { get; private set; }

        [JsonProperty]
        public long TotalLoadTimeMS { get; private set; }

        [JsonIgnore]
        public long AverageLoadTime
        {
            get
            {
                if (PageCount == 0)
                {
                    return 0;
                }

                return TotalLoadTimeMS / PageCount;
            }
        }

        [JsonIgnore]
        public long AverageSize
        {
            get
            {
                if (PageCount == 0)
                {
                    return 0;
                }

                return TotalSizeBytes / PageCount;
            }
        }

        [JsonIgnore]
        public IEnumerable<KeyValuePair<HttpStatusCode, long>> Statuses
        {
            get
            {
                return _statuses;
            }
        }

        public override string ToString()
        {
            return $" [" +
                   $" {nameof(TotalSizeBytes)} = {TotalSizeBytes}," +
                   $" {nameof(PageCount)} = {PageCount}," +
                   $" {nameof(TotalLoadTimeMS)} = {TotalLoadTimeMS}" +
                   $"]";
        }
    }
}
