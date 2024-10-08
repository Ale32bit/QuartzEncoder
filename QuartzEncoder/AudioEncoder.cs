﻿namespace QuartzEncoder;

using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Pipes;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class AudioEncoder
{
    public static readonly string CachePath = Path.Combine(Environment.CurrentDirectory, "cache");
    public static readonly HttpClient HttpClient = new();
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);
    // 100 MiB
    public const long MaxFileSize = 100 * 1024 * 1024;

    private static async Task<bool> TryYTDLP(string url, MemoryStream memoryStream)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            if (await HostFilterHandler.IsLocalhostOrPrivateNetwork(uri))
                return false;

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

        return memoryStream.Length > 0;
    }

    private static async Task<bool> TryHTTP(string url, MemoryStream memoryStream)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri?.Scheme is "http" or "https"))
            return false;

        if (await HostFilterHandler.IsLocalhostOrPrivateNetwork(uri))
            return false;

        var request = new HttpRequestMessage(HttpMethod.Head, uri);
        var head = await HttpClient.SendAsync(request);
        if (!head.IsSuccessStatusCode)
            return false;

        var mimeType = head.Content.Headers.ContentType?.MediaType;
        var length = head.Content.Headers.ContentLength;

        if (mimeType is null || length is null)
            return false;

        if (!mimeType.StartsWith("audio/"))
            return false;

        if (length > MaxFileSize)
            return false;

        var stream = await HttpClient.GetStreamAsync(url);
        await stream.CopyToAsync(memoryStream);

        return true;
    }

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

        if (!await TryHTTP(url, memoryStream))
        {
            if (!await TryYTDLP(url, memoryStream))
            {
                return memoryStream;
            }
        }


        memoryStream.Seek(0, SeekOrigin.Begin);
        using var writeStream = File.OpenWrite(Path.Combine(CachePath, hash));
        await memoryStream.CopyToAsync(writeStream);
        await writeStream.DisposeAsync();
        memoryStream.Seek(0, SeekOrigin.Begin);

        // clean up old files
        foreach (var filePath in Directory.EnumerateFiles(CachePath))
        {
            var fileDate = File.GetCreationTime(filePath);
            var age = DateTime.Now - fileDate;
            if (age > CacheLifetime)
                File.Delete(Path.Combine(CachePath, filePath));
        }

        return memoryStream;
    }

    public static async Task<byte[]?> ToDfpwm(Stream stream)
    {
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
                o.WithCustomArgument("-ac 1");
            })
            .ProcessAsynchronously();

        return memoryStream.ToArray();
    }

    public static async Task<byte[]?> DownloadDfpwm(string url)
    {
        using var stream = await DownloadTrack(url);

        return await ToDfpwm(stream);
    }

    public class MdfpwmMeta
    {
        public string? Artist { get; set; }
        public string? Title { get; set; }
        public string? Album { get; set; }
    }

    public static async Task<byte[]?> ToMdfpwm(Stream stream, MdfpwmMeta meta)
    {
        if (stream.Length == 0)
            return null;

        static FFMpegArgumentOptions Options(FFMpegArgumentOptions o)
        {
            o.WithAudioSamplingRate(48000);
            o.WithAudioBitrate(48);
            o.ForceFormat("dfpwm");
            return o;
        }

        using var leftChannel = new MemoryStream();
        using var rightChannel = new MemoryStream();

        await FFMpegArguments
            .FromPipeInput(new StreamPipeSource(stream), args =>
                args.WithCustomArgument("-filter_complex \"[0:a]channelsplit=channel_layout=stereo[left][right]\"")
            )
            .MultiOutput(args =>
            {
                args
                    .OutputToPipe(new StreamPipeSink(leftChannel), o =>
                    {
                        Options(o);
                        /*o.WithAudioFilters(o =>
                        {
                            o.Pan();
                        });*/
                        o.WithCustomArgument("-map \"[left]\"");
                    })
                    .OutputToPipe(new StreamPipeSink(rightChannel), o =>
                    {
                        Options(o);
                        o.WithCustomArgument("-map \"[right]\"");
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
            await mdfpwm.WriteAsync(mbuff.AsMemory(0, l));
            for (int j = 0; j < (6000 - l); j++)
            {
                mdfpwm.WriteByte(0x55);
            }
            var r = await rightChannel.ReadAsync(mbuff);
            await mdfpwm.WriteAsync(mbuff.AsMemory(0, r));
            for (int j = 0; j < (6000 - r); j++)
            {
                mdfpwm.WriteByte(0x55);
            }
        }

        return mdfpwm.ToArray();
    }

    public static async Task<byte[]?> DownloadMdfpwm(string url, MdfpwmMeta meta)
    {
        using var stream = await DownloadTrack(url);

        return await ToMdfpwm(stream, meta);
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
        if (buff.Length > 255)
            Array.Resize(ref buff, 255);
        var outp = new byte[Math.Min(buff.Length, 255) + 1];
        outp[0] = (byte)buff.Length;
        buff.CopyTo(outp, 1);
        return outp;
    }
}
