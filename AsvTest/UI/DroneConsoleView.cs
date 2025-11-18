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

    public void UpdatePosition(Core.DroneTelemetry.TelemetrySample coord)
    {
        if (_suppressRender) return;
        var help = new Markup("[grey]Keys: t=takeoff | l=land | g=goto | r=rtl | q=quit[/]");
        var status = string.Empty;
        if (!string.IsNullOrEmpty(_status))
        {
            status = _status.Length > 120 ? _status[..120] + "..." : _status;
        }

        _table.Rows.Clear();
        _table.AddRow(
            coord.Lat.ToString("F6"),
            coord.Lon.ToString("F6"),
            coord.AbsAlt.ToString("F1"),
            coord.RelAlt.ToString("F1"),
            coord.Speed.ToString("F2")
        );

        AnsiConsole.Clear();
        AnsiConsole.Write(help);
        AnsiConsole.WriteLine();
        if (!string.IsNullOrEmpty(status))
        {
            AnsiConsole.Write(new Markup($"[yellow]Status:[/] {status}"));
            AnsiConsole.WriteLine();
        }
        AnsiConsole.Write(_table);
    }

    public DroneConsoleView()
    {
        _table.Rows.Clear();
        _table.AddRow("-", "-", "-", "-", "-");
        AnsiConsole.Clear();
        var h = new Markup("[grey]Keys: t=takeoff | l=land | g=goto | r=rtl | q=quit[/]");
        AnsiConsole.Write(h);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(_table);
    }

}