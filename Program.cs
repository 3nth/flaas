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

app.Run("http://*:5112");


// Our custom JSON serializer context that generates code to serialize
// arrays of planets so that we can use it with our HTTP endpoint.
[JsonSerializable(typeof(State[]))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}