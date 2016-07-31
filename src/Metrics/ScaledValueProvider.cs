using System;

namespace Metrics.MetricData
{
    public sealed class ScaledValueProvider<T> : MetricValueProvider<T>
    {
        private readonly Func<T, T> scalingFunction;

        public ScaledValueProvider(MetricValueProvider<T> valueProvider, Func<T, T> transformation)
        {
            ValueProvider = valueProvider;
            scalingFunction = transformation;
        }

        public MetricValueProvider<T> ValueProvider { get; }

        public T Value
        {
            get { return scalingFunction(ValueProvider.Value); }
        }

        public T GetValue(bool resetMetric = false)
        {
            return scalingFunction(ValueProvider.GetValue(resetMetric));
        }
    }
}