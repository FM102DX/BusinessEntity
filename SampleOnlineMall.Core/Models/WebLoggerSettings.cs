using SampleOnlineMall.DataAccess.Models;
using System;

namespace SampleOnlineMall.Core
{
    public class WebLoggerSettings:BaseEntity
    {
        public int RefreshPeriodMs { get; set; }

        public int ItemsPerPage { get; set; }

        public TimeSpan LogsKeepingPeriod { get; set; }

    }
}
