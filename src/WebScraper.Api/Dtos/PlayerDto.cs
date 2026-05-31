namespace WebScraper.Api.Dtos;

public class PlayerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string? TeamAbbreviation { get; set; }
    public string Position { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }
    public string? Height { get; set; }
    public int? Weight { get; set; }
    public string? College { get; set; }
    public string? EspnId { get; set; }
    public MetaDto Meta { get; set; } = new();
}
