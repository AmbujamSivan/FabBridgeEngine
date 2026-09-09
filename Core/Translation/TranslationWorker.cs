using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Models;

namespace FabBridgeEngine.Core.Translation;

/// <summary>
/// The engine's beating heart: continuously drains the event channel, translates each
/// <see cref="SecsEvent"/> into an <see cref="EquipmentStateChange"/>, and fans the
/// result out to every registered <see cref="IStateChangeSink"/>.
///
/// Kept as a plain class (not a <c>BackgroundService</c>) so it stays dependency-free
/// and unit-testable without a host. A thin hosted-service wrapper will call
/// <see cref="RunAsync"/> when we build the runnable service in a later step.
/// </summary>
public sealed class TranslationWorker
{
    private readonly IEventConsumer _events;
    private readonly ICeidTranslator _translator;
    private readonly IReadOnlyList<IStateChangeSink> _sinks;

    public TranslationWorker(
        IEventConsumer events,
        ICeidTranslator translator,
        IEnumerable<IStateChangeSink> sinks)
    {
        _events = events;
        _translator = translator;
        _sinks = sinks.ToArray();
    }

    /// <summary>
    /// Runs until the channel is completed and drained, or <paramref name="cancellationToken"/>
    /// is cancelled. One steady consumer, processing events in FIFO order.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var evt in _events.ReadAllAsync(cancellationToken))
        {
            var change = _translator.Translate(evt);

            // Fan out to every sink. One sink failing (e.g. a transient DB blip) must NOT
            // stop the pipeline or take down the other sinks — isolate each publish.
            foreach (var sink in _sinks)
            {
                try
                {
                    await sink.PublishAsync(change, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // genuine shutdown — let it propagate
                }
                catch (Exception ex)
                {
                    // In a later step this becomes structured logging (ILogger).
                    Console.Error.WriteLine(
                        $"[TranslationWorker] sink {sink.GetType().Name} failed for " +
                        $"{change.EquipmentId} (CEID {change.SourceCeid}): {ex.Message}");
                }
            }
        }
    }
}
