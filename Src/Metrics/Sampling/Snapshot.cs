using System;
using System.Collections.Generic;
using HdrHistogram;
using Metrics.ConcurrencyUtilities;
using System.Linq;
using System.Threading;
using Metrics.Utils;

namespace Metrics.Sampling
{
    public struct WeightedSample
    {
        public readonly long Value;
        public readonly string UserValue;
        public readonly double Weight;

        public WeightedSample(long value, string userValue, double weight)
        {
            this.Value = value;
            this.UserValue = userValue;
            this.Weight = weight;
        }
    }

    public sealed class WeightedSnapshot : Snapshot
    {
        private readonly long[] values;
        private readonly double[] normWeights;
        private readonly double[] quantiles;

        private class WeightedSampleComparer : IComparer<WeightedSample>
        {
            public static readonly IComparer<WeightedSample> Instance = new WeightedSampleComparer();

            public int Compare(WeightedSample x, WeightedSample y)
            {
                return Comparer<long>.Default.Compare(x.Value, y.Value);
            }
        }

        public WeightedSnapshot(long count, IEnumerable<WeightedSample> values)
        {
            this.Count = count;
            var sample = values.ToArray();
            Array.Sort(sample, WeightedSampleComparer.Instance);

            var sumWeight = sample.Sum(s => s.Weight);

            this.values = new long[sample.Length];
            this.normWeights = new double[sample.Length];
            this.quantiles = new double[sample.Length];

            for (var i = 0; i < sample.Length; i++)
            {
                this.values[i] = sample[i].Value;
                this.normWeights[i] = sample[i].Weight / sumWeight;
                if (i > 0)
                {
                    this.quantiles[i] = this.quantiles[i - 1] + this.normWeights[i - 1];
                }
            }

            this.MinUserValue = sample.Select(s => s.UserValue).FirstOrDefault();
            this.MaxUserValue = sample.Select(s => s.UserValue).LastOrDefault();
        }

        public long Count { get; }
        public int Size => this.values.Length;

        public long Max => this.values.LastOrDefault();
        public long Min => this.values.FirstOrDefault();

        public string MaxUserValue { get; }
        public string MinUserValue { get; }

        public double Mean
        {
            get
            {
                if (this.values.Length == 0)
                {
                    return 0.0;
                }

                double sum = 0;
                for (var i = 0; i < this.values.Length; i++)
                {
                    sum += this.values[i] * this.normWeights[i];
                }
                return sum;
            }
        }

        public double StdDev
        {
            get
            {
                if (this.Size <= 1)
                {
                    return 0;
                }

                var mean = this.Mean;
                double variance = 0;

                for (var i = 0; i < this.values.Length; i++)
                {
                    var diff = this.values[i] - mean;
                    variance += this.normWeights[i] * diff * diff;
                }

                return Math.Sqrt(variance);
            }
        }

        public double Median => GetValue(0.5d);
        public double Percentile75 => GetValue(0.75d);
        public double Percentile95 => GetValue(0.95d);
        public double Percentile98 => GetValue(0.98d);
        public double Percentile99 => GetValue(0.99d);
        public double Percentile999 => GetValue(0.999d);

        public IEnumerable<long> Values => this.values;

        public double GetValue(double quantile)
        {
            if (quantile < 0.0 || quantile > 1.0 || double.IsNaN(quantile))
            {
                throw new ArgumentException($"{quantile} is not in [0..1]");
            }

            if (Size == 0)
            {
                return 0;
            }

            var posx = Array.BinarySearch(this.quantiles, quantile);
            if (posx < 0)
            {
                posx = ~posx - 1;
            }

            if (posx < 1)
            {
                return this.values[0];
            }

            return posx >= this.values.Length ? this.values[this.values.Length - 1] : this.values[posx];
        }
    }

    public sealed class ExponentiallyDecayingReservoir : Reservoir, IDisposable
    {
        private const int DefaultSize = 1028;
        private const double DefaultAlpha = 0.015;
        private static readonly TimeSpan RescaleInterval = TimeSpan.FromHours(1);

        private class ReverseOrderDoubleComparer : IComparer<double>
        {
            public static readonly IComparer<double> Instance = new ReverseOrderDoubleComparer();

            public int Compare(double x, double y)
            {
                return y.CompareTo(x);
            }
        }

        private readonly SortedList<double, WeightedSample> values;

        private SpinLock @lock = new SpinLock();

        private readonly double alpha;
        private readonly int size;
        private AtomicLong count = new AtomicLong();
        private AtomicLong startTime;

        private readonly Clock clock;

        private readonly Scheduler rescaleScheduler;

        public ExponentiallyDecayingReservoir()
            : this(DefaultSize, DefaultAlpha)
        { }

        public ExponentiallyDecayingReservoir(int size, double alpha)
            : this(size, alpha, Clock.Default, new ActionScheduler())
        { }

        public ExponentiallyDecayingReservoir(Clock clock, Scheduler scheduler)
            : this(DefaultSize, DefaultAlpha, clock, scheduler)
        { }

        public ExponentiallyDecayingReservoir(int size, double alpha, Clock clock, Scheduler scheduler)
        {
            this.size = size;
            this.alpha = alpha;
            this.clock = clock;

            this.values = new SortedList<double, WeightedSample>(size, ReverseOrderDoubleComparer.Instance);

            this.rescaleScheduler = scheduler;
            this.rescaleScheduler.Start(RescaleInterval, () => Rescale());

            this.startTime = new AtomicLong(clock.Seconds);
        }

        public int Size { get { return Math.Min(this.size, (int)this.count.GetValue()); } }

        public Snapshot GetSnapshot(bool resetReservoir = false)
        {
            var lockTaken = false;
            try
            {
                this.@lock.Enter(ref lockTaken);
                var snapshot = new WeightedSnapshot(this.count.GetValue(), this.values.Values);
                if (resetReservoir)
                {
                    ResetReservoir();
                }
                return snapshot;
            }
            finally
            {
                if (lockTaken)
                {
                    this.@lock.Exit();
                }
            }
        }

        public void Update(long value, string userValue = null)
        {
            Update(value, userValue, this.clock.Seconds);
        }

        public void Reset()
        {
            var lockTaken = false;
            try
            {
                this.@lock.Enter(ref lockTaken);
                ResetReservoir();
            }
            finally
            {
                if (lockTaken)
                {
                    this.@lock.Exit();
                }
            }
        }

        private void ResetReservoir()
        {
            this.values.Clear();
            this.count.SetValue(0L);
            this.startTime.SetValue(this.clock.Seconds);
        }

        private void Update(long value, string userValue, long timestamp)
        {
            var lockTaken = false;
            try
            {
                this.@lock.Enter(ref lockTaken);

                var itemWeight = Math.Exp(this.alpha * (timestamp - this.startTime.GetValue()));
                var sample = new WeightedSample(value, userValue, itemWeight);

                var random = 0.0;
                // Prevent division by 0
                while (random.Equals(.0))
                {
                    random = ThreadLocalRandom.NextDouble();
                }

                var priority = itemWeight / random;

                var newCount = this.count.GetValue();
                newCount++;
                this.count.SetValue(newCount);

                if (newCount <= this.size)
                {
                    this.values[priority] = sample;
                }
                else
                {
                    var first = this.values.Keys[this.values.Count - 1];
                    if (first < priority)
                    {
                        this.values.Remove(first);
                        this.values[priority] = sample;
                    }
                }
            }
            finally
            {
                if (lockTaken)
                {
                    this.@lock.Exit();
                }
            }
        }

        public void Dispose()
        {
            using (this.rescaleScheduler) { }
        }

        ///* "A common feature of the above techniques—indeed, the key technique that
        // * allows us to track the decayed weights efficiently—is that they maintain
        // * counts and other quantities based on g(ti ? L), and only scale by g(t ? L)
        // * at query time. But while g(ti ?L)/g(t?L) is guaranteed to lie between zero
        // * and one, the intermediate values of g(ti ? L) could become very large. For
        // * polynomial functions, these values should not grow too large, and should be
        // * effectively represented in practice by floating point values without loss of
        // * precision. For exponential functions, these values could grow quite large as
        // * new values of (ti ? L) become large, and potentially exceed the capacity of
        // * common floating point types. However, since the values stored by the
        // * algorithms are linear combinations of g values (scaled sums), they can be
        // * rescaled relative to a new landmark. That is, by the analysis of exponential
        // * decay in Section III-A, the choice of L does not affect the final result. We
        // * can therefore multiply each value based on L by a factor of exp(??(L? ? L)),
        // * and obtain the correct value as if we had instead computed relative to a new
        // * landmark L? (and then use this new L? at query time). This can be done with
        // * a linear pass over whatever data structure is being used."
        // */
        private void Rescale()
        {
            var lockTaken = false;
            try
            {
                this.@lock.Enter(ref lockTaken);
                var oldStartTime = this.startTime.GetValue();
                this.startTime.SetValue(this.clock.Seconds);

                var scalingFactor = Math.Exp(-this.alpha * (this.startTime.GetValue() - oldStartTime));

                var keys = new List<double>(this.values.Keys);
                foreach (var key in keys)
                {
                    var sample = this.values[key];
                    this.values.Remove(key);
                    var newKey = key * Math.Exp(-this.alpha * (this.startTime.GetValue() - oldStartTime));
                    var newSample = new WeightedSample(sample.Value, sample.UserValue, sample.Weight * scalingFactor);
                    this.values[newKey] = newSample;
                }
                // make sure the counter is in sync with the number of stored samples.
                this.count.SetValue(this.values.Count);
            }
            finally
            {
                if (lockTaken)
                {
                    this.@lock.Exit();
                }
            }
        }
    }

    public sealed class SlidingWindowReservoir : Reservoir
    {
        private const int DefaultSize = 1028;

        private readonly UserValueWrapper[] values;
        private AtomicLong count = new AtomicLong();

        public SlidingWindowReservoir()
            : this(DefaultSize) { }

        public SlidingWindowReservoir(int size)
        {
            this.values = new UserValueWrapper[size];
        }

        public void Update(long value, string userValue = null)
        {
            var newCount = this.count.Increment();
            this.values[(int)((newCount - 1) % this.values.Length)] = new UserValueWrapper(value, userValue);
        }

        public void Reset()
        {
            Array.Clear(this.values, 0, this.values.Length);
            this.count.SetValue(0L);
        }

        public Snapshot GetSnapshot(bool resetReservoir = false)
        {
            var size = Math.Min((int)this.count.GetValue(), this.values.Length);
            if (size == 0)
            {
                return new UniformSnapshot(0, Enumerable.Empty<long>());
            }

            var snapshotValues = new UserValueWrapper[size];
            Array.Copy(this.values, snapshotValues, size);

            if (resetReservoir)
            {
                Array.Clear(this.values, 0, snapshotValues.Length);
                this.count.SetValue(0L);
            }

            Array.Sort(snapshotValues, UserValueWrapper.Comparer);
            var minValue = snapshotValues[0].UserValue;
            var maxValue = snapshotValues[size - 1].UserValue;
            return new UniformSnapshot(this.count.GetValue(), snapshotValues.Select(v => v.Value), valuesAreSorted: true, minUserValue: minValue, maxUserValue: maxValue);
        }
    }

    public sealed class UniformSnapshot : Snapshot
    {
        private readonly long[] values;

        public UniformSnapshot(long count, IEnumerable<long> values, bool valuesAreSorted = false, string minUserValue = null, string maxUserValue = null)
        {
            this.Count = count;
            this.values = values.ToArray();
            if (!valuesAreSorted)
            {
                Array.Sort(this.values);
            }
            this.MinUserValue = minUserValue;
            this.MaxUserValue = maxUserValue;
        }

        public long Count { get; }

        public int Size => this.values.Length;

        public long Max => this.values.LastOrDefault();
        public long Min => this.values.FirstOrDefault();

        public string MaxUserValue { get; }
        public string MinUserValue { get; }

        public double Mean => Size == 0 ? 0.0 : this.values.Average();

        public double StdDev
        {
            get
            {
                if (this.Size <= 1)
                {
                    return 0;
                }

                var avg = this.values.Average();
                var sum = this.values.Sum(d => Math.Pow(d - avg, 2));

                return Math.Sqrt((sum) / (this.values.Length - 1));
            }
        }

        public double Median => GetValue(0.5d);
        public double Percentile75 => GetValue(0.75d);
        public double Percentile95 => GetValue(0.95d);
        public double Percentile98 => GetValue(0.98d);
        public double Percentile99 => GetValue(0.99d);
        public double Percentile999 => GetValue(0.999d);

        public IEnumerable<long> Values => this.values;

        public double GetValue(double quantile)
        {
            if (quantile < 0.0 || quantile > 1.0 || double.IsNaN(quantile))
            {
                throw new ArgumentException($"{quantile} is not in [0..1]");
            }

            if (this.Size == 0)
            {
                return 0;
            }

            var pos = quantile * (this.values.Length + 1);
            var index = (int)pos;

            if (index < 1)
            {
                return this.values[0];
            }

            if (index >= this.values.Length)
            {
                return this.values[this.values.Length - 1];
            }

            double lower = this.values[index - 1];
            double upper = this.values[index];

            return lower + (pos - Math.Floor(pos)) * (upper - lower);
        }
    }

    public sealed class UniformReservoir : Reservoir
    {
        private const int DefaultSize = 1028;

        private AtomicLong count = new AtomicLong();

        private readonly UserValueWrapper[] values;

        public UniformReservoir()
            : this(DefaultSize)
        { }

        public UniformReservoir(int size)
        {
            this.values = new UserValueWrapper[size];
        }

        public int Size => Math.Min((int)this.count.GetValue(), this.values.Length);

        public Snapshot GetSnapshot(bool resetReservoir = false)
        {
            var size = Size;
            if (size == 0)
            {
                return new UniformSnapshot(0, Enumerable.Empty<long>());
            }

            var snapshotValues = new UserValueWrapper[size];
            Array.Copy(this.values, snapshotValues, size);

            if (resetReservoir)
            {
                this.count.SetValue(0L);
            }

            Array.Sort(snapshotValues, UserValueWrapper.Comparer);
            var minValue = snapshotValues[0].UserValue;
            var maxValue = snapshotValues[size - 1].UserValue;
            return new UniformSnapshot(this.count.GetValue(), snapshotValues.Select(v => v.Value), valuesAreSorted: true, minUserValue: minValue, maxUserValue: maxValue);
        }

        public void Update(long value, string userValue = null)
        {
            var c = this.count.Increment();
            if (c <= this.values.Length)
            {
                this.values[(int)c - 1] = new UserValueWrapper(value, userValue);
            }
            else
            {
                var r = ThreadLocalRandom.NextLong(c);
                if (r < this.values.Length)
                {
                    this.values[(int)r] = new UserValueWrapper(value, userValue);
                }
            }
        }

        public void Reset()
        {
            this.count.SetValue(0L);
        }
    }

    internal sealed class HdrSnapshot : Snapshot
    {
        private readonly AbstractHistogram histogram;

        public HdrSnapshot(AbstractHistogram histogram, long minValue, string minUserValue, long maxValue, string maxUserValue)
        {
            this.histogram = histogram;
            this.Min = minValue;
            this.MinUserValue = minUserValue;
            this.Max = maxValue;
            this.MaxUserValue = maxUserValue;
        }

        public IEnumerable<long> Values
        {
            get { return this.histogram.RecordedValues().Select(v => v.getValueIteratedTo()); }
        }

        public double GetValue(double quantile)
        {
            return this.histogram.getValueAtPercentile(quantile * 100);
        }

        public long Min { get; }
        public string MinUserValue { get; }
        public long Max { get; }
        public string MaxUserValue { get; }

        public long Count => this.histogram.getTotalCount();
        public double Mean => this.histogram.getMean();
        public double StdDev => this.histogram.getStdDeviation();

        public double Median => this.histogram.getValueAtPercentile(50);
        public double Percentile75 => this.histogram.getValueAtPercentile(75);
        public double Percentile95 => this.histogram.getValueAtPercentile(95);
        public double Percentile98 => this.histogram.getValueAtPercentile(98);
        public double Percentile99 => this.histogram.getValueAtPercentile(99);
        public double Percentile999 => this.histogram.getValueAtPercentile(99.9);

        public int Size => this.histogram.getEstimatedFootprintInBytes();
    }

    /// <summary>
    /// Sampling reservoir based on HdrHistogram.
    /// Based on the java version from Marshall Pierce https://bitbucket.org/marshallpierce/hdrhistogram-metrics-reservoir/src/83a8ec568a1e?at=master
    /// </summary>
    public sealed class HdrHistogramReservoir : Reservoir
    {
        private readonly Recorder recorder;

        private readonly HdrHistogram.Histogram runningTotals;
        private HdrHistogram.Histogram intervalHistogram;

        private AtomicLong maxValue = new AtomicLong(0);
        private string maxUserValue;
        private readonly object maxValueLock = new object();

        private AtomicLong minValue = new AtomicLong(long.MaxValue);
        private string minUserValue;
        private readonly object minValueLock = new object();

        public HdrHistogramReservoir()
            : this(new Recorder(2))
        { }

        internal HdrHistogramReservoir(Recorder recorder)
        {
            this.recorder = recorder;

            this.intervalHistogram = recorder.GetIntervalHistogram();
            this.runningTotals = new HdrHistogram.Histogram(this.intervalHistogram.NumberOfSignificantValueDigits);
        }

        public void Update(long value, string userValue = null)
        {
            this.recorder.RecordValue(value);
            if (userValue != null)
            {
                TrackMinMaxUserValue(value, userValue);
            }
        }

        public Snapshot GetSnapshot(bool resetReservoir = false)
        {
            var snapshot = new HdrSnapshot(UpdateTotals(), this.minValue.GetValue(), this.minUserValue, this.maxValue.GetValue(), this.maxUserValue);
            if (resetReservoir)
            {
                this.Reset();
            }
            return snapshot;
        }

        public void Reset()
        {
            this.recorder.Reset();
            this.runningTotals.reset();
            this.intervalHistogram.reset();
        }

        private HdrHistogram.Histogram UpdateTotals()
        {
            lock (this.runningTotals)
            {
                this.intervalHistogram = this.recorder.GetIntervalHistogram(this.intervalHistogram);
                this.runningTotals.add(this.intervalHistogram);
                return this.runningTotals.copy() as HdrHistogram.Histogram;
            }
        }

        private void TrackMinMaxUserValue(long value, string userValue)
        {
            if (value > this.maxValue.NonVolatileGetValue())
            {
                SetMaxValue(value, userValue);
            }

            if (value < this.minValue.NonVolatileGetValue())
            {
                SetMinValue(value, userValue);
            }
        }

        private void SetMaxValue(long value, string userValue)
        {
            long current;
            while (value > (current = this.maxValue.GetValue()))
            {
                this.maxValue.CompareAndSwap(current, value);
            }

            if (value == current)
            {
                lock (this.maxValueLock)
                {
                    if (value == this.maxValue.GetValue())
                    {
                        this.maxUserValue = userValue;
                    }
                }
            }
        }

        private void SetMinValue(long value, string userValue)
        {
            long current;
            while (value < (current = this.minValue.GetValue()))
            {
                this.minValue.CompareAndSwap(current, value);
            }

            if (value == current)
            {
                lock (this.minValueLock)
                {
                    if (value == this.minValue.GetValue())
                    {
                        this.minUserValue = userValue;
                    }
                }
            }
        }
    }

    public struct UserValueWrapper
    {
        public static readonly UserValueWrapper Empty = new UserValueWrapper();
        public static readonly IComparer<UserValueWrapper> Comparer = new UserValueComparer();

        public readonly long Value;
        public readonly string UserValue;

        public UserValueWrapper(long value, string userValue = null)
        {
            this.Value = value;
            this.UserValue = userValue;
        }

        private class UserValueComparer : IComparer<UserValueWrapper>
        {
            public int Compare(UserValueWrapper x, UserValueWrapper y)
            {
                return Comparer<long>.Default.Compare(x.Value, y.Value);
            }
        }
    }

    public interface Reservoir
    {
        void Update(long value, string userValue = null);
        Snapshot GetSnapshot(bool resetReservoir = false);
        void Reset();
    }

    public interface Snapshot
    {
        long Count { get; }
        IEnumerable<long> Values { get; }
        double GetValue(double quantile);
        long Max { get; }
        string MaxUserValue { get; }
        double Mean { get; }
        double Median { get; }
        long Min { get; }
        string MinUserValue { get; }
        double Percentile75 { get; }
        double Percentile95 { get; }
        double Percentile98 { get; }
        double Percentile99 { get; }
        double Percentile999 { get; }
        double StdDev { get; }
        int Size { get; }
    }
}