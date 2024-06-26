using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MikanScan.ConsoleApp.EventHandler;
using MikanScan.ConsoleApp.Services;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;

namespace MikanScan.ConsoleApp;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpGuidsModule),
    typeof(AbpEventBusModule)
)]
public class ConsoleAppModule : AbpModule
{
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var logger = context.ServiceProvider.GetRequiredService<ILogger<ConsoleAppModule>>();
        var hostEnvironment = context.ServiceProvider.GetRequiredService<IHostEnvironment>();
        logger.LogInformation($"EnvironmentName => {hostEnvironment.EnvironmentName}");

        //初始化数据表
        var seeder = context.ServiceProvider.GetRequiredService<DataSeedService>();
        await seeder.InitDataBaseAsync();
        await seeder.UpdateDatabaseAsync();

        // var pusher = context.ServiceProvider.GetRequiredService<ILocalEventBus>();
        // await pusher.PublishAsync(new NotifyEto
        // {
        //     Title = "加入队列成功",
        //     Content = "侦探: 第一集 已加入下载队列"
        // });
        
        
        // var notify = context.ServiceProvider.GetRequiredService<ILocalEventBus>();
        // await notify.PublishAsync(new NotifyEto
        // {
        //     Title = "加入队列成功",
        //     Content = "秘密的偶像公主: [LoliHouse] 秘密的偶像公主 / Himitsu no AiPri - 10 [WebRip 1080p HEVC-10bit AAC][无字幕] 已加入下载队列",
        //     Poster = "https://mikan.skylandone.asia/images/Bangumi/202304/1d84a9bf.jpg"
        // });

    }
}
