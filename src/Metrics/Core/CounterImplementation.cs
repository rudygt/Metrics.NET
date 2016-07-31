using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Metrics.ConcurrencyUtilities;
using Metrics.MetricData;
using Metrics.Sampling;
using Metrics.Utils;

namespace Metrics.Core
{

    public interface TimerImplementation : ITimer, MetricValueProvider<TimerValue> { }

    public sealed class TimerMetric : TimerImplementation, IDisposable
    {
        private readonly Clock clock;
        private readonly MeterImplementation meter;
        private readonly HistogramImplementation histogram;
        private readonly StripedLongAdder activeSessionsCounter = new StripedLongAdder();
        private readonly StripedLongAdder totalRecordedTime = new StripedLongAdder();

        public TimerMetric()
            : this(new HistogramMetric(), new MeterMetric(), Clock.Default) { }

        public TimerMetric(SamplingType samplingType)
            : this(new HistogramMetric(samplingType), new MeterMetric(), Clock.Default) { }

        public TimerMetric(HistogramImplementation histogram)
            : this(histogram, new MeterMetric(), Clock.Default) { }

        public TimerMetric(Reservoir reservoir)
            : this(new HistogramMetric(reservoir), new MeterMetric(), Clock.Default) { }

        public TimerMetric(SamplingType samplingType, MeterImplementation meter, Clock clock)
            : this(new HistogramMetric(samplingType), meter, clock) { }

        public TimerMetric(HistogramImplementation histogram, MeterImplementation meter, Clock clock)
        {
            this.clock = clock;
            this.meter = meter;
            this.histogram = histogram;
        }

        public void Record(long duration, TimeUnit unit, string userValue = null)
        {
            var nanos = unit.ToNanoseconds(duration);
            if (nanos >= 0)
            {
                this.histogram.Update(nanos, userValue);
                this.meter.Mark(userValue);
                this.totalRecordedTime.Add(nanos);
            }
        }

        public void Time(Action action, string userValue = null)
        {
            var start = this.clock.Nanoseconds;
            try
            {
                this.activeSessionsCounter.Increment();
                action();
            }
            finally
            {
                this.activeSessionsCounter.Decrement();
                Record(this.clock.Nanoseconds - start, TimeUnit.Nanoseconds, userValue);
            }
        }

        public T Time<T>(Func<T> action, string userValue = null)
        {
            var start = this.clock.Nanoseconds;
            try
            {
                this.activeSessionsCounter.Increment();
                return action();
            }
            finally
            {
                this.activeSessionsCounter.Decrement();
                Record(this.clock.Nanoseconds - start, TimeUnit.Nanoseconds, userValue);
            }
        }

        public long StartRecording()
        {
            this.activeSessionsCounter.Increment();
            return this.clock.Nanoseconds;
        }

        public long CurrentTime()
        {
            return this.clock.Nanoseconds;
        }

        public long EndRecording()
        {
            this.activeSessionsCounter.Decrement();
            return this.clock.Nanoseconds;
        }

        public TimerContext NewContext(string userValue = null)
        {
            return new TimerContext(this, userValue);
        }

        public TimerValue Value
        {
            get
            {
                return GetValue();
            }
        }

        public TimerValue GetValue(bool resetMetric = false)
        {
            var total = resetMetric ? this.totalRecordedTime.GetAndReset() : this.totalRecordedTime.GetValue();
            return new TimerValue(this.meter.GetValue(resetMetric), this.histogram.GetValue(resetMetric), this.activeSessionsCounter.GetValue(), total, TimeUnit.Nanoseconds);
        }

        public void Reset()
        {
            this.meter.Reset();
            this.histogram.Reset();
        }

        public void Dispose()
        {
            using (this.histogram as IDisposable) { }
            using (this.meter as IDisposable) { }
        }
    }


    public class SimpleMeter
    {
        private const long NanosInSecond = 1000L * 1000L * 1000L;
        private const long IntervalSeconds = 5L;
        private const double Interval = IntervalSeconds * NanosInSecond;
        private const double SecondsPerMinute = 60.0;
        private const int OneMinute = 1;
        private const int FiveMinutes = 5;
        private const int FifteenMinutes = 15;
        private static readonly double M1Alpha = 1 - Math.Exp(-IntervalSeconds / SecondsPerMinute / OneMinute);
        private static readonly double M5Alpha = 1 - Math.Exp(-IntervalSeconds / SecondsPerMinute / FiveMinutes);
        private static readonly double M15Alpha = 1 - Math.Exp(-IntervalSeconds / SecondsPerMinute / FifteenMinutes);

        private readonly StripedLongAdder uncounted = new StripedLongAdder();

        private AtomicLong total = new AtomicLong(0L);
        private VolatileDouble m1Rate = new VolatileDouble(0.0);
        private VolatileDouble m5Rate = new VolatileDouble(0.0);
        private VolatileDouble m15Rate = new VolatileDouble(0.0);
        private volatile bool initialized;

        public void Mark(long count)
        {
            this.uncounted.Add(count);
        }

        public void Tick()
        {
            var count = this.uncounted.GetAndReset();
            Tick(count);
        }

        private void Tick(long count)
        {
            this.total.Add(count);
            var instantRate = count / Interval;
            if (this.initialized)
            {
                var rate = this.m1Rate.GetValue();
                this.m1Rate.SetValue(rate + M1Alpha * (instantRate - rate));

                rate = this.m5Rate.GetValue();
                this.m5Rate.SetValue(rate + M5Alpha * (instantRate - rate));

                rate = this.m15Rate.GetValue();
                this.m15Rate.SetValue(rate + M15Alpha * (instantRate - rate));
            }
            else
            {
                this.m1Rate.SetValue(instantRate);
                this.m5Rate.SetValue(instantRate);
                this.m15Rate.SetValue(instantRate);
                this.initialized = true;
            }
        }

        public void Reset()
        {
            this.uncounted.Reset();
            this.total.SetValue(0L);
            this.m1Rate.SetValue(0.0);
            this.m5Rate.SetValue(0.0);
            this.m15Rate.SetValue(0.0);
        }

        public MeterValue GetValue(double elapsed)
        {
            var count = this.total.GetValue() + this.uncounted.GetValue();
            return new MeterValue(count, GetMeanRate(count, elapsed), OneMinuteRate, FiveMinuteRate, FifteenMinuteRate, TimeUnit.Seconds);
        }

        private static double GetMeanRate(long value, double elapsed)
        {
            if (value == 0)
            {
                return 0.0;
            }

            return value / elapsed * TimeUnit.Seconds.ToNanoseconds(1);
        }

        private double FifteenMinuteRate { get { return this.m15Rate.GetValue() * NanosInSecond; } }
        private double FiveMinuteRate { get { return this.m5Rate.GetValue() * NanosInSecond; } }
        private double OneMinuteRate { get { return this.m1Rate.GetValue() * NanosInSecond; } }
    }

    public interface MeterImplementation : Meter, MetricValueProvider<MeterValue> { }

    public sealed class MeterMetric : SimpleMeter, MeterImplementation, IDisposable
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

        private ConcurrentDictionary<string, SimpleMeter> setMeters;

        private readonly Clock clock;
        private readonly Scheduler tickScheduler;

        private long startTime;

        public MeterMetric()
            : this(Clock.Default, new ActionScheduler())
        { }

        public MeterMetric(Clock clock, Scheduler scheduler)
        {
            this.clock = clock;
            this.startTime = this.clock.Nanoseconds;
            this.tickScheduler = scheduler;
            this.tickScheduler.Start(TickInterval, (Action)Tick);
        }

        public MeterValue Value { get { return GetValue(); } }

        public void Mark()
        {
            Mark(1L);
        }

        public new void Mark(long count)
        {
            base.Mark(count);
        }

        public void Mark(string item)
        {
            Mark(item, 1L);
        }

        public void Mark(string item, long count)
        {
            Mark(count);

            if (item == null)
            {
                return;
            }

            if (this.setMeters == null)
            {
                Interlocked.CompareExchange(ref this.setMeters, new ConcurrentDictionary<string, SimpleMeter>(), null);
            }

            Debug.Assert(this.setMeters != null);
            this.setMeters.GetOrAdd(item, v => new SimpleMeter()).Mark(count);
        }

        public MeterValue GetValue(bool resetMetric = false)
        {
            if (this.setMeters == null || this.setMeters.Count == 0)
            {
                double elapsed = (this.clock.Nanoseconds - this.startTime);
                var value = base.GetValue(elapsed);
                if (resetMetric)
                {
                    Reset();
                }
                return value;
            }

            return GetValueWithSetItems(resetMetric);
        }

        private MeterValue GetValueWithSetItems(bool resetMetric)
        {
            double elapsed = this.clock.Nanoseconds - this.startTime;
            var value = base.GetValue(elapsed);

            Debug.Assert(this.setMeters != null);

            var items = new MeterValue.SetItem[this.setMeters.Count];
            var index = 0;

            foreach (var meter in this.setMeters)
            {
                var itemValue = meter.Value.GetValue(elapsed);
                var percent = value.Count > 0 ? itemValue.Count / (double)value.Count * 100 : 0.0;
                items[index++] = new MeterValue.SetItem(meter.Key, percent, itemValue);
                if (index == items.Length)
                {
                    break;
                }
            }

            Array.Sort(items, MeterValue.SetItemComparer);
            var result = new MeterValue(value.Count, value.MeanRate, value.OneMinuteRate, value.FiveMinuteRate, value.FifteenMinuteRate, TimeUnit.Seconds, items);
            if (resetMetric)
            {
                Reset();
            }
            return result;
        }

        private new void Tick()
        {
            base.Tick();
            if (this.setMeters != null)
            {
                foreach (var value in this.setMeters.Values)
                {
                    value.Tick();
                }
            }
        }

        public void Dispose()
        {
            this.tickScheduler.Stop();
            using (this.tickScheduler) { }

            if (this.setMeters != null)
            {
                this.setMeters.Clear();
                this.setMeters = null;
            }
        }

        public new void Reset()
        {
            this.startTime = this.clock.Nanoseconds;
            base.Reset();
            if (this.setMeters != null)
            {
                foreach (var meter in this.setMeters.Values)
                {
                    meter.Reset();
                }
            }
        }
    }

    public interface CounterImplementation : Counter, MetricValueProvider<CounterValue> { }
}