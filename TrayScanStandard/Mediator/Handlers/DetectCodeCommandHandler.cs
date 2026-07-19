using LinxUniverse.Algo.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.Mediator.Commands;
using MugenCodeDetecter;
using System.Collections.Immutable;
using VMWebAIClient;
using LinxUniverse.Utils;
using System.IO;
namespace TrayScanStandard.Mediator.Handlers
{
    public class DetectCodeCommandHandler(
        IVMWebAIClient vMWebAIClient
        ) : IRequestHandler<DetectCodeCommand, Either<string, ImmutableArray<CodeDetectResult>>>
    {
        public Task<Either<string, ImmutableArray<CodeDetectResult>>> Handle(DetectCodeCommand request, CancellationToken cancellationToken)
        {
            var data = request.Params.Select((p, i) =>
            {
                try
                {
                    var path = Path.Combine(FilenameHelper.AppPath, "Data2D", $"Detect-{FilenameHelper.FileName}-{i}.png");
                    File.WriteAllBytes(path, p.ImageByte);
                    return vMWebAIClient.DetectCodesV1Async(path, p.ROIS, cancellationToken);
                }
                catch (Exception ex)
                {
                    return Task.FromResult(Either<string, CodeDetectResult>.Left($"检测异常: {ex.Message}"));
                }
            })
                .TraverseSerial(s => s)
            .Map(s => s.Traverse(s => s).Map(s => s.ToImmutableArray()));
            return data;
        }
    }
}
