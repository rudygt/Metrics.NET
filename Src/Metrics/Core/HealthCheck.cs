using System;

namespace Metrics.Core
{
    public class HealthCheck
    {
        private readonly Func<HealthCheckResult> _check;

        protected HealthCheck(string name)
            : this(name, () => { })
        {
        }

        public HealthCheck(string name, Action check)
            : this(name, () =>
            {
                check();
                return string.Empty;
            })
        {
        }

        public HealthCheck(string name, Func<string> check)
            : this(name, () => HealthCheckResult.Healthy(check()))
        {
        }

        public HealthCheck(string name, Func<HealthCheckResult> check)
        {
            Name = name;
            _check = check;
        }

        public string Name { get; }

        protected virtual HealthCheckResult Check()
        {
            return _check();
        }

        public Result Execute()
        {
            try
            {
                return new Result(Name, Check());
            }
            catch (Exception x)
            {
                return new Result(Name, HealthCheckResult.Unhealthy(x));
            }
        }

        public struct Result
        {
            public readonly string Name;
            public readonly HealthCheckResult Check;

            public Result(string name, HealthCheckResult check)
            {
                Name = name;
                Check = check;
            }
        }
    }
}