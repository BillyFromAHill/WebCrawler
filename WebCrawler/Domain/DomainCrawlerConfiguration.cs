using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    public class DomainCrawlerConfiguration
    {
        public DomainCrawlerConfiguration()
        {
            MaxTasks = 100;

            MaxPageLevel = 10;

            MaxPages = 400;

            ContentFileMask = "*.png";
        }

        public string ContentFileMask { get; set; }

        public int MaxTasks { get; set; }

        public string StopString { get; set; }

        public int MaxPages { get; set; }

        public int MaxPageLevel { get; set; }

        public override string ToString()
        {
            return $" [" +
                   $" {nameof(ContentFileMask)} = {ContentFileMask}," +
                   $" {nameof(MaxTasks)} = {MaxTasks}," +
                   $" {nameof(StopString)} = {StopString}," +
                   $" {nameof(MaxPages)} = {MaxPages}," +
                   $" {nameof(MaxPageLevel)} = {MaxPageLevel}" +
                   $"]";
        }
    }
}
