using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(
        IConfiguration configuration,
        ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // =========================================================
    // 1. GỬI EMAIL - ASP.NET CORE IDENTITY
    // =========================================================

    public async Task SendEmailAsync(
        string email,
        string subject,
        string htmlMessage)
    {
        _logger.LogWarning(
            "========== EMAIL SENDER ĐƯỢC GỌI ==========");

        try
        {
            var message = new MimeMessage();

            // Người gửi
            message.From.Add(
                new MailboxAddress(
                    "EZ-Rent",
                    _configuration["EmailSettings:Email"]));

            // Người nhận
            message.To.Add(
                MailboxAddress.Parse(email));

            // Tiêu đề
            message.Subject = subject;

            // Nội dung HTML
            message.Body = new TextPart("html")
            {
                Text = htmlMessage
            };

            // Gửi qua Gmail
            await SendEmailAsync(message);

            _logger.LogInformation(
                "Email đã được gửi tới {Email}",
                email);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Không thể gửi email tới {Email}",
                email);

            await SaveEmailToFileAsync(
                email,
                subject,
                htmlMessage);

            throw;
        }
    }

    // =========================================================
    // 2. GỬI EMAIL QUA GMAIL SMTP
    // =========================================================

    private async Task SendEmailAsync(
        MimeMessage message)
    {
        var host = _configuration["EmailSettings:Host"];

        var port = int.Parse(
            _configuration["EmailSettings:Port"]!);

        var username =
            _configuration["EmailSettings:Email"];

        var password =
            _configuration["EmailSettings:Password"];

        using var smtp = new SmtpClient();

        // Kết nối Gmail SMTP
        await smtp.ConnectAsync(
            host,
            port,
            SecureSocketOptions.StartTls);

        // Đăng nhập Gmail
        await smtp.AuthenticateAsync(
            username,
            password);

        // Gửi email
        await smtp.SendAsync(message);

        // Đóng kết nối
        await smtp.DisconnectAsync(true);
    }

    // =========================================================
    // 3. FALLBACK LƯU EMAIL VÀO FILE
    // =========================================================

    private async Task SaveEmailToFileAsync(
        string email,
        string type,
        string content)
    {
        var folderPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "EmailLogs");

        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(
            folderPath,
            "emails.txt");

        var log = $"""

            ========================================
            Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            To: {email}
            Type: {type}

            Content:
            {content}
            ========================================

            """;

        await File.AppendAllTextAsync(
            filePath,
            log);
    }
}