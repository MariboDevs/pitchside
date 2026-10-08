using Pitchside.Api;
using Pitchside.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    foreach (var converter in Pitchside.Contracts.ContractJson.Options.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMatchEndpoints();
app.Run();
