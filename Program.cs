using flaas;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Emit;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

var sensor = FanLightController.CreateFanLightController(app.Configuration["SensorName"]);

app.MapGet("/", () => Results.Ok(sensor.Get()));

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

app.MapPost("/brightness", ([FromBody] Level level) =>
{
    sensor.Set(level.Value);
    return Results.Accepted();
});

app.Run("http://*:5112");

public record Level(int Value);

// Our custom JSON serializer context that generates code to serialize
// arrays of planets so that we can use it with our HTTP endpoint.
[JsonSerializable(typeof(Level[]))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}