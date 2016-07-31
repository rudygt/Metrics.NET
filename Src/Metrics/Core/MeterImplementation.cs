using Metrics.MetricData;

namespace Metrics.Core
{
    public interface MeterImplementation : Meter, MetricValueProvider<MeterValue> { }
}