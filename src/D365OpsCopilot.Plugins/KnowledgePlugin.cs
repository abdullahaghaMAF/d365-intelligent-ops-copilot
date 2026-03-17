using System.ComponentModel;
using System.Text.Json;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.SemanticKernel;

namespace D365OpsCopilot.Plugins;

public class KnowledgePlugin
{
    private readonly SearchClient _searchClient;

    public KnowledgePlugin(SearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    [KernelFunction("search_company_knowledge")]
    [Description("Searches company SOPs, policies, guidelines, and operations manuals. Use this when users ask about company rules, procedures, approval processes, warehouse operations, procurement guidelines, or any internal policy.")]
    public async Task<string> SearchKnowledge(
        [Description("The search query describing what information is needed")] string query)
    {
        var searchOptions = new SearchOptions
        {
            Size = 3,
            Select = { "content", "title", "source" }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions);

        var results = new List<object>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(new
            {
                content = result.Document.GetString("content"),
                title = result.Document.GetString("title"),
                source = result.Document.GetString("source"),
                score = result.Score
            });
        }

        if (results.Count == 0)
        {
            return JsonSerializer.Serialize(new { message = "No relevant documents found.", query });
        }

        return JsonSerializer.Serialize(new { query, resultsCount = results.Count, results });
    }
}