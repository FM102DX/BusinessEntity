using SampleOnlineMall.DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.DataAccess.Repository
{
    public class RepositoryRequest<T> 
    {
        public string Text { get; set; }
        public string ServiceName { get; set; }
        public string MessageType { get; set; }
        public int Page { get; set; }
        public int ItemsPerPage { get; set; }
        public bool UsePagination { get; set; }

        public Func<T, bool> SearchFunc { get; set; }

    }
}
