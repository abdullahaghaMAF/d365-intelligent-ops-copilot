using Azure;
using Azure.Search.Documents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using D365OpsCopilot.Plugins;

namespace D365OpsCopilot.Agents;

public class KnowledgeAgent
{
    private readonly Kernel _kernel;

    public KnowledgeAgent(Kernel kernel, SearchClient searchClient)
    {
        _kernel = kernel;
        var knowledgePlugin = new KnowledgePlugin(searchClient);
        _kernel.Plugins.AddFromObject(knowledgePlugin);
    }

    public ChatCompletionAgent Build()
    {
        return new ChatCompletionAgent
        {
            Name = "KnowledgeAgent",
            Instructions =
                "You are a company knowledge specialist. " +
                "Your job is to answer questions about company policies, SOPs, " +
                "procurement guidelines, warehouse operations, and internal procedures. " +
                "ALWAYS use the knowledge search tool to find relevant documents before answering. " +
                "Always cite the source document name in your response.",
            Kernel = _kernel,
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
        };
    }
}