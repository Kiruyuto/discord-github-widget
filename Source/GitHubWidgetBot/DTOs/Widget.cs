using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitHubWidgetBot.DTOs;

[DebuggerDisplay("Username = {Data.Username}, DynamicCount = {Data.Dynamic.Length}")]
internal readonly record struct Widget(Widget.DataPayload Data)
{
    internal readonly record struct DataPayload(string Username, DynamicEntry[] Dynamic);
    internal readonly record struct DynamicEntry(uint Type, string Name, object? Value);

    private sealed record DynamicImage(string Url);

    public static Widget Create(
        string username,
        string profileHandle,
        string? avatarImage,
        string profileName,
        string profileBio,
        uint? contributions,
        uint followers,
        uint following,
        uint starsTotal,
        uint? publicRepos,
        string topLanguage
    )
    {
        var dynamic = new DynamicEntry[]
        {
            // Top widget
            new(Type: 1, Name: "user_profile_handle", Value: $"@{profileHandle}"),
            new(Type: 3, Name: "user_avatar_image", Value: avatarImage == null ? null : new DynamicImage(avatarImage)),
            new(Type: 1, Name: "user_profile_name", Value: profileName),
            new(Type: 1, Name: "user_profile_bio", Value: profileBio),

            // Bottom widget
            new(Type: 1, Name: "user_contributions", Value: FormatCount(contributions)),
            new(Type: 1, Name: "user_followers", Value: FormatCount(followers)),
            new(Type: 1, Name: "user_following", Value: FormatCount(following)),
            new(Type: 1, Name: "user_stars_total", Value: FormatCount(starsTotal)),
            new(Type: 1, Name: "user_public_repos", Value: FormatCount(publicRepos)),
            new(Type: 1, Name: "user_top_language", Value: topLanguage)
        };

        return new Widget(new DataPayload(username, dynamic));
    }

    public ReadOnlyMemoryContent ToJsonContent()
    {
        var buffer = new ArrayBufferWriter<byte>(512);

        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteJson(writer);
        }

        return new ReadOnlyMemoryContent(buffer.WrittenMemory)
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
        };
    }

    private void WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("username"u8, Data.Username);
        writer.WritePropertyName("data"u8);
        writer.WriteStartObject();
        writer.WritePropertyName("dynamic"u8);
        writer.WriteStartArray();

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < Data.Dynamic.Length; i++)
        {
            writer.WriteStartObject();
            writer.WriteNumber("type"u8, Data.Dynamic[i].Type);
            writer.WriteString("name"u8, Data.Dynamic[i].Name);
            writer.WritePropertyName("value"u8);

            switch (Data.Dynamic[i].Value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string text:
                    writer.WriteStringValue(text);
                    break;
                case DynamicImage image:
                    writer.WriteStartObject();
                    writer.WriteString("url"u8, image.Url);
                    writer.WriteEndObject();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported widget dynamic value type.");
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string? FormatCount(uint? value) => value.HasValue ? FormatCount(value.Value) : null;

    private static string FormatCount(uint value)
    {
        Span<char> buffer = stackalloc char[13];
        var index = buffer.Length;
        var groupDigits = 0;

        do
        {
            if (groupDigits == 3)
            {
                buffer[--index] = ' ';
                groupDigits = 0;
            }

            buffer[--index] = (char)('0' + (value % 10));
            value /= 10;
            groupDigits++;
        } while (value != 0);

        return new string(buffer[index..]);
    }
}