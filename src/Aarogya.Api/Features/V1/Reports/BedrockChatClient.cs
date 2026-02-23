using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.AI;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class BedrockChatClient(
  IAmazonBedrockRuntime bedrockRuntime,
  string modelId) : IChatClient
{
  [SuppressMessage("Design", "S1075:URIs should not be hardcoded", Justification = "Static metadata endpoint for AWS Bedrock.")]
  public ChatClientMetadata Metadata { get; } = new("bedrock", new Uri("https://bedrock.amazonaws.com"), modelId);

  public async Task<ChatResponse> GetResponseAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(messages);

    var messageList = messages.ToList();
    var converseRequest = new ConverseRequest
    {
      ModelId = modelId,
      Messages = MapMessages(messageList),
      InferenceConfig = new InferenceConfiguration
      {
        MaxTokens = options?.MaxOutputTokens ?? 4096,
        Temperature = options?.Temperature ?? 0.1f
      }
    };

    var systemMessages = messageList
      .Where(m => m.Role == ChatRole.System)
      .SelectMany(m => m.Contents.OfType<TextContent>())
      .Select(t => new SystemContentBlock { Text = t.Text })
      .ToList();

    if (systemMessages.Count > 0)
    {
      converseRequest.System = systemMessages;
    }

    var response = await bedrockRuntime.ConverseAsync(converseRequest, cancellationToken);

    var responseText = response.Output?.Message?.Content
      ?.Where(c => c.Text is not null)
      .Select(c => c.Text)
      .FirstOrDefault() ?? string.Empty;

    var chatMessage = new ChatMessage(ChatRole.Assistant, responseText);

    return new ChatResponse(chatMessage)
    {
      Usage = new UsageDetails
      {
        InputTokenCount = response.Usage?.InputTokens,
        OutputTokenCount = response.Usage?.OutputTokens
      }
    };
  }

  public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var response = await GetResponseAsync(messages, options, cancellationToken);
    foreach (var message in response.Messages)
    {
      foreach (var content in message.Contents)
      {
        if (content is TextContent textContent)
        {
          yield return new ChatResponseUpdate
          {
            Role = ChatRole.Assistant,
            Contents = [textContent]
          };
        }
      }
    }
  }

  public object? GetService(Type serviceType, object? serviceKey = null)
  {
    return serviceType == typeof(IChatClient) ? this : null;
  }

  public void Dispose()
  {
    // BedrockRuntime client lifecycle is managed by DI
  }

  private static List<Message> MapMessages(List<ChatMessage> messages)
  {
    return messages
      .Where(m => m.Role != ChatRole.System)
      .Select(m => new Message
      {
        Role = m.Role == ChatRole.User
          ? ConversationRole.User
          : ConversationRole.Assistant,
        Content = m.Contents
          .OfType<TextContent>()
          .Select(t => new ContentBlock { Text = t.Text })
          .ToList()
      })
      .ToList();
  }
}
