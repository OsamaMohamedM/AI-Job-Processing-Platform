using System.ComponentModel.DataAnnotations;

namespace Application.AI;

public sealed class AiOptions
{
    [Required]
    public string Provider { get; set; }

    [Required, RegularExpression(@"^https?://.+/$", ErrorMessage = "BaseUrl must end with '/'")]
    public string BaseUrl { get; set; }

    [Required]
    public string Model { get; set; }

    [Required]
    public string ApiKey { get; set; } = default!;

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 60;
}