using System;

namespace Metrics.MetricData
{
    public sealed class ScaledValueProvider<T> : MetricValueProvider<T>
    {
        private readonly Func<T, T> scalingFunction;

        public ScaledValueProvider(MetricValueProvider<T> valueProvider, Func<T, T> transformation)
        {
            ValueProvider = valueProvider;
            this.scalingFunction = transformation;
        }

        public MetricValueProvider<T> ValueProvider { get; }

        public T Value
        {
            get { return this.scalingFunction(ValueProvider.Value); }
        }

        public T GetValue(bool resetMetric = false)
        {
            return this.scalingFunction(ValueProvider.GetValue(resetMetric));
        }
    }
}