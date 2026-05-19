# Research: AI-Powered Job Scraper Agent

## Decision: Use Microsoft Agent Framework for agent orchestration

**Rationale**: The specification requires using the Microsoft Agent Framework (REQ-002). The framework provides `AIAgent` with built-in tool calling via `AIFunctionFactory.Create()`, prompt template support, and streaming response handling. It replaces the earlier BackgroundService approach with a proper AI agent pattern.

**Alternatives considered**:
- Semantic Kernel (predecessor): Would work but is superseded by Agent Framework. Agent Framework is the direct successor combining Semantic Kernel + AutoGen.
- Custom agent loop (manual LLM calls + tool dispatch): More control but reinvents the framework's built-in capabilities.
- BackgroundService (001 approach): No AI analysis capability; just mechanical scraping.

## Decision: Use OpenAIClient (Microsoft.Extensions.AI.OpenAI) for custom endpoint

**Rationale**: The local LLM at http://192.168.1.21:8080/v1 is an OpenAI-compatible endpoint. `Microsoft.Extensions.AI.OpenAI` provides `OpenAIClient` that works with any OpenAI-compatible API, not just Azure. The client can be configured with a custom base URL, API key (placeholder required but endpoint may not enforce), and model name.

**Configuration example**:
```csharp
var client = new OpenAIClient(new Uri("http://192.168.1.21:8080/v1"), new ApiKeyCredential("u-mkuhne"));
var chatClient = client.GetChatClient("local-model").AsIChatClient();
```

**Alternatives considered**:
- Azure OpenAI SDK: Requires Azure endpoints and credential flow; incompatible with local non-Azure endpoints.
- Direct HTTP calls to the endpoint: Fully manual; loses framework integration benefits.

## Decision: Use AngleSharp for HTML sanitization (already in project)

**Rationale**: AngleSharp is already a project dependency from the 001 implementation. It can parse HTML into a DOM, strip unwanted rendering elements (fonts, font styles, scripts, styles), and return clean markdown-formatted text. The fetch tool will use AngleSharp to sanitize fetched content before returning it to the agent.

**HTML elements to strip**: `<font>`, `<style>`, `<link>`, `<script>`, inline `style` attributes, SVG, font-family/style declarations.

**Alternatives considered**:
- HtmlAgilityPack: Similar capability but AngleSharp is already in the project.
- Regex-based stripping: Fragile and error-prone for complex HTML.

## Decision: Continue with EF Core + SQLite for job storage (from 001)

**Rationale**: Entity Framework Core with SQLite provider is already implemented in the 001 codebase. It provides type-safe queries, migrations, and LINQ support. The new data model requires additional fields (score, pros, cons, resume_hints, recommendation) which are easily added as new columns.

**Alternatives considered**:
- Raw ADO.NET: Less abstraction but also less setup; rejected because EF Core is already in the project and provides better testability.
- Dapper: Lighter but requires manual SQL for the new query features (get_jobs filtering by date/employer/ID).

## Decision: CLI invocation model (not BackgroundService)

**Rationale**: Unlike the 001 approach which ran as a long-lived BackgroundService, the 002 specification calls for a command-line program (REQ-001) that runs one search cycle per invocation. The program parses CLI arguments, creates the agent with tools, runs it synchronously, and exits.

**CLI arguments**: `--email <email> --keywords <keywords> --resume <file> [--narrative <file>]`

**Alternatives considered**:
- BackgroundService from 001: Not suitable for one-shot CLI usage.
- Interactive REPL: Overkill for the current requirements.

## Decision: Tools registered via AIFunctionFactory.Create

**Rationale**: The Microsoft Agent Framework uses `AIFunctionFactory.Create()` to wrap C# methods as agent-callable tools. Each tool (fetch, save_job, get_jobs) will be a static method with `[Description]` and `[ParameterDescription]` attributes so the agent understands when to call them.

| Tool | Method | Description |
|------|--------|-------------|
| fetch | `FetchWebsite(string url)` | Fetches web page content, strips HTML rendering elements |
| save_job | `SaveJob(JobData job)` | Stores a job record with analysis results |
| get_jobs | `GetJobs(JobQuery query)` | Retrieves stored jobs by date, employer, job ID, website |

**Alternatives considered**:
- MCP server integration: More complex setup; direct tools are simpler for a CLI tool.
- Manual function calling: Would require implementing the function dispatch loop ourselves.

## Decision: Prompt template with {{email}} and {{query}} substitution

**Rationale**: The agent instructions contain template variables `{{email}}` and `{{query}}` that must be substituted before being sent to the LLM. A simple string replacement at startup is the most straightforward approach. The full agent instructions from the spec are embedded as a constant string with placeholders.

**Alternatives considered**:
- Jinja2-style template engine: Overkill for just two substitutions.
- LLM-based template filling: Would add unnecessary latency and cost.