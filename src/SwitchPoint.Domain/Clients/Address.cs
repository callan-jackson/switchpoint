namespace SwitchPoint.Domain.Clients;

public sealed record Address(string? Line1, string? Line2, string? Town, string? County, string? Postcode, string Country = "United Kingdom");
