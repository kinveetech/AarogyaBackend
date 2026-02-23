using Aarogya.Api.Features.V1.Reports;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class BedrockChatClientTests : IDisposable
{
  private readonly Mock<IAmazonBedrockRuntime> _bedrockMock;
  private readonly BedrockChatClient _client;

  public BedrockChatClientTests()
  {
    _bedrockMock = new Mock<IAmazonBedrockRuntime>();
    _client = new BedrockChatClient(_bedrockMock.Object, "anthropic.claude-sonnet-4-20250514");
  }

  [Fact]
  public async Task GetResponseAsync_ShouldReturnResponseTextAsync()
  {
    SetupConverseResponse("Hello, world!");

    var messages = new List<ChatMessage>
    {
      new(ChatRole.User, "Say hello")
    };

    var response = await _client.GetResponseAsync(messages);

    response.Text.Should().Be("Hello, world!");
  }

  [Fact]
  public async Task GetResponseAsync_ShouldMapSystemMessagesToConverseSystemAsync()
  {
    SetupConverseResponse("Response text");

    var messages = new List<ChatMessage>
    {
      new(ChatRole.System, "You are a helpful assistant."),
      new(ChatRole.User, "Hello")
    };

    await _client.GetResponseAsync(messages);

    _bedrockMock.Verify(x => x.ConverseAsync(
      It.Is<ConverseRequest>(r =>
        r.System.Count == 1 &&
        r.System[0].Text == "You are a helpful assistant." &&
        r.Messages.Count == 1 &&
        r.Messages[0].Role == ConversationRole.User),
      It.IsAny<CancellationToken>()));
  }

  [Fact]
  public async Task GetResponseAsync_ShouldPassChatOptionsToInferenceConfigAsync()
  {
    SetupConverseResponse("Response");

    var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
    var options = new ChatOptions
    {
      MaxOutputTokens = 1024,
      Temperature = 0.5f
    };

    await _client.GetResponseAsync(messages, options);

    _bedrockMock.Verify(x => x.ConverseAsync(
      It.Is<ConverseRequest>(r =>
        r.InferenceConfig.MaxTokens == 1024),
      It.IsAny<CancellationToken>()));
  }

  [Fact]
  public async Task GetResponseAsync_ShouldReturnUsageDetailsAsync()
  {
    _bedrockMock
      .Setup(x => x.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ConverseResponse
      {
        Output = new ConverseOutput
        {
          Message = new Message
          {
            Role = ConversationRole.Assistant,
            Content = [new ContentBlock { Text = "Test" }]
          }
        },
        Usage = new TokenUsage
        {
          InputTokens = 100,
          OutputTokens = 50
        }
      });

    var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

    var response = await _client.GetResponseAsync(messages);

    response.Usage.Should().NotBeNull();
    response.Usage!.InputTokenCount.Should().Be(100);
    response.Usage.OutputTokenCount.Should().Be(50);
  }

  [Fact]
  public async Task GetResponseAsync_ShouldReturnEmptyStringWhenNoContentAsync()
  {
    _bedrockMock
      .Setup(x => x.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ConverseResponse
      {
        Output = new ConverseOutput
        {
          Message = new Message
          {
            Role = ConversationRole.Assistant,
            Content = []
          }
        }
      });

    var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

    var response = await _client.GetResponseAsync(messages);

    response.Text.Should().BeEmpty();
  }

  [Fact]
  public async Task GetResponseAsync_ShouldThrowOnNullMessagesAsync()
  {
    var act = () => _client.GetResponseAsync(null!);

    await act.Should().ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public void Metadata_ShouldReflectModelId()
  {
    _client.Metadata.DefaultModelId.Should().Be("anthropic.claude-sonnet-4-20250514");
    _client.Metadata.ProviderName.Should().Be("bedrock");
  }

  [Fact]
  public void GetService_ShouldReturnSelfForIChatClient()
  {
    var service = _client.GetService(typeof(IChatClient));

    service.Should().BeSameAs(_client);
  }

  [Fact]
  public void GetService_ShouldReturnNullForOtherTypes()
  {
    var service = _client.GetService(typeof(string));

    service.Should().BeNull();
  }

  public void Dispose()
  {
    _client.Dispose();
  }

  private void SetupConverseResponse(string text)
  {
    _bedrockMock
      .Setup(x => x.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ConverseResponse
      {
        Output = new ConverseOutput
        {
          Message = new Message
          {
            Role = ConversationRole.Assistant,
            Content = [new ContentBlock { Text = text }]
          }
        }
      });
  }
}
