using System.Net;
using System.Net.Mail;
using System.Text;

namespace ASG.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private static bool TryParseEmail(string? address, out MailAddress? parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(address)) return false;
            try
            {
                parsed = new MailAddress(address.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendAsync(string to, string subject, string body)
        {
            var host = _config["Smtp:Host"];
            var portStr = _config["Smtp:Port"];
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var configuredFrom = _config["Smtp:From"];

            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning("SMTP 未配置主机，邮件未发送。Subject={Subject}, To={To}", subject, to);
                return false;
            }

            // 选择有效的发件人地址：优先 Smtp:From；否则仅在 Username 是有效邮箱时采用
            MailAddress? fromAddr = null;
            if (!string.IsNullOrWhiteSpace(configuredFrom) && TryParseEmail(configuredFrom, out var parsedFrom))
            {
                fromAddr = parsedFrom;
            }
            else if (TryParseEmail(username, out var parsedFromByUser))
            {
                fromAddr = parsedFromByUser;
            }
            else
            {
                _logger.LogWarning("SMTP 发件人未配置或格式无效，邮件未发送。Subject={Subject}, To={To}, From={From}, Username={Username}", subject, to, configuredFrom, username);
                return false;
            }

            // 收件人地址校验
            if (!TryParseEmail(to, out var toAddr))
            {
                _logger.LogWarning("收件人邮箱格式无效，邮件未发送。Subject={Subject}, To={To}", subject, to);
                return false;
            }

            using var client = new SmtpClient(host)
            {
                Port = int.TryParse(portStr, out var p) ? p : 587,
                EnableSsl = true,
                Credentials = !string.IsNullOrEmpty(username)
                    ? new NetworkCredential(username, password)
                    : CredentialCache.DefaultNetworkCredentials
            };

            using var mail = new MailMessage(fromAddr.Address, toAddr.Address)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            await client.SendMailAsync(mail);
            return true;
        }

        public async Task<bool> SendHtmlAsync(string to, string subject, string htmlBody, string? plainTextBody = null)
        {
            var host = _config["Smtp:Host"];
            var portStr = _config["Smtp:Port"];
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var configuredFrom = _config["Smtp:From"];

            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning("SMTP 未配置主机，HTML 邮件未发送。Subject={Subject}, To={To}", subject, to);
                return false;
            }

            // 选择有效的发件人地址：优先 Smtp:From；否则仅在 Username 是有效邮箱时采用
            MailAddress? fromAddr = null;
            if (!string.IsNullOrWhiteSpace(configuredFrom) && TryParseEmail(configuredFrom, out var parsedFrom))
            {
                fromAddr = parsedFrom;
            }
            else if (TryParseEmail(username, out var parsedFromByUser))
            {
                fromAddr = parsedFromByUser;
            }
            else
            {
                _logger.LogWarning("SMTP 发件人未配置或格式无效，HTML 邮件未发送。Subject={Subject}, To={To}, From={From}, Username={Username}", subject, to, configuredFrom, username);
                return false;
            }

            // 收件人地址校验
            if (!TryParseEmail(to, out var toAddr))
            {
                _logger.LogWarning("收件人邮箱格式无效，HTML 邮件未发送。Subject={Subject}, To={To}", subject, to);
                return false;
            }

            using var client = new SmtpClient(host)
            {
                Port = int.TryParse(portStr, out var p) ? p : 587,
                EnableSsl = true,
                Credentials = !string.IsNullOrEmpty(username)
                    ? new NetworkCredential(username, password)
                    : CredentialCache.DefaultNetworkCredentials
            };

            using var mail = new MailMessage(fromAddr.Address, toAddr.Address)
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            // 添加备选视图以提升兼容性
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
            mail.AlternateViews.Add(htmlView);
            if (!string.IsNullOrWhiteSpace(plainTextBody))
            {
                var textView = AlternateView.CreateAlternateViewFromString(plainTextBody!, Encoding.UTF8, "text/plain");
                mail.AlternateViews.Add(textView);
            }

            await client.SendMailAsync(mail);
            return true;
        }
    }
}