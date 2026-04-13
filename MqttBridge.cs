using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;

namespace flaas;

public class MqttBridge : BackgroundService
{
    private readonly FanLightController _controller;
    private readonly IConfiguration _config;
    private readonly ILogger<MqttBridge> _logger;
    private IMqttClient? _client;

    private string TopicPrefix => _config["Mqtt:TopicPrefix"] ?? "flaas";
    private string StateTopic => $"{TopicPrefix}/light/state";
    private string BrightnessStateTopic => $"{TopicPrefix}/light/brightness/state";
    private string CommandTopic => $"{TopicPrefix}/light/set";
    private string BrightnessCommandTopic => $"{TopicPrefix}/light/brightness/set";
    private string AvailabilityTopic => $"{TopicPrefix}/availability";
    private string DiscoveryTopic => $"homeassistant/light/{TopicPrefix}/config";
    private string HAStatusTopic => "homeassistant/status";

    public MqttBridge(FanLightController controller, IConfiguration config, ILogger<MqttBridge> logger)
    {
        _controller = controller;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = _config["Mqtt:Host"];
        if (string.IsNullOrEmpty(host))
        {
            _logger.LogInformation("MQTT not configured (Mqtt:Host is empty), skipping MQTT bridge");
            return;
        }

        var port = int.TryParse(_config["Mqtt:Port"], out var p) ? p : 1883;
        var user = _config["Mqtt:Username"] ?? "";
        var pass = _config["Mqtt:Password"] ?? "";

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId($"flaas-{Environment.MachineName}")
            .WithCleanSession(false)
            .WithWillTopic(AvailabilityTopic)
            .WithWillPayload("offline")
            .WithWillRetain(true);

        if (!string.IsNullOrEmpty(user))
            optionsBuilder.WithCredentials(user, pass);

        var options = optionsBuilder.Build();

        _client.ApplicationMessageReceivedAsync += OnMessageReceived;

        _controller.StateChanged += state =>
        {
            if (_client?.IsConnected == true)
                _ = PublishStateAsync(state).ContinueWith(
                    t => _logger.LogWarning(t.Exception, "Failed to publish state to MQTT"),
                    TaskContinuationOptions.OnlyOnFaulted);
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port}", host, port);
                    await _client.ConnectAsync(options, stoppingToken);
                    _logger.LogInformation("Connected to MQTT broker");

                    await SubscribeAndAnnounceAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT connection failed, retrying in 10s");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        if (_client?.IsConnected == true)
        {
            // Mark as offline on clean shutdown
            await _client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(AvailabilityTopic)
                .WithPayload("offline")
                .WithRetainFlag(true)
                .Build(), CancellationToken.None);

            await _client.DisconnectAsync();
        }
    }

    private async Task SubscribeAndAnnounceAsync()
    {
        // Subscribe to command topics
        await _client!.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic(CommandTopic)
            .Build());
        await _client!.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic(BrightnessCommandTopic)
            .Build());

        // Subscribe to HA birth message so we re-announce on HA restart
        await _client!.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic(HAStatusTopic)
            .Build());

        await PublishDiscoveryAsync();

        // Publish availability
        await _client!.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(AvailabilityTopic)
            .WithPayload("online")
            .WithRetainFlag(true)
            .Build());

        // Publish current state
        await PublishStateAsync(_controller.Get());
    }

    private async Task PublishDiscoveryAsync()
    {
        var discoveryPayload = new Dictionary<string, object>
        {
            ["name"] = _config["Mqtt:DeviceName"] ?? "Fan Light",
            ["object_id"] = $"{TopicPrefix}_light",
            ["unique_id"] = $"{TopicPrefix}_light",
            ["command_topic"] = CommandTopic,
            ["state_topic"] = StateTopic,
            ["brightness_command_topic"] = BrightnessCommandTopic,
            ["brightness_state_topic"] = BrightnessStateTopic,
            ["brightness_scale"] = 100,
            ["availability_topic"] = AvailabilityTopic,
            ["device"] = new Dictionary<string, object>
            {
                ["identifiers"] = $"{TopicPrefix}_light",
                ["name"] = _config["Mqtt:DeviceName"] ?? "Fan Light",
                ["manufacturer"] = "flaas",
                ["model"] = "Fan Light Controller",
                ["sw_version"] = "1.0"
            }
        };

        await _client!.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(DiscoveryTopic)
            .WithPayload(JsonSerializer.Serialize(discoveryPayload))
            .WithRetainFlag(true)
            .Build());

        _logger.LogInformation("Published HA discovery config to {Topic}", DiscoveryTopic);
    }

    private async Task PublishStateAsync(State state)
    {
        await _client!.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(StateTopic)
            .WithPayload(state.IsOn ? "ON" : "OFF")
            .WithRetainFlag(true)
            .Build());

        await _client!.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(BrightnessStateTopic)
            .WithPayload(((int)state.Brightness).ToString())
            .WithRetainFlag(true)
            .Build());
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            if (topic == HAStatusTopic && payload == "online")
            {
                _logger.LogInformation("Home Assistant came online, re-publishing discovery");
                await PublishDiscoveryAsync();
                await _client!.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(AvailabilityTopic)
                    .WithPayload("online")
                    .WithRetainFlag(true)
                    .Build());
                await PublishStateAsync(_controller.Get());
            }
            else if (topic == CommandTopic)
            {
                if (payload == "ON")
                    _controller.On();
                else if (payload == "OFF")
                    _controller.Off();
            }
            else if (topic == BrightnessCommandTopic)
            {
                if (float.TryParse(payload, out var brightness))
                    _controller.SetBrightness(brightness);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process MQTT command");
        }
    }
}
