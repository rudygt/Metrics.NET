using System;

namespace Metrics.Utils
{
    public static class DateTimeExtensions
    {
        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime().ToUniversalTime();

        public static long ToUnixTime(this DateTime date)
        {
            return Convert.ToInt64((date.ToUniversalTime() - unixEpoch).TotalSeconds);
        }
    }
}