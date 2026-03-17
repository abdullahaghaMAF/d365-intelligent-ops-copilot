using System.Net;
using Azure;
using Azure.Search.Documents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using D365OpsCopilot.Shared.Models;
using D365OpsCopilot.Agents;

namespace D365OpsCopilot.Functions.Functions;

public class ChatFunction
{
    private readonly IConfiguration _config;

    public ChatFunction(IConfiguration config)
    {
        _config = config;
    }

    [Function("Chat")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var requestBody = await req.ReadFromJsonAsync<ChatRequest>();

        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Message))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "Message is required" });
            return badResponse;
        }

        // Create three separate Kernels - one per agent
        var orchestratorKernel = CreateKernel();
        var dataKernel = CreateKernel();
        var knowledgeKernel = CreateKernel();

        // Create the Search Client for KnowledgeAgent
        var searchClient = new SearchClient(
            new Uri(_config["AZURE_SEARCH_ENDPOINT"]!),
            _config["AZURE_SEARCH_INDEX_NAME"]!,
            new AzureKeyCredential(_config["AZURE_SEARCH_API_KEY"]!));

        // Build the Orchestrator with all agents
        var orchestrator = new OrchestratorAgent(
            orchestratorKernel,
            dataKernel,
            knowledgeKernel,
            searchClient);

        // Process the message through the multi-agent system
        var agentResponse = await orchestrator.ProcessAsync(requestBody.Message);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ChatResponse
        {
            Reply = agentResponse.Reply,
            SessionId = requestBody.SessionId,
            ToolsUsed = agentResponse.ToolsUsed
        });

        return response;
    }

    private Kernel CreateKernel()
    {
        return Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: _config["AZURE_OPENAI_DEPLOYMENT_NAME"]!,
                endpoint: _config["AZURE_OPENAI_ENDPOINT"]!,
                apiKey: _config["AZURE_OPENAI_API_KEY"]!)
            .Build();
    }
}