using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace EmailClientApp;

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderPassword { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
}

public class EmailClient : IDisposable
{
    private readonly EmailSettings _settings;
    private readonly SmtpClient _smtpClient;

    public EmailClient(string configPath = "appsettings.json")
    {
        Program.GlobalLogger.LogDebug("Initializing EmailClient with config path: {ConfigPath}", configPath);
        _settings = LoadSettings(configPath);
        _smtpClient = BuildSmtpClient(_settings);
    }

    public async Task SendEmailAsync(string subject, string body, bool isHtml = false)
    {
        Program.GlobalLogger.LogDebug("Sending email to {RecipientEmail} with subject: {Subject}", _settings.RecipientEmail, subject);
        using var message = BuildMessage(subject, body, isHtml,
                                         _settings.RecipientEmail,
                                         _settings.RecipientName);
        await _smtpClient.SendMailAsync(message);
        Program.GlobalLogger.LogInformation("Email sent successfully to {RecipientEmail}", _settings.RecipientEmail);
    }

    private static EmailSettings LoadSettings(string configPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException(
                $"Configuration file not found: {Path.GetFullPath(configPath)}");

        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        return config.GetSection("EmailSettings").Get<EmailSettings>()
               ?? throw new InvalidOperationException(
                      "Section 'EmailSettings' missing or empty in config file.");
    }

    private static SmtpClient BuildSmtpClient(EmailSettings s)
    {
        return new SmtpClient(s.SmtpHost, s.SmtpPort)
        {
            Credentials = new NetworkCredential(s.SenderEmail, s.SenderPassword),
            EnableSsl = s.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
    }

    private MailMessage BuildMessage(string subject, string body,
                                     bool isHtml,
                                     string toEmail, string toName)
    {
        var from = new MailAddress(_settings.SenderEmail, _settings.SenderName);
        var to = new MailAddress(toEmail, toName);

        return new MailMessage(from, to)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
    }

    public void Dispose() => _smtpClient.Dispose();
}