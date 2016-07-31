using Metrics.MetricData;

namespace Metrics.Core
{
    public interface CounterImplementation : Counter, MetricValueProvider<CounterValue> { }
}