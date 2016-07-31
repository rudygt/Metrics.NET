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
}