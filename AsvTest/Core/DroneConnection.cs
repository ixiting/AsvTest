using Asv.Cfg;
using Asv.IO;
using Asv.Mavlink;

using ObservableCollections;

using R3;

namespace AsvTest.Core;

public class DroneConnection(string host = "127.0.0.1", int port = 5760) : IAsyncDisposable, IDisposable {

    private IProtocolRouter? _router;
    private IDeviceExplorer? _deviceExplorer;
    private IClientDevice? _device;

    private IPositionClient? PositionClient { get; set; }

    private IControlClient? ControlClient { get; set; }

    private ICommandClient? CommandClient { get; set; }

    public DroneTelemetry? Telemetry { get; private set; }

    public DroneController? Controller { get; private set; }

    public async Task StartAsync(CancellationToken cancel = default) {
        var protocol = Protocol.Create(builder => {
            builder.RegisterMavlinkV2Protocol();
            builder.Features.RegisterBroadcastFeature<MavlinkMessage>();
            builder.Formatters.RegisterSimpleFormatter();
        });

        _router = protocol.CreateRouter("ROUTER");
        _router.AddTcpClientPort(p => {
            p.Host = host;
            p.Port = port;
        });

        var seq = new PacketSequenceCalculator();
        var identity = new MavlinkIdentity(255, 255);

        _deviceExplorer = DeviceExplorer.Create(_router, builder => {
            builder.SetConfig(new ClientDeviceBrowserConfig() {
                DeviceTimeoutMs = 1000,
                DeviceCheckIntervalMs = 30_000
            });

            builder.Factories.RegisterDefaultDevices(
                new MavlinkIdentity(identity.SystemId, identity.ComponentId),
                seq,
                new InMemoryConfiguration());
        });

        var tcs = new TaskCompletionSource();
        using var sub = _deviceExplorer.Devices.ObserveAdd().Take(1).Subscribe(kvp => {
            _device = kvp.Value.Value;
            tcs.TrySetResult();
        });

        using var csrc = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        csrc.CancelAfter(TimeSpan.FromSeconds(20));

        try {
            await tcs.Task.ConfigureAwait(false);
        } catch (TaskCanceledException) {
            throw new InvalidOperationException("Timed out waiting for device. Make sure SITL is running and reachable.");
        }

        if (_device is null) throw new InvalidOperationException("Device not found");

        var readyTcs = new TaskCompletionSource();
        using var readySub = _device.State.Subscribe(s => {
            if (s == ClientDeviceState.Complete) readyTcs.TrySetResult();
        });
        await readyTcs.Task.ConfigureAwait(false);

        PositionClient = _device.GetMicroservice<IPositionClient>();
        ControlClient = _device.GetMicroservice<IControlClient>();
        CommandClient = _device.GetMicroservice<ICommandClient>();
        _device.GetMicroservice<IHeartbeatClient>();

        if (PositionClient == null) throw new InvalidOperationException("Position client not available on device");

        Telemetry = new DroneTelemetry(PositionClient);
        Controller = new DroneController(ControlClient ?? throw new InvalidOperationException("Control client required"), CommandClient, Telemetry);
    }

    public async ValueTask DisposeAsync() {
        try {
            Controller?.Dispose();
        } catch {
            // ignored
        }

        try {
            if (CommandClient != null) await CommandClient.DisposeAsync();
        } catch {
            // ignored
        }

        try {
            if (ControlClient != null) await ControlClient.DisposeAsync();
        } catch {
            // ignored
        }

        try {
            if (PositionClient != null) await PositionClient.DisposeAsync();
        } catch {
            // ignored
        }

        try {
            if (_device != null) await _device.DisposeAsync();
        } catch {
            // ignored
        }

        try {
            if (_deviceExplorer != null) await _deviceExplorer.DisposeAsync();
        } catch {
            // ignored
        }

        try {
            if (_router != null) await _router.DisposeAsync();
        } catch {
            // ignored
        }
    }

    public void Dispose() {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

}