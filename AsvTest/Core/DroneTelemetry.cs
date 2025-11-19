using Asv.Mavlink;
using R3;

namespace AsvTest.Core;

public class DroneTelemetry
{
    public record TelemetrySample(double Lat, double Lon, double AbsAlt, double RelAlt, double Speed);

    public Observable<TelemetrySample> Coordinates { get; }

    public DroneTelemetry(IPositionClient positionClient, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(positionClient);

        var interval = pollInterval ?? TimeSpan.FromMilliseconds(200);

        Coordinates = Observable.Interval(interval)
            .Select(_ => positionClient.GlobalPosition.CurrentValue)
            .Where(p => p is not null)
            .Select(p => {
                double lat = MavlinkTypesHelper.LatLonFromInt32E7ToDegDouble(p.Lat);
                double lon = MavlinkTypesHelper.LatLonFromInt32E7ToDegDouble(p.Lon);
                double relAlt = ToMeters(p.RelativeAlt);
                double absAlt = ToMeters(p.Alt);

                double speed = ComputeSpeed(p);

                return new TelemetrySample(
                    Math.Round(lat, 7),
                    Math.Round(lon, 7),
                    Math.Round(absAlt, 2),
                    Math.Round(relAlt, 2),
                    Math.Round(speed, 2)
                );
            })
            .DistinctUntilChanged()
            .Publish()
            .RefCount();
    }

    private static double ToMeters(object? raw)
    {
        if (raw is null) return 0.0;
        return raw switch
        {
            int i => i / 1000.0,
            long l => l / 1000.0,
            double d => d,
            float f => f,
            decimal m => (double)m,
            _ => TryNumericConvertToDouble(raw, 1000.0)
        };
    }

    private static double TryNumericConvertToDouble(object raw, double scale)
    {
        try
        {
            var s = Convert.ToDouble(raw);
            if (Math.Abs(s) > 1000)
            {
                return s / scale;
            }
            return s;
        }
        catch
        {
            return 0.0;
        }
    }

    private static double ComputeSpeed(dynamic p)
    {
        double vx = (double)Convert.ToDouble(p.Vx) / 100.0;
        double vy = (double)Convert.ToDouble(p.Vy) / 100.0;
        double vz = (double)Convert.ToDouble(p.Vz) / 100.0;

        return Math.Sqrt(vx * vx + vy * vy + vz * vz);
    }

}