using Google.Ai.Generativelanguage.V1Beta;
using Google.Api.Gax.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AWise.AiExtensionsForGemini;

public class GenerativeServiceChatClient : IChatClient
{
    const string TOOL_RESULT_NAME = "result";

    private readonly GenerativeService.GenerativeServiceClient _client;
    private readonly string? _defaultModelId;

    // TODO: replace with offical implementation of GenerativeServiceClient when available
    public GenerativeServiceChatClient(GenerativeServiceClientBuilder builder, string? defaultModelId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        this._client = builder.Build();
        this._defaultModelId = defaultModelId;
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        GenerateContentResponse response;
        try
        {
            response = await _client.GenerateContentAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
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
        // TODO: uncomment when we have a good way to make the unit tests ignore this from run-to-run
        // chatResponse.ResponseId = response.ResponseId;
        if (candidate.FinishReason != Candidate.Types.FinishReason.Unspecified)
        {
            chatResponse.FinishReason = GetFinishReason(candidate.FinishReason);
        }

        chatResponse.Messages.Add(new ChatMessage()
        {
            Role = GetRole(candidate.Content),
            Contents = ConvertToAiContent(candidate.Content),
        });
        return chatResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var callSettings = CallSettings.FromCancellationToken(cancellationToken);
        using (var stream = _client.StreamGenerateContent(request, cancellationToken: cancellationToken))
        {
            var responseStream = stream.ResponseStream;
            while (await responseStream.MoveNext(cancellationToken).ConfigureAwait(false))
            {
                var response = responseStream.Current;
                if (response.Candidates.Count != 1)
                {
                    throw new InvalidOperationException($"Unexpected number of candidates: {response.Candidates.Count}");
                }
                var candidate = response.Candidates[0];
                var chatResponse = new ChatResponseUpdate()
                {
                    ResponseId = response.ResponseId,
                };
                if (candidate.FinishReason != Candidate.Types.FinishReason.Unspecified)
                {
                    chatResponse.FinishReason = GetFinishReason(candidate.FinishReason);
                }
                chatResponse.Role = GetRole(candidate.Content);
                chatResponse.Contents = ConvertToAiContent(candidate.Content);
                yield return chatResponse;
            }
        }
    }

    private GenerateContentRequest CreateRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var request = new GenerateContentRequest();
        request.GenerationConfig = new GenerationConfig();
        var systemInstruction = new Content();
        if (options != null)
        {
            if (options.ConversationId != null)
            {
                throw new NotImplementedException("ConversationId must be null; stateful client not yet implemented.");
            }
            if (options.Instructions != null)
            {
                systemInstruction.Parts.Add(new Part()
                {
                    Text = options.Instructions,
                });
            }
            if (options.Temperature.HasValue)
            {
                request.GenerationConfig.Temperature = options.Temperature.Value;
            }
            if (options.MaxOutputTokens.HasValue)
            {
                request.GenerationConfig.MaxOutputTokens = options.MaxOutputTokens.Value;
            }
            if (options.TopP.HasValue)
            {
                request.GenerationConfig.TopP = options.TopP.Value;
            }
            if (options.TopK.HasValue)
            {
                request.GenerationConfig.TopK = options.TopK.Value;
            }
            if (options.FrequencyPenalty.HasValue)
            {
                request.GenerationConfig.FrequencyPenalty = options.FrequencyPenalty.Value;
            }
            if (options.PresencePenalty.HasValue)
            {
                request.GenerationConfig.PresencePenalty = options.PresencePenalty.Value;
            }
            if (options.Seed.HasValue)
            {
                // TODO: consider either throwing an ArgumentOutOfRange exception instead of a cast error.
                request.GenerationConfig.Seed = (int)options.Seed.Value;
            }
            if (options.ResponseFormat != null)
            {
                if (options.ResponseFormat is ChatResponseFormatText)
                {
                    request.GenerationConfig.ResponseMimeType = "text/plain";
                }
                else if (options.ResponseFormat is ChatResponseFormatJson json)
                {
                    request.GenerationConfig.ResponseMimeType = "application/json";
                    if (json.Schema.HasValue)
                    {
                        request.GenerationConfig.ResponseJsonSchema = ProtoJsonConversions.ConvertJsonElementToValue(json.Schema.Value);
                    }
                }
                else
                {
                    throw new ArgumentException("Unexpected ChatResponseFormat: " + options.ResponseFormat.GetType().Name);
                }
            }
            if (options.ModelId != null)
            {
                request.Model = options.ModelId;
            }
            if (options.StopSequences != null)
            {
                request.GenerationConfig.StopSequences.AddRange(options.StopSequences);
            }
            if (options.AllowMultipleToolCalls.HasValue)
            {
                throw new NotImplementedException("AllowMultipleToolCalls not yet implemented.");
            }
            if (options.ToolMode != null)
            {
                request.ToolConfig ??= new ToolConfig();
                request.ToolConfig.FunctionCallingConfig ??= new FunctionCallingConfig();
                if (options.ToolMode is AutoChatToolMode)
                {
                    request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.Auto;
                }
                else if (options.ToolMode is NoneChatToolMode)
                {
                    request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.None;
                }
                else if (options.ToolMode is RequiredChatToolMode required)
                {
                    if (required.RequiredFunctionName is null)
                    {
                        request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.Any;
                    }
                    else
                    {
                        request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.Any;
                        request.ToolConfig.FunctionCallingConfig.AllowedFunctionNames.Add(required.RequiredFunctionName);
                    }
                }
                else
                {
                    throw new ArgumentException("Unexpected tool mode type: " + options.ToolMode.GetType().Name);
                }
            }
            if (options.Tools != null)
            {
                foreach (var optionTool in options.Tools)
                {
                    var functionDeclarations = new List<FunctionDeclaration>();
                    if (optionTool is AIFunction function)
                    {
                        var decl = new FunctionDeclaration()
                        {
                            Name = function.Name,
                            Description = function.Description,
                        };
                        // TODO: is there a better way to detect empty object?
                        if (function.JsonSchema.ValueKind == JsonValueKind.Object && function.JsonSchema.EnumerateObject().GetEnumerator().MoveNext())
                        {
                            decl.ParametersJsonSchema = ProtoJsonConversions.ConvertJsonElementToValue(function.JsonSchema);
                        }
                        if (function.ReturnJsonSchema.HasValue)
                        {
                            var responseSchema = ProtoJsonConversions.ConvertJsonElementToValue(function.ReturnJsonSchema.Value);
                            if (responseSchema.StructValue.Fields["type"].StringValue != "object")
                            {
                                // The API only supports a Struct for the response, see other references to TOOL_RESULT_NAME for more details.
                                responseSchema = new Google.Protobuf.WellKnownTypes.Value()
                                {
                                    StructValue = new Google.Protobuf.WellKnownTypes.Struct()
                                    {
                                        Fields =
                                        {
                                            ["type"] = Google.Protobuf.WellKnownTypes.Value.ForString("object"),
                                            ["properties"] = new Google.Protobuf.WellKnownTypes.Value()
                                            {
                                                StructValue = new Google.Protobuf.WellKnownTypes.Struct()
                                                {
                                                    Fields =
                                                    {
                                                        [TOOL_RESULT_NAME] = responseSchema,
                                                    },
                                                },
                                            },
                                            ["required"] = new Google.Protobuf.WellKnownTypes.Value()
                                            {
                                                ListValue = new Google.Protobuf.WellKnownTypes.ListValue()
                                                {
                                                    Values = { Google.Protobuf.WellKnownTypes.Value.ForString(TOOL_RESULT_NAME) },
                                                },
                                            },
                                        },
                                    },
                                };
                            }
                            decl.ResponseJsonSchema = responseSchema;
                        }
                        if (function.AdditionalProperties.Count != 0)
                        {
                            throw new NotImplementedException("AIFunction.AdditionalProperties not yet supported");
                        }

                        functionDeclarations.Add(decl);
                    }
                    else
                    {
                        // TODO: implement grounding with Google search: https://cloud.google.com/vertex-ai/generative-ai/docs/grounding/grounding-with-google-search
                        // TOOD: implement code execution: https://cloud.google.com/vertex-ai/generative-ai/docs/multimodal/code-execution
                        throw new NotImplementedException("Not yet implemented tool type: " + optionTool.GetType().Name);
                    }
                    // TODO: figure out what to do about the limit of 1 tool. It currently returns this error message for more than 1 tools:
                    //   "Multiple tools are supported only when they are all search tools"
                    if (functionDeclarations.Count != 0)
                    {
                        var tool = new Tool();
                        tool.FunctionDeclarations.AddRange(functionDeclarations);
                        request.Tools.Add(tool);
                    }
                }
            }
            if (options.RawRepresentationFactory != null)
            {
                throw new NotImplementedException("RawRepresentationFactory not implemented.");
            }
            if (options.AdditionalProperties != null)
            {
                throw new NotImplementedException("AdditionalProperties not implemented.");
            }
        }

        if (string.IsNullOrEmpty(request.Model))
        {
            if (string.IsNullOrEmpty(_defaultModelId))
            {
                // The error message from the API when we don't specify a model is somewhat inscrutable:
                //   Invalid resource field value in the request.
                // And forgetting to set a model is easy. So this is the one piece of validation we do
                // before sending the request.
                throw new ArgumentException($"Please specify the ModelId, either in ChatOptions.ModelId or the defaultModelId when creating the {nameof(GenerativeServiceChatClient)}.");
            }
            else
            {
                request.Model = _defaultModelId;
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
                else if (messageContent is DataContent dataContent)
                {
                    part.InlineData = new Blob()
                    {
                        // dataContent.Data is a ReadOnlyMemory, so its size can't change.
                        // The only this that could change is the backing array, but that
                        // should not be unsafe.
                        Data = UnsafeByteOperations.UnsafeWrap(dataContent.Data),
                        MimeType = dataContent.MediaType,
                    };
                }
                else if (messageContent is FunctionCallContent functionCall)
                {
                    part.FunctionCall = new FunctionCall()
                    {
                        Name = functionCall.Name,
                    };
                    if (functionCall.Arguments != null)
                    {
                        var args = new Google.Protobuf.WellKnownTypes.Struct();
                        foreach (var arg in functionCall.Arguments)
                        {
                            args.Fields[arg.Key] = ProtoJsonConversions.ConvertObjectToValue(arg.Value);
                        }
                        part.FunctionCall.Args = args;
                    }
                }
                else if (messageContent is FunctionResultContent functionResult)
                {
                    var functionResponse = new FunctionResponse()
                    {
                        Name = functionResult.CallId,
                    };
                    var result = ProtoJsonConversions.ConvertObjectToValue(functionResult.Result);
                    if (result.KindCase == Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StructValue)
                    {
                        functionResponse.Response = result.StructValue;
                    }
                    else
                    {
                        functionResponse.Response = new Google.Protobuf.WellKnownTypes.Struct();
                        functionResponse.Response.Fields[TOOL_RESULT_NAME] = result;
                    }
                    part.FunctionResponse = functionResponse;
                }
                // TODO: implement more content types
                else
                {
                    throw new NotImplementedException("Unimplemented AIContent type: " + messageContent.GetType().Name);
                }
                content.Parts.Add(part);
            }

            if (message.Role == ChatRole.System)
            {
                systemInstruction.Parts.AddRange(content.Parts);
            }
            else
            {
                request.Contents.Add(content);
            }
        }
        if (systemInstruction.Parts.Count != 0)
        {
            request.SystemInstruction = systemInstruction;
        }
        return request;
    }

    // TODO: make sure these mappings and exceptions make sense. Like would it be better to create custom ChatFinishReasons for each type?
    private static ChatFinishReason? GetFinishReason(Candidate.Types.FinishReason finishReason)
    {
        return finishReason switch
        {
            Candidate.Types.FinishReason.Stop => ChatFinishReason.Stop,
            Candidate.Types.FinishReason.MaxTokens => ChatFinishReason.Length,
            Candidate.Types.FinishReason.Safety => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.Recitation => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.Spii => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.Language => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.Blocklist => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.ProhibitedContent => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.ImageSafety => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.MalformedFunctionCall => throw new InvalidOperationException("Malformed tool call."),
            Candidate.Types.FinishReason.UnexpectedToolCall => throw new InvalidOperationException("Unexpected tool call."),
            Candidate.Types.FinishReason.Other => throw new InvalidOperationException("Other finish reason."),
            Candidate.Types.FinishReason.Unspecified => null,
            _ => null,
        };
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
                    var args = part.FunctionCall.Args.Fields.ToDictionary(a => a.Key, a => (object?)ProtoJsonConversions.ConvertValueToJsonNode(a.Value));
                    ret.Add(new FunctionCallContent(part.FunctionCall.Name, part.FunctionCall.Name, args));
                    break;
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
