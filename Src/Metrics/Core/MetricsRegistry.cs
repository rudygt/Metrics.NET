using System;
using Metrics.MetricData;

namespace Metrics.Core
{
    public interface MetricsRegistry
    {
        RegistryDataProvider DataProvider { get; }

        void Gauge(string name, Func<MetricValueProvider<double>> valueProvider, Unit unit, MetricTags tags);

        Counter Counter<T>(string name, Func<T> builder, Unit unit, MetricTags tags)
            where T : CounterImplementation;

        Meter Meter<T>(string name, Func<T> builder, Unit unit, TimeUnit rateUnit, MetricTags tags)
            where T : MeterImplementation;

        Histogram Histogram<T>(string name, Func<T> builder, Unit unit, MetricTags tags)
            where T : HistogramImplementation;

        ITimer Timer<T>(string name, Func<T> builder, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, MetricTags tags)
            where T : TimerImplementation;

        void ClearAllMetrics();
        void ResetMetricsValues();
    }
}