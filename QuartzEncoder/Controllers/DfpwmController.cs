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

    /// <summary>
    /// Download and convert audio to mono format DFPWM1a
    /// </summary>
    /// <param name="url"></param>
    /// <returns>File converted to DFPWM</returns>
    [HttpGet("dfpwm")]
    public async Task<IActionResult> Dfpwm([FromQuery] string url)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress;
        _logger.LogInformation("[{clientIp}] Converting {url}", clientIp, url);

        var buffer = await AudioEncoder.DownloadDfpwm(url);

        if (buffer == null)
            return NotFound("Source not found, too big, rate limited or incompatible!");

        return File(buffer, "audio/dfpwm", "audio.dfpwm");
    }

    /// <summary>
    /// Download and convert audio to stereo format MDFPWMv3 by Drucifer
    /// </summary>
    /// <param name="url"></param>
    /// <param name="artist"></param>
    /// <param name="title"></param>
    /// <param name="album"></param>
    /// <returns>File converted to MDFPWM</returns>
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
            return NotFound("Source not found, too big, rate limited or incompatible!");

        return File(buffer, "audio/mdfpwm", "audio.mdfpwm");
    }
}
