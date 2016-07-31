using Metrics.Utils;

namespace Metrics.MetricData
{
    public interface MetricsFilter : IHideObjectMembers
    {
        bool IsMatch(string context);

        bool IsMatch(GaugeValueSource gauge);
        bool IsMatch(CounterValueSource counter);
        bool IsMatch(MeterValueSource meter);
        bool IsMatch(HistogramValueSource histogram);
        bool IsMatch(TimerValueSource timer);
    }
}