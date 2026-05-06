using Quartz;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.Jobs
{
    internal class HourJobs(MainViewModel mainViewModel) : IJob
    {
        /// <summary>
        /// 每日执行任务
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine("11111111111111");


            //检查WCS是否启用
            if (!mainViewModel.IsWcsEnable) return Task.CompletedTask;

            var files = new DirectoryInfo("Data2D").GetFiles().OrderByDescending(s => s.CreationTime).ToArray();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];

                // 超过保留天数
                bool isTooOld = DateTime.Now - file.CreationTime > TimeSpan.FromDays(MainStorage.Saves.LogDeleteDay);

                // 排名500以后
                bool isBeyondCountLimit = i >= 500;

                // 满足任一条件就删除
                if (isTooOld || isBeyondCountLimit)
                {
                    File.Delete(file.FullName);
                }
            }


            //foreach (var file in files.Skip(50))
            //{
            //    File.Delete(file.FullName);

            //}
            //foreach (var file in files.Take(50))
            //{
            //    if (DateTime.Now - file.CreationTime > TimeSpan.FromDays(MainStorage.Saves.LogDeleteDay))
            //    {
            //        File.Delete(file.FullName);
            //    }
            //}


            var dirs = new DirectoryInfo("DataFrame").GetDirectories().OrderByDescending(s => s.CreationTime).ToArray();
            foreach (var dir in dirs.Skip(500))
            {
             
                    Directory.Delete(dir.FullName, true);
            }
            foreach (var dir in dirs.Take(500))
            {
                if (DateTime.Now - dir.CreationTime > TimeSpan.FromDays(MainStorage.Saves.LogDeleteDay))
                {
                    Directory.Delete(dir.FullName, true);
                }
            }
            return Task.CompletedTask;
        }
    }
}
