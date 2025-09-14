using AWise.AiExtensionsForGemini;
using Microsoft.Extensions.AI;
using static Google.Api.Gax.Grpc.ClientHelper;

string projectId = "ai-test-414105";
string location = "us-central1";
string publisher = "google";
string model = "models/gemini-2.5-flash-lite";

var client = new GenerativeServiceChatClient(new GenerativeServiceClientBuilder()
{
    Endpoint = $"https://generativelanguage.googleapis.com",
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new InvalidOperationException("Set GEMINI_API_KEY environment variable to your API key."),
}, model);

Console.WriteLine("Trying GetResponse:");
var response = await client.GetResponseAsync(new ChatMessage(ChatRole.User, "Say hi."));
Console.WriteLine(response);
Console.WriteLine();

Console.WriteLine("DONE!");
