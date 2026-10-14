using System.Net.Http.Json;
using System.Text.Json;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

internal static class Json
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ContractJson.Options))!;

    public static async Task<JsonElement> ReadElementAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ContractJson.Options);
}
