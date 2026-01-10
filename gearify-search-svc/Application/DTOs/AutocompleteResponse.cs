namespace Gearify.SearchService.Application.DTOs;

public class AutocompleteResponse
{
    public List<AutocompleteSuggestion> Suggestions { get; set; } = new();
}

public class AutocompleteSuggestion
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "product", "brand", "category"
    public string? Id { get; set; }
    public string? Slug { get; set; }
}
