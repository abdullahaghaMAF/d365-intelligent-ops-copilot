using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using D365OpsCopilot.Plugins;

namespace D365OpsCopilot.Agents;

public class DataAgent
{
    private readonly Kernel _kernel;

    public DataAgent(Kernel kernel)
    {
        _kernel = kernel;
        _kernel.Plugins.AddFromType<D365DataPlugin>();
    }

    public ChatCompletionAgent Build()
    {
        return new ChatCompletionAgent
        {
            Name = "DataAgent",
            Instructions =
                "You are a D365 Finance & Operations data specialist. " +
                "Your job is to retrieve operational data including inventory levels, " +
                "purchase orders, and warehouse summaries. " +
                "ALWAYS use your available tools to fetch data. Never guess or make up numbers. " +
                "Present data in a clear, structured format.",
            Kernel = _kernel,
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
        };
    }
}