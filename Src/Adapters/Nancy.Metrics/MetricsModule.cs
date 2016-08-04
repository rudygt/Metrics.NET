using System;
using System.Linq;
using Metrics;
using Metrics.Json;
using Metrics.MetricData;
using Metrics.Reporters;
using Metrics.Visualization;

namespace Nancy.Metrics
{
    public class MetricsModule : NancyModule
    {
        private static ModuleConfig Config;
        private static bool healthChecksAlwaysReturnHttpStatusOk;

        public MetricsModule()
            : base(Config.ModulePath ?? "/")
        {
            if (string.IsNullOrEmpty(Config.ModulePath))
            {
                return;
            }

            if (Config.ModuleConfigAction != null)
            {
                Config.ModuleConfigAction(this);
            }

            object[] noCacheHeaders =
            {
                new {Header = "Cache-Control", Value = "no-cache, no-store, must-revalidate"},
                new {Header = "Pragma", Value = "no-cache"},
                new {Header = "Expires", Value = "0"}
            };

            Get("/", _ =>
            {
                if (!Request.Url.Path.EndsWith("/"))
                {
                    return Response.AsRedirect(Request.Url.ToString() + "/");
                }
                var gzip = AcceptsGzip();
                var response = Response.FromStream(FlotWebApp.GetAppStream(!gzip), "text/html");
                if (gzip)
                {
                    response.WithHeader("Content-Encoding", "gzip");
                }
                return response;
            });

            Get("/text", _ => Response.AsText(StringReport.RenderMetrics(Config.DataProvider.CurrentMetricsData, Config.HealthStatus))
                .WithHeaders(noCacheHeaders));

            Get("/json", _ => Response.AsText(JsonBuilderV1.BuildJson(Config.DataProvider.CurrentMetricsData), "text/json")
                .WithHeaders(noCacheHeaders));

            Get("/v2/json", _ => Response.AsText(JsonBuilderV2.BuildJson(Config.DataProvider.CurrentMetricsData), "text/json")
                .WithHeaders(noCacheHeaders));

            Get("/ping", _ => Response.AsText("pong", "text/plain")
                .WithHeaders(noCacheHeaders));

            Get("/health", _ => GetHealthStatus()
                .WithHeaders(noCacheHeaders));
        }

        internal static void Configure(MetricsDataProvider dataProvider, Func<HealthStatus> healthStatus, Action<INancyModule> moduleConfig, string metricsPath)
        {
            Config = new ModuleConfig(dataProvider, healthStatus, moduleConfig, metricsPath);
        }

        internal static void ConfigureHealthChecks(bool alwaysReturnOk)
        {
            healthChecksAlwaysReturnHttpStatusOk = alwaysReturnOk;
        }


        private bool AcceptsGzip()
        {
            return Request.Headers.AcceptEncoding.Any(e => e.Equals("gzip", StringComparison.OrdinalIgnoreCase));
        }

        private Response GetHealthStatus()
        {
            var status = Config.HealthStatus();
            var content = JsonHealthChecks.BuildJson(status);

            var response = Response.AsText(content, "application/json");
            if (!healthChecksAlwaysReturnHttpStatusOk)
            {
                response.StatusCode = status.IsHealthy ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;
            }
            else
            {
                response.StatusCode = HttpStatusCode.OK;
            }
            return response;
        }

        private struct ModuleConfig
        {
            public readonly string ModulePath;
            public readonly Action<INancyModule> ModuleConfigAction;
            public readonly MetricsDataProvider DataProvider;
            public readonly Func<HealthStatus> HealthStatus;

            public ModuleConfig(MetricsDataProvider dataProvider, Func<HealthStatus> healthStatus, Action<INancyModule> moduleConfig, string metricsPath)
            {
                DataProvider = dataProvider;
                HealthStatus = healthStatus;
                ModuleConfigAction = moduleConfig;
                ModulePath = metricsPath;
            }
        }
    }
}