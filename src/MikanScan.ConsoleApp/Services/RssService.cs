using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mikan_scan.Dto;
using MikanScan.ConsoleApp.Entities;
using MikanScan.ConsoleApp.EventHandler;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;

namespace MikanScan.ConsoleApp.Services;

public class RssService : ITransientDependency
{

    private readonly ILogger<RssService> _logger;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IConfiguration _configuration;
    private readonly BittorrentApiClient _bittorrentApiClient;
    private readonly ILocalEventBus _localEventBus;
    public RssService(ILogger<RssService> logger, IGuidGenerator guidGenerator, IConfiguration configuration, BittorrentApiClient bittorrentApiClient, ILocalEventBus localEventBus)
    {
        _logger = logger;
        _guidGenerator = guidGenerator;
        _configuration = configuration;
        _bittorrentApiClient = bittorrentApiClient;
        _localEventBus = localEventBus;
    }

    public async Task RefreshRssAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始刷新RSS");
        var sql = $"""SELECT * FROM Anime""";
        await using var cnn = new SqliteConnection(_configuration["ConnectionString"]);
        var list = await cnn.QueryAsync<Anime>(sql);
        foreach (var item in list)
        {
            if (cancellationToken.IsCancellationRequested) break;
            _logger.LogLine();
            _logger.LogInformation($"开始处理{item.Title}的RSS");
            try
            {
                var rss = await ParseRssAsync($"{_configuration["BaseUrl"]}{item.Rss}", cancellationToken);
                await ProcessRssAsync(item, rss, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogInformation($"处理{item.Title}的RSS失败：{e.Message}");
            }

            await Task.Delay(Random.Shared.Next(500, 1000), cancellationToken);
        }
    }

    private Task<List<RssModel>> ParseRssAsync(string url, CancellationToken cancellationToken = default)
    {
        var xml = XDocument.Load(url);
        var root = xml.Root;
        var list = new List<RssModel>();
        if (root == null)
        {
            return Task.FromResult(list);
        }

        var channel = root.Elements().FirstOrDefault(x => x.Name == "channel");
        if (channel == null)
        {
            return Task.FromResult(list);
        }

        var items = channel.Elements().Where(x => x.Name == "item").ToList();

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var title = item.Element("title")?.Value;
            //排除合集
            if (title != null && title.Contains("合集"))
            {
                continue;
            }

            var link = item.Element("enclosure")?.Attribute("url")?.Value;
            if (title == null || link == null)
            {
                continue;
            }

            list.Add(new RssModel { Title = title, Link = link });
        }

        //如果有简体和1080的数据，只要简体和1080的，否则就要全部
        var temps = list.FindAll(a => a.Title.Contains("简") && !a.Title.Contains("繁") && a.Title.Contains("1080"));
        temps = temps.Count != 0 ? temps : list.FindAll(a => a.Title.Contains("简") && a.Title.Contains("1080"));
        temps = temps.Count != 0 ? temps : list.FindAll(a => a.Title.Contains("CHS", StringComparison.InvariantCultureIgnoreCase) && a.Title.Contains("1080"));
        temps = temps.Count != 0 ? temps : list.FindAll(a => a.Title.Contains("1080"));
        if (temps.Count != 0)
        {
            list = temps;
        }

        _logger.LogInformation("已解析RSS");
        return Task.FromResult(list);
    }

    private async Task ProcessRssAsync(Anime anime, List<RssModel> rss, CancellationToken cancellationToken = default)
    {
        var sql = $"""SELECT * FROM AnimeRss WHERE AnimeId = @AnimeId""";
        await using var cnn = new SqliteConnection(_configuration["ConnectionString"]);
        var entities = await cnn.QueryAsync<AnimeRss>(sql, new { AnimeId = anime.Id });
        //筛选需要添加的数据
        var add = rss.Where(x => entities.All(y => y.Title != x.Title));
        if (add.Any())
        {
            var insertSql = $"""
                             INSERT INTO  AnimeRss(Id, AnimeId, Title, Link, LastUpdateTime)
                             VALUES (@Id, @AnimeId, @Title, @Link, @LastUpdateTime)
                             """;

            foreach (var info in add)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var entity = new AnimeRss
                {
                    Id = _guidGenerator.Create().ToString(),
                    AnimeId = anime.Id,
                    Title = info.Title,
                    Link = info.Link,
                    LastUpdateTime = DateTime.Now
                };
                if (_configuration.GetValue<bool>("Debug") is false)
                {
                    var success = await _bittorrentApiClient.AddTorrentAsync(entity.Link, $"{_configuration["QbBaseDir"]}{anime.Title}",
                        $"{_configuration["QbBaseCategory"]}{anime.Title}");
                    if (success)
                    {
                        await cnn.ExecuteAsync(insertSql, entity);
                        _logger.LogInformation($"{anime.Title}: {entity.Title} 已加入下载队列", true);
                        await _localEventBus.PublishAsync(new NotifyEto
                        {
                            Title = "加入队列成功",
                            Content = $"{anime.Title}: {entity.Title} 已加入下载队列",
                            Poster = anime.PosterUrl
                        });

                    }
                    else
                    {
                        _logger.LogError($"{anime.Title}: {entity.Title} 加入下载队列失败", true);
                        await _localEventBus.PublishAsync(new NotifyEto
                        {
                            Title = "加入队列失败",
                            Content = $"{anime.Title}: {entity.Title} 加入下载队列失败",
                            Poster = anime.PosterUrl
                        });
                    }
                }
                else
                {
                    await cnn.ExecuteAsync(insertSql, entity);
                }
            }
        }

        _logger.LogInformation($"{anime.Title}:已处理RSS，更新{add.Count()}条");
    }
}