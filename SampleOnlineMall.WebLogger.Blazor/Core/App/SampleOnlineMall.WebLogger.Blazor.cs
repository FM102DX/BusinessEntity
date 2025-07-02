using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebLogger.Blazor.Core.App
{
    public class SampleOnlineMallWebLoggerBlazorApp
    {

        public SampleOnlineMallWebLoggerBlazorApp() 
        {
            
        }

        public String? BaseDirectory 
        {
            get
            {
                var directory = Directory.GetCurrentDirectory();
                return directory;
            }
        }

        public String? LogsDirectory
        {
            get
            {
                var directory = System.IO.Path.Combine(BaseDirectory, "Logs");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }
    }
}
