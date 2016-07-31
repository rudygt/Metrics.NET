namespace Metrics.MetricData
{
    /// <summary>
    ///     Combines the value of the histogram with the defined unit for the value.
    /// </summary>
    public sealed class HistogramValueSource : MetricValueSource<HistogramValue>
    {
        public HistogramValueSource(string name, MetricValueProvider<HistogramValue> valueProvider, Unit unit, MetricTags tags)
            : base(name, valueProvider, unit, tags)
        {
        }
    }
}