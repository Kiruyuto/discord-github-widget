using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GitHubWidgetBot.DTOs;
using GitHubWidgetBot.DTOs.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitHubWidgetBot.Services;

internal sealed class GitHubService(ILogger<GitHubService> logger, HttpClient httpClient, IOptions<GitHubOptions> options)
{
    private readonly string _token = options.Value.Token;

    /// <param name="username">Sanitized, GitHub-friendly username</param>
    /// <param name="excludeUnknown">Whether exclude repositories with <code>"TopLanguage": null</code> from calculation</param>
    /// <returns>
    /// Widget with full or partial data, depending on what was fetched successfully or null
    /// when we failed to fetch anything
    /// </returns>
    public async Task<Widget?> FetchUserDataAsync(string username, bool excludeUnknown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var contributionsTask = FetchContributionsAsync(username);
        var profileTask = FetchProfileDataAsync(username);
        var reposTask = FetchReposDataAsync(username, excludeUnknown);

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

    private async Task<uint> FetchContributionsAsync(string username)
    {
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Starting to fetch contributions for @{Username}", username);
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "graphql");
            var buffer = new ArrayBufferWriter<byte>(256 + username.Length);

            await using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("query"u8, "query($login: String!) { user(login: $login) { contributionsCollection { contributionCalendar { totalContributions } } } }"u8);
                writer.WritePropertyName("variables"u8);
                writer.WriteStartObject();
                writer.WriteString("login"u8, username);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            request.Content = new ByteArrayContent(buffer.WrittenSpan.ToArray())
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
    private async Task<GitHubProfileData?> FetchProfileDataAsync(string username)
    {
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Starting to fetch profile data for @{Username}", username);
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"users/{Uri.EscapeDataString(username)}");
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

    private async Task<GitHubReposData> FetchReposDataAsync(string username, bool excludeUnknown)
    {
        const string UnknownLanguage = "Unknown";

        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Starting to fetch repos data for @{Username}", username);
        try
        {
            var requestUri = $"users/{Uri.EscapeDataString(username)}/repos?per_page=100&type=owner";
            uint starsTotal = 0;
            var languageCounts = new Dictionary<string, uint>(capacity: 16, StringComparer.Ordinal);

            while (requestUri is not null)
            {
                using var request = CreateRequest(HttpMethod.Get, requestUri);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Failed to fetch repos data for @{Username}. Status code: {Status}", username, response.StatusCode);
                    return new GitHubReposData(0, UnknownLanguage);
                }

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var reader = new Utf8JsonReader(bytes);

                while (reader.Read())
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("stargazers_count"u8))
                    {
                        if (reader.Read() && reader.TokenType == JsonTokenType.Number && reader.TryGetUInt32(out var stars))
                        {
                            starsTotal += stars;
                        }

                        continue;
                    }

                    if (reader.ValueTextEquals("language"u8))
                    {
                        string? language = null;

                        if (reader.Read() && reader.TokenType == JsonTokenType.String)
                            language = reader.GetString();

                        var key = string.IsNullOrWhiteSpace(language) ? UnknownLanguage : language;
                        languageCounts.TryGetValue(key, out var count);
                        languageCounts[key] = count + 1;
                    }
                }

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

    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.UserAgent.ParseAdd($"discord-github-widget/{GlobalConstants.Version}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2026-03-10");
        return request;
    }
}