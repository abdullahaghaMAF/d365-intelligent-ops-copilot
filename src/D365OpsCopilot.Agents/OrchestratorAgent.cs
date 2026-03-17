using Azure;
using Azure.Search.Documents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace D365OpsCopilot.Agents;

public class OrchestratorAgent
{
    private readonly ChatCompletionAgent _dataAgent;
    private readonly ChatCompletionAgent _knowledgeAgent;
    private readonly Kernel _kernel;

    public OrchestratorAgent(
        Kernel orchestratorKernel,
        Kernel dataKernel,
        Kernel knowledgeKernel,
        SearchClient searchClient)
    {
        _kernel = orchestratorKernel;
        _dataAgent = new DataAgent(dataKernel).Build();
        _knowledgeAgent = new KnowledgeAgent(knowledgeKernel, searchClient).Build();
    }

    public async Task<AgentResponse> ProcessAsync(string userMessage)
    {
        var toolsUsed = new List<string>();

        // Step 1: Classify the intent
        var classification = await ClassifyIntent(userMessage);

        string finalResponse;

        switch (classification)
        {
            case "data":
                finalResponse = await InvokeAgent(_dataAgent, userMessage);
                toolsUsed.Add("DataAgent");
                break;

            case "knowledge":
                finalResponse = await InvokeAgent(_knowledgeAgent, userMessage);
                toolsUsed.Add("KnowledgeAgent");
                break;

            case "both":
                var dataTask = InvokeAgent(_dataAgent, userMessage);
                var knowledgeTask = InvokeAgent(_knowledgeAgent, userMessage);
                await Task.WhenAll(dataTask, knowledgeTask);

                finalResponse = await SynthesizeResponses(
                    userMessage, dataTask.Result, knowledgeTask.Result);
                toolsUsed.Add("DataAgent");
                toolsUsed.Add("KnowledgeAgent");
                break;

            default:
                finalResponse = await GeneralResponse(userMessage);
                toolsUsed.Add("GeneralKnowledge");
                break;
        }

        return new AgentResponse
        {
            Reply = finalResponse,
            ToolsUsed = toolsUsed,
            Classification = classification
        };
    }

    private async Task<string> ClassifyIntent(string userMessage)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(
            "Classify the user's message into exactly one category. Respond with ONLY one word:\n" +
            "- 'data' if asking about inventory levels, purchase orders, warehouse quantities, or operational metrics\n" +
            "- 'knowledge' if asking about company policies, SOPs, procedures, guidelines, or approval processes\n" +
            "- 'both' if the question requires both operational data AND policy/procedure information\n" +
            "- 'general' if it's a general question not related to either\n" +
            "Respond with only: data, knowledge, both, or general");
        history.AddUserMessage(userMessage);

        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content?.Trim().ToLower() ?? "general";
    }

    private async Task<string> InvokeAgent(ChatCompletionAgent agent, string userMessage)
    {
        var chatService = agent.Kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(agent.Instructions ?? string.Empty);
        history.AddUserMessage(userMessage);

        var settings = new Microsoft.SemanticKernel.Connectors.AzureOpenAI.AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await chatService.GetChatMessageContentAsync(history, settings, agent.Kernel);
        return result.Content ?? "No response generated.";
    }

    private async Task<string> SynthesizeResponses(
        string userMessage, string dataResponse, string knowledgeResponse)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are a helpful assistant that synthesizes information from multiple sources. " +
            "Combine the following responses into a single, coherent answer. " +
            "Clearly distinguish between operational data and policy information.");
        history.AddUserMessage(
            $"Original question: {userMessage}\n\n" +
            $"Operational Data:\n{dataResponse}\n\n" +
            $"Policy Information:\n{knowledgeResponse}");

        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content ?? "Unable to synthesize responses.";
    }

    private async Task<string> GeneralResponse(string userMessage)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are an operations assistant for a retail company running D365 F&O. " +
            "Answer general questions helpfully and professionally.");
        history.AddUserMessage(userMessage);

        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content ?? "No response generated.";
    }
}

public class AgentResponse
{
    public string Reply { get; set; } = string.Empty;
    public List<string> ToolsUsed { get; set; } = new();
    public string Classification { get; set; } = string.Empty;
}

public class MaxTurnsTerminationStrategy : TerminationStrategy
{
    private readonly int _maxTurns;
    private int _currentTurn;

    public MaxTurnsTerminationStrategy(int maxTurns)
    {
        _maxTurns = maxTurns;
    }

    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
    {
        _currentTurn++;
        return Task.FromResult(_currentTurn >= _maxTurns);
    }
}