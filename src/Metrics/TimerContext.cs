using System;
using Metrics.Utils;

namespace Metrics
{
    /// <summary>
    /// This struct is meant to be returned by the timer.NewContext() method and is intended to be used inside a using statement:
    /// <code>
    /// using(timer.NewContext())
    /// {
    ///     ExecuteMethodThatNeedsMonitoring();
    /// }
    /// </code>
    /// <remarks>
    /// Double disposing the instance, or a copy of the instance (since it is a struct) will cause the timer to record wrong values.
    /// Stick to directly passing it to the using() statement.
    /// </remarks>
    /// </summary>
    public struct TimerContext : IDisposable
    {
        private readonly long _start;
        private string _userValue;
        private ITimer _timer;

        public TimerContext(ITimer timer, string userValue)
        {
            this._start = timer.StartRecording();
            this._timer = timer;
            this._userValue = userValue;
        }

        /// <summary>
        /// Set the user value for this timer context.
        /// </summary>
        /// <param name="value">New user value to use for this context.</param>
        public void TrackUserValue(string value)
        {
            this._userValue = value;
        }

        /// <summary>
        /// Provides the currently elapsed time from when the instance has been created
        /// </summary>
        public TimeSpan Elapsed
        {
            get
            {
                if (this._timer == null)
                {
                    return TimeSpan.Zero;
                }
                var milliseconds = TimeUnit.Nanoseconds.Convert(TimeUnit.Milliseconds, this._timer.CurrentTime() - this._start);
                return TimeSpan.FromMilliseconds(milliseconds);
            }
        }

        public void Dispose()
        {
            if (this._timer != null)
            {
                var end = _timer.EndRecording();
                _timer.Record(end - _start, TimeUnit.Nanoseconds, _userValue);
                this._timer = null;
            }
        }
    }
}