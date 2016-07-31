using System;
using System.Collections.Generic;
using System.Linq;

namespace Metrics.Reports
{
    public abstract class CSVAppender
    {
        public const string CommaDelimiter = ",";

        private readonly string delimiter;

        protected CSVAppender(string delimiter)
        {
            if (delimiter == null)
            {
                throw new ArgumentNullException(nameof(delimiter));
            }

            this.delimiter = delimiter;
        }

        public abstract void AppendLine(DateTime timestamp, string metricType, string metricName, IEnumerable<CSVReport.Value> values);

        protected virtual string GetHeader(IEnumerable<CSVReport.Value> values)
        {
            return string.Join(this.delimiter, new[] { "Date", "Ticks" }.Concat(values.Select(v => v.Name)));
        }

        protected virtual string GetValues(DateTime timestamp, IEnumerable<CSVReport.Value> values)
        {
            return string.Join(this.delimiter, new[] { timestamp.ToString("u"), timestamp.Ticks.ToString("D") }.Concat(values.Select(v => v.FormattedValue)));
        }
    }
}