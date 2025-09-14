using Google.Ai.Generativelanguage.V1Beta;
using Grpc.Core;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWise.AiExtensionsForGemini;

public class GenerativeServiceChatClient : IChatClient
{
    private readonly GenerativeService.GenerativeServiceClient _client;
    private readonly string? _defaultModel;

    // TODO: replace with offical implementation of GenerativeServiceClient when available
    public GenerativeServiceChatClient(GenerativeServiceClientBuilder builder, string? defaultModel = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        this._client = builder.Build();
        this._defaultModel = defaultModel;
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        GenerateContentResponse response;
        try
        {
            response = await _client.GenerateContentAsync(request, cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            var errorInfoBytes = ex.Trailers.GetValueBytes("google.rpc.errorinfo-bin");
            if (errorInfoBytes is not null)
            {
                // TODO: make a better errror message? Or determine that this does not add value and delete.
                var errorInfo = Google.Rpc.ErrorInfo.Parser.ParseFrom(errorInfoBytes);
                throw new InvalidOperationException(errorInfo.ToString(), ex);
            }
            throw;
        }
        if (response.Candidates.Count != 1)
        {
            throw new InvalidOperationException($"Unexpected number of candidates: {response.Candidates.Count}");
        }
        var candidate = response.Candidates[0];
        var chatResponse = new ChatResponse()
        {
        };

        chatResponse.Messages.Add(new ChatMessage()
        {
            Role = GetRole(candidate.Content),
            Contents = ConvertToAiContent(candidate.Content),
        });
        return chatResponse;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private GenerateContentRequest CreateRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var request = new GenerateContentRequest();
        request.GenerationConfig = new GenerationConfig();

        if (string.IsNullOrEmpty(request.Model))
        {
            if (string.IsNullOrEmpty(_defaultModel))
            {
                // The error message from the API when we don't specify a model is somewhat inscrutable:
                //   Invalid resource field value in the request.
                // And forgetting to set a model is easy. So this is the one piece of validation we do
                // before sending the request.
                throw new ArgumentException($"Please specify the Model, either in ChatOptions.ModelId or the defaultModel when creating the {nameof(GenerativeServiceChatClient)}.");
            }
            else
            {
                request.Model = _defaultModel;
            }
        }

        foreach (var message in messages)
        {
            var content = new Content();
            if (message.Role == ChatRole.User || message.Role == ChatRole.Tool)
            {
                content.Role = "user";
            }
            else if (message.Role == ChatRole.Assistant)
            {
                content.Role = "model";
            }
            else if (message.Role == ChatRole.System)
            {
                // No role needed, we are going to append this message to the system instructions.
            }
            else
            {
                throw new ArgumentException("Unexpected chat role: " + message.Role.Value);
            }
            // TODO: throw for other properties we don't support??
            foreach (var messageContent in message.Contents)
            {
                var part = new Part();
                if (messageContent is TextContent textContent)
                {
                    part.Text = textContent.Text;
                }
                // TODO: implement more content types
                else
                {
                    throw new NotImplementedException("Unimplemented AIContent type: " + messageContent.GetType().Name);
                }
                content.Parts.Add(part);
            }
            request.Contents.Add(content);
        }

        return request;
    }

    private static IList<AIContent> ConvertToAiContent(Content content)
    {
        var ret = new List<AIContent>();
        foreach (var part in content.Parts)
        {
            switch (part.DataCase)
            {
                case Part.DataOneofCase.Text:
                    ret.Add(new TextContent(part.Text));
                    break;
                case Part.DataOneofCase.FunctionCall:
                case Part.DataOneofCase.FunctionResponse:
                case Part.DataOneofCase.InlineData:
                case Part.DataOneofCase.FileData:
                case Part.DataOneofCase.ExecutableCode:
                case Part.DataOneofCase.CodeExecutionResult:
                    throw new NotImplementedException("Not implemented part type: " + part.DataCase);
                case Part.DataOneofCase.None:
                default:
                    throw new InvalidOperationException("Unexpected part type: " + part.DataCase);
            }
        }
        return ret;
    }

    private static ChatRole GetRole(Content content)
    {
        return content.Role switch
        {
            "user" => ChatRole.User,
            "model" => ChatRole.Assistant,
            _ => throw new InvalidOperationException("Unexpected role: " + content.Role),
        };
    }

    object? IChatClient.GetService(System.Type serviceType, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }
        else if (serviceType == typeof(GenerativeServiceChatClient))
        {
            return this;
        }
        else if (serviceType == typeof(GenerativeService.GenerativeServiceClient))
        {
            return _client;
        }
        return null;
    }

    void IDisposable.Dispose()
    {
        // No resources to dispose
    }
}
