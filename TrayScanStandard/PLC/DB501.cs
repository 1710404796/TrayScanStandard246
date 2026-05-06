using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinxUniverse.PLC.Common.Interface;
using LinxUniverse.PLC.Common.Models;

namespace TrayScanStandard.PLC
{
    /// <summary>
    /// CCD扫码完成信号反馈
    /// </summary>
    public class DB501: DataStruct,ITaskCodeFeedbackDB
    {
        public DB501()
        {
            Db = 501;
            Length = 1;
        }
        [S7Offset(0)]
        public byte ToFeedback { get; set; }

        public byte ToFeedbacks(int idx) => ToFeedback;
    }
}
