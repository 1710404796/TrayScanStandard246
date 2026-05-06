using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.Service;

namespace TrayScanStandard.Models
{
    public sealed class HeartbeatsRequest
    {
        public string SystemCode { get; set; } = MainStorage.Saves.SystemCode;

        public List<string> Heartbeat { get; set; } = ["ccd1", "ccd2"];
    }
    public sealed class HeartbeatsResponse : IWcsResponse
    {
        public int ResponseCode { get; set; }


        public string? ResponseMessage { get; set; }
    }
}
