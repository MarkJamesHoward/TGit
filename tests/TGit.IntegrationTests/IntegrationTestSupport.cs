using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TGit.IntegrationTests;

internal static class IntegrationTestSupport
{
    internal static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var slnPath = Path.Combine(current.FullName, "TGit.sln");
            if (File.Exists(slnPath))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find TGit.sln from test output directory.");
    }

    internal static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IEnumerable<string> args,
        string workingDirectory,
        IDictionary<string, string?>? env = null,
        int timeoutMs = 30000
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (env != null)
        {
            foreach (var kvp in env)
            {
                process.StartInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeoutMs);
        await process.WaitForExitAsync(cts.Token);

        var stdout = await outputTask;
        var stderr = await errorTask;

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    internal static async Task<string> CreateTempGitRepoAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "tgit-it", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var initResult = await RunProcessAsync("git", ["init"], root);
        if (initResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"git init failed: {initResult.StdErr}");
        }

        var emailResult = await RunProcessAsync(
            "git",
            ["config", "user.email", "integration@test.local"],
            root
        );
        if (emailResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git config user.email failed: {emailResult.StdErr}"
            );
        }

        var nameResult = await RunProcessAsync(
            "git",
            ["config", "user.name", "Integration Tester"],
            root
        );
        if (nameResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git config user.name failed: {nameResult.StdErr}"
            );
        }

        return root;
    }

    internal static async Task<ProcessResult> RunTgitAsync(
        string repoRoot,
        string workingDirectory,
        IEnumerable<string> tgitArgs,
        IDictionary<string, string?>? extraEnv = null
    )
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TGIT_DEBUG"] = "1",
        };

        if (extraEnv != null)
        {
            foreach (var kvp in extraEnv)
            {
                env[kvp.Key] = kvp.Value;
            }
        }

        var args = new List<string>
        {
            "run",
            "--project",
            Path.Combine(repoRoot, "cli", "TGit.csproj"),
            "--",
        };
        args.AddRange(tgitArgs);

        return await RunProcessAsync("dotnet", args, workingDirectory, env, timeoutMs: 60000);
    }
}

internal sealed class LoopbackHttpCaptureServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly TaskCompletionSource<CapturedHttpRequest> _requestTcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly CancellationTokenSource _cts = new();

    private LoopbackHttpCaptureServer(TcpListener listener)
    {
        _listener = listener;
        _listener.Start();
        _ = AcceptOnceAsync();
    }

    internal string BaseUrl
    {
        get
        {
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            return $"http://127.0.0.1:{endpoint.Port}";
        }
    }

    internal static LoopbackHttpCaptureServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        return new LoopbackHttpCaptureServer(listener);
    }

    internal async Task<CapturedHttpRequest> WaitForRequestAsync(int timeoutMs = 15000)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        await using var _ = timeout.Token.Register(() => _requestTcs.TrySetCanceled(timeout.Token));
        return await _requestTcs.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        await Task.CompletedTask;
    }

    private async Task AcceptOnceAsync()
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
            using var stream = client.GetStream();

            var headerBuffer = new List<byte>();
            var temp = new byte[1024];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(temp, _cts.Token)) > 0)
            {
                headerBuffer.AddRange(temp.AsSpan(0, bytesRead).ToArray());
                if (TryFindHeaderTerminator(headerBuffer, out var headerEndIndex))
                {
                    var allBytes = headerBuffer.ToArray();
                    var headerText = Encoding.UTF8.GetString(allBytes, 0, headerEndIndex);
                    var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                    var requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    var method = requestLine[0];
                    var path = requestLine.Length > 1 ? requestLine[1] : "/";

                    var contentLength = 0;
                    foreach (var line in lines.Skip(1))
                    {
                        if (
                            line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(line["Content-Length:".Length..].Trim(), out var parsed)
                        )
                        {
                            contentLength = parsed;
                        }
                    }

                    var bodyStart = headerEndIndex + 4;
                    var bodyBytes = new byte[contentLength];
                    var alreadyInBuffer = Math.Max(0, allBytes.Length - bodyStart);
                    if (alreadyInBuffer > 0)
                    {
                        Array.Copy(
                            allBytes,
                            bodyStart,
                            bodyBytes,
                            0,
                            Math.Min(alreadyInBuffer, contentLength)
                        );
                    }

                    var offset = Math.Min(alreadyInBuffer, contentLength);
                    while (offset < contentLength)
                    {
                        var read = await stream.ReadAsync(
                            bodyBytes.AsMemory(offset, contentLength - offset),
                            _cts.Token
                        );
                        if (read == 0)
                        {
                            break;
                        }
                        offset += read;
                    }

                    var body = Encoding.UTF8.GetString(bodyBytes, 0, offset);

                    var responseBytes = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    );
                    await stream.WriteAsync(responseBytes, _cts.Token);

                    _requestTcs.TrySetResult(new CapturedHttpRequest(method, path, body));
                    return;
                }
            }

            _requestTcs.TrySetException(new InvalidOperationException("No HTTP request captured."));
        }
        catch (Exception ex)
        {
            _requestTcs.TrySetException(ex);
        }
    }

    private static bool TryFindHeaderTerminator(List<byte> bytes, out int index)
    {
        for (var i = 0; i <= bytes.Count - 4; i++)
        {
            if (
                bytes[i] == '\r'
                && bytes[i + 1] == '\n'
                && bytes[i + 2] == '\r'
                && bytes[i + 3] == '\n'
            )
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }
}

internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

internal sealed record CapturedHttpRequest(string Method, string Path, string Body);
