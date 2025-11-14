using System.Reactive.Subjects;

namespace AsvTest.UI;

public class MenuHandler : IDisposable {

    private readonly Subject<string> _commands = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public IObservable<string> Commands => _commands;

    public void Start() {
        if (_loopTask != null) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => Loop(_cts.Token));
    }

    public void Stop() {
        _cts?.Cancel();
        try {
            _loopTask?.Wait(500);
        } catch {
            // ignored
        }

        _loopTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    private void Loop(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            var key = Console.ReadKey(true);
            var k = key.KeyChar.ToString().ToLowerInvariant();
            if (string.IsNullOrEmpty(k)) continue;
            _commands.OnNext(k);
            if (k == "q") break;
        }
    }

    public void Dispose() {
        Stop();
        _commands.OnCompleted();
        _commands.Dispose();
    }

}