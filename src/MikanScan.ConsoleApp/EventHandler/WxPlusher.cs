using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using System;
using Volo.Abp.EventBus;

namespace MikanScan.ConsoleApp.EventHandler;

public class WxPlusher : ILocalEventHandler<NotifyEto>, ITransientDependency
{
    private const string Host = "https://wxpusher.zjiecode.com/api/send/message";
    private readonly IConfiguration _configuration;
    private readonly ILogger<WxPlusher> _logger;

    public WxPlusher(IConfiguration configuration, ILogger<WxPlusher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task HandleEventAsync(NotifyEto eventData)
    {
        return SendMessageAsync(eventData.Title, eventData.Content);
    }

    private async Task SendMessageAsync(string title, string content)
    {
        var token = _configuration.GetSection("WxPusher:AppToken").Get<string>();
        var uids = _configuration.GetSection("WxPusher:Uids").Get<string[]>();
        if (token.IsNullOrWhiteSpace())
        {
            _logger.LogWarning("未配置WxPlusher Token");
            return;
        }

        if (uids == null || uids.Length == 0)
        {
            _logger.LogWarning("未配置WxPlusher uids");
            return;
        }

        var req = new WxPlusherNotifyReq
        {
            AppToken = token,
            Content = content,
            Summary = title,
            Uids = uids
        };


        try
        {
            var resp = await Host
                .PostJsonAsync(req)
                .ReceiveJson<WxPlusherNotifyResp>();
            if (resp.Success != true)
            {
                _logger.LogWarning($"发送消息失败:{resp.Msg}");
            }
        }
        catch (FlurlHttpException ex)
        {
            var err = await ex.GetResponseStringAsync();
            _logger.LogWarning($"Error returned from {ex.Call.Request.Url}: {err}");
        }
    }
}

internal class WxPlusherNotifyReq
{
    [JsonPropertyName("appToken")] public required string AppToken { get; set; }

    [JsonPropertyName("content")] public required string Content { get; set; }

    [JsonPropertyName("summary")] public required string Summary { get; set; }

    [JsonPropertyName("contentType")] public int ContentType { get; set; } = 2;

    [JsonPropertyName("uids")] public required string[] Uids { get; set; }

    [JsonPropertyName("verifyPayType")] public int VerifyPayType { get; set; } = 0;
}

internal class WxPlusherNotifyResp
{
    [JsonPropertyName("code")] public int? Code { get; set; }

    [JsonPropertyName("msg")] public string? Msg { get; set; }

    [JsonPropertyName("success")] public bool? Success { get; set; }
}