using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using System;
using Flurl;
using MikanScan.ConsoleApp.Extensions;
using Volo.Abp.EventBus;

namespace MikanScan.ConsoleApp.EventHandler;

public class TelegramMessagePlusher : ILocalEventHandler<NotifyEto>, ITransientDependency
{
    private const string DefaultHost = "https://api.telegram.org";
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramMessagePlusher> _logger;

    public TelegramMessagePlusher(IConfiguration configuration, ILogger<TelegramMessagePlusher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task HandleEventAsync(NotifyEto eventData)
    {
        return SendMessageAsync(eventData.Title, eventData.Content, eventData.Poster);
    }

    private async Task SendMessageAsync(string title, string content, string? poster)
    {
        var host = _configuration.GetSection("Telegram:Host").Get<string>() ?? DefaultHost;
        var token = _configuration.GetSection("Telegram:Token").Get<string>() ?? "";
        var chatId = _configuration.GetSection("Telegram:ChatId").Get<string>() ?? "";
        if (token.IsNullOrWhiteSpace())
        {
            _logger.LogWarning("未配置TelegramMessage Token");
            return;
        }

        if (chatId.IsNullOrWhiteSpace())
        {
            _logger.LogWarning("未配置TelegramMessage chatId");
            return;
        }

        var isUrl = poster != null && poster.IsUrl();
        try
        {
            if (isUrl)
            {
                //发送图文类型通知
                await host
                    .AppendPathSegment($"bot{token}")
                    .AppendPathSegment("sendPhoto")
                    .AppendQueryParam("chat_id", chatId)
                    .AppendQueryParam("photo", poster)
                    .AppendQueryParam("caption", content)
                    .PostStringAsync("");
            }
            else
            {
                //发送文字通知
                await host
                    .AppendPathSegment($"bot{token}")
                    .AppendPathSegment("sendMessage")
                    .AppendQueryParam("chat_id", chatId)
                    .AppendQueryParam("text", $"{content}")
                    .PostStringAsync("");
            }
        }
        catch (FlurlHttpException ex)
        {
            var err = await ex.GetResponseStringAsync();
            _logger.LogWarning($"Error returned from {ex.Call.Request.Url}: {err}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息失败");
        }
    }
}