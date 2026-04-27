using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Responses;
using System.Text.Json;
using System.ClientModel;
using VirtualHRFoundryAgent;

#pragma warning disable OPENAI001
[ApiController]
[Route("api/[controller]")]
public class HRAgentController : ControllerBase
{
    private readonly IConfiguration _config;
    public HRAgentController(IConfiguration config) => _config = config;

    [HttpPost("chat")]
    public async IAsyncEnumerable<string> Chat([FromBody] ChatRequest req)
    {
        var endpoint = _config["azureopenai:projectendpoint"] ?? "";
        var deployment = _config["azureopenai:deployment"] ?? "gpt-4.1";
        var agentVersion = _config["azureopenai:agentVersion"] ?? "1";

        var client = new AIProjectClient(new Uri(endpoint), new AzureCliCredential());

        ClientResult<ProjectConversation> conversationResult =
            client.ProjectOpenAIClient.GetProjectConversationsClient().CreateProjectConversation();

        var responseOptions = new CreateResponseOptions()
        {
            Agent = new AgentReference("HRPolicyAgent", agentVersion),
            AgentConversationId = conversationResult.Value.Id,
            StreamingEnabled = true,
        };

        responseOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(req.Message));

        Response.ContentType = "text/event-stream";

        while (true)
        {
            var inputItems = new List<ResponseItem>();
            bool functionCalled = false;

            foreach (var streamResponse in client.ProjectOpenAIClient
                .GetResponsesClient().CreateResponseStreaming(responseOptions))
            {
                if (streamResponse is StreamingResponseOutputItemDoneUpdate itemDoneUpdate)
                {
                    if (itemDoneUpdate.Item is FunctionCallResponseItem functionToolCall)
                    {
                        var functionOutputItem = CitationFormatter.GetResolvedToolOutput(functionToolCall);
                        if (functionOutputItem != null)
                        {
                            inputItems.Add(functionOutputItem);
                            functionCalled = true;
                            yield return $"data: {JsonSerializer.Serialize(new { type = "citation", text = functionOutputItem.FunctionOutput })}\n\n";
                        }
                    }
                }

                if (streamResponse is StreamingResponseOutputTextDeltaUpdate textDelta)
                {
                    yield return $"data: {JsonSerializer.Serialize(new { type = "text", text = textDelta.Delta })}\n\n";
                }
                else if (streamResponse is StreamingResponseErrorUpdate errorUpdate)
                {
                    yield return $"data: {JsonSerializer.Serialize(new { type = "error", text = errorUpdate.Message })}\n\n";
                    yield break;
                }
            }

            if (functionCalled)
            {
                responseOptions.InputItems.Clear();
                foreach (var item in inputItems)
                    responseOptions.InputItems.Add(item);
            }
            else break;
        }

        yield return $"data: {JsonSerializer.Serialize(new { type = "done" })}\n\n";
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });
}

public record ChatRequest(string Message);