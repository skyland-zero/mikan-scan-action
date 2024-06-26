using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace MikanScan.ConsoleApp.Services;

public class DataSeedService : ITransientDependency
{
    private readonly IConfiguration _configuration;

    public DataSeedService(IConfiguration configuration)
    {
        _configuration = configuration;
    }


    /// <summary>
    /// 初始化数据库
    /// </summary>
    public async Task InitDataBaseAsync()
    {
        //创建数据目录
        if (!Directory.Exists("data"))
        {
            Directory.CreateDirectory("data");
        }

        var animeSql = $"""
                        
                                                        CREATE TABLE IF NOT EXISTS
                                                        Anime (
                                                            Id character(36) NOT NULL PRIMARY KEY,
                                                            Title TEXT NOT NULL,
                                                            SubgroupName TEXT NOT NULL,
                                                            SubgroupLevel unsigned NOT NULL,
                                                            Rss TEXT NOT NULL,
                                                            BgmTvUrl TEXT,
                                                            LastUpdateTime DATETIME NOT NULL
                                                        );
                                                 
                        """;
        var animeRssSql = $"""
                           
                                                           CREATE TABLE IF NOT EXISTS
                                                           AnimeRss (
                                                               Id character(36) NOT NULL PRIMARY KEY,
                                                               AnimeId character(36) NOT NULL,
                                                               Title TEXT NOT NULL,
                                                               Link TEXT NOT NULL,
                                                               LastUpdateTime DATETIME NOT NULL
                                                           );
                                                    
                           """;
        var xx = _configuration["ConnectionString"];
        await using var cnn = new SqliteConnection(_configuration["ConnectionString"]);
        await cnn.ExecuteAsync(animeSql);
        await cnn.ExecuteAsync(animeRssSql);
    }

    public async Task UpdateDatabaseAsync()
    {
        var checkSql = $"""select * from sqlite_master where type = 'table' and name = 'Anime' and sql like '%PosterUrl%'""";
        var sql = $"""ALTER TABLE 'Anime' ADD 'PosterUrl' TEXT;""";
        await using var cnn = new SqliteConnection(_configuration["ConnectionString"]);
        var isExist = await cnn.QueryFirstOrDefaultAsync(checkSql);
        if (isExist == null)
        {
            await cnn.ExecuteAsync(sql);
        }
    }
}