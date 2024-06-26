using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HtmlAgilityPack;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MikanScan.ConsoleApp.Entities;
using MikanScan.ConsoleApp.Models;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace MikanScan.ConsoleApp.Services;

public class MikanService : ITransientDependency
{
    private readonly string[] _betterSubgroups;
    private readonly string[] _excludeAnime;
    private readonly ILogger<MikanService> _logger;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IConfiguration _configuration;

    public MikanService(ILogger<MikanService> logger, IGuidGenerator guidGenerator, IConfiguration configuration)
    {
        _logger = logger;
        _guidGenerator = guidGenerator;
        _configuration = configuration;
        _betterSubgroups = configuration.GetSection("BetterSubgroups").Get<string[]>() ?? [];
        _excludeAnime = configuration.GetSection("ExcludeAnime").Get<string[]>() ?? [];
    }

    /// <summary>
    /// 解析首页数据
    /// </summary>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    public async Task ParseHome(string url, CancellationToken cancellationToken = default)
    {
        var web = new HtmlWeb
        {
            OverrideEncoding = Encoding.UTF8
        };
        _logger.LogInformation("开始加载网页");
        var doc = web.Load(url);
        _logger.LogInformation("网页加载结束");


        var ul = doc.DocumentNode.SelectNodes("//ul[@class='list-inline an-ul']");
        foreach (var items in ul)
        {
            if (cancellationToken.IsCancellationRequested) break;
            foreach (var li in items.SelectNodes("./li"))
            {
                if (cancellationToken.IsCancellationRequested) break;
                var content = li.SelectSingleNode(".//a[@class='an-text']");
                if (content == null)
                {
                    continue;
                }

                var name = WebUtility.HtmlDecode(content.Attributes["title"].Value ?? "").Trim();
                var detailUrl = content.Attributes["href"]?.Value;
                if (string.IsNullOrWhiteSpace(name) || detailUrl == null)
                {
                    _logger.LogError("名称或url解析失败，解析下一个");
                    continue;
                }

                if (_excludeAnime.Any(x => name.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                {
                    _logger.LogInformation($"{name}已被排除，解析下一个");
                    continue;
                }

                _logger.LogLine();
                _logger.LogInformation("开始解析： " + name + " 详情页：" + detailUrl);
                try
                {
                    await ParseDetail($"{_configuration["BaseUrl"]}{detailUrl}", name, cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"{name} 详情解析失败: {e.Message}");
                }

                await Task.Delay(Random.Shared.Next(500, 1200), cancellationToken);
            }
        }

        //请求前一个季度数据
        // var next = doc.DocumentNode.SelectNodes("//ul[@class='dropdown-menu']//ul//li//a");
        // if (next != null && next.Count >= 2)
        // {
        //     var nextInfo = next[1];
        //     var year = nextInfo.Attributes["data-year"]?.Value;
        //     var season = nextInfo.Attributes["data-season"]?.Value;
        //     if (year != null && season != null)
        //     {
        //         var nextUrl = $"{ConfigTool.GetValue("BaseUrl")}/Home/BangumiCoverFlowByDayOfWeek?year={year}&seasonStr={WebUtility.HtmlDecode(season)}";
        //         MyLogger.Line();
        //         _logger.LogInformation("请求前一个季度的数据： " + nextUrl);
        //         await ParseHome(nextUrl);
        //     }
        // }
    }

    /// <summary>
    /// 解析详情数据
    /// </summary>
    /// <param name="url"></param>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    private async Task ParseDetail(string url, string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"{name}:开始解析详情");
        var web = new HtmlWeb
        {
            OverrideEncoding = Encoding.UTF8
        };
        var doc = web.Load(url);

        var sql = $"""SELECT * FROM Anime WHERE Title = @Title""";
        await using var cnn = new SqliteConnection(_configuration["ConnectionString"]);
        var entity = await cnn.QueryFirstOrDefaultAsync<Anime>(sql, new { Title = name });

        var subgroup = ParseDetailSubgroup(doc, entity);
        if (subgroup == null)
        {
            _logger.LogError("未找到字幕组信息");
            return;
        }

        var bgmtvNode = doc.DocumentNode.SelectNodes("//a[@class='w-other-c']/@href").FirstOrDefault(x => x.InnerText.Contains("bgm.tv"));
        var posterNode = doc.DocumentNode.SelectSingleNode("//div[@class='bangumi-poster']");
        var poster = Regex.Match(posterNode.GetAttributeValue("style", ""), @"(?<=url\()(.*)(?=\))").Groups[1].Value?.Replace("'", "");
        var posterUrl = $"{_configuration["BaseUrl"]}{poster?.Split("?")[0]}";

        _logger.LogInformation($"{name}:已解析到字幕组信息，当前字幕组为：{subgroup.Name}");
        await SaveDetailAsync(name, subgroup, bgmtvNode?.InnerText, posterUrl, cancellationToken);
    }

    /// <summary>
    /// 保存详情信息
    /// </summary>
    /// <param name="name"></param>
    /// <param name="subgroup"></param>
    /// <param name="bgmTvUrl"></param>
    /// <param name="poster"></param>
    /// <param name="cancellationToken"></param>
    private async Task SaveDetailAsync(string name, BetterSubgroupModel subgroup, string? bgmTvUrl, string? poster,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""SELECT * FROM Anime WHERE Title = @Title""";
        await using var cnn = new SqliteConnection(_configuration["ConnectionString"]);
        var entity = await cnn.QueryFirstOrDefaultAsync<Anime>(sql, new { Title = name });
        if (entity == null)
        {
            entity = new Anime
            {
                Id = _guidGenerator.Create().ToString(),
                Title = name,
                SubgroupName = subgroup.Name,
                SubgroupLevel = subgroup.Level,
                Rss = subgroup.Rss,
                BgmTvUrl = bgmTvUrl,
                LastUpdateTime = DateTime.Now,
                PosterUrl = poster
            };
            var insertSql = $"""
                             INSERT INTO  Anime(Id, Title, SubgroupName, SubgroupLevel, Rss, BgmTvUrl, LastUpdateTime, PosterUrl)
                             VALUES (@Id, @Title, @SubgroupName, @SubgroupLevel, @Rss, @BgmTvUrl, @LastUpdateTime, @PosterUrl)
                             """;
            await cnn.ExecuteAsync(insertSql, entity);
        }
        else
        {
            //todo 判断数据是否有变化，无变化则不需要更新到数据库
            var updateSql = $"""
                             update Anime
                             set LastUpdateTime = @LastUpdateTime, SubgroupName=@SubgroupName, SubgroupLevel=@SubgroupLevel,Rss=@Rss, BgmTvUrl=@BgmTvUrl, PosterUrl=@PosterUrl
                             where Title = @Title;
                             """;
            await cnn.ExecuteAsync(updateSql, new
            {
                LastUpdateTime = DateTime.Now,
                SubgroupName = subgroup.Name,
                SubgroupLevel = subgroup.Level,
                Rss = subgroup.Rss,
                BgmTvUrl = bgmTvUrl,
                Title = name,
                PosterUrl = poster
            });
        }

        _logger.LogInformation($"{name}:已保存信息");
    }


    /// <summary>
    /// 解析字幕组数据
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="anime"></param>
    /// <returns></returns>
    private BetterSubgroupModel? ParseDetailSubgroup(HtmlDocument doc, Anime? anime)
    {
        var subgroup = doc.DocumentNode.SelectNodes("//div[@class='subgroup-text']");
        var list = new List<SubgroupModel>();
        foreach (var sub in subgroup)
        {
            var subName = WebUtility.HtmlDecode(sub.SelectSingleNode("./a")?.InnerText ?? "").Trim().Replace(" ", "");
            var rss = sub.SelectSingleNode(".//a[@class='mikan-rss']")?.Attributes["href"]?.Value;
            if (string.IsNullOrWhiteSpace(subName) || rss == null)
            {
                continue;
            }

            if (subName.Equals(_configuration["BestSubgroup"], StringComparison.InvariantCultureIgnoreCase))
            {
                return new BetterSubgroupModel { Name = subName, Rss = rss, Level = 1 };
            }

            list.Add(new SubgroupModel { Name = subName, Rss = rss });
        }

        if (!list.Any())
        {
            return null;
        }

        return FindBetterSubgroup(list, anime);
    }

    /// <summary>
    /// 查找最合适的字幕组
    /// </summary>
    /// <param name="subgroups"></param>
    /// <returns></returns>
    private BetterSubgroupModel FindBetterSubgroup(List<SubgroupModel> subgroups, Anime? anime)
    {
        var group = subgroups[0];
        ushort groupLevel = 9;
        foreach (var sub in _betterSubgroups)
        {
            var temp = subgroups.FirstOrDefault(x => x.Name.Contains(sub, StringComparison.InvariantCultureIgnoreCase));
            if (temp != null)
            {
                group = temp;
                groupLevel = 2;
                break;
            }
        }

        //如果新解析到的字幕组比已保存的更好，使用新的，否则使用原有字幕组
        if (anime != null)
        {
            if (anime.SubgroupLevel <= groupLevel)
            {
                group.Name = anime.SubgroupName;
                groupLevel = anime.SubgroupLevel;
                group.Rss = anime.Rss;
            }
        }

        return new BetterSubgroupModel
        {
            Name = group.Name,
            Level = groupLevel,
            Rss = group.Rss
        };
    }
}