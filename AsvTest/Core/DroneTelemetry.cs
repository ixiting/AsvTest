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
                .Select(p =>
                {
                    var pp = p!;

                    double lat = ToDegrees(pp.Lat);
                    double lon = ToDegrees(pp.Lon);
                    double relAlt = ToMeters(pp.RelativeAlt);

                    double absAlt = relAlt;
                    try
                    {
                        var pt = pp.GetType();
                        var cand = pt.GetProperty("Alt") ?? pt.GetProperty("AltMs") ?? pt.GetProperty("AbsoluteAlt") ?? pt.GetProperty("Altitude");
                        if (cand is not null)
                        {
                            var raw = cand.GetValue(pp);
                            absAlt = ToMeters(raw);
                        }
                    } catch {
                        // ignored
                    }

                    double speed = 0;
                    try
                    {
                        speed = ComputeSpeed(pp);
                    } catch {
                        // ignored
                    }

                    double heading = 0;
                    try
                    {
                        heading = ToHeading(pp);
                    } catch {
                        // ignored
                    }

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

    private static double ToDegrees(object? raw)
    {
        if (raw is null) return 0.0;
        return raw switch
        {
            int i => i / 1e7,
            long l => l / 1e7,
            double d => d,
            float f => f,
            decimal m => (double)m,
            _ => TryNumericConvertToDouble(raw, 1e7)
        };
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
        try
        {
            double vx = 0, vy = 0;
            try { vx = (double)Convert.ToDouble(p.Vx) / 100.0; } catch { }
            try { vy = (double)Convert.ToDouble(p.Vy) / 100.0; } catch { }
            return Math.Sqrt(vx * vx + vy * vy);
        }
        catch
        {
            return 0;
        }
    }

    private static double ToHeading(dynamic p)
    {
        try
        {
            return (double)Convert.ToDouble(p.Hdg) / 100.0;
        }
        catch
        {
            try { return (double)Convert.ToDouble(p.Hdg); } catch { return 0; }
        }
    }
}