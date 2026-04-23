using System.Text.Json;
using CsLiveMute.Core.Models;

namespace CsLiveMute.Core.Gsi;

public static class GsiPayloadParser
{
    public static bool TryParse(string payload, string expectedToken, out GsiPayloadState state, out string? error)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var token = GetNestedString(root, "auth", "token") ?? GetString(root, "token");
            var isAuthenticated = string.Equals(token, expectedToken, StringComparison.Ordinal);

            state = new GsiPayloadState(
                isAuthenticated,
                token,
                GetNestedString(root, "provider", "name"),
                GetNestedString(root, "map", "phase"),
                GetNestedString(root, "round", "phase"),
                payload);

            error = isAuthenticated ? null : "The incoming GSI auth token did not match the configured token.";
            return true;
        }
        catch (JsonException exception)
        {
            state = new GsiPayloadState(false, null, null, null, null, payload);
            error = $"The GSI payload could not be parsed: {exception.Message}";
            return false;
        }
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!TryGetPropertyIgnoreCase(current, segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return TryGetPropertyIgnoreCase(element, propertyName, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
