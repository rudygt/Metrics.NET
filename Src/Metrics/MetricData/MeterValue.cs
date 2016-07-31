using System;
using System.Collections.Generic;
using System.Linq;
using Metrics.Utils;

namespace Metrics.MetricData
{
    /// <summary>
    ///     The value reported by a Meter Metric
    /// </summary>
    public sealed class MeterValue
    {
        private static readonly SetItem[] noItems = new SetItem[0];

        public static readonly IComparer<SetItem> SetItemComparer = Comparer<SetItem>.Create((x, y) =>
        {
            var percent = Comparer<double>.Default.Compare(x.Percent, y.Percent);
            return percent == 0 ? Comparer<string>.Default.Compare(x.Item, y.Item) : percent;
        });

        public readonly long Count;
        public readonly double FifteenMinuteRate;
        public readonly double FiveMinuteRate;
        public readonly SetItem[] Items;
        public readonly double MeanRate;
        public readonly double OneMinuteRate;
        public readonly TimeUnit RateUnit;

        internal MeterValue(long count, double meanRate, double oneMinuteRate, double fiveMinuteRate, double fifteenMinuteRate, TimeUnit rateUnit)
            : this(count, meanRate, oneMinuteRate, fiveMinuteRate, fifteenMinuteRate, rateUnit, noItems)
        {
        }

        public MeterValue(long count, double meanRate, double oneMinuteRate, double fiveMinuteRate, double fifteenMinuteRate, TimeUnit rateUnit, SetItem[] items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            this.Count = count;
            this.MeanRate = meanRate;
            this.OneMinuteRate = oneMinuteRate;
            this.FiveMinuteRate = fiveMinuteRate;
            this.FifteenMinuteRate = fifteenMinuteRate;
            this.RateUnit = rateUnit;
            this.Items = items;
        }

        public MeterValue Scale(TimeUnit unit)
        {
            if (unit == this.RateUnit)
            {
                return this;
            }

            var factor = unit.ScalingFactorFor(TimeUnit.Seconds);
            return new MeterValue(this.Count,
                this.MeanRate*factor,
                this.OneMinuteRate*factor,
                this.FiveMinuteRate*factor,
                this.FifteenMinuteRate*factor,
                unit,
                this.Items.Select(i => new SetItem(i.Item, i.Percent, i.Value.Scale(unit))).ToArray());
        }

        public struct SetItem
        {
            public readonly string Item;
            public readonly double Percent;
            public readonly MeterValue Value;

            public SetItem(string item, double percent, MeterValue value)
            {
                this.Item = item;
                this.Percent = percent;
                this.Value = value;
            }
        }
    }
}