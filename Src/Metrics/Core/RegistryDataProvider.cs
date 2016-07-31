using System.Collections.Generic;
using Metrics.MetricData;

namespace Metrics.Core
{
    public interface RegistryDataProvider
    {
        IEnumerable<GaugeValueSource> Gauges { get; }
        IEnumerable<CounterValueSource> Counters { get; }
        IEnumerable<MeterValueSource> Meters { get; }
        IEnumerable<HistogramValueSource> Histograms { get; }
        IEnumerable<TimerValueSource> Timers { get; }
    }
}