using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Persistence.Messages;

public class RabbitMqOptions
{
    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string VirtualHost { get; set; } = "/";

    [Required]
    public string Username { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;

    [Required]
    public string Exchange { get; set; } = "aijobs.exchange";

    [Required]
    public string Queue { get; set; } = "aijobs.process";

    [Required]
    public string RoutingKey { get; set; } = "job.process";

    [Required]
    public string DeadLetterExchange { get; set; } = "aijobs.dlx";

    [Required]
    public string DeadLetterQueue { get; set; } = "aijobs.process.dlq";

    [Required]
    public string DeadLetterRoutingKey { get; set; } = "job.dead";
}