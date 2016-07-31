using System;
using System.Collections.Generic;
using System.Linq;
using Metrics.Utils;

namespace Metrics
{
    /// <summary>
    ///     Collection of tags that can be attached to a metric.
    /// </summary>
    public struct MetricTags : IHideObjectMembers
    {
        private static readonly string[] Empty = new string[0];

        public static readonly MetricTags None = new MetricTags(Enumerable.Empty<string>());

        private readonly string[] _tags;

        public MetricTags(params string[] tags)
        {
            _tags = tags.ToArray();
        }

        public MetricTags(IEnumerable<string> tags)
            : this(tags.ToArray())
        {
        }

        public MetricTags(string commaSeparatedTags)
            : this(ToTags(commaSeparatedTags))
        {
        }

        public string[] Tags => _tags ?? Empty;

        private static IEnumerable<string> ToTags(string commaSeparatedTags)
        {
            if (string.IsNullOrWhiteSpace(commaSeparatedTags))
            {
                return Enumerable.Empty<string>();
            }

            return commaSeparatedTags.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant());
        }

        public static implicit operator MetricTags(string commaSeparatedTags)
        {
            return new MetricTags(commaSeparatedTags);
        }

        public static implicit operator MetricTags(string[] tags)
        {
            return new MetricTags(tags);
        }
    }
}