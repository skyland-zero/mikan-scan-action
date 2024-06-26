using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace MikanScan.ConsoleApp.Services;

public class NotifyService : ISingletonDependency
{
    private readonly HttpClient _notifyClient;
    private readonly IConfiguration _configuration;

    public NotifyService(IConfiguration configuration)
    {
        _configuration = configuration;
        _notifyClient = new()
        {
            BaseAddress = new Uri(_configuration.GetValue<string>("Gotify") ?? "")
        };

    }

    public void Send(string title, string message)
    {
        _notifyClient.PostAsync("", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "title", title },
            { "message", message },
            { "priority", "6" }
        }));
    }
}