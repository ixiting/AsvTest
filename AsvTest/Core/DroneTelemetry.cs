using Asv.Mavlink;

using System.Reactive.Linq;

namespace AsvTest.Core;

public class DroneTelemetry {

    public record TelemetrySample(double Lat, double Lon, double AbsAlt, double RelAlt, double Vz);

    public IObservable<TelemetrySample> Coordinates { get; }

    public DroneTelemetry(IPositionClient positionClient, TimeSpan? pollInterval = null) {
        ArgumentNullException.ThrowIfNull(positionClient);

        var interval = pollInterval ?? TimeSpan.FromMilliseconds(200);

        Coordinates = Observable.Interval(interval)
            .Select(_ => positionClient.GlobalPosition.CurrentValue)
            .Where(p => p != null)
            .Select(p => {
                var lat = p!.Lat / 1e7;
                var lon = p.Lon / 1e7;
                var absAlt = p.Alt / 1000.0;
                var relAlt = p.RelativeAlt / 1000.0;
                var vz = p.Vz / 1000.0;
                return new TelemetrySample(Math.Round(lat, 7), Math.Round(lon, 7), Math.Round(absAlt, 2), Math.Round(relAlt, 2), Math.Round(vz, 2));
            })
            .DistinctUntilChanged()
            .Publish()
            .RefCount();
    }

}