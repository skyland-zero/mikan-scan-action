using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MikanScan.ConsoleApp.Services;

public class BittorrentApiClient : ISingletonDependency
{

    private DateTime? LastLoginTime;
    private readonly HttpClient _qbClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BittorrentApiClient> _logger;
    public BittorrentApiClient(IConfiguration configuration, ILogger<BittorrentApiClient> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _qbClient = new()
        {
            BaseAddress = new Uri(configuration["QbHost"] ?? "")
        };
    }


    public async Task<bool> LoginAsync()
    {
        using var response = await _qbClient.PostAsync("/api/v2/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "username", _configuration["QbUserName"]! },
            { "password", _configuration["QbPassword"]! }
        }));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        if (result.Contains("ok", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogInformation("登录qbtorrent成功");
            LastLoginTime = DateTime.Now;
            return true;
        }
        else
        {
            _logger.LogError("登录qbtorrent失败");
            return false;
        }
    }

    public async Task<bool> CheckLoginAsync()
    {
        if (LastLoginTime == null)
        {
            return await LoginAsync();
        }

        if (LastLoginTime.Value.AddMinutes(30) < DateTime.Now)
        {
            return await LoginAsync();
        }

        return true;
    }

    public async Task<bool> AddTorrentAsync(string url, string path, string category, CancellationToken cancellationToken = default)
    {
        await CheckLoginAsync();
        using var response = await _qbClient.PostAsync("/api/v2/torrents/add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "urls", url },
            { "savepath", path },
            { "category", category }
        }), cancellationToken: cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        if (result.Contains("ok", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogInformation("发送种子到qbtorrent成功");
            return true;
        }
        else
        {
            _logger.LogError("发送种子到qbtorrent失败");
            return false;
        }
    }

}