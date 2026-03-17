using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

var openAiEndpoint = config["AZURE_OPENAI_ENDPOINT"]!;
var openAiKey = config["AZURE_OPENAI_API_KEY"]!;
var embeddingDeployment = config["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"]!;
var searchEndpoint = config["AZURE_SEARCH_ENDPOINT"]!;
var searchKey = config["AZURE_SEARCH_API_KEY"]!;
var indexName = config["AZURE_SEARCH_INDEX_NAME"]!;
var dataFolder = config["DATA_FOLDER"]!;

Console.WriteLine("=== D365 Ops Copilot - Document Indexer ===\n");

// Step 1: Create the search index with vector field
Console.WriteLine("Creating search index...");
var indexClient = new SearchIndexClient(
    new Uri(searchEndpoint),
    new AzureKeyCredential(searchKey));

var searchIndex = new SearchIndex(indexName)
{
    Fields = new[]
    {
        new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
        new SearchableField("content") { AnalyzerName = LexicalAnalyzerName.EnLucene },
        new SearchableField("title") { IsFilterable = true },
        new SimpleField("source", SearchFieldDataType.String) { IsFilterable = true },
        new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsFilterable = true },
        new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
        {
            IsSearchable = true,
            VectorSearchDimensions = 3072,
            VectorSearchProfileName = "vector-profile"
        }
    },
    VectorSearch = new VectorSearch
    {
        Profiles =
        {
            new VectorSearchProfile("vector-profile", "vector-algorithm")
        },
        Algorithms =
        {
            new HnswAlgorithmConfiguration("vector-algorithm")
        }
    }
};

await indexClient.CreateOrUpdateIndexAsync(searchIndex);
Console.WriteLine($"Index '{indexName}' created successfully.\n");

// Step 2: Read and chunk documents
Console.WriteLine("Reading documents...");
var chunks = new List<Dictionary<string, object>>();
var markdownFiles = Directory.GetFiles(dataFolder, "*.md");

foreach (var file in markdownFiles)
{
    var fileName = Path.GetFileNameWithoutExtension(file);
    var content = await File.ReadAllTextAsync(file);
    var sections = content.Split("\n## ", StringSplitOptions.RemoveEmptyEntries);

    for (int i = 0; i < sections.Length; i++)
    {
        var section = sections[i].Trim();
        if (string.IsNullOrWhiteSpace(section)) continue;

        // Re-add the ## prefix that was removed by split (except for first chunk which has #)
        if (i > 0) section = "## " + section;

        chunks.Add(new Dictionary<string, object>
        {
            ["id"] = $"{fileName}-chunk-{i}",
            ["content"] = section,
            ["title"] = fileName.Replace("-", " ").ToUpper(),
            ["source"] = Path.GetFileName(file),
            ["chunkIndex"] = i
        });
    }
}

Console.WriteLine($"Created {chunks.Count} chunks from {markdownFiles.Length} files.\n");

// Step 3: Generate embeddings
Console.WriteLine("Generating embeddings...");
var openAiClient = new AzureOpenAIClient(
    new Uri(openAiEndpoint),
    new AzureKeyCredential(openAiKey));

var embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);

foreach (var chunk in chunks)
{
    var embeddingResult = await embeddingClient.GenerateEmbeddingAsync(chunk["content"].ToString());
    var vector = embeddingResult.Value.ToFloats().ToArray();
    chunk["contentVector"] = vector;
    Console.WriteLine($"  Embedded: {chunk["id"]} ({vector.Length} dimensions)");
}

Console.WriteLine();

// Step 4: Upload to Azure AI Search
Console.WriteLine("Uploading to Azure AI Search...");
var searchClient = new SearchClient(
    new Uri(searchEndpoint),
    indexName,
    new AzureKeyCredential(searchKey));

var batch = IndexDocumentsBatch.Upload(chunks);
var uploadResult = await searchClient.IndexDocumentsAsync(batch);

Console.WriteLine($"Uploaded {uploadResult.Value.Results.Count} documents.");
Console.WriteLine($"\n=== Indexing complete! ===");