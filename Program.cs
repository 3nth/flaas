using flaas;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

if (args.Contains("--list-sensors"))
{
    var sensors = FanLightController.ListControlSensors();
    if (sensors.Count == 0)
    {
        Console.WriteLine("No control sensors found.");
        return;
    }
    Console.WriteLine("Available control sensors:");
    foreach (var name in sensors)
        Console.WriteLine($"  {name}");
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddWindowsService();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var config = builder.Configuration;
var port = config["Port"] ?? "5112";
builder.WebHost.UseUrls($"http://*:{port}");
var hardwareMin = float.TryParse(config["HardwareMin"], out var min) ? min : 1;
var hardwareMax = float.TryParse(config["HardwareMax"], out var max) ? max : 100;
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var sensor = FanLightController.CreateFanLightController(config["SensorName"], loggerFactory.CreateLogger<FanLightController>(), hardwareMin, hardwareMax);
builder.Services.AddSingleton(sensor);
builder.Services.AddSingleton<MqttBridge>();
builder.Services.AddHostedService<MqttBridge>(sp => sp.GetRequiredService<MqttBridge>());

var app = builder.Build();

app.MapGet("/ui", () =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "ui.html");
    return Results.File(path, "text/html");
});

app.MapGet("/health", (MqttBridge mqtt) => Results.Ok(new Health(mqtt.MqttEnabled, mqtt.MqttConnected)));

app.MapGet("/", () => Results.Ok(sensor.Get()));

app.MapPost("/", ([FromBody] State state) =>
{
    sensor.Set(state);
    return Results.Accepted();
});

app.MapPost("/on", () =>
{
    sensor.On();
    return Results.Accepted();
});

app.MapPost("/off", () =>
{
    sensor.Off();
    return Results.Accepted();
});

app.MapPost("/brightness", ([FromBody] State level) =>
{
    sensor.SetBrightness(level.Brightness);
    return Results.Accepted();
});


await app.RunAsync();



public record Health(bool MqttEnabled, bool MqttConnected);

[JsonSerializable(typeof(State))]
[JsonSerializable(typeof(Health))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}