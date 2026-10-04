namespace Application.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public int ExpirationInMinutes { get; set; }
}