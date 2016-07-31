using Metrics.Utils;

namespace Metrics.MetricData
{
    /// <summary>
    ///     Provides the value of a metric and information about units.
    ///     This is the class that metric consumers should use.
    /// </summary>
    /// <typeparam name="T">Type of the metric value</typeparam>
    public abstract class MetricValueSource<T> : IHideObjectMembers
    {
        protected MetricValueSource(string name, MetricValueProvider<T> valueProvider, Unit unit, MetricTags tags)
        {
            Name = name;
            Unit = unit;
            ValueProvider = valueProvider;
            Tags = tags.Tags;
        }

        /// <summary>
        ///     Name of the metric.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     The current value of the metric.
        /// </summary>
        public T Value
        {
            get { return ValueProvider.Value; }
        }

        /// <summary>
        ///     Unit representing what the metric is measuring.
        /// </summary>
        public Unit Unit { get; }

        /// <summary>
        ///     Tags associated with the metric.
        /// </summary>
        public string[] Tags { get; }

        /// <summary>
        ///     Instance capable of returning the current value for the metric.
        /// </summary>
        public MetricValueProvider<T> ValueProvider { get; }
    }
}