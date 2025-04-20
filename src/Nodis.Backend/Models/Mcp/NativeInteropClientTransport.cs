using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Utils.Json;
using Nodis.Backend.Interfaces;
using Nodis.Core.Extensions;
using Nodis.Core.Models;

namespace Nodis.Backend.Models.Mcp;

public partial class NativeInteropClientTransport(
    INativeInterop nativeInterop,
    NativeInteropClientTransportOptions options,
    ILoggerFactory? loggerFactory = null) : IClientTransport
{
    public string Name { get; } = options.Name ?? $"native-{NameRegex().Replace(Path.GetFileName(options.Command), "-")}";

    public async Task<ITransport> ConnectAsync(CancellationToken cancellationToken = new())
    {
        var process = nativeInterop.CreateProcess(
            new BashProcessCreationOptions
            {
                CommandLines = [$"{options.Command} {string.Join(" ", options.Arguments ?? Array.Empty<string>())}"],
                WorkingDirectory = options.WorkingDirectory,
                EnvironmentVariables = options.EnvironmentVariables ?? new Dictionary<string, string>(),
                KillOnExit = true,
                AutoExit = false
            });
        await process.StartAsync(cancellationToken);
        return new Transport(process, loggerFactory);
    }

    [GeneratedRegex(@"[\s\.]+")]
    private static partial Regex NameRegex();

    private class Transport : TransportBase
    {
        private readonly IProcess process;
        private readonly SemaphoreSlim sendLock = new(1, 1);
        private readonly CancellationTokenSource cancellationTokenSource = new();

        public Transport(IProcess process, ILoggerFactory? loggerFactory) : base(loggerFactory)
        {
            this.process = process;
            new Task<Task>(
                static that =>
                {
                    var transport = that.To<Transport>()!;
                    return transport.ReadMessagesAsync(transport.cancellationTokenSource.Token);
                },
                this,
                TaskCreationOptions.DenyChildAttach).Start();
            SetConnected(true);
        }

        public override async Task SendMessageAsync(IJsonRpcMessage message, CancellationToken cancellationToken = new())
        {
            if (!IsConnected) throw new McpTransportException("Transport is not connected");

            var json = JsonSerializer.Serialize(message, McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IJsonRpcMessage)));
            await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Write the message followed by a newline using our UTF-8 writer
                await process.StandardInput.WriteLineAsync(json).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new McpTransportException("Failed to send message", ex);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task ReadMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is not { } line) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    await ProcessMessageAsync(line, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await DisposeAsync();
            }
        }

        private async Task ProcessMessageAsync(string line, CancellationToken cancellationToken)
        {
            var message = (IJsonRpcMessage?)JsonSerializer.Deserialize(
                line.AsSpan().Trim(),
                McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IJsonRpcMessage)));
            if (message != null)
            {
                await WriteMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await cancellationTokenSource.CancelAsync();
            if (!process.HasExited)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignored
                }
            }
            await process.WaitForExitAsync(CancellationToken.None);
        }
    }
}