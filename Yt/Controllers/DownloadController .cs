using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;


[ApiController]
[Route("api/[controller]")]
public class DownloadController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> DownloadAudio([FromBody] string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return BadRequest("No URL provided");

        string toolsPath = @"C:\Tools";
        string downloadsPath = Path.Combine(toolsPath, "Downloads");
        Directory.CreateDirectory(downloadsPath);

        // Step 1: Get video title
        string title = await GetVideoTitleAsync(videoUrl, toolsPath);
        if (string.IsNullOrWhiteSpace(title))
            return StatusCode(500, "Failed to get video title.");

        string safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        string outputTemplate = Path.Combine(downloadsPath, $"{safeTitle}.%(ext)s");
        string expectedOutput = Path.Combine(downloadsPath, $"{safeTitle}.mp3");

        // Step 2: Download + Convert to MP3
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(toolsPath, "yt-dlp.exe"),
            Arguments = $"-x --audio-format mp3 --audio-quality 0 -o \"{outputTemplate}\" {videoUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["PATH"] = Environment.GetEnvironmentVariable("PATH") + @";" + toolsPath
            }
        };

        using var process = Process.Start(psi);
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Optional: log errors
        System.IO.File.WriteAllText(Path.Combine(downloadsPath, "yt-dlp-log.txt"), stdout + "\n\n" + stderr);

        if (!System.IO.File.Exists(expectedOutput))
            return StatusCode(500, "Failed to create MP3. Check yt-dlp/ffmpeg setup.");

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(expectedOutput);
        System.IO.File.Delete(expectedOutput);

        return Ok(new DownloadResult
        {
            Base64 = Convert.ToBase64String(bytes),
            FileName = $"{safeTitle}.mp3"
        });
    }

    private async Task<string> GetVideoTitleAsync(string videoUrl, string toolsPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(toolsPath, "yt-dlp.exe"),
            Arguments = $"--get-title {videoUrl}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }
}
public class DownloadResult
{
    public string Base64 { get; set; } = "";
    public string FileName { get; set; } = "";
}