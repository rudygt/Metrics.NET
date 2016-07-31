using System;

namespace Metrics.Core
{
    public class RatioGauge : FunctionGauge
    {
        public RatioGauge(Func<double> numerator, Func<double> denominator)
            : base(() => numerator() / denominator())
        { }
    }
}