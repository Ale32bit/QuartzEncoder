using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace QuartzEncoder.Controllers;

[Route("")]
[ApiController]
public class DfpwmController : ControllerBase
{
    private readonly ILogger<DfpwmController> _logger;
    public DfpwmController(ILogger<DfpwmController> logger)
    {
        _logger = logger;
    }

    [HttpGet("dfpwm")]
    public async Task<IActionResult> Dfpwm([FromQuery] string url)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress;
        _logger.LogInformation("[{clientIp}] Converting {url}", clientIp, url);

        var buffer = await AudioEncoder.DownloadDfpwm(url);

        if (buffer == null)
            return NotFound("Source not found, too big or rate limited!");

        return File(buffer, "audio/dfpwm", "audio.dfpwm");
    }

    [HttpGet("mdfpwm")]
    public async Task<IActionResult> Mdfpwm([FromQuery] string url, string? artist, string? title, string? album)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress;
        _logger.LogInformation("[{clientIp}] Converting {url}", clientIp, url);

        var buffer = await AudioEncoder.DownloadMdfpwm(url, new()
        {
            Artist = artist,
            Title = title,
            Album = album,
        });

        if (buffer == null)
            return NotFound("Source not found, too big or rate limited!");

        return File(buffer, "audio/mdfpwm", "audio.mdfpwm");
    }
}
