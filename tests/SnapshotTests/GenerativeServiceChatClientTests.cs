using AWise.AiExtensionsForGemini;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace SnapshotTests;

public class GenerativeServiceChatClientTests : TestBase
{
    [Fact]
    public async Task TestBasicTextGeneration()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new GenerativeServiceChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);

        var res = await client.GetResponseAsync(new ChatMessage(ChatRole.User, "Say Hi."));

        await Verify(res);
    }

    [Fact]
    public async Task TestImageClassification()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new GenerativeServiceChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        List<ChatMessage> chatMessage =
        [
            new ChatMessage(ChatRole.User, [new DataContent(Properties.Resources.circle, "image/png")]),
            new ChatMessage(ChatRole.User, "What type of shape is in this picture?"),
        ];

        var res = await client.GetResponseAsync(chatMessage);

        await Verify(res);
    }

    [Fact]
    public async Task TestSystemInstructions()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new GenerativeServiceChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        var options = new ChatOptions()
        {
            Instructions = "You're a language translator. Your mission is to translate text in English to French.",
        };

        var res = await client.GetResponseAsync(new ChatMessage(ChatRole.User, "Why is the sky blue?"), options);

        await Verify(res);
    }

    [Fact]
    public async Task TestSystemMessage()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new GenerativeServiceChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        List<ChatMessage> chatMessage =
        [
            new ChatMessage(ChatRole.System, "You're a language translator. Your mission is to translate text in English to French."),
            new ChatMessage(ChatRole.User, "Why is the sky blue?"),
        ];

        var res = await client.GetResponseAsync(chatMessage);

        await Verify(res);
    }

    [Fact(Skip = "Not currently working.")]
    public async Task TestFunctionCalling()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new GenerativeServiceChatClient(vertex, defaultModelId: FLASH_MODEL_NAME).AsBuilder().UseFunctionInvocation().Build();
        var options = new ChatOptions()
        {
            Tools = [AIFunctionFactory.Create(AddTwoNumbers)],
        };

        var response = await client.GetResponseAsync("What is the sum of 1087 and 5099?", options);

        await Verify(response);
    }

    [Description("Adds two numbers together")]
    static int AddTwoNumbers(int a, int b)
    {
        return a + b;
    }
}
