using Asv.Mavlink;
using Asv.Common;

namespace AsvTest.Core;

public class DroneController(IControlClient controlClient) : IDisposable {

    private readonly IControlClient _control = controlClient;
    private bool _disposed;

    public async Task TakeOffAsync(double altitudeMeters = 10.0)
    {
        await _control.SetGuidedMode().ConfigureAwait(false);
        await _control.TakeOff(altitudeMeters).ConfigureAwait(false);
    }

    public async Task LandAsync()
    {
        await _control.DoLand().ConfigureAwait(false);
    }

    public async Task DoRtlAsync()
    {
        await _control.DoRtl().ConfigureAwait(false);
    }

    public async Task GoToAsync(double lat, double lon, double altMeters = 0.0)
    {
        await _control.SetGuidedMode().ConfigureAwait(false);

        var point = new GeoPoint(lat, lon, altMeters);
        await _control.GoTo(point).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
