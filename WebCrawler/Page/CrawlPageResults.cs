using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    class CrawlPageResults
    {
        public Uri CrawledUri { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public long ContentLength { get; set; }

        public long LoadTimeMS { get; set; }

        public IEnumerable<Uri> References { get; set; }

        public IEnumerable<Uri> ContentUris { get; set; }

        public string PageContent { get; set; }

        public override string ToString()
        {
            return $" [" +
                   $" {nameof(CrawledUri)} = {CrawledUri}," +
                   $" {nameof(StatusCode)} = {StatusCode}," +
                   $" {nameof(ContentLength)} = {ContentLength}," +
                   $" {nameof(LoadTimeMS)} = {LoadTimeMS}" +
                   $"]";
        }
    }
}
