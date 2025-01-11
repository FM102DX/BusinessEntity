using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleOnlineMall.Service.WebLogging
{
    public class WebLoggerLocalSettings
    {
        public string HostAliasWhenDocker { get; set; }
        public string HostAliasWhenIISExpress { get; set; }
        public string ServiceCode { get; set; }
    }
}
