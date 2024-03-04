namespace QuartzEncoder;

using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using Microsoft.AspNetCore.Http.HttpResults;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class AudioEncoder
{
    public static readonly string CachePath = Path.Combine(Environment.CurrentDirectory, "cache");

    private static async Task<Stream> DownloadTrack(string url)
    {
        if (!Directory.Exists(CachePath))
        {
            Directory.CreateDirectory(CachePath);
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));

        var memoryStream = new MemoryStream();

        if (File.Exists(Path.Combine(CachePath, hash)))
        {
            using var streamReader = File.OpenRead(Path.Combine(CachePath, hash));
            await streamReader.CopyToAsync(memoryStream);
            await streamReader.DisposeAsync();
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        var ytDlpProcessStartInfo = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments = $"--match-filters \"duration<10m\" -x -o - {url}",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var ytDlpProcess = Process.Start(ytDlpProcessStartInfo);

        using (var ytDlpOutput = ytDlpProcess.StandardOutput.BaseStream)
        {
            await ytDlpOutput.CopyToAsync(memoryStream);
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        if (memoryStream.Length > 0)
        {
            using var writeStream = File.OpenWrite(Path.Combine(CachePath, hash));
            await memoryStream.CopyToAsync(writeStream);
            await writeStream.DisposeAsync();
        }
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;

    }

    public static async Task<byte[]?> DownloadDfpwm(string url)
    {
        using var stream = await DownloadTrack(url);
        if (stream.Length == 0)
            return null;

        using var memoryStream = new MemoryStream();
        await FFMpegArguments
            .FromPipeInput(new StreamPipeSource(stream))
            .OutputToPipe(new StreamPipeSink(memoryStream), o =>
            {
                o.WithAudioSamplingRate(48000);
                o.WithAudioBitrate(48);
                o.ForceFormat("dfpwm");
                o.WithCustomArgument("-af \"pan=mono|c0=c0+c1\"");
            })
            .ProcessAsynchronously();

        return memoryStream.ToArray();
    }

    public class MdfpwmMeta
    {
        public string? Artist { get; set; }
        public string? Title { get; set; }
        public string? Album { get; set; }
    }

    public static async Task<byte[]?> DownloadMdfpwm(string url, MdfpwmMeta meta)
    {
        using var stream = await DownloadTrack(url);

        if(stream.Length == 0)
            return null;

        using var leftChannel = new MemoryStream();
        using var rightChannel = new MemoryStream();

        await FFMpegArguments
            .FromPipeInput(new StreamPipeSource(stream))
            .MultiOutput(args =>
            {
                args
                    .OutputToPipe(new StreamPipeSink(leftChannel), o =>
                     {
                         o.WithAudioSamplingRate(48000);
                         o.WithAudioBitrate(48);
                         o.ForceFormat("dfpwm");
                         o.WithCustomArgument("-map_channel 0.0.0");
                     })
                    .OutputToPipe(new StreamPipeSink(rightChannel), o =>
                    {
                        o.WithAudioSamplingRate(48000);
                        o.WithAudioBitrate(48);
                        o.ForceFormat("dfpwm");
                        o.WithCustomArgument("-map_channel 0.0.1");
                    });
            })
            .ProcessAsynchronously();

        var length = leftChannel.Length + rightChannel.Length;

        var mdfpwm = new MemoryStream();
        await mdfpwm.WriteAsync(Encoding.ASCII.GetBytes("MDFPWM\x03"));
        await mdfpwm.WriteAsync(ToLittleEndianBytes((int)length));
        await mdfpwm.WriteAsync(ToByteString(meta.Artist ?? ""));
        await mdfpwm.WriteAsync(ToByteString(meta.Title ?? ""));
        await mdfpwm.WriteAsync(ToByteString(meta.Album ?? ""));

        leftChannel.Seek(0, SeekOrigin.Begin);
        rightChannel.Seek(0, SeekOrigin.Begin);

        var mbuff = new byte[6000];
        for (int i = 0; i < length / 12000; i++)
        {
            var l = await leftChannel.ReadAsync(mbuff);
            await mdfpwm.WriteAsync(mbuff, 0, l);
            for(int j = 0; j < (6000 - l); j++)
            {
                mdfpwm.WriteByte(0x55);
            }
            var r = await rightChannel.ReadAsync(mbuff);
            await mdfpwm.WriteAsync(mbuff, 0, r);
            for (int j = 0; j < (6000 - r); j++)
            {
                mdfpwm.WriteByte(0x55);
            }
        }

        return mdfpwm.ToArray();
    }

    static byte[] ToLittleEndianBytes(int number)
    {
        byte[] bytes =
        [
            (byte)(number & 0xFF),
            (byte)((number >> 8) & 0xFF),
            (byte)((number >> 16) & 0xFF),
            (byte)((number >> 24) & 0xFF),
        ];
        return bytes;
    }

    static byte[] ToByteString(string str)
    {
        var buff = Encoding.ASCII.GetBytes(str);
        var outp = new byte[buff.Length + 1];
        outp[0] = (byte)buff.Length;
        buff.CopyTo(outp, 1);
        return outp;
    }
}
