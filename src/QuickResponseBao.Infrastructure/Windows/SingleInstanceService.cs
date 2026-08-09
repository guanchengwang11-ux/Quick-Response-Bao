using System.IO.Pipes;

namespace QuickResponseBao.Infrastructure.Windows;

public sealed class SingleInstanceService : IDisposable
{
    public const string DefaultMutexName = @"Local\QuickResponseBao.SingleInstance";
    public const string DefaultPipeName = "QuickResponseBao.Activate";
    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private Mutex? _mutex;
    private bool _ownsMutex;
    private Task? _server;

    public SingleInstanceService(string? mutexName = null, string? pipeName = null)
    {
        _mutexName = mutexName ?? DefaultMutexName;
        _pipeName = pipeName ?? DefaultPipeName;
    }

    public bool IsPrimary => _ownsMutex;
    public event EventHandler? ActivationRequested;

    public bool TryAcquire()
    {
        if (_mutex is not null) return _ownsMutex;
        _mutex = new Mutex(true, _mutexName, out var created);
        _ownsMutex = created;
        return created;
    }

    public void StartActivationServer()
    {
        if (!_ownsMutex || _server is not null) return;
        _server = ListenAsync(_shutdown.Token);
    }

    public async Task<bool> SignalPrimaryAsync(int timeoutMilliseconds = 1500)
    {
        if (_ownsMutex) return false;
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(timeoutMilliseconds);
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await client.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
            await client.FlushAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or UnauthorizedAccessException) { return false; }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var buffer = new byte[1];
                if (await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0) ActivationRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (IOException) when (!cancellationToken.IsCancellationRequested) { await Task.Delay(100, cancellationToken).ConfigureAwait(false); }
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        if (_ownsMutex) { try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { } }
        _mutex?.Dispose(); _shutdown.Dispose(); _ownsMutex = false; GC.SuppressFinalize(this);
    }
}
