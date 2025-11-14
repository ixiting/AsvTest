using AsvTest.Core;
using AsvTest.UI;

namespace AsvTest;

public class AsvApp : IDisposable {

    private DroneConnection? _connection;
    private DroneConsoleView? _view;
    private MenuHandler? _menu;

    public async Task RunAsync() {
        using var cts = new CancellationTokenSource();
        _connection = new DroneConnection();

        try {
            await _connection.StartAsync(cts.Token).ConfigureAwait(false);
        } catch (Exception ex) {
            Console.WriteLine("Failed to start connection: " + ex.Message);
            throw;
        }

        _view = new DroneConsoleView();

        var telemetry = _connection.Telemetry ?? throw new InvalidOperationException("Telemetry missing");
        var controller = _connection.Controller ?? throw new InvalidOperationException("Controller missing");
        var teleSub = telemetry.Coordinates.Subscribe(c => _view.UpdatePosition(c));

        _menu = new MenuHandler();
        _menu.Start();

        var exitTcs = new TaskCompletionSource<bool>();

        var menuSub = _menu.Commands.Subscribe(OnNext);

        await exitTcs.Task.ConfigureAwait(false);

        try {
            menuSub.Dispose();
        } catch {
            // ignored
        }

        try {
            teleSub.Dispose();
        } catch {
            // ignored
        }

        _menu?.Stop();

        await _connection.DisposeAsync().ConfigureAwait(false);
        return;

        async void OnNext(string cmd) {
            try {
                switch (cmd) {
                    case "t":
                        await controller.TakeOffAsync(cancel: cts.Token).ConfigureAwait(false);
                        break;
                    case "l":
                        await controller.LandAsync(cts.Token).ConfigureAwait(false);
                        break;
                    case "h":
                        await controller.DoRtlAsync(cts.Token).ConfigureAwait(false);
                        break;
                    case "g":
                        _view.SuppressRender(true);
                        string? inputLine;
                        try {
                            Console.Clear();
                            Console.WriteLine("=== GoTo Command ===");
                            Console.WriteLine("Enter target coordinates: lat lon [alt]");
                            Console.Write("Coordinates: ");
                            inputLine = Console.ReadLine();
                        } finally {
                            _view.SuppressRender(false);
                        }

                        if (string.IsNullOrWhiteSpace(inputLine)) break;

                        var parts2 = inputLine.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts2.Length < 2) {
                            Console.WriteLine("Invalid input, need lat and lon");
                            Thread.Sleep(1200);
                            break;
                        }

                        if (!double.TryParse(parts2[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latVal) ||
                            !double.TryParse(parts2[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lonVal)) {
                            Console.WriteLine("Invalid lat/lon format");
                            Thread.Sleep(1200);
                            break;
                        }

                        var altVal = 0.0;
                        if (parts2.Length >= 3)
                            double.TryParse(parts2[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out altVal);

                        Console.WriteLine($"Going to: lat={latVal:F6} lon={lonVal:F6} alt={altVal:F1}");

                        var gotoTask = controller.GoToAsync(latVal, lonVal, altVal, cts.Token);
                        _view.SetStatus($"GoTo sent -> lat={latVal:F6} lon={lonVal:F6} alt={altVal:F1}");

                        _ = gotoTask.ContinueWith(t => {
                            if (t.IsFaulted) {
                                var be = t.Exception?.GetBaseException();
                                var msg = be?.Message ?? "GoTo failed";
                                _view.SetStatus("GoTo error: " + msg);
                                try {
                                    File.AppendAllText("commands.log", DateTime.Now.ToString("o") + " GoTo error: " + be + Environment.NewLine);
                                } catch {
                                    // ignored
                                }
                            } else {
                                _view.SetStatus("GoTo completed");
                            }
                        }, TaskScheduler.Default);

                        Console.WriteLine("GoTo command sent. Press any key to continue...");
                        Console.ReadKey(true);

                        break;
                    case "q":
                        exitTcs.TrySetResult(true);
                        break;
                }
            } catch (Exception ex) {
                _view?.SetStatus("Command error: " + ex.Message);
                try {
                    await File.AppendAllTextAsync("commands.log", DateTime.Now.ToString("o") + " " + ex + Environment.NewLine, cts.Token);
                } catch {
                    // ignored
                }
            }
        }
    }

    public void Dispose() {
        try {
            _menu?.Dispose();
        } catch {
            // ignored
        }

        try {
            _connection?.Dispose();
        } catch {
            // ignored
        }
    }

}