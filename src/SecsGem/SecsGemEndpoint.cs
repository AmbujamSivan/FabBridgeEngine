using Microsoft.Extensions.Options;
using Secs4Net;

namespace FabBridgeEngine.SecsGem;

/// <summary>No-op logger; every <see cref="ISecsGemLogger"/> method has a default implementation.</summary>
internal sealed class NullSecsGemLogger : ISecsGemLogger { }

/// <summary>
/// Builds and owns a secs4net HSMS endpoint (the connection + the SECS/GEM layer) constructed
/// by hand rather than via DI — because we run two endpoints (equipment + host) in one process,
/// each needing its own options, which a single DI registration can't express.
/// </summary>
internal sealed class SecsGemEndpoint : IAsyncDisposable
{
    public ISecsConnection Connection { get; }
    public ISecsGem Gem { get; }

    private SecsGemEndpoint(ISecsConnection connection, ISecsGem gem)
    {
        Connection = connection;
        Gem = gem;
    }

    public static SecsGemEndpoint Create(SecsGemOptions options, ISecsGemLogger logger)
    {
        var opt = Options.Create(options);
        var connection = new HsmsConnection(opt, logger);         // ISecsConnection
        var gem = new Secs4Net.SecsGem(opt, connection, logger);  // ISecsGem (fully-qualified: our namespace is also 'SecsGem')
        return new SecsGemEndpoint(connection, gem);
    }

    /// <summary>Completes once the HSMS link reaches <see cref="ConnectionState.Selected"/> (ready to exchange messages).</summary>
    public async Task WaitUntilSelectedAsync(CancellationToken ct)
    {
        if (Connection.State == ConnectionState.Selected) return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? _, ConnectionState state)
        {
            if (state == ConnectionState.Selected) tcs.TrySetResult();
        }

        Connection.ConnectionChanged += OnChanged;
        try
        {
            if (Connection.State == ConnectionState.Selected) return;   // re-check after subscribing (race)
            await using (ct.Register(() => tcs.TrySetCanceled(ct)))
                await tcs.Task;
        }
        finally
        {
            Connection.ConnectionChanged -= OnChanged;
        }
    }

    public async ValueTask DisposeAsync()
    {
        (Gem as IDisposable)?.Dispose();
        if (Connection is IAsyncDisposable ad) await ad.DisposeAsync();
    }
}
