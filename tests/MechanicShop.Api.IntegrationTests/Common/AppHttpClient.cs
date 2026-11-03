using System.Net.Http.Headers;
using System.Net.Http.Json;

using MechanicShop.Application.Features.Identity;
using MechanicShop.Application.Features.Identity.Queries.GenerateTokens;
using MechanicShop.Infrastructure.identity;

namespace MechanicShop.Api.IntegrationTests.Common;

public class AppHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Func<AppUser, CancellationToken, Task<string>>? _tokenProvider;

    public AppHttpClient(
        HttpClient httpClient,
        Func<AppUser, CancellationToken, Task<string>>? tokenProvider = null)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    public Task<string> GenerateTokenAsync(
        AppUser user,
        CancellationToken cancellationToken = default)
    {
        if (_tokenProvider is not null)
        {
            return _tokenProvider(user, cancellationToken);
        }

        return GenerateTokenFromEndpointAsync(user, cancellationToken);
    }

    internal async Task<string> GenerateTokenFromEndpointAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var generateTokenQuery = new GenerateTokenQuery(user.Email!, user.Email!);

        var response = await _httpClient.PostAsJsonAsync(
            "identity/token/generate",
            generateTokenQuery,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Token generation failed with status code {response.StatusCode}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        if (tokenResponse is null)
        {
            throw new InvalidOperationException("Token response is null.");
        }

        return tokenResponse.AccessToken!;
    }

    public void SetAuthorizationHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAuthorizationHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetAsync(requestUri, cancellationToken);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default)
    {
        return await _httpClient.PostAsJsonAsync(requestUri, value, cancellationToken);
    }

    public async Task<HttpResponseMessage> PutAsJsonAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default)
    {
        return await _httpClient.PutAsJsonAsync(requestUri, value, cancellationToken);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        return await _httpClient.DeleteAsync(requestUri, cancellationToken);
    }

    public async Task<HttpResponseMessage> PatchAsJsonAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default)
    {
        return await _httpClient.PatchAsJsonAsync(requestUri, value, cancellationToken);
    }

    public async Task<T?> GetFromJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    public async Task<T?> PostAndGetFromJsonAsync<TRequest, T>(string requestUri, TRequest value, CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(requestUri, value, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
