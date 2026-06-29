using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using GitHubWidgetBot.DTOs;
using GitHubWidgetBot.DTOs.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitHubWidgetBot.Services;

internal sealed class GitHubService(ILogger<GitHubService> logger, HttpClient httpClient, IOptions<GitHubOptions> options)
{
    private const string UnknownLanguage = "Unknown";

    private readonly string _token = options.Value.Token;
    private readonly string _oauthClientId = options.Value.OAuthClientId;

    public bool IsOAuthDeviceFlowConfigured => !string.IsNullOrWhiteSpace(_oauthClientId);

    /// <param name="username">Sanitized, GitHub-friendly username</param>
    /// <param name="excludeUnknown">Whether exclude repositories with <code>"TopLanguage": null</code> from calculation</param>
    /// <param name="token">GitHub OAuth token to use instead of the configured bot token</param>
    /// <returns>
    /// Widget with full or partial data, depending on what was fetched successfully or null
    /// when we failed to fetch anything
    /// </returns>
    public async Task<Widget?> FetchUserDataAsync(string username, bool excludeUnknown, string? token = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var contributionsTask = FetchContributionsAsync(username, token);
        var profileTask = FetchProfileDataAsync(username, token);
        var reposTask = FetchReposDataAsync(username, excludeUnknown, token);

        await Task.WhenAll(profileTask, reposTask, contributionsTask).ConfigureAwait(false);

        var profile = await profileTask.ConfigureAwait(false);
        if (profile is null) return null;

        var contributions = await contributionsTask.ConfigureAwait(false);
        var repos = await reposTask.ConfigureAwait(false);

        var widget = Widget.Create(
            username: profile.Login,
            profileHandle: profile.Login,
            avatarImage: profile.AvatarUrl,
            profileName: profile.Name ?? string.Empty,
            profileBio: profile.Bio ?? string.Empty,
            contributions: contributions,
            followers: profile.Followers,
            following: profile.Following,
            starsTotal: repos.StarsTotal,
            publicRepos: profile.PublicRepos,
            topLanguage: repos.TopLanguage
        );

        return widget;
    }

    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded")]
    public async Task<GitHubDeviceAuthorization?> StartDeviceAuthorizationAsync()
    {
        if (!IsOAuthDeviceFlowConfigured)
        {
            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Cannot start GitHub device authorization because GitHub:OAuthClientId is not configured");
            return null;
        }

        try
        {
            using var request = CreateDeviceFlowRequest(
                requestUri: "https://github.com/login/device/code",
                values:
                [
                    new KeyValuePair<string, string>("client_id", _oauthClientId),
                    new KeyValuePair<string, string>("scope", string.Empty)
                ]
            );

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to start GitHub device authorization. Status code: {Status}", response.StatusCode);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var reader = new Utf8JsonReader(bytes);

            string? deviceCode = null;
            string? userCode = null;
            string? verificationUri = null;
            var expiresIn = 0;
            var interval = 0u;

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                if (reader.ValueTextEquals("device_code"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String) deviceCode = reader.GetString();
                    continue;
                }

                if (reader.ValueTextEquals("user_code"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String) userCode = reader.GetString();
                    continue;
                }

                if (reader.ValueTextEquals("verification_uri"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String) verificationUri = reader.GetString();
                    continue;
                }

                if (reader.ValueTextEquals("expires_in"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.Number) reader.TryGetInt32(out expiresIn);
                    continue;
                }

                if (reader.ValueTextEquals("interval"u8) && reader.Read() && reader.TokenType == JsonTokenType.Number)
                {
                    reader.TryGetUInt32(out interval);
                }
            }

            if (string.IsNullOrWhiteSpace(deviceCode) || string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(verificationUri) || expiresIn <= 0)
            {
                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to start GitHub device authorization. Response was missing required fields");
                return null;
            }

            if (interval < 5) interval = 5;

            return new GitHubDeviceAuthorization(
                DeviceCode: deviceCode,
                UserCode: userCode,
                VerificationUrl: verificationUri,
                ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(expiresIn),
                PollIntervalSeconds: interval
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to start GitHub device authorization");
            return null;
        }
    }

    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded")]
    public async Task<(string Login, string AccessToken)?> CheckDeviceAuthorizationAsync(string deviceCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceCode);

        try
        {
            using var request = CreateDeviceFlowRequest(
                requestUri: "https://github.com/login/oauth/access_token",
                values:
                [
                    new KeyValuePair<string, string>("client_id", _oauthClientId),
                    new KeyValuePair<string, string>("device_code", deviceCode),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                ]
            );

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to check GitHub device authorization. Status code: {Status}", response.StatusCode);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var reader = new Utf8JsonReader(bytes);

            string? accessToken = null;
            string? error = null;

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                if (reader.ValueTextEquals("access_token"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String) accessToken = reader.GetString();
                    continue;
                }

                if (reader.ValueTextEquals("error"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String) error = reader.GetString();
                    continue;
                }

                if (reader.ValueTextEquals("interval"u8)) reader.Skip();
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                using var userRequest = CreateRequest(HttpMethod.Get, "user", accessToken);
                using var userResponse = await httpClient.SendAsync(userRequest, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (userResponse.StatusCode != HttpStatusCode.OK)
                {
                    if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch authenticated GitHub user. Status code: {Status}", userResponse.StatusCode);
                    return null;
                }

                var userBytes = await userResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var userReader = new Utf8JsonReader(userBytes);

                while (userReader.Read())
                {
                    if (userReader.TokenType != JsonTokenType.PropertyName) continue;
                    if (!userReader.ValueTextEquals("login"u8)) continue;

                    if (userReader.Read() && userReader.TokenType == JsonTokenType.String)
                    {
                        var login = userReader.GetString();
                        return string.IsNullOrWhiteSpace(login) ? null : (login, accessToken);
                    }

                    break;
                }

                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch authenticated GitHub user. Response was missing required fields");
                return null;
            }

            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("GitHub device authorization did not return an access token. Error: {Error}", error);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to check GitHub device authorization");
            return null;
        }
    }

    private async Task<uint> FetchContributionsAsync(string username, string? token)
    {
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Starting to fetch contributions for @{Username}", username);
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "graphql", token);
            var buffer = new ArrayBufferWriter<byte>(256 + username.Length);

            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("query"u8, "query($login: String!) { user(login: $login) { contributionsCollection { contributionCalendar { totalContributions } } } }"u8);
                writer.WritePropertyName("variables"u8);
                writer.WriteStartObject();
                writer.WriteString("login"u8, username);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            request.Content = new ReadOnlyMemoryContent(buffer.WrittenMemory)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            };

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch contributions for @{Username}. Status code: {Status}", username, response.StatusCode);
                return 0;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var reader = new Utf8JsonReader(bytes);

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                if (!reader.ValueTextEquals("totalContributions"u8)) continue;
                if (reader.Read() && reader.TokenType == JsonTokenType.Number && reader.TryGetUInt32(out var totalContributions))
                {
                    if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Successfully fetched for @{Username}", username);
                    return totalContributions;
                }

                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch contributions for @{Username}. Response did not contain correct JsonToken", username);
                break;
            }

            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to fetch contributions for @{Username}", username);
            return 0;
        }
    }

    /// <returns>
    /// <list type="bullet">
    /// <item><see cref="GitHubProfileData"/> with full data if fetch succeeded</item>
    /// <item>
    ///     <see cref="GitHubProfileData"/> with partial data (Only <see cref="GitHubProfileData.Name"/> is filled.
    ///     Other values default to <see langword="null"/> or their default values) if fetch succeeded but parsing failed
    /// </item>
    /// <item><see langword="null"/> if fetch failed or unknown exception occurred</item>
    /// </list>
    /// </returns>
    private async Task<GitHubProfileData?> FetchProfileDataAsync(string username, string? token)
    {
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Starting to fetch profile data for @{Username}", username);
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"users/{Uri.EscapeDataString(username)}", token);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch profile data for @{Username}. Status code: {Status}", username, response.StatusCode);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var reader = new Utf8JsonReader(bytes);

            string? login = null;
            string? avatarUrl = null;
            string? name = null;
            string? bio = null;
            uint followers = 0;
            uint following = 0;
            uint publicRepos = 0;

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                if (reader.ValueTextEquals("login"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String) login = reader.GetString();
                    continue;
                }

                if (reader.ValueTextEquals("avatar_url"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String) avatarUrl = reader.GetString();
                    continue;
                }

                if (reader.ValueTextEquals("name"u8))
                {
                    if (reader.Read()) name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    continue;
                }

                if (reader.ValueTextEquals("bio"u8))
                {
                    if (reader.Read()) bio = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    continue;
                }

                if (reader.ValueTextEquals("followers"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.Number) reader.TryGetUInt32(out followers);
                    continue;
                }

                if (reader.ValueTextEquals("following"u8))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.Number) reader.TryGetUInt32(out following);
                    continue;
                }

                if (reader.ValueTextEquals("public_repos"u8) && reader.Read() && reader.TokenType == JsonTokenType.Number)
                {
                    reader.TryGetUInt32(out publicRepos);
                }
            }

            if (!string.IsNullOrWhiteSpace(login) && !string.IsNullOrWhiteSpace(avatarUrl))
            {
                if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Successfully fetched profile data for @{Username}", username);
                return new GitHubProfileData(login, avatarUrl, name, bio, followers, following, publicRepos);
            }

            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch profile data for @{Username}. Response was missing required fields", username);
            return new GitHubProfileData(Login: username, AvatarUrl: null, Name: null, Bio: null, Followers: 0, Following: 0, PublicRepos: 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to fetch profile data for @{Username}", username);
            return null;
        }
    }

    private async Task<GitHubReposData> FetchReposDataAsync(string username, bool excludeUnknown, string? token)
    {
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Starting to fetch repos data for @{Username}", username);
        try
        {
            var requestUri = $"users/{Uri.EscapeDataString(username)}/repos?per_page=100&type=owner";
            uint starsTotal = 0;
            var languageCounts = new Dictionary<string, uint>(capacity: 16, StringComparer.Ordinal);

            while (requestUri is not null)
            {
                using var request = CreateRequest(HttpMethod.Get, requestUri, token);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch repos data for @{Username}. Status code: {Status}", username, response.StatusCode);
                    return new GitHubReposData(0, UnknownLanguage);
                }

                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                starsTotal += await ReadReposPageAsync(stream, languageCounts).ConfigureAwait(false);

                if (!response.Headers.TryGetValues("Link", out var headerValues)) break;

                requestUri = null;
                foreach (var value in headerValues)
                {
                    var links = value.AsSpan();
                    while (!links.IsEmpty)
                    {
                        var separatorIndex = links.IndexOf(',');
                        var link = separatorIndex >= 0 ? links[..separatorIndex].Trim() : links.Trim();

                        if (link.Contains("rel=\"next\"".AsSpan(), StringComparison.Ordinal))
                        {
                            var startIndex = link.IndexOf('<');
                            var endIndex = link.IndexOf('>');
                            if (startIndex >= 0 && endIndex > startIndex)
                            {
                                requestUri = link[(startIndex + 1)..endIndex].ToString();
                                break;
                            }
                        }

                        if (separatorIndex < 0) break;
                        links = links[(separatorIndex + 1)..];
                    }

                    if (requestUri is not null) break;
                }
            }

            var topLanguage = UnknownLanguage;
            uint topLanguageCount = 0;

            foreach (var (language, count) in languageCounts)
            {
                if (excludeUnknown && string.Equals(language, UnknownLanguage, StringComparison.Ordinal))
                    continue;

                if (count > topLanguageCount || (count == topLanguageCount && string.CompareOrdinal(language, topLanguage) < 0))
                {
                    topLanguage = language;
                    topLanguageCount = count;
                }
            }

            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Successfully fetched repos data for @{Username}", username);
            return new GitHubReposData(starsTotal, topLanguage);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError(ex, "Failed to fetch repos data for @{Username}", username);
            return new GitHubReposData(0, UnknownLanguage);
        }
    }

    private static async Task<uint> ReadReposPageAsync(Stream stream, Dictionary<string, uint> languageCounts)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: 32 * 1024);
        var state = new JsonReaderState();
        var pendingProperty = 0; // 0 none, 1 stars, 2 language
        var bytesInBuffer = 0;
        var isFinalBlock = false;
        uint starsForPage = 0;

        try
        {
            while (!isFinalBlock)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(start: bytesInBuffer, length: buffer.Length - bytesInBuffer)).ConfigureAwait(false);
                isFinalBlock = bytesRead == 0;

                var bytesToParse = bytesInBuffer + bytesRead;
                var reader = new Utf8JsonReader(buffer.AsSpan(start: 0, length: bytesToParse), isFinalBlock, state);
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals("stargazers_count"u8))
                        {
                            pendingProperty = 1;
                            continue;
                        }

                        if (reader.ValueTextEquals("language"u8))
                        {
                            pendingProperty = 2;
                            continue;
                        }
                    }

                    if (pendingProperty == 1)
                    {
                        if (reader.TokenType == JsonTokenType.Number && reader.TryGetUInt32(out var stars))
                            starsForPage += stars;

                        pendingProperty = 0;
                        continue;
                    }

                    if (pendingProperty == 2)
                    {
                        var language = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        var key = string.IsNullOrWhiteSpace(language) ? UnknownLanguage : language;
                        ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(languageCounts, key, out _);
                        count++;
                        pendingProperty = 0;
                    }
                }

                state = reader.CurrentState;
                var bytesConsumed = (int)reader.BytesConsumed;
                var bytesRemaining = bytesToParse - bytesConsumed;

                if (bytesRemaining == buffer.Length)
                    throw new JsonException("A single GitHub repositories JSON token exceeded the parser buffer size.");

                if (bytesRemaining > 0)
                    buffer.AsSpan(bytesConsumed, bytesRemaining).CopyTo(buffer);

                bytesInBuffer = bytesRemaining;
            }

            return starsForPage;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static HttpRequestMessage CreateDeviceFlowRequest(string requestUri, IEnumerable<KeyValuePair<string, string>> values)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = new FormUrlEncodedContent(values) };

        request.Headers.UserAgent.ParseAdd(ApplicationConfiguration.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri, string? token = null)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _token);
        request.Headers.UserAgent.ParseAdd(ApplicationConfiguration.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2026-03-10");
        return request;
    }
}