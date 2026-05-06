using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrayScanStandard.Service.Models
{
    /// <summary>
    /// 电芯条码
    /// </summary>
    public sealed class CellCodeDateRequest
    {

        public string SystemCode { get; set; } = MainStorage.Saves.SystemCode;

        public List<CellData> Data { get; set; } = [];

        public sealed class CellData
        {
            public string CellCode { get; set; }

            public string CcdCode { get; set; }

            public int Channel { get; set; }
        }
    }

    public sealed class CellCodeDateResponse : IWcsResponse
    {
        public int ResponseCode { get; set; }


        public string? ResponseMessage { get; set; }
    }
}
