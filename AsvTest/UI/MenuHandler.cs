using R3;

namespace AsvTest.UI;

public class MenuHandler : IDisposable, IAsyncDisposable {

    private readonly Subject<string> _commands = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;

    public Observable<string> Commands => _commands.AsObservable();

    public Task StartAsync(CancellationToken token = default)
    {
        if (_loopTask is not null && !_loopTask.IsCompleted)
            return Task.CompletedTask;

        _cts = token == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(token);

        _loopTask = Task.Run(() => LoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync() {
        if (_cts is null) return;
        await _cts.CancelAsync();
        try {
            if (_loopTask is not null) await _loopTask.ConfigureAwait(false);
        } catch (OperationCanceledException) { } finally {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    private async Task LoopAsync(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            try {
                if (!Console.KeyAvailable) {
                    await Task.Delay(50, token).ConfigureAwait(false);
                    continue;
                }

                var keyInfo = Console.ReadKey(true);

                string? cmd = keyInfo.Key switch {
                    ConsoleKey.T => "t",
                    ConsoleKey.L => "l",
                    ConsoleKey.R => "r",
                    ConsoleKey.G => "g",
                    ConsoleKey.Q => "q",
                    _ => null
                };

                if (cmd is null) {
                    var ch = char.ToLowerInvariant(keyInfo.KeyChar);
                    cmd = ch switch {
                        't' => "t", 'l' => "l", 'r' => "r", 'g' => "g", 'q' => "q",
                        'е' => "t", 'л' => "l", 'к' => "r", 'п' => "g", 'й' => "q",
                        _ => null
                    };
                }

                if (cmd is not null) {
                    _commands.OnNext(cmd);
                    if (cmd == "q") break;
                }
            } catch (OperationCanceledException) {
                break;
            } catch (Exception) {
                // ignored
            }
        }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _commands.OnCompleted();
        _commands.Dispose();
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
        _commands.OnCompleted();
        _commands.Dispose();
    }

}