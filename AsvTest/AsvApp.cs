using R3;

using Spectre.Console;

using AsvTest.Core;
using AsvTest.UI;

namespace AsvTest;

public class AsvApp(string host = "127.0.0.1", int port = 5760) : IAsyncDisposable {

    private readonly DroneConnection _connection = new(host, port);
    private readonly DroneConsoleView? _view = new();
    private readonly MenuHandler? _menu = new();
    private DroneTelemetry.TelemetrySample? _lastSample;

    public async Task RunAsync(CancellationToken appCancelToken) {
        try {
            await _connection.StartAsync(appCancelToken);
            AnsiConsole.MarkupLine("[green]✓ Connected to drone[/]");
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            AnsiConsole.MarkupLine($"[red]✗ Connection failed: {ex.Message}[/]");
            return;
        }

        var controller = _connection.Controller ?? throw new InvalidOperationException("Controller not initialized");
        var telemetry = _connection.Telemetry ?? throw new InvalidOperationException("Telemetry not initialized");

        _menu?.StartAsync();

        var commandTcs = new TaskCompletionSource<bool>();

        using var subscription = _menu!.Commands.Subscribe(cmd => {
            _ = HandleCommandAsync(cmd, controller);
            if (cmd == "q") commandTcs.TrySetResult(true);
        });

        using var telemetrySub = telemetry.Coordinates.Subscribe(sample => {
            _lastSample = sample;
            _view?.UpdatePosition(sample);
        });

        try {
            await commandTcs.Task;
        } catch (OperationCanceledException) { } finally {
            if (_menu is not null) {
                try {
                    await _menu.StopAsync().ConfigureAwait(false);
                } catch {
                    // ignored
                }
            }
        }
    }

    private async Task HandleCommandAsync(string cmd, DroneController controller) {
        try {
            switch (cmd) {
                case "t":
                    await controller.TakeOffAsync();
                    break;

                case "l":
                    await controller.LandAsync();
                    break;

                case "r":
                    await controller.DoRtlAsync();
                    break;

                case "g":
                    await HandleGoTo(controller);
                    break;

                case "q":
                    break;

                default:
                    AnsiConsole.MarkupLine($"[yellow]Unknown command: {cmd}[/]");
                    break;
            }
        } catch (OperationCanceledException) { } catch (Exception ex) {
            AnsiConsole.MarkupLine($"[red]✗ Command error: {ex.Message}[/]");
        }
    }

    private async Task HandleGoTo(DroneController controller) {
        var telemetry = _connection.Telemetry;
        if (telemetry is null) {
            _view?.SetStatus("No telemetry available");
            return;
        }

        _view?.SuppressRender(true);
        try {
            var homeStr = GetHomeCoordsString();
            if (!string.IsNullOrEmpty(homeStr)) {
                _view?.SetStatus($"Home: {homeStr}");
            }

            if (_lastSample is not null) {
                _view?.SetStatus($"Current: lat={_lastSample.Lat:F6} lon={_lastSample.Lon:F6} abs={_lastSample.AbsAlt:F1} rel={_lastSample.RelAlt:F1}");
            }

            Console.Write("Target lat: ");
            var latStr = Console.ReadLine();
            if (!TryParseDoubleFlexible(latStr, out var lat)) {
                _view?.SetStatus("Invalid latitude");
                return;
            }

            Console.Write("Target lon: ");
            var lonStr = Console.ReadLine();
            if (!TryParseDoubleFlexible(lonStr, out var lon)) {
                _view?.SetStatus("Invalid longitude");
                return;
            }

            Console.Write("Target alt: ");
            var altStr = Console.ReadLine();
            if (!TryParseDoubleFlexible(altStr, out var alt)) {
                _view?.SetStatus("Invalid altitude");
                return;
            }

            double targetAltAbs = alt;
            double fromLat = double.NaN, fromLon = double.NaN, fromAbs = double.NaN, fromRel = double.NaN;
            if (_lastSample is not null) {
                targetAltAbs = _lastSample.AbsAlt + alt;
                fromLat = _lastSample.Lat;
                fromLon = _lastSample.Lon;
                fromAbs = _lastSample.AbsAlt;
                fromRel = _lastSample.RelAlt;
            }

            _view?.SetStatus(
                !double.IsNaN(fromLat)
                    ? $"GoTo From: lat={fromLat:F6} lon={fromLon:F6} abs={fromAbs:F1} rel={fromRel:F1} -> To: lat={lat:F6} lon={lon:F6} relAlt={alt:F1} targetAbs={targetAltAbs:F1}"
                    : $"GoTo To: lat={lat:F6} lon={lon:F6} relAlt={alt:F1} targetAbs={targetAltAbs:F1}");

            try {
                await controller.GoToAsync(lat, lon, targetAltAbs);
                _view?.SetStatus(!double.IsNaN(fromLat)
                    ? $"GoTo command sent From: lat={fromLat:F6} lon={fromLon:F6} -> To: lat={lat:F6} lon={lon:F6} targetAbs={targetAltAbs:F1}"
                    : $"GoTo command sent -> lat={lat:F6} lon={lon:F6} targetAbs={targetAltAbs:F1}");
            } catch (OperationCanceledException) {
                _view?.SetStatus("GoTo cancelled");
            } catch (Exception ex) {
                _view?.SetStatus($"GoTo failed: {ex.Message}");
            }
        } finally {
            _view?.SuppressRender(false);
        }
    }

    private string? GetHomeCoordsString() {
        try {
            var posClient = _connection.PositionClient as object;

            if (posClient is null) return null;

            var prop = posClient.GetType().GetProperty("Home")
                       ?? posClient.GetType().GetProperty("HomePosition")
                       ?? posClient.GetType().GetProperty("HomePoint");

            var homeContainer = prop?.GetValue(posClient);
            if (homeContainer is null) {
                var directLat = posClient.GetType().GetProperty("HomeLat")?.GetValue(posClient);
                var directLon = posClient.GetType().GetProperty("HomeLon")?.GetValue(posClient);
                if (directLat is not null && directLon is not null) {
                    double homeLat = Convert.ToDouble(directLat) / (Math.Abs(Convert.ToDouble(directLat)) > 1000 ? 1e7 : 1.0);
                    double homeLon = Convert.ToDouble(directLon) / (Math.Abs(Convert.ToDouble(directLon)) > 1000 ? 1e7 : 1.0);
                    return $"lat={homeLat:F6} lon={homeLon:F6}";
                }

                return null;
            }

            var currentProp = homeContainer.GetType().GetProperty("CurrentValue") ?? homeContainer.GetType().GetProperty("Value");
            var hv = currentProp?.GetValue(homeContainer);
            if (hv is null) return null;

            var latProp = hv.GetType().GetProperty("Lat") ?? hv.GetType().GetProperty("Latitude");
            var lonProp = hv.GetType().GetProperty("Lon") ?? hv.GetType().GetProperty("Longitude");
            var altProp = hv.GetType().GetProperty("RelativeAlt") ?? hv.GetType().GetProperty("Alt") ?? hv.GetType().GetProperty("Altitude");

            if (latProp is null || lonProp is null) return null;

            var rlat = latProp.GetValue(hv);
            var rlon = lonProp.GetValue(hv);
            double homeLatVal, homeLonVal, homeAltVal = double.NaN;
            if (rlat is long llat) homeLatVal = llat / 1e7;
            else homeLatVal = Convert.ToDouble(rlat) / (Math.Abs(Convert.ToDouble(rlat)) > 1000 ? 1e7 : 1.0);
            if (rlon is long llon) homeLonVal = llon / 1e7;
            else homeLonVal = Convert.ToDouble(rlon) / (Math.Abs(Convert.ToDouble(rlon)) > 1000 ? 1e7 : 1.0);
            if (altProp is null) {
                return double.IsNaN(homeAltVal) ? $"lat={homeLatVal:F6} lon={homeLonVal:F6}" : $"lat={homeLatVal:F6} lon={homeLonVal:F6} alt={homeAltVal:F1}";
            }

            var ralt = altProp.GetValue(hv);
            
            homeAltVal = ralt switch {
                int ia => ia / 1000.0,
                long la => la / 1000.0,
                _ => Convert.ToDouble(ralt)
            };

            return double.IsNaN(homeAltVal) ? $"lat={homeLatVal:F6} lon={homeLonVal:F6}" : $"lat={homeLatVal:F6} lon={homeLonVal:F6} alt={homeAltVal:F1}";
        } catch {
            return null;
        }
    }

    private static bool TryParseDoubleFlexible(string? input, out double value) {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (double.TryParse(input, out value)) return true;

        var alt = input.Replace(',', '.');
        if (double.TryParse(alt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) return true;

        alt = input.Replace('.', ',');
        return double.TryParse(alt, out value);
    }

    async ValueTask IAsyncDisposable.DisposeAsync() {
        if (_menu is IAsyncDisposable am) {
            try {
                await am.DisposeAsync().ConfigureAwait(false);
            } catch {
                // ignored
            }
        } else {
            try {
                _menu?.Dispose();
            } catch {
                // ignored
            }
        }

        _connection.Controller?.Dispose();
        await _connection.DisposeAsync();
    }

}