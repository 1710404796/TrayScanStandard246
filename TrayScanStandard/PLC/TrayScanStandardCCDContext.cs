using LinxUniverse.PLC.Common.Interface;
using LinxUniverse.PLC.Meditor.Queries;
using LinxUniverse.PLCProtos;
using MediatR;
using Microsoft.Extensions.Logging;
using S7.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrayScanStandard.PLC
{
    public class TrayScanStandardCCDContext(IMediator mediator,ILogger<TrayScanStandardCCDContext> logger) : IPLCContext
    {
        public string PlcGuid { get; set; }
        public Plc Plc { get; set; }

        public ITaskCodeDelectDB CodeDelectDB => throw new NotImplementedException();

        public ITaskCodeFeedbackDB CodeFeedbackDB => throw new NotImplementedException();

        public ITaskCodeDelectDB PCCodeDelectDB => throw new NotImplementedException();

        public ITaskCodeFeedbackDB PCCodeFeedbackDB => throw new NotImplementedException();

        public DB500 DB500 { get; set; } = new ();
        public DB501 DB501 { get; set; } = new ();

        public IMediator Mediator { get; } = mediator;
        public ILogger<TrayScanStandardCCDContext> Logger { get; } = logger;
        public ITaskCodeDelectDB GetCodeDelectDB(int idx) => idx switch
        {   
            500 => DB500,
            _=>DB500,   
        };

        public ITaskCodeFeedbackDB GetCodeFeedbackDB(int idx) => idx switch
        {
            501 => DB501,
            _ => DB501,
        };

        public Task Loop()
        {
            return Task.CompletedTask;
            throw new NotImplementedException();
        }

        public async Task<bool> UpdateData()
        {
            //return Task.FromResult(true);
            //throw new NotImplementedException();
            var flag = true;
            if (flag)
                flag &= await Mediator.Send(new S7ReadDataBlockQuery<DB500>(PlcGuid, DB500));
            if (flag)
                flag &= await Mediator.Send(new S7ReadDataBlockQuery<DB501>(PlcGuid, DB501));
            return flag;
        }
    }
}
