using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SampleOnlineMall.Core.Appilcation
{
    public class SampleOnlineMallAssortmentApiApp
    {

        public SampleOnlineMallAssortmentApiApp() 
        {

        }

        public bool IsDocker { get; set; } = false;

        public String BaseUrl { get => "https://mallassortapi01.t109.tech/"; }
        

        public String? BaseDirectory { get => System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location); }

        public String? LogsDirectory
        {
            get
            {
                var directory = System.IO.Path.Combine(BaseDirectory, "Logs");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }
        public String? DataDirectory
        {
            get
            {
                var directory = System.IO.Path.Combine(BaseDirectory, "Data");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public String? CommodityItemImageDirectory
        {
            get
            {
                var directory = System.IO.Path.Combine(BaseDirectory, "CommodityItemImages");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public String? GetCommodityItemImageDirectoryFromGuid (Guid guid)
        {
                var directory = System.IO.Path.Combine(CommodityItemImageDirectory, $"{guid}");
                Directory.CreateDirectory(directory);
                return directory;
        }

        public String? OutputDirectory
        {
            get
            {
                var directory = System.IO.Path.Combine(BaseDirectory, "Output");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }
       
    }
}
