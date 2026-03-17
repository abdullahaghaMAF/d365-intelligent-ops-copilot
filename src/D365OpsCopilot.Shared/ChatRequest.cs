namespace D365OpsCopilot.Shared.Models;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public List<string> ToolsUsed { get; set; } = new();
}