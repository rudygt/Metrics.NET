using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Metrics.Logging;

namespace Metrics
{
    public class MetricsErrorHandler
    {
        private static readonly ILog log = LogProvider.GetLogger(typeof(MetricsErrorHandler));

        private static readonly Meter ErrorMeter = null;//TODO: initialize withthis -> Metric.Internal.Meter("Metrics Errors", Unit.Errors);

        private static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;

        private readonly ConcurrentBag<Action<Exception, string>> _handlers = new ConcurrentBag<Action<Exception, string>>();

        private MetricsErrorHandler()
        {
            AddHandler((x, msg) => log.ErrorException("Metrics: Unhandled exception in Metrics.NET Library {0} {1}", x, msg, x.Message));
            AddHandler((x, msg) => Trace.TraceError("Metrics: Unhandled exception in Metrics.NET Library " + x.ToString()));
            AddHandler((x, msg) => Console.WriteLine("Metrics: Unhandled exception in Metrics.NET Library {0} {1}", msg, x.ToString()));
        }

        internal static MetricsErrorHandler Handler { get; } = new MetricsErrorHandler();

        internal void AddHandler(Action<Exception> handler)
        {
            AddHandler((x, msg) => handler(x));
        }

        internal void AddHandler(Action<Exception, string> handler)
        {
            _handlers.Add(handler);
        }

        internal void ClearHandlers()
        {
            while (!_handlers.IsEmpty)
            {
                Action<Exception, string> item;
                _handlers.TryTake(out item);
            }
        }

        private void InternalHandle(Exception exception, string message)
        {
            ErrorMeter.Mark();

            foreach (var handler in _handlers)
            {
                try
                {
                    handler(exception, message);
                }
                catch
                {
                    // error handler throw-ed on us, hope you have a debugger attached.
                }
            }
        }

        public static void Handle(Exception exception)
        {
            Handle(exception, string.Empty);
        }

        public static void Handle(Exception exception, string message)
        {
            Handler.InternalHandle(exception, message);
        }
    }
}