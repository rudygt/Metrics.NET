using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Metrics.Utils
{

    /// <summary>
    /// Utility class to schedule an Action to be executed repeatedly according to the interval.
    /// </summary>
    /// <remarks>
    /// The scheduling code is inspired form Daniel Crenna's metrics port
    /// https://github.com/danielcrenna/metrics-net/blob/master/src/metrics/Reporting/ReporterBase.cs
    /// </remarks>
    public sealed class ActionScheduler : Scheduler
    {
        private CancellationTokenSource token;

        public void Start(TimeSpan interval, Action action)
        {
            Start(interval, t =>
            {
                if (!t.IsCancellationRequested)
                {
                    action();
                }
            });
        }

        public void Start(TimeSpan interval, Action<CancellationToken> action)
        {
            Start(interval, t =>
            {
                action(t);
                return Task.FromResult(true);
            });
        }

        public void Start(TimeSpan interval, Func<Task> action)
        {
            Start(interval, t => t.IsCancellationRequested ? action() : Task.FromResult(true));
        }

        public void Start(TimeSpan interval, Func<CancellationToken, Task> action)
        {
            if (interval.TotalSeconds == 0)
            {
                throw new ArgumentException("interval must be > 0 seconds", nameof(interval));
            }

            if (this.token != null)
            {
                throw new InvalidOperationException("Scheduler is already started.");
            }

            this.token = new CancellationTokenSource();

            RunScheduler(interval, action, this.token);
        }

        private static void RunScheduler(TimeSpan interval, Func<CancellationToken, Task> action, CancellationTokenSource token)
        {
            Task.Factory.StartNew(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(interval, token.Token).ConfigureAwait(false);
                        try
                        {
                            await action(token.Token).ConfigureAwait(false);
                        }
                        catch (Exception x)
                        {
                            MetricsErrorHandler.Handle(x, "Error while executing action scheduler.");
                            token.Cancel();
                        }
                    }
                    catch (TaskCanceledException) { }
                }
            }, token.Token);
        }

        public void Stop()
        {
            if (this.token != null)
            {
                token.Cancel();
            }
        }

        public void Dispose()
        {
            if (this.token != null)
            {
                this.token.Cancel();
                this.token.Dispose();
            }
        }
    }

    public abstract class Clock
    {
        private sealed class StopwatchClock : Clock
        {
            private static readonly long factor = (1000L * 1000L * 1000L) / Stopwatch.Frequency;
            public override long Nanoseconds { get { return Stopwatch.GetTimestamp() * factor; } }
            public override DateTime UTCDateTime { get { return DateTime.UtcNow; } }
        }

        private sealed class SystemClock : Clock
        {
            public override long Nanoseconds { get { return DateTime.UtcNow.Ticks * 100L; } }
            public override DateTime UTCDateTime { get { return DateTime.UtcNow; } }
        }

        public static readonly Clock SystemDateTime = new SystemClock();
        public static readonly Clock Default = new StopwatchClock();

        public abstract long Nanoseconds { get; }
        public abstract DateTime UTCDateTime { get; }

        public long Seconds { get { return TimeUnit.Nanoseconds.ToSeconds(Nanoseconds); } }

        public static string FormatTimestamp(DateTime timestamp)
        {
            return timestamp.ToString("yyyy-MM-ddTHH:mm:ss.ffffK", CultureInfo.InvariantCulture);
        }
    }
}