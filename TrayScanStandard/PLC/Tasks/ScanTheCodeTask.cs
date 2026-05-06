using LinxUniverse.PLC.Common.Models;
using LinxUniverse.PLCProtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.Mediator.Commands.CCD;

namespace TrayScanStandard.PLC.Tasks
{
    [S7TaskDb(500, 501)]
    public class ScanTheCodeTask : CoreTask<TrayScanStandardCCDContext>
    {
        public ScanTheCodeTask (): base(1)
        {
            TaskName = "扫码任务";
            //TaskName = Properties.Resources.ScanCodeTask;
        }
        //获取PLC数据
        public override bool GetPLCData()
        {
            return base.GetPLCData(); 

        }
        //做任务
        public override async Task<bool> DoSth()
        {
            await Context.Mediator.Send(new StartDelectTaskCommand(true));

            //todo:null需要替换成实际数据
            //var res = await Context.Mediator.Send(new DelectCCDCommand());
            //if (res == null)
            //{
            //    Context.Logger.LogError("{name}任务执行失败", TaskName);
            //    return false;
            //}
            //await Context.Mediator.Send(new UploadResultCommand(res));
            return await base.DoSth();


        }
        ////写入PLC
        //protected override Task WriteDataToPlc()
        //{
        //    ///Context.Plc.WriteBytes();
        //    return base.WriteDataToPlc();
        //}
        ////清理
        //protected override Task<bool> CleanData()
        //{
        //    return base.CleanData();

        //}
    }
}
