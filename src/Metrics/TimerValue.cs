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
            Rate = rate;
            Histogram = histogram;
            ActiveSessions = activeSessions;
            TotalTime = totalTime;
            this.durationUnit = durationUnit;
        }

        public TimerValue Scale(TimeUnit rate, TimeUnit duration)
        {
            var durationFactor = durationUnit.ScalingFactorFor(duration);
            var total = durationUnit.Convert(duration, TotalTime);
            return new TimerValue(Rate.Scale(rate), Histogram.Scale(durationFactor), ActiveSessions, total, duration);
        }
    }
}