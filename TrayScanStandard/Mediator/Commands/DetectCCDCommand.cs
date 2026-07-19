using LinxUniverse.Algo.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Mediator.Queries;

namespace TrayScanStandard.Mediator.Commands
{
    public record DetectResult(
        Arr<CodeInfo> Channels
        //, string[] Imgs
        );
    public record DetectCCDCommand(BatteryTypeInfo? BatteryTypeInfo): IRequest<Either<string, DetectResult>>;
}
