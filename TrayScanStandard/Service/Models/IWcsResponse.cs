using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TrayScanStandard.Service.Models
{
    public interface IWcsResponse
    {
        int ResponseCode { get; }
        string? ResponseMessage { get; }
    }

    public interface ILinxResponse
    {
        bool Success { get; }
        string? Message { get; }
    }
}
