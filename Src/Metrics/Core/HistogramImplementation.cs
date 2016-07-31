using Metrics.MetricData;

namespace Metrics.Core
{
    public interface HistogramImplementation : Histogram, MetricValueProvider<HistogramValue> { }
}