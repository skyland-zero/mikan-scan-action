using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MikanScan.ConsoleApp.EventHandler;
using MikanScan.ConsoleApp.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MikanScan.ConsoleApp.Workers;

public class ScanHostedService : BackgroundService
{
    private readonly IAbpLazyServiceProvider _serviceProvider;
    private readonly ILogger<ScanHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILocalEventBus _eventBus;
    private readonly int _period = 10;
    private DateTime _lastAnimeTime = DateTime.MinValue;
    private DateTime _lastRssTime = DateTime.MinValue;


    public ScanHostedService(IAbpLazyServiceProvider serviceProvider, ILogger<ScanHostedService> logger,
        IConfiguration configuration, ILocalEventBus eventBus)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _eventBus = eventBus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogLine();
            _logger.LogInformation($"执行检测上次抓取时间");
            if (_lastAnimeTime.AddHours(2) <= DateTime.Now)
            {
                try
                {
                    var mikan = _serviceProvider.LazyGetRequiredService<MikanService>();
                    await _eventBus.PublishAsync(new NotifyEto
                    {
                        Title = "开始Anime抓取",
                        Content = "开始Anime抓取",
                        Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
                    });
                    await mikan.ParseHome(_configuration["BaseUrl"] ?? "", stoppingToken);
                    _lastAnimeTime = DateTime.Now;
                    _logger.LogInformation($"Anime抓取成功");
                    await _eventBus.PublishAsync(new NotifyEto
                    {
                        Title = "Anime抓取成功",
                        Content = "Anime抓取成功",
                        Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
                    });
                    _logger.LogLine();
                }
                catch (Exception e)
                {
                    _logger.LogError($"Anime抓取出错：{e.Message}");
                    _logger.LogLine();
                }
            }
            else if (_lastRssTime.AddMinutes(30) <= DateTime.Now)
            {
                try
                {
                    var rss = _serviceProvider.LazyGetRequiredService<RssService>();
                    await _eventBus.PublishAsync(new NotifyEto
                    {
                        Title = "RSS更新开始",
                        Content = "RSS更新开始",
                        Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
                    });
                    await rss.RefreshRssAsync(stoppingToken);
                    _lastRssTime = DateTime.Now;
                    _logger.LogInformation($"RSS更新成功");
                    await _eventBus.PublishAsync(new NotifyEto
                    {
                        Title = "RSS更新成功",
                        Content = "RSS更新成功",
                        Poster = "https://mikan.skylandone.asia/images/mikan-pic.png"
                    });
                    _logger.LogLine();
                }
                catch (Exception e)
                {
                    _logger.LogError($"RSS更新出错：{e.Message}");
                    _logger.LogLine();
                }
            }

            await Task.Delay(_period * 1000, stoppingToken);
        }
    }
}