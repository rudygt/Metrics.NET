using Metrics;
using Metrics.Utils;
using Nancy.Bootstrapper;

namespace Nancy.Metrics
{
    public class NancyGlobalMetrics : IHideObjectMembers
    {
        private const string RequestStartTimeKey = "__Metrics.RequestStartTime__";

        private static MetricsContext nancyGlobalMetricsContext;

        public static MetricsContext NancyGlobalMetricsContext
        {
            get
            {
                return nancyGlobalMetricsContext ?? Metric.Context("NancyFx");
            }
        }

        private readonly MetricsContext context;
        private readonly IPipelines nancyPipelines;

        public NancyGlobalMetrics(MetricsContext context, IPipelines nancyPipelines)
        {
            this.nancyPipelines = nancyPipelines;
            this.context = context;
            nancyGlobalMetricsContext = context;
        }

        /// <summary>
        /// Registers a Meter metric named "NancyFx.Errors" that records the rate at witch unhanded errors occurred while 
        /// processing Nancy requests.
        /// Registers a Timer metric named "NancyFx.Requests" that records how many requests per second are handled and also
        /// keeps a histogram of the request duration.
        /// Registers a counter for the number of active requests.
        /// Registers a histogram for the size of POST, PUT and PATCH requests.
        /// Registers a timer metric for each non-error request.
        /// </summary>
        public NancyGlobalMetrics WithAllMetrics()
        {
            return WithRequestTimer()
                .WithErrorsMeter()
                .WithActiveRequestCounter()
                .WithPostPutAndPatchRequestSizeHistogram()
                .WithTimerForEachRequest();
        }

        /// <summary>
        /// Registers a Timer metric named "NancyFx.Requests" that records how many requests per second are handled and also
        /// keeps a histogram of the request duration.
        /// </summary>
        /// <param name="metricName">Name of the metric.</param>
        public NancyGlobalMetrics WithRequestTimer(string metricName = "Requests")
        {
            var requestTimer = this.context.Timer(metricName, Unit.Requests);

            this.nancyPipelines.BeforeRequest.AddItemToStartOfPipeline(ctx =>
            {
                ctx.Items[RequestStartTimeKey] = requestTimer.StartRecording();
                return null;
            });

            this.nancyPipelines.AfterRequest.AddItemToEndOfPipeline(ctx =>
            {
                object timer;
                if (ctx.Items.TryGetValue(RequestStartTimeKey, out timer))
                {
                    if (timer is long)
                    {
                        var startTime = (long)timer;
                        var endTime = requestTimer.EndRecording();
                        requestTimer.Record(endTime - startTime, TimeUnit.Nanoseconds);
                    }
                    ctx.Items.Remove(RequestStartTimeKey);
                }
            });

            return this;
        }

        /// <summary>
        /// Registers a Meter metric named "NancyFx.Errors" that records the rate at witch unhanded errors occurred while 
        /// processing Nancy requests.
        /// </summary>
        /// <param name="metricName">Name of the metric.</param>
        public NancyGlobalMetrics WithErrorsMeter(string metricName = "Errors")
        {
            var errorMeter = this.context.Meter(metricName, Unit.Errors, TimeUnit.Seconds);

            this.nancyPipelines.OnError.AddItemToStartOfPipeline((ctx, ex) =>
            {
                errorMeter.Mark();
                return null;
            });

            return this;
        }

        /// <summary>
        /// Registers a Counter metric named "NancyFx.ActiveRequests" that shows the current number of active requests
        /// </summary>
        /// <param name="metricName">Name of the metric.</param>
        public NancyGlobalMetrics WithActiveRequestCounter(string metricName = "Active Requests")
        {
            var counter = this.context.Counter(metricName, Unit.Custom("ActiveRequests"));

            this.nancyPipelines.BeforeRequest.AddItemToStartOfPipeline(ctx =>
            {
                counter.Increment();
                return null;
            });

            this.nancyPipelines.AfterRequest.AddItemToEndOfPipeline(ctx =>
            {
                counter.Decrement();
            });

            return this;
        }

        /// <summary>
        /// Register a Histogram metric named "Nancy.PostPutAndPatchRequestsSize" on the size of the POST, PUT, and PATCH requests
        /// </summary>
        /// <param name="metricName">Name of the metric.</param>
        public NancyGlobalMetrics WithPostPutAndPatchRequestSizeHistogram(string metricName = "Post, Put & Patch Request Size")
        {
            var histogram = this.context.Histogram(metricName, Unit.Bytes);

            this.nancyPipelines.BeforeRequest.AddItemToStartOfPipeline(ctx =>
            {
                var method = ctx.Request.Method.ToUpper();
                if (method == "POST" || method == "PUT" || method == "PATCH")
                {
                    histogram.Update(ctx.Request.Headers.ContentLength);
                }
                return null;
            });

            return this;
        }

        /// <summary>
        /// Registers a timer for each request.
        /// Timer is created based on route and will be named:
        /// [NancyFx] {HTTP_METHOD_NAME} {ROUTE_PATH}
        /// </summary>
        public NancyGlobalMetrics WithTimerForEachRequest()
        {
            this.nancyPipelines.BeforeRequest.AddItemToStartOfPipeline(ctx =>
            {
                ctx.Items["RequestStartTimeKey"] = Clock.Default.Nanoseconds;
                return null;
            });

            this.nancyPipelines.AfterRequest.AddItemToEndOfPipeline(ctx =>
            {
                if (ctx.ResolvedRoute != null && !(ctx.ResolvedRoute is Routing.NotFoundRoute))
                {
                    string name = string.Format("{0} {1}", ctx.ResolvedRoute.Description.Method, ctx.ResolvedRoute.Description.Path);
                    var startTime = (long)ctx.Items["RequestStartTimeKey"];
                    var elapsed = Clock.Default.Nanoseconds - startTime;
                    this.context.Timer(name, Unit.Requests)
                        .Record(elapsed, TimeUnit.Nanoseconds);
                }
            });

            return this;
        }
    }
}