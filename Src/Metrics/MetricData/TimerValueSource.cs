namespace Metrics.MetricData
{
    /// <summary>
    ///     Combines the value of the timer with the defined unit and the time units for rate and duration.
    /// </summary>
    public class TimerValueSource : MetricValueSource<TimerValue>
    {
        public TimerValueSource(string name, MetricValueProvider<TimerValue> value, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, MetricTags tags)
            : base(name, new ScaledValueProvider<TimerValue>(value, v => v.Scale(rateUnit, durationUnit)), unit, tags)
        {
            RateUnit = rateUnit;
            DurationUnit = durationUnit;
        }

        public TimeUnit RateUnit { get; }
        public TimeUnit DurationUnit { get; }
    }
}