using System.Net;
using QuartzEncoder.Models;

namespace QuartzEncoder;

public class DiscordService(IConfiguration configuration, ILogger<DiscordService> logger)
{
    private readonly HttpClient _httpClient = new();

    public Uri? WebhookUrl => configuration.GetValue<Uri?>("DiscordWebhook", null);

    public async Task Alert(string file, IPAddress ipAddress)
    {
        if (WebhookUrl is null)
        {
            logger.LogWarning("Discord webhook URL is not set in configuration `DiscordWebhook`.");
            return;
        }

        var embed = new WebhookMessage.Embed
        {
            Title = "Quartz Encoder request.",
            Color = 0xFF0000, // Red
            Author = new WebhookMessage.Author("Quartz Encoder Alert"),
            Fields =
            [
                new WebhookMessage.Field("File", file),
                new WebhookMessage.Field("Remote IP Address", ipAddress.ToString()),
            ]
        };

        var message = new WebhookMessage
        {
            Embeds = [embed],
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(WebhookUrl, message);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to send Discord webhook message: {StatusCode} {Content}", response.StatusCode,
                    await response.Content.ReadAsStringAsync());
            }
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to send Discord webhook message");
        }
    }
}