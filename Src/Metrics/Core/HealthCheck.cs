using System;
using Metrics.MetricData;
using Metrics.Sampling;

namespace Metrics.Core
{
    public interface MetricsBuilder
    {
        MetricValueProvider<double> BuildPerformanceCounter(string name, Unit unit, string counterCategory, string counterName, string counterInstance);
        MetricValueProvider<double> BuildGauge(string name, Unit unit, Func<double> valueProvider);
        CounterImplementation BuildCounter(string name, Unit unit);
        MeterImplementation BuildMeter(string name, Unit unit, TimeUnit rateUnit);
        HistogramImplementation BuildHistogram(string name, Unit unit, SamplingType samplingType);
        HistogramImplementation BuildHistogram(string name, Unit unit, Reservoir reservoir);
        TimerImplementation BuildTimer(string name, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, SamplingType samplingType);
        TimerImplementation BuildTimer(string name, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, HistogramImplementation histogram);
        TimerImplementation BuildTimer(string name, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, Reservoir reservoir);
    }


    public interface HistogramImplementation : Histogram, MetricValueProvider<HistogramValue> { }

    public sealed class HistogramMetric : HistogramImplementation
    {
        private readonly Reservoir reservoir;
        private UserValueWrapper last;

        public HistogramMetric()
            : this(SamplingType.Default) { }

        public HistogramMetric(SamplingType samplingType)
            : this(SamplingTypeToReservoir(samplingType)) { }

        public HistogramMetric(Reservoir reservoir)
        {
            this.reservoir = reservoir;
        }

        public void Update(long value, string userValue = null)
        {
            this.last = new UserValueWrapper(value, userValue);
            this.reservoir.Update(value, userValue);
        }

        public HistogramValue GetValue(bool resetMetric = false)
        {
            var value = new HistogramValue(this.last.Value, this.last.UserValue, this.reservoir.GetSnapshot(resetMetric));
            if (resetMetric)
            {
                this.last = UserValueWrapper.Empty;
            }
            return value;
        }

        public HistogramValue Value
        {
            get
            {
                return GetValue();
            }
        }

        public void Reset()
        {
            this.last = UserValueWrapper.Empty;
            this.reservoir.Reset();
        }

        private static Reservoir SamplingTypeToReservoir(SamplingType samplingType)
        {
            while (true)
            {
                switch (samplingType)
                {
                    case SamplingType.Default:
                        samplingType = SamplingType.Default; // TODO: replace with this -> Metric.Config.DefaultSamplingType;
                        continue;
                    case SamplingType.HighDynamicRange:
                        return new HdrHistogramReservoir();
                    case SamplingType.ExponentiallyDecaying:
                        return new ExponentiallyDecayingReservoir();
                    case SamplingType.LongTerm:
                        return new UniformReservoir();
                    case SamplingType.SlidingWindow:
                        return new SlidingWindowReservoir();
                }
                throw new InvalidOperationException("Sampling type not implemented " + samplingType);
            }
        }
    }

    public class HealthCheck
    {
        private readonly Func<HealthCheckResult> _check;

        protected HealthCheck(string name)
            : this(name, () => { })
        {
        }

        public HealthCheck(string name, Action check)
            : this(name, () =>
            {
                check();
                return string.Empty;
            })
        {
        }

        public HealthCheck(string name, Func<string> check)
            : this(name, () => HealthCheckResult.Healthy(check()))
        {
        }

        public HealthCheck(string name, Func<HealthCheckResult> check)
        {
            Name = name;
            _check = check;
        }

        public string Name { get; }

        protected virtual HealthCheckResult Check()
        {
            return _check();
        }

        public Result Execute()
        {
            try
            {
                return new Result(Name, Check());
            }
            catch (Exception x)
            {
                return new Result(Name, HealthCheckResult.Unhealthy(x));
            }
        }

        public struct Result
        {
            public readonly string Name;
            public readonly HealthCheckResult Check;

            public Result(string name, HealthCheckResult check)
            {
                Name = name;
                Check = check;
            }
        }
    }
}