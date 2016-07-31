using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Metrics.Utils
{
    public class ConfigurationManager
    {
        public static IConfiguration Configuration { get; set; }
    }
}
