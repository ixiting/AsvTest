using Asv.Mavlink;

namespace AsvTest.Core;

public class DroneController : IDisposable {

    private readonly IControlClient _control;

    public ICommandClient? Command { get; }
    public DroneTelemetry Telemetry { get; }

    private readonly CancellationTokenSource _cts = new();

    public DroneController(IControlClient controlClient, ICommandClient? commandClient, DroneTelemetry telemetry) {
        _control = controlClient ?? throw new ArgumentNullException(nameof(controlClient));
        Command = commandClient;
        Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public async Task TakeOffAsync(double altitudeMeters = 10.0, CancellationToken cancel = default) {
        var token = cancel == default ? _cts.Token : cancel;

        try {
            await _control.SetGuidedMode(token).ConfigureAwait(false);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(3)) {
                var isGuided = await _control.IsGuidedMode(token).ConfigureAwait(false);
                if (isGuided) break;
                await Task.Delay(200, token).ConfigureAwait(false);
            }
        } catch (Exception ex) {
            Console.WriteLine($"Warning: SetGuidedMode failed: {ex.Message}");
        }

        try {
            await _control.TakeOff(altitudeMeters, token).ConfigureAwait(false);
        } catch (Exception ex) {
            throw new InvalidOperationException($"TakeOff failed: {ex.Message}", ex);
        }
    }

    public async Task LandAsync(CancellationToken cancel = default) {
        await _control.DoLand(cancel == default ? _cts.Token : cancel).ConfigureAwait(false);
    }

    public async Task DoRtlAsync(CancellationToken cancel = default) {
        var token = cancel == default ? _cts.Token : cancel;
        try {
            await _control.DoRtl(token).ConfigureAwait(false);
        } catch (Exception ex) {
            throw new InvalidOperationException("Return-to-Home failed", ex);
        }
    }

    public async Task GoToAsync(double lat, double lon, double altMeters = 0.0, CancellationToken cancel = default) {
        var token = cancel == default ? _cts.Token : cancel;

        var ctl = _control ?? throw new InvalidOperationException("Control client required");

        try {
            var mi = ctl.GetType().GetMethod("GoTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (mi != null) {
                var parms = mi.GetParameters();
                if (parms.Length >= 1) {
                    var pt = parms[0].ParameterType;

                    object? pointInstance;

                    var ctor = pt.GetConstructor([typeof(double), typeof(double), typeof(double)]);
                    if (ctor != null) {
                        pointInstance = ctor.Invoke([lat, lon, altMeters]);
                    } else {
                        pointInstance = Activator.CreateInstance(pt);
                        if (pointInstance != null) {
                            var latProp = pt.GetProperty("Lat") ?? pt.GetProperty("Latitude") ?? pt.GetProperty("lat");
                            var lonProp = pt.GetProperty("Lon") ?? pt.GetProperty("Longitude") ?? pt.GetProperty("lon");
                            var altProp = pt.GetProperty("Alt") ?? pt.GetProperty("Altitude") ?? pt.GetProperty("alt") ?? pt.GetProperty("Height");

                            if (latProp != null && latProp.CanWrite) latProp.SetValue(pointInstance, lat);
                            if (lonProp != null && lonProp.CanWrite) lonProp.SetValue(pointInstance, lon);
                            if (altProp != null && altProp.CanWrite) altProp.SetValue(pointInstance, altMeters);
                        }
                    }

                    object?[] args;
                    if (parms.Length == 1) args = [pointInstance];
                    else if (parms.Length >= 2 && parms[1].ParameterType == typeof(CancellationToken)) args = [pointInstance, token];
                    else args = [pointInstance];

                    var res = mi.Invoke(ctl, args);
                    if (res is Task t) {
                        await t.ConfigureAwait(false);
                    } else if (res != null && res.GetType().Name.StartsWith("ValueTask")) {
                        await (dynamic)res;
                    }

                    return;
                }
            }
        } catch (System.Reflection.TargetInvocationException tie) {
            throw new InvalidOperationException("GoTo invocation failed", tie.InnerException ?? tie);
        } catch (Exception ex) {
            throw new InvalidOperationException("GoTo failed", ex);
        }

        throw new InvalidOperationException("GoTo is not available on the control client");
    }

    public void Dispose() {
        _cts.Cancel();
        _cts.Dispose();
    }

}