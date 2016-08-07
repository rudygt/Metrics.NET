using System;
using System.IO;
using Metrics.ElasticSearch;
using Metrics.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Nancy;
using Nancy.Metrics;
using Nancy.Owin;

namespace Metrics.TestConsole
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseUrls("http://*:5000")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }

    public class HelloModule : NancyModule
    {
        public HelloModule()
        {
            Get("/", parameters => "Hello World");
        }
    }

    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .SetBasePath(env.ContentRootPath);

            Configuration = builder.Build();

            ConfigurationManager.Configuration = Configuration;
        }

        public IConfiguration Configuration { get; set; }

        public void Configure(IApplicationBuilder app)
        {
            var config = Configuration;
            var appConfig = new AppConfiguration();
            config.Bind(appConfig);

            app.UseOwin(x => x.UseNancy(opt => opt.Bootstrapper = new DemoBootstrapper(appConfig)));
        }
    }

    public class AppConfiguration
    {
        public Logging Logging { get; set; }
        public Smtp Smtp { get; set; }
    }

    public class LogLevel
    {
        public string Default { get; set; }
        public string System { get; set; }
        public string Microsoft { get; set; }
    }

    public class Logging
    {
        public bool IncludeScopes { get; set; }
        public LogLevel LogLevel { get; set; }
    }

    public class Smtp
    {
        public string Server { get; set; }
        public string User { get; set; }
        public string Pass { get; set; }
        public string Port { get; set; }
    }

    public class DemoBootstrapper : DefaultNancyBootstrapper
    {
        public DemoBootstrapper()
        {
        }

        public DemoBootstrapper(AppConfiguration appConfig)
        {
            /*
            We could register appConfig as an instance in the container which can
            be injected into areas that need it or we could create our own INancyEnvironment
            extension and use that.
            */
            Console.WriteLine(appConfig.Smtp.Server);
            Console.WriteLine(appConfig.Smtp.User);
            Console.WriteLine(appConfig.Logging.IncludeScopes);

            Metric.Config.WithNancy(ApplicationPipelines).WithReporting(config => config.WithConsoleReport(TimeSpan.FromSeconds(30)).WithElasticSearch(new ElasticReportsConfig() {Host = "127.0.0.1", Port = 9200, Index = "sample", RollingIndexType = RollingIndexType.Daily}, TimeSpan.FromSeconds(30)));
        }
    }
}