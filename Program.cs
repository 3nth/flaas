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

builder.WebHost.UseUrls("http://*:5112");

var app = builder.Build();

var sensor = FanLightController.CreateFanLightController(app.Configuration["SensorName"]);

app.MapGet("/ui", () =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "ui.html");
    return Results.File(path, "text/html");
});

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



[JsonSerializable(typeof(State))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}