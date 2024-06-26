using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
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


        var runType = configuration.GetSection("RunType").Get<int>();
        logger.LogInformation(runType.ToString());
        if (runType == 1)
        {
            await ParseHomeAsync(context.ServiceProvider, configuration, logger);
        }
        else
        {
            await ParseRssAsync(context.ServiceProvider, configuration, logger);
        }
        

        var lifetime = context.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
        lifetime.StopApplication();
    }

    public async Task ParseHomeAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        var mikan = serviceProvider.GetRequiredService<MikanService>();
        var eventBus = serviceProvider.GetRequiredService<ILocalEventBus>();

        await eventBus.PublishAsync(new NotifyEto
        {
            Title = "开始Anime抓取",
            Content = "开始Anime抓取",
            Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
        });
        await mikan.ParseHome(configuration["BaseUrl"] ?? "");
        logger.LogInformation($"Anime抓取成功");
        await eventBus.PublishAsync(new NotifyEto
        {
            Title = "Anime抓取成功",
            Content = "Anime抓取成功",
            Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
        });
        logger.LogLine();
    }

    public async Task ParseRssAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        var rss = serviceProvider.GetRequiredService<RssService>();
        var eventBus = serviceProvider.GetRequiredService<ILocalEventBus>();
        await eventBus.PublishAsync(new NotifyEto
        {
            Title = "RSS更新开始",
            Content = "RSS更新开始",
            Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
        });
        await rss.RefreshRssAsync();
        logger.LogInformation($"RSS更新成功");
        await eventBus.PublishAsync(new NotifyEto
        {
            Title = "RSS更新成功",
            Content = "RSS更新成功",
            Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
        });
        logger.LogLine();
    }
}