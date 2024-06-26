using System;

namespace MikanScan.ConsoleApp.Entities;

public class Anime
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string SubgroupName { get; set; }
    public ushort SubgroupLevel { get; set; }
    public required string Rss { get; set; }
    public string? BgmTvUrl { get; set; }
    
    public string? PosterUrl { get; set; }
    public DateTime LastUpdateTime { get; set; }
    
}