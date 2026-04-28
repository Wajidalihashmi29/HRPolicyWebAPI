using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Responses;
using OpenAI.Files;
using OpenAI.VectorStores;
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

    private AIProjectClient CreateClient()
    {
        var endpoint = _config["AzureOpenAI:ProjectEndpoint"]
            ?? "https://af-msaitraining-b1-g2.services.ai.azure.com/api/projects/hrpolicy";
        return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
    }

    // ── Health ───────────────────────────────────────────────────────────────
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });

    // ── Create Agent ─────────────────────────────────────────────────────────
    [HttpPost("setup/agent")]
    public async Task<IActionResult> CreateAgent()
    {
        try
        {
            var client = CreateClient();
            var deployment = _config["AzureOpenAI:Deployment"] ?? "gpt-4.1";

            // Check if vector store exists
            var vctClient = client.ProjectOpenAIClient.GetVectorStoreClient();
            var store = vctClient.GetVectorStores()
                .FirstOrDefault(s => s.Name == "hr-policy-documents-vectorstore");

            if (store == null)
            {
                return BadRequest(new
                {
                    error = "Vector store 'hr-policy-documents-vectorstore' not found. " +
                            "Please upload policy documents first via /api/hragent/setup/knowledge-base."
                });
            }

            var agentDefinition = new DeclarativeAgentDefinition(model: deployment)
            {
                Temperature = 0.2f,
                Instructions = @"You are a helpful Human Resources agent that can help employees understand our policies. 
Your job is to explain the policies in a way that's comprehensive yet easy to understand and helpful for the user.
Give accurate citation verbatim as written in the policy documents.
-[Important] Avoid very short responses. Use the retrieved policy information to provide comprehensive responses.
-[Critical] You must include citations for all information sourced from the uploaded files. Never give a response that is not explicitly grounded up by document citation.
-[Critical] If information found in the knowledge source is insufficient, say 'Sorry, I lack the information to assist you with this query.'
-[Critical] Do not entertain any request unrelated to HR policies. Politely tell the user you are unable to help with such requests.
-[Critical] You must pass the output of the FileSearch tool directly to the 'formatCitation' tool to prepare citations.
-[Critical] You are strictly forbidden from rephrasing or modifying content from the FileSearch tool while calling 'formatCitation'.
- You must call formatCitation for every citation you include.",
                Tools =
                {
                    ResponseTool.CreateFileSearchTool(new List<string> { store.Id }, 12),
                    CitationFormatter.FormatCitationTool
                }
            };

            var agentVersion = client.AgentAdministrationClient.CreateAgentVersion(
                agentName: "HRPolicyAgent",
                options: new ProjectsAgentVersionCreationOptions(agentDefinition)
            );

            return Ok(new
            {
                success = true,
                agentName = "HRPolicyAgent",
                version = agentVersion.Value.Version,
                message = $"Agent created successfully. Use version: {agentVersion.Value.Version}"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── Setup Knowledge Base (upload PDFs + create vector store) ─────────────
    [HttpPost("setup/knowledge-base")]
    public async IAsyncEnumerable<string> SetupKnowledgeBase()
    {
        var client = CreateClient();
        string pdfFolder = "C:\\AzureFoundry\\HRPolicyWebAPI\\Policy Documents";

        if (!Directory.Exists(pdfFolder))
        {
            yield return Evt("error", $"PolicyDocuments folder not found at: {pdfFolder}");
            yield break;
        }

        var files = Directory.GetFiles(pdfFolder, "*", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            yield return Evt("error", "No files found in PolicyDocuments folder.");
            yield break;
        }

        yield return Evt("log", $"Found {files.Length} file(s) to upload...");

        var fileIds = new List<string>();
        var existingFiles = await client.ProjectOpenAIClient
            .GetProjectFilesClient().GetFilesAsync(FilePurpose.Assistants);

        foreach (var filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var existingId = existingFiles.Value.FirstOrDefault(f => f.Filename == fileName)?.Id;
            if (!string.IsNullOrEmpty(existingId))
                await client.ProjectOpenAIClient.GetProjectFilesClient().DeleteFileAsync(existingId);

            var uploaded = await client.ProjectOpenAIClient.GetProjectFilesClient()
                .UploadFileAsync(new BinaryData(fileBytes), fileName, FileUploadPurpose.Assistants);
            fileIds.Add(uploaded.Value.Id);
            yield return Evt("log", $"Uploaded: {fileName}");
        }

        yield return Evt("log", "Creating vector store...");

        var vctClient = client.ProjectOpenAIClient.GetVectorStoreClient();
        var existing = vctClient.GetVectorStores()
            .FirstOrDefault(s => s.Name == "hr-policy-documents-vectorstore");
        if (existing != null)
        {
            await vctClient.DeleteVectorStoreAsync(existing.Id);
            yield return Evt("log", "Deleted old vector store.");
        }

        var vsOptions = new VectorStoreCreationOptions { Name = "hr-policy-documents-vectorstore" };
        var vectorStore = await vctClient.CreateVectorStoreAsync(vsOptions);
        await vctClient.AddFileBatchToVectorStoreAsync(vectorStore.Value.Id, fileIds);

        yield return Evt("done", $"Knowledge base ready. Vector store ID: {vectorStore.Value.Id}");
    }

    // ── Chat (streaming SSE) ─────────────────────────────────────────────────
    [HttpPost("chat")]
    public async IAsyncEnumerable<string> Chat([FromBody] ChatRequest req)
    {
        var agentVersion = _config["AzureOpenAI:AgentVersion"] ?? "1";
        var client = CreateClient();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var conversationResult = client.ProjectOpenAIClient
            .GetProjectConversationsClient()
            .CreateProjectConversation(new ProjectConversationCreationOptions());

        var responseOptions = new CreateResponseOptions()
        {
            Agent = new AgentReference("HRPolicyAgent", agentVersion),
            AgentConversationId = conversationResult.Value.Id,
            StreamingEnabled = true,
        };

        responseOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(req.Message));

        var responsesClient = client.ProjectOpenAIClient
            .GetProjectResponsesClientForAgent(responseOptions.Agent, conversationResult.Value.Id);

        while (true)
        {
            var inputItems = new List<ResponseItem>();
            bool functionCalled = false;

            await foreach (var streamResponse in responsesClient
                .CreateResponseStreamingAsync(responseOptions, HttpContext.RequestAborted))
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
                    yield return $"data: {JsonSerializer.Serialize(new { type = "text", text = textDelta.Delta })}\n\n";
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

    // ── Helper ───────────────────────────────────────────────────────────────
    private static string Evt(string type, string text) =>
        $"data: {JsonSerializer.Serialize(new { type, text })}\n\n";
}

public record ChatRequest(string Message);