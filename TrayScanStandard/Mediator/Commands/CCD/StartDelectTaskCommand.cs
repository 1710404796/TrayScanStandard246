using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrayScanStandard.Mediator.Commands.CCD
{
    public record StartDelectTaskCommand(bool start) : IRequest;
}
