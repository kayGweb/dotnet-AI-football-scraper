namespace WebScraper.Api.Dtos;

public class VenueDto
{
    public int Id { get; set; }
    public string EspnId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsGrass { get; set; }
    public bool IsIndoor { get; set; }
    public MetaDto Meta { get; set; } = new();
}
