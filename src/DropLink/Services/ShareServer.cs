using System.IO;
using System.Net;
using System.Text;
using DropLink.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace DropLink.Services;

public sealed class ShareServer : IAsyncDisposable
{
    private readonly ShareManager _shares;
    private WebApplication? _application;
    private int _activeDownloads;
    private readonly SemaphoreSlim _downloadSlots = new(4, 4);

    public int Port { get; }
    public int ActiveDownloads => Volatile.Read(ref _activeDownloads);
    public bool IsRunning { get; private set; }

    public ShareServer(ShareManager shares, int port)
    {
        _shares = shares;
        Port = port;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_application is not null) return;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ApplicationName = typeof(ShareServer).Assembly.GetName().Name
        });
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(Port));

        var app = builder.Build();
        app.MapGet("/health", () => Results.Text("DropLink is running", "text/plain; charset=utf-8"));
        app.MapMethods("/s/{token}", new[] { HttpMethods.Get, HttpMethods.Head }, (HttpContext context) => HandleSharePageAsync(context));
        app.MapMethods("/s/{token}/download", new[] { HttpMethods.Get, HttpMethods.Head }, (HttpContext context) => HandleDownloadAsync(context));
        _application = app;
        await app.StartAsync(cancellationToken);
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null) return;
        try { await _application.StopAsync(cancellationToken); }
        finally
        {
            await _application.DisposeAsync();
            _application = null;
            IsRunning = false;
        }
    }

    private async Task HandleSharePageAsync(HttpContext context)
    {
        var token = context.Request.RouteValues["token"]?.ToString();
        if (!_shares.TryGet(token ?? string.Empty, out var share))
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "Link non disponibile", "Questo link non esiste, è stato revocato o è scaduto.");
            return;
        }
        if (!IsFileAvailable(share, out var errorStatus))
        {
            await WriteErrorAsync(context, errorStatus, "File non disponibile", "Il file è stato spostato, eliminato o modificato dopo la creazione del link.");
            return;
        }

        var html = BuildDownloadPage(share);
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        AddSecurityHeaders(context.Response);
        context.Response.Headers["Cache-Control"] = "no-store";
        if (!HttpMethods.IsHead(context.Request.Method))
            await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    private async Task HandleDownloadAsync(HttpContext context)
    {
        var token = context.Request.RouteValues["token"]?.ToString();
        if (!_shares.TryGet(token ?? string.Empty, out var share))
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "Link non disponibile", "Questo link non esiste, è stato revocato o è scaduto.");
            return;
        }
        if (!IsFileAvailable(share, out var errorStatus))
        {
            await WriteErrorAsync(context, errorStatus, "File non disponibile", "Il file è stato spostato, eliminato o modificato dopo la creazione del link.");
            return;
        }

        var size = share.Size;
        ByteRange? range = null;
        if (context.Request.Headers.TryGetValue("Range", out var rangeHeader))
        {
            if (!RangeParser.TryParse(rangeHeader.ToString(), size, out var parsed))
            {
                context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                context.Response.Headers["Content-Range"] = $"bytes */{size}";
                AddSecurityHeaders(context.Response);
                return;
            }
            range = parsed;
        }

        var start = range?.Start ?? 0;
        var length = range?.Length ?? size;
        context.Response.StatusCode = range.HasValue ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
        context.Response.ContentType = "application/octet-stream";
        context.Response.ContentLength = length;
        context.Response.Headers["Accept-Ranges"] = "bytes";
        context.Response.Headers["Content-Disposition"] = BuildContentDisposition(share.Name);
        context.Response.Headers["Cache-Control"] = "no-store";
        if (range is not null)
            context.Response.Headers["Content-Range"] = $"bytes {range.Value.Start}-{range.Value.End}/{size}";
        AddSecurityHeaders(context.Response);
        if (HttpMethods.IsHead(context.Request.Method) || length == 0) return;

        if (!_downloadSlots.Wait(0))
        {
            context.Response.Clear();
            await WriteErrorAsync(context, StatusCodes.Status429TooManyRequests, "Troppi download", "Il limite di quattro download simultanei è stato raggiunto. Riprova tra poco.");
            return;
        }
        Interlocked.Increment(ref _activeDownloads);
        try
        {
            await using var file = new FileStream(share.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (file.Length != size)
            {
                context.Response.Clear();
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, "File modificato", "Le dimensioni del file sono cambiate. Revoca e ricrea il link.");
                return;
            }
            file.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[64 * 1024];
            var remaining = length;
            while (remaining > 0 && !context.RequestAborted.IsCancellationRequested)
            {
                if (!_shares.IsCurrent(share)) break;
                var read = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), context.RequestAborted);
                if (read == 0) break;
                await context.Response.Body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
                remaining -= read;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, "File non disponibile", "Il file non è più accessibile dal PC mittente.");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeDownloads);
            _downloadSlots.Release();
        }
    }

    private static bool IsFileAvailable(ShareRecord share, out int errorStatus)
    {
        try
        {
            var fullPath = Path.GetFullPath(share.FullPath);
            if (!string.Equals(fullPath, share.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                errorStatus = StatusCodes.Status404NotFound;
                return false;
            }
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                errorStatus = StatusCodes.Status404NotFound;
                return false;
            }
            if (info.Length != share.Size)
            {
                errorStatus = StatusCodes.Status409Conflict;
                return false;
            }
            errorStatus = StatusCodes.Status200OK;
            return true;
        }
        catch
        {
            errorStatus = StatusCodes.Status404NotFound;
            return false;
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int status, string title, string message)
    {
        var html = $"<!doctype html><html lang=\"it\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{Escape(title)} — DropLink</title><style>body{{margin:0;background:#101610;color:#eff5ec;font:16px system-ui;display:grid;place-items:center;min-height:100vh;padding:24px;box-sizing:border-box}}main{{max-width:520px;background:#19221a;border:1px solid #304030;border-radius:20px;padding:28px}}p{{color:#a8b6a5;line-height:1.6}}</style><main><strong>DropLink</strong><h1>{Escape(title)}</h1><p>{Escape(message)}</p></main></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        AddSecurityHeaders(context.Response);
        if (!HttpMethods.IsHead(context.Request.Method))
            await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    private static void AddSecurityHeaders(HttpResponse response)
    {
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'";
    }

    private static string BuildContentDisposition(string fileName)
    {
        var fallback = new string(fileName.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_').ToArray());
        return $"attachment; filename=\"{fallback}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }

    private static string BuildDownloadPage(ShareRecord share)
    {
        var name = Escape(share.Name);
        var size = FormatSize(share.Size);
        return $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"referrer\" content=\"no-referrer\"><meta name=\"robots\" content=\"noindex,nofollow\"><title>Download — DropLink</title><style>:root{{color-scheme:dark;--bg:#101610;--panel:#19221a;--line:#304030;--text:#f1f6ef;--muted:#a7b5a5;--green:#b5f17a}}*{{box-sizing:border-box}}body{{margin:0;min-height:100vh;display:grid;place-items:center;padding:24px;background:radial-gradient(ellipse at top,#263b25 0%,var(--bg) 58%);font:16px/1.5 system-ui,-apple-system,\"Segoe UI\",sans-serif;color:var(--text)}}main{{width:min(100%,540px);border:1px solid var(--line);border-radius:24px;background:var(--panel);padding:32px;box-shadow:0 24px 80px #0005}}.brand{{display:flex;align-items:center;gap:10px;color:var(--green);font-weight:800}}.mark{{display:grid;place-items:center;width:34px;height:34px;border-radius:11px;background:var(--green);color:#17220e;font-weight:900}}.file{{margin:32px 0 8px;font-size:clamp(20px,4vw,28px);font-weight:750;overflow-wrap:anywhere}}.meta{{color:var(--muted);margin:0}}a.button{{display:flex;justify-content:center;align-items:center;margin-top:28px;border-radius:13px;padding:14px 18px;background:var(--green);color:#17220e;text-decoration:none;font-weight:800}}.notice{{margin-top:24px;padding-top:16px;border-top:1px solid var(--line);color:var(--muted);font-size:12px}}.notice strong{{color:#f4d38b}}</style></head><body><main><div class=\"brand\"><span class=\"mark\">D</span> DropLink</div><h1 class=\"file\">{name}</h1><p class=\"meta\">{size}</p><a class=\"button\" href=\"/s/{share.Token}/download\">Download file ↓</a><div class=\"notice\"><strong>Direct transfer from the sender's PC.</strong><br>For Internet sharing, use the HTTPS domain configured by the sender. Avoid unencrypted HTTP links for confidential data, and only download files you trust.</div></main></body></html>";
    }

    private static string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal).Replace("'", "&#39;", StringComparison.Ordinal);

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return unit == 0 ? $"{bytes} B" : $"{value:0.0} {units[unit]}";
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
