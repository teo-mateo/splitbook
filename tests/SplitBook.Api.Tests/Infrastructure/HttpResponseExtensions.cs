using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;

namespace SplitBook.Api.Tests.Infrastructure;

public static class HttpResponseExtensions
{
    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(
            $"expected 2xx but got {(int)response.StatusCode} {response.StatusCode}. Body:\n{body}");
        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
