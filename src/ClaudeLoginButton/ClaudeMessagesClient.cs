using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClaudeLoginButton;

public sealed record ClaudeMessage(string Role, string Content);

/// <summary>
/// Small, host-side client for the official Claude Messages API. Credentials
/// are supplied by the host and never stored by this control library.
/// </summary>
public sealed class ClaudeMessagesClient
{
    private readonly Func<CancellationToken, Task<ClaudeCredential>> _credentialProvider;
    private readonly HttpClient _httpClient;

    public ClaudeMessagesClient(
        Func<CancellationToken, Task<ClaudeCredential>> credentialProvider,
        HttpClient? httpClient = null)
    {
        _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        _httpClient = httpClient ?? new HttpClient();
    }

    public Uri Endpoint { get; init; } = new("https://api.anthropic.com/v1/messages");

    public string Model { get; init; } = "claude-sonnet-5";

    public int MaxTokens { get; init; } = 1024;

    public async Task<string> SendAsync(
        IReadOnlyList<ClaudeMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages is null || messages.Count == 0)
        {
            throw new ArgumentException("At least one message is required.", nameof(messages));
        }

        var credential = await _credentialProvider(cancellationToken).ConfigureAwait(false);
        if (credential is null || string.IsNullOrWhiteSpace(credential.Value))
        {
            throw new InvalidOperationException("The host did not provide a Claude credential.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("anthropic-version", "2023-06-01");
        if (credential.Kind == ClaudeCredentialKind.ApiKey)
        {
            request.Headers.TryAddWithoutValidation("x-api-key", credential.Value);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Value);
        }

        request.Content = JsonContent.Create(new
        {
            model = Model,
            max_tokens = MaxTokens,
            messages = messages.Select(message => new
            {
                role = message.Role,
                content = message.Content,
            }),
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ClaudeApiException(response.StatusCode, ExtractErrorMessage(responseText));
        }

        return ExtractText(responseText);
    }

    private static string ExtractText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        if (!document.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            throw new ClaudeApiException(HttpStatusCode.OK, "Claude returned no content blocks.");
        }

        var text = content.EnumerateArray()
            .Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(block => block.TryGetProperty("text", out var value) ? value.GetString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value));

        var joined = string.Join("\n", text);
        return string.IsNullOrWhiteSpace(joined)
            ? throw new ClaudeApiException(HttpStatusCode.OK, "Claude returned no text content.")
            : joined;
    }

    private static string ExtractErrorMessage(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "Claude returned an error.";
            }
        }
        catch (JsonException)
        {
            // Keep the raw body out of the UI when the provider returned HTML or plain text.
        }

        return "Claude returned an error. Check the host credential, model and API limits.";
    }
}

public sealed class ClaudeApiException : Exception
{
    public ClaudeApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
