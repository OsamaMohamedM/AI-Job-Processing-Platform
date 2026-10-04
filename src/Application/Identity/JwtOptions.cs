namespace Application.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "aijobplatform";
    public string Audience { get; set; } = "aijobplatform-clients";
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
