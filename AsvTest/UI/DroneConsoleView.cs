using Spectre.Console;

namespace AsvTest.UI;

public class DroneConsoleView {

    private readonly Table _table = new Table()
        .AddColumn("Latitude")
        .AddColumn("Longitude")
        .AddColumn("Abs Alt (m)")
        .AddColumn("Rel Alt (m)")
        .AddColumn("Vz (m/s)")
        .Border(TableBorder.Rounded);
    private bool _suppressRender;
    private string _status = string.Empty;

    public void SuppressRender(bool suppress) => _suppressRender = suppress;

    public void SetStatus(string? status) {
        _status = status ?? string.Empty;
    }

    public void UpdatePosition(Core.DroneTelemetry.TelemetrySample coord) {
        if (_suppressRender) return;

        Console.Clear();
        Console.WriteLine("Starting interactive console. Keys: t=takeoff, l=land, g=goto, q=quit");
        if (!string.IsNullOrEmpty(_status)) {
            var s = _status.Length > 120 ? _status[..120] + "..." : _status;
            Console.WriteLine($"Status: {s}");
        }

        _table.Rows.Clear();
        _table.AddRow(
            coord.Lat.ToString("F6"),
            coord.Lon.ToString("F6"),
            coord.AbsAlt.ToString("F1"),
            coord.RelAlt.ToString("F1"),
            coord.Vz.ToString("F2")
        );
        AnsiConsole.Write(_table);
    }

}