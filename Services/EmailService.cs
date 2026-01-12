using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Configuration;

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

        string accessToken = credential.Token.AccessToken;

        // Step 2: Build Email
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Your Company", userEmail));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        // Step 3: Send via MailKit with OAuth2
        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        var oauth2 = new SaslMechanismOAuth2(userEmail, accessToken);
        await client.AuthenticateAsync(oauth2);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}