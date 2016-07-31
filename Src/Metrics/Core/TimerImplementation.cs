using Metrics.MetricData;

namespace Metrics.Core
{
    public interface TimerImplementation : ITimer, MetricValueProvider<TimerValue> { }
}