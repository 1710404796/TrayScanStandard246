using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrayScanStandard.Service
{
    public interface IWcsResponse
    {
        int ResponseCode { get; }
        string? ResponseMessage { get; }
    }
}
