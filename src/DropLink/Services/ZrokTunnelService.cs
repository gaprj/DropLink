using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DropLink.Services;

/// <summary>
/// Starts and stops a temporary public zrok share for the lifetime of a DropLink session.
/// The zrok executable is downloaded from the official GitHub release and checksum-verified
/// on first use. Account credentials are owned and stored by zrok, not by DropLink.
/// </summary>
public sealed class ZrokTunnelService : IAsyncDisposable
{
    private const string ReleaseApi = "https://api.github.com/repos/openziti/zrok/releases/latest";
    private const string AccountUrl = "https://myzrok.io/";
    private static readonly HttpClient DownloadClient = CreateHttpClient();
    private static readonly Regex PublicUrlRegex = new(
        @"https://[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?(?:\:[0-9]{1,5})?(?:/[A-Za-z0-9._~:/?#\[\]@!$&'()*+,;=%-]*)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    // Current zrok CLI logs may print the public endpoint as a bare hostname (often inside JSON
    // log output), without an https:// prefix. Accept only the provider's public-share domains.
    private static readonly Regex PublicHostRegex = new(
        @"(?<![A-Za-z0-9.-])(?<host>[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.(?:shares|share)\.zrok\.io)(?![A-Za-z0-9.-])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ChecksumLineRegex = new(
        @"(?m)^\s*([a-fA-F0-9]{64})\s+\*?([^\r\n]+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly StringBuilder _output = new();
    private readonly object _outputLock = new();
    private Process? _process;
    private Uri? _publicBaseUri;

    public bool IsRunning
    {
        get
        {
            try { return _publicBaseUri is not null && _process is { HasExited: false }; }
            catch { return false; }
        }
    }

    public Uri? PublicBaseUri => IsRunning ? _publicBaseUri : null;

    /// <summary>Search known locations, including DropLink's per-user managed tools directory.</summary>
    public static string? FindExecutable()
    {
        var fileName = OperatingSystem.IsWindows() ? "zrok2.exe" : "zrok2";
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "tools", fileName),
            Path.Combine(localAppData, "DropLink", "tools", fileName),
            Path.Combine(localAppData, "Programs", "zrok", fileName)
        };

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try { candidates.Add(Path.Combine(directory.Trim('"'), fileName)); }
            catch { /* Ignore malformed PATH entries. */ }
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Ensures the official zrok2 executable exists locally. Users no longer need to install it by hand.
    /// </summary>
    public async Task<string> EnsureExecutableAsync(CancellationToken cancellationToken = default)
    {
        var existing = FindExecutable();
        if (existing is not null) return existing;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Il download automatico di zrok è configurato per Windows.");
        if (Environment.Is64BitOperatingSystem is false)
            throw new PlatformNotSupportedException("La versione automatica di zrok richiede Windows a 64 bit.");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var toolsDirectory = Path.Combine(localAppData, "DropLink", "tools");
        var destination = Path.Combine(toolsDirectory, "zrok2.exe");
        Directory.CreateDirectory(toolsDirectory);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var token = timeout.Token;

            using var releaseRequest = new HttpRequestMessage(HttpMethod.Get, ReleaseApi);
            releaseRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var releaseResponse = await DownloadClient.SendAsync(releaseRequest, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
            releaseResponse.EnsureSuccessStatusCode();
            await using var releaseStream = await releaseResponse.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var release = await JsonDocument.ParseAsync(releaseStream, cancellationToken: token).ConfigureAwait(false);

            var root = release.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var version = tag.TrimStart('v', 'V');
            if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant))
                throw new InvalidDataException("GitHub ha restituito una versione zrok non valida.");

            var archiveName = $"zrok_{version}_windows_amd64.tar.gz";
            string? archiveUrl = null;
            string? checksumUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var url = asset.GetProperty("browser_download_url").GetString();
                if (string.Equals(name, archiveName, StringComparison.Ordinal)) archiveUrl = url;
                if (string.Equals(name, "checksums.sha256.txt", StringComparison.Ordinal)) checksumUrl = url;
            }

            if (string.IsNullOrWhiteSpace(archiveUrl) || string.IsNullOrWhiteSpace(checksumUrl))
                throw new InvalidDataException($"Il rilascio ufficiale zrok {tag} non contiene l'archivio Windows o il file dei checksum atteso.");

            var checksums = await DownloadClient.GetStringAsync(checksumUrl, token).ConfigureAwait(false);
            var expectedHash = FindExpectedChecksum(checksums, archiveName);
            if (expectedHash is null)
                throw new InvalidDataException($"Non è stato trovato il checksum ufficiale per {archiveName}.");

            var archiveBytes = await DownloadClient.GetByteArrayAsync(archiveUrl, token).ConfigureAwait(false);
            var actualHash = Convert.ToHexString(SHA256.HashData(archiveBytes));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Il controllo d'integrità del download zrok non è riuscito. Il file è stato rifiutato.");

            var temporaryExecutable = destination + ".download";
            try
            {
                await using var compressed = new MemoryStream(archiveBytes, writable: false);
                await using var gzip = new GZipStream(compressed, System.IO.Compression.CompressionMode.Decompress);
                using var tarReader = new TarReader(gzip);
                TarEntry? entry;
                var found = false;
                while ((entry = tarReader.GetNextEntry(copyData: true)) is not null)
                {
                    if (!string.Equals(Path.GetFileName(entry.Name), "zrok2.exe", StringComparison.OrdinalIgnoreCase) || entry.DataStream is null)
                        continue;

                    await using (var output = new FileStream(temporaryExecutable, FileMode.Create, FileAccess.Write, FileShare.None))
                        await entry.DataStream.CopyToAsync(output, token).ConfigureAwait(false);
                    found = true;
                    break;
                }

                if (!found || !File.Exists(temporaryExecutable) || new FileInfo(temporaryExecutable).Length < 1024 * 1024)
                    throw new InvalidDataException("L'archivio zrok scaricato non contiene un eseguibile zrok2.exe valido.");

                File.Move(temporaryExecutable, destination, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(temporaryExecutable)) File.Delete(temporaryExecutable); }
                catch { /* Clean-up is best effort. */ }
            }

            return destination;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Il download automatico di zrok ha superato il limite di 3 minuti. Controlla la connessione e riprova.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("Non riesco a scaricare zrok dal rilascio ufficiale GitHub. Controlla la connessione Internet e riprova.", exception);
        }
    }

    public async Task EnableAsync(string accountToken, CancellationToken cancellationToken = default)
    {
        var token = accountToken?.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Inserisci il token personale del tuo account zrok.", nameof(accountToken));
        if (token.Length > 4096 || token.Any(char.IsControl)) throw new ArgumentException("Il token inserito non è valido.", nameof(accountToken));

        var executable = await EnsureExecutableAsync(cancellationToken).ConfigureAwait(false);
        var startInfo = CreateStartInfo(executable);
        startInfo.ArgumentList.Add("enable");
        startInfo.ArgumentList.Add(token);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Non è stato possibile avviare la configurazione zrok.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            var output = (stdout + Environment.NewLine + stderr).Replace(token, "[token nascosto]", StringComparison.Ordinal);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("zrok non è riuscito a configurare l'account. Controlla che il token sia corretto e che l'account sia attivo.\n\n" + Tail(output));
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    public async Task<Uri> StartAsync(int localPort, CancellationToken cancellationToken = default)
    {
        if (localPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(localPort));
        if (IsRunning && _publicBaseUri is not null) return _publicBaseUri;
        await StopAsync().ConfigureAwait(false);

        // Self-install on first use; no user-facing installer or PowerShell command is required.
        var executable = await EnsureExecutableAsync(cancellationToken).ConfigureAwait(false);
        lock (_outputLock) _output.Clear();

        var startInfo = CreateStartInfo(executable);
        startInfo.ArgumentList.Add("share");
        startInfo.ArgumentList.Add("public");
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{localPort}");

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var endpointReady = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OutputDataReceived += (_, args) => ProcessOutputLine(args.Data, endpointReady);
        process.ErrorDataReceived += (_, args) => ProcessOutputLine(args.Data, endpointReady);
        process.Exited += (_, _) => exited.TrySetResult(true);

        try
        {
            if (!process.Start()) throw new InvalidOperationException("Non è stato possibile avviare zrok2.");
            _process = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var completed = await Task.WhenAny(endpointReady.Task, exited.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token)).ConfigureAwait(false);

            if (completed == exited.Task && !endpointReady.Task.IsCompleted)
            {
                // Allow asynchronous stdout/stderr handlers to flush before interpreting the failure.
                await Task.Delay(150, CancellationToken.None).ConfigureAwait(false);
                if (!endpointReady.Task.IsCompleted)
                    endpointReady.TrySetException(BuildFailureException(process.ExitCode));
            }
            else if (completed != endpointReady.Task && !endpointReady.Task.IsCompleted)
            {
                if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();
                endpointReady.TrySetException(new TimeoutException(
                    "zrok non ha restituito il link pubblico entro 60 secondi. Controlla la connessione Internet e la configurazione dell'account.\n\n" + GetOutputTail()));
            }

            var endpoint = await endpointReady.Task.ConfigureAwait(false);
            _publicBaseUri = new Uri(endpoint.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/", UriKind.Absolute);
            return _publicBaseUri;
        }
        catch
        {
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        _publicBaseUri = null;
        var process = Interlocked.Exchange(ref _process, null);
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        catch (TimeoutException) { }
        finally { process.Dispose(); }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DropLink", "0.4.7"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static ProcessStartInfo CreateStartInfo(string executable) => new()
    {
        FileName = executable,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    private static string? FindExpectedChecksum(string checksums, string fileName)
    {
        foreach (Match match in ChecksumLineRegex.Matches(checksums))
        {
            var listedName = Path.GetFileName(match.Groups[2].Value.Trim().TrimStart('*'));
            if (string.Equals(listedName, fileName, StringComparison.Ordinal)) return match.Groups[1].Value.ToUpperInvariant();
        }
        return null;
    }

    private void ProcessOutputLine(string? line, TaskCompletionSource<Uri> endpointReady)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_outputLock)
        {
            _output.AppendLine(line);
            if (_output.Length > 12000) _output.Remove(0, _output.Length - 12000);
        }

        if (LooksLikeEnvironmentSetupRequired(line))
        {
            endpointReady.TrySetException(new ZrokSetupRequiredException(
                "Per attivare il link Internet serve abilitare una volta il tuo account zrok. DropLink può eseguire questa configurazione senza usare PowerShell."));
            return;
        }

        // zrok versions differ in how they print endpoints: some include an HTTPS URL,
        // others log only the hostname, sometimes inside a JSON message. Normalize both forms.
        var publicEndpoint = ExtractPublicEndpoint(line);
        if (publicEndpoint is not null)
            endpointReady.TrySetResult(publicEndpoint);
    }

    private Exception BuildFailureException(int exitCode)
    {
        var tail = GetOutputTail();
        if (LooksLikeEnvironmentSetupRequired(tail))
            return new ZrokSetupRequiredException(
                "Per attivare il link Internet serve abilitare una volta il tuo account zrok. DropLink può eseguire questa configurazione senza usare PowerShell.");
        return new InvalidOperationException($"zrok2 si è chiuso prima di creare il link pubblico (codice {exitCode}).\n\n{tail}");
    }


    internal static Uri? ExtractPublicEndpoint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // The CLI's structured log sometimes serializes newlines as literal \n sequences.
        // Normalize these separators so a hostname immediately after the escape is recognized.
        var normalizedText = text.Replace("\\r", "\r", StringComparison.Ordinal)
                                 .Replace("\\n", "\n", StringComparison.Ordinal)
                                 .Replace("\\t", "\t", StringComparison.Ordinal);

        foreach (Match match in PublicUrlRegex.Matches(normalizedText))
        {
            var candidate = match.Value.TrimEnd('.', ',', ';', ')', ']', '"');
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsZrokPublicHost(uri.Host)) continue;
            return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
        }

        foreach (Match match in PublicHostRegex.Matches(normalizedText))
        {
            var host = match.Groups["host"].Value.TrimEnd('.', ',', ';', ')', ']', '"');
            if (!IsZrokPublicHost(host)) continue;
            return new Uri($"https://{host}/", UriKind.Absolute);
        }

        return null;
    }

    private static bool IsZrokPublicHost(string host) =>
        host.EndsWith(".share.zrok.io", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".shares.zrok.io", StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeEnvironmentSetupRequired(string text)
    {
        var value = text.ToLowerInvariant();
        return value.Contains("unable to load environment", StringComparison.Ordinal) ||
               value.Contains("did you 'zrok2 enable'", StringComparison.Ordinal) ||
               value.Contains("environment is not enabled", StringComparison.Ordinal) ||
               value.Contains("environment not enabled", StringComparison.Ordinal) ||
               value.Contains("no enabled environment", StringComparison.Ordinal) ||
               value.Contains("account token is not set", StringComparison.Ordinal) ||
               value.Contains("run zrok2 enable", StringComparison.Ordinal) ||
               value.Contains("zrok2 enable <", StringComparison.Ordinal) ||
               value.Contains("enable your environment", StringComparison.Ordinal) ||
               value.Contains("environment has not been enabled", StringComparison.Ordinal);
    }

    private string GetOutputTail()
    {
        lock (_outputLock) return Tail(_output.ToString());
    }

    private static string Tail(string output)
    {
        output = output.Trim();
        if (output.Length > 3500) output = output[^3500..];
        return output.Length == 0 ? "Nessun dettaglio restituito da zrok2." : output;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

public sealed class ZrokSetupRequiredException(string message) : InvalidOperationException(message);
