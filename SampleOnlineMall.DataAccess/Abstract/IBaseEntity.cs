using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SampleOnlineMall.DataAccess.Models;


namespace SampleOnlineMall.DataAccess.Abstract
{
    public interface IBaseEntity
    {
        public Guid Id { get; set; }

        public string SysMessage { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public DateTime LastModifiedDatTime { get; set; }
    }
}
