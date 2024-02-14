using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace QuartzEncoder.Controllers;

[Route("")]
[ApiController]
public class DfpwmController : ControllerBase
{
    [HttpGet("dfpwm")]
    public async Task<IActionResult> Dfpwm([FromQuery] string url)
    {
        var buffer = await AudioEncoder.DownloadDfpwm(url);

        if (buffer == null)
            return NotFound("Source not found, too big or rate limited!");

        return File(buffer, "audio/dfpwm", "audio.dfpwm");
    }

    [HttpGet("mdfpwm")]
    public async Task<IActionResult> Mdfpwm([FromQuery] string url, string? artist, string? title, string? album)
    {
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
