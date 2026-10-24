using MailKit.Net.Smtp;
using MailKit.Security;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MechanicShop.Infrastructure.Services;

public sealed class NotificationService(
    ILogger<NotificationService> logger,
    IOptions<EmailSettings> options)
    : INotificationService
{
    private const string Message =
        "Your vehicle service is complete. You may collect it from the shop at your earliest convenience.";

    private readonly EmailSettings _settings = options.Value;

    public async Task SendEmailAsync(
        string to,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                _settings.SenderName,
                _settings.SenderEmail));

        message.To.Add(MailboxAddress.Parse(to));

        message.Subject = "Your Vehicle Service Is Complete";

        message.Body = new TextPart("plain")
        {
            Text = Message
        };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTls,
                cancellationToken);

            await client.AuthenticateAsync(
                _settings.Username,
                _settings.Password,
                cancellationToken);

            await client.SendAsync(
                message,
                cancellationToken);

            logger.LogInformation(
                "Email sent successfully to {Email}",
                MaskEmail(to));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send email to {Email}",
                MaskEmail(to));

            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(
                    true,
                    CancellationToken.None);
            }
        }
    }

    public async Task SendSmsAsync(
    string phoneNumber,
    CancellationToken cancellationToken = default)
    {
        Console.WriteLine("========== SMS Notification ==========");
        Console.WriteLine($"To: {phoneNumber}");
        Console.WriteLine("Message: SMS sending was triggered successfully.");
        Console.WriteLine("======================================");

        await Task.CompletedTask;
    }
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');

        return at > 1
            ? email[..2] + "***" + email[at..]
            : "***";
    }
}