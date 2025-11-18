using Asv.Cfg;
using Asv.IO;
using Asv.Mavlink;
using ObservableCollections;
using R3;

namespace AsvTest.Core;

public sealed class DroneConnection(string host = "127.0.0.1", int port = 5760) : IAsyncDisposable
{
    private IProtocolRouter? _router;
    private IDeviceExplorer? _deviceExplorer;
    private IClientDevice? _device;

    public IPositionClient? PositionClient { get; private set; }

    public IControlClient? ControlClient { get; private set; }

    public ICommandClient? CommandClient { get; private set; }

    public DroneTelemetry? Telemetry { get; private set; }

    public DroneController? Controller { get; private set; }

    public async Task StartAsync(CancellationToken cancel = default)
    {
        var protocol = Protocol.Create(builder =>
        {
            builder.RegisterMavlinkV2Protocol();
            builder.Features.RegisterBroadcastFeature<MavlinkMessage>();
            builder.Formatters.RegisterSimpleFormatter();
        });

        _router = protocol.CreateRouter("ROUTER");
        _router.AddTcpClientPort(p =>
        {
            p.Host = host;
            p.Port = port;
        });

        var seq = new PacketSequenceCalculator();
        var identity = new MavlinkIdentity(255, 255);

        _deviceExplorer = DeviceExplorer.Create(_router, builder =>
        {
            builder.SetConfig(new ClientDeviceBrowserConfig {
                DeviceTimeoutMs = 1000,
                DeviceCheckIntervalMs = 30_000
            });

            builder.Factories.RegisterDefaultDevices(
                new MavlinkIdentity(identity.SystemId, identity.ComponentId),
                seq,
                new InMemoryConfiguration());
        });

        _device = await _deviceExplorer
            .InitializedDevices
            .ObserveAdd()
            .Select(x => x.Value)
            .FirstAsync(cancel);

        PositionClient = _device.GetMicroservice<IPositionClient>() ?? throw new InvalidOperationException("Position client not available on device");
        ControlClient = _device.GetMicroservice<IControlClient>() ?? throw new InvalidOperationException("Control client not available on device");
        CommandClient = _device.GetMicroservice<ICommandClient>() ?? throw new InvalidOperationException("Command client not available on device");

        Telemetry = new DroneTelemetry(PositionClient);
        Controller = new DroneController(ControlClient);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();

        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsyncCore()
    {
        Controller?.Dispose();

        if (_device is not null)
            await _device.DisposeAsync();
        if (_deviceExplorer is not null)
            await _deviceExplorer.DisposeAsync();
        if (_router is not null)
            await _router.DisposeAsync();
    }
}