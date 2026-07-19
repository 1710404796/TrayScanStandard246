using Camera.Fs.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrayScanStandard.Mediator.Commands
{
    public record CaptureInfo( 
        MugenCamera.MugenCamera Camera
        , int[] Exps
        );
    // 要有单相机拍照的最好 null
    public record CamCaptureCommand(Option< CaptureInfo>[] CaptureInfos) : IRequest<Either<string, IEnumerable<Image2DResult[]>>>;
}
