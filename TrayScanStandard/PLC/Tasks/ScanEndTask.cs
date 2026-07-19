using LinxUniverse.PLC.Common.Models;
using LinxUniverse.PLCProtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.Mediator.Commands.CCD;

namespace TrayScanStandard.PLC.Tasks
{
    [S7TaskDb(500, 501)]
    public class ScanEndTask : CoreTask<TrayScanStandardCCDContext>
    {
        public ScanEndTask() : base(2)
        {
            TaskName = "托盘离开";
            //TaskName = Properties.Resources.Trayleaving;
        }

        public override async Task<bool> DoSth()
        {
            // 请阻止结果继续上传
            await Context.Mediator.Send(new StartDelectTaskCommand(false));

            return await base.DoSth();

        }
    }
}
