using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Service
{
    public class CommonOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";

        public object? ReturningValue { get; set; }
        public static CommonOperationResult GetInstance(bool success, string msg, object returningValue = null)
        {
            CommonOperationResult c = new CommonOperationResult()
            {
                Message = msg,
                ReturningValue = returningValue,
                Success = success
            };
            return c;
        }

        public static CommonOperationResult SayFail(string _msg = "") { return GetInstance(false, _msg, null); }
        public static CommonOperationResult SayOk(string _msg = "") { return GetInstance(true, _msg, null); }
        public static CommonOperationResult SayItsNull(string _msg = "") { return GetInstance(true, _msg, null); }
        public string AsShrotString() => $"Operation resilt: success={Success} message={Message}";

        public Guid ReturningGuid { get; set; }
    }
}


