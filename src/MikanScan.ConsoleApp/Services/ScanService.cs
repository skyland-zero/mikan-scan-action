using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MikanScan.ConsoleApp.Services;

public class ScanService : ITransientDependency
{
    private readonly ILogger<ScanService> _logger;
    private readonly MikanService _mikanService;
    private readonly RssService _rssService;
    private readonly IConfiguration _configuration;

    public ScanService(ILogger<ScanService> logger, MikanService mikanService, RssService rssService,
        IConfiguration configuration)
    {
        _logger = logger;
        _mikanService = mikanService;
        _rssService = rssService;
        _configuration = configuration;
    }

    public async Task ScanAsync(DateTime lastAnimeTime, DateTime lastRssTime)
    {
        _logger.LogLine();
        _logger.LogInformation("执行检测上次抓取时间");
        if (lastAnimeTime.AddHours(2) <= DateTime.Now)
        {
            try
            {
                await _mikanService.ParseHome(_configuration["BaseUrl"] ?? "");
                lastAnimeTime = DateTime.Now;
                _logger.LogInformation($"Anime抓取成功");
                _logger.LogLine();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Anime抓取出错：{e.Message}");
                _logger.LogLine();
            }
        }
        else if (lastRssTime.AddMinutes(30) <= DateTime.Now)
        {
            try
            {
                await _rssService.RefreshRssAsync();
                lastRssTime = DateTime.Now;
                _logger.LogInformation($"RSS更新成功");
                _logger.LogLine();
            }
            catch (Exception e)
            {
                _logger.LogError($"RSS更新出错：{e.Message}");
                _logger.LogLine();
            }
        }
    }
}