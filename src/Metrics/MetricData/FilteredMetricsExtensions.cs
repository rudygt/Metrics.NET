namespace Metrics.MetricData
{
    public static class FilteredMetricsExtensions
    {
        public static MetricsDataProvider WithFilter(this MetricsDataProvider provider, MetricsFilter filter)
        {
            if (filter == null)
            {
                return provider;
            }
            return new FilteredMetrics(provider, filter);
        }
    }
}