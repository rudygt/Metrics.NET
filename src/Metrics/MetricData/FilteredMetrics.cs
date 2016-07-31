namespace Metrics.MetricData
{
    public sealed class FilteredMetrics : MetricsDataProvider
    {
        private readonly MetricsFilter filter;
        private readonly MetricsDataProvider provider;

        public FilteredMetrics(MetricsDataProvider provider, MetricsFilter filter)
        {
            this.provider = provider;
            this.filter = filter;
        }

        public MetricsData CurrentMetricsData
        {
            get { return provider.CurrentMetricsData.Filter(filter); }
        }
    }
}