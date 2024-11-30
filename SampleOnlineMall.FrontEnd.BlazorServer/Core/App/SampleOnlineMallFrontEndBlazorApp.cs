using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SampleOnlineMall.FrontEnd.BlazorServer
{
    public class SampleOnlineMallFrontEndBlazorApp
    {

        public SampleOnlineMallFrontEndBlazorApp() 
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
