using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EmailClientApp;

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    // SecureSocketOptions: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable
    public string SecureSocket { get; set; } = "StartTls";
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderPassword { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
}

public class EmailClient
{
    private readonly EmailSettings _settings;
    private readonly SecureSocketOptions _socketOptions;
    private readonly ILogger logger;

    public EmailClient(IConfiguration config, ILogger logger)
    {
        this.logger = logger;
        this.logger.LogDebug("Initializing EmailClient.");
        _settings = config.GetSection("EmailSettings").Get<EmailSettings>()
            ?? throw new InvalidOperationException("Section 'EmailSettings' missing or empty in config file.");
        _socketOptions = ParseSocketOptions(_settings.SecureSocket);
    }

    /// <returns>True if the email was sent successfully, false otherwise.</returns>
    public async Task<bool> SendEmailAsync(string subject, string body, bool isHtml = false, CancellationToken ct = default)
    {
        this.logger.LogDebug("Sending email to {RecipientEmail} with subject: {Subject}",
            _settings.RecipientEmail, subject);

        MimeMessage message = BuildMessage(subject, body, isHtml);

        int maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // MailKit's SmtpClient is not thread-safe — create a new one per send
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, _socketOptions);
                await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(quit: true);

                this.logger.LogInformation("Email sent successfully to {RecipientEmail}",
                    _settings.RecipientEmail);
                return true;
            }
            catch (Exception ex) when (ex is not ArgumentException && !(ex is OperationCanceledException && ct.IsCancellationRequested))
            {
                // OperationCanceledException must propagate so the monitor loop shuts down cleanly on Ctrl+C.
                // ArgumentException from BuildMessage indicates a config problem — retrying won't help.
                this.logger.LogWarning(ex, "Attempt {Attempt} of {MaxRetries} to send email failed.",
                    attempt, maxRetries);

                if (attempt == maxRetries)
                {
                    this.logger.LogError(ex,
                        "Failed to send email to {RecipientEmail} after {MaxRetries} attempts.",
                        _settings.RecipientEmail, maxRetries);
                    return false;
                }

                await Task.Delay(2000, ct);
            }
        }

        return false;
    }

    private MimeMessage BuildMessage(string subject, string body, bool isHtml)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(new MailboxAddress(_settings.RecipientName, _settings.RecipientEmail));
            message.Subject = subject;
            message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };
            return message;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to build mail message for {ToEmail}.",
                _settings.RecipientEmail);
            throw;
        }
    }

    private static SecureSocketOptions ParseSocketOptions(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" => SecureSocketOptions.None,
            "auto" => SecureSocketOptions.Auto,
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
            _ => throw new InvalidOperationException(
                $"Unknown SecureSocket value '{value}'. Valid values: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable.")
        };
}