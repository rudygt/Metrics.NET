using System;
using System.Collections.Generic;

namespace Metrics.Reports
{
    public class ConsoleCSVAppender : CSVAppender
    {
        public ConsoleCSVAppender(string delimiter = CSVAppender.CommaDelimiter) : base(delimiter) { }

        public override void AppendLine(DateTime timestamp, string metricType, string metricName, IEnumerable<CSVReport.Value> values)
        {
            Console.WriteLine(GetHeader(values));
            Console.WriteLine(GetValues(timestamp, values));
        }
    }
}