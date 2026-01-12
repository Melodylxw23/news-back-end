using MimeKit;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit;

public class GmailEmailService
{
    private readonly IConfiguration _config;

    public GmailEmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var settings = _config.GetSection("EmailSettings");
        string clientId = settings["ClientId"];
        string clientSecret = settings["ClientSecret"];
        string refreshToken = settings["RefreshToken"];
        string userEmail = settings["UserEmail"];

        // Step 1: Get Access Token from Refresh Token
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            }
        });

        var token = new TokenResponse { RefreshToken = refreshToken };
        var credential = new UserCredential(flow, userEmail, token);
        await credential.RefreshTokenAsync(System.Threading.CancellationToken.None);

        string accessToken = credential.Token?.AccessToken;

        // Fail fast if we don't have an access token
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Failed to obtain Gmail access token. Verify refresh token and scopes (mail scope required).");

        // Step 2: Build MIME message
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Your Company", userEmail));
        mime.To.Add(new MailboxAddress("", toEmail));
        mime.Subject = subject;
        mime.Body = new TextPart("html") { Text = htmlBody };

        // Convert MIME to base64url string required by Gmail API
        byte[] rawBytes;
        using (var ms = new MemoryStream())
        {
            await mime.WriteToAsync(ms, CancellationToken.None);
            rawBytes = ms.ToArray();
        }

        string raw = Convert.ToBase64String(rawBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        // Step 3: Send via Gmail REST API
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "NewsBackEnd"
        });

        var msg = new Message { Raw = raw };
        await service.Users.Messages.Send(msg, "me").ExecuteAsync();
    }
}