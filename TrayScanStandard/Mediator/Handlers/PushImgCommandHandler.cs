using MediatR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Service;

namespace TrayScanStandard.Mediator.Handlers
{
    internal class PushImgCommandHandler(ScanCameraService scanCameraService) : IRequestHandler<PushImgCommand>
    {
        public async Task Handle(PushImgCommand request, CancellationToken cancellationToken)
        {
            for (int i = 0; i < request.Imgs.Length && i < scanCameraService.Image2DViewModels.Length; i++)
            {
                var imgPath = request.Imgs[i];
                if (string.IsNullOrWhiteSpace(imgPath) || !File.Exists(imgPath))
                {
                    continue;
                }

                var idx = i;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    scanCameraService.Image2DViewModels[idx].ResultImg = imgPath;
                    scanCameraService.Image2DViewModels[idx].tempImg = File.ReadAllBytes(imgPath);
                    scanCameraService.Image2DViewModels[idx].Update();
                    scanCameraService.Image2DViewModels[idx].UpdateResult();
                });
            }
        }
    }
}
