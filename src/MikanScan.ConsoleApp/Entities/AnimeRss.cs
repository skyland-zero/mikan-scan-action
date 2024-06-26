using System;

namespace MikanScan.ConsoleApp.Entities;

public class AnimeRss
{
    public required string Id { get; set; }

    public required string AnimeId { get; set; }

    public required string Title { get; set; }

    public required string Link { get; set; }

    public DateTime LastUpdateTime { get; set; }
}