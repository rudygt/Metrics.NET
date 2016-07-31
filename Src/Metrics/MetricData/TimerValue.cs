using Metrics.Utils;

namespace Metrics.MetricData
{
    /// <summary>
    ///     The value reported by a Timer Metric
    /// </summary>
    public sealed class TimerValue
    {
        public readonly long ActiveSessions;
        private readonly TimeUnit durationUnit;
        public readonly HistogramValue Histogram;
        public readonly MeterValue Rate;
        public readonly long TotalTime;

        public TimerValue(MeterValue rate, HistogramValue histogram, long activeSessions, long totalTime, TimeUnit durationUnit)
        {
            this.Rate = rate;
            this.Histogram = histogram;
            this.ActiveSessions = activeSessions;
            this.TotalTime = totalTime;
            this.durationUnit = durationUnit;
        }

        public TimerValue Scale(TimeUnit rate, TimeUnit duration)
        {
            var durationFactor = this.durationUnit.ScalingFactorFor(duration);
            var total = this.durationUnit.Convert(duration, this.TotalTime);
            return new TimerValue(this.Rate.Scale(rate), this.Histogram.Scale(durationFactor), this.ActiveSessions, total, duration);
        }
    }
}