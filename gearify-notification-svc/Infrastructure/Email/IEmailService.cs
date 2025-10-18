using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Gearify.NotificationService.Infrastructure.Email;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}

public class MailHogEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public MailHogEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // MailHog SMTP integration
        var smtpHost = _configuration["MailHog:Host"] ?? "mailhog";
        var smtpPort = _configuration.GetValue<int>("MailHog:Port", 1025);

        // TODO: Implement SMTP client
        await Task.CompletedTask;
    }
}
