# Implementation Plan: AI-Powered Job Scraper Agent

**Branch**: `002-job-scraper-agent` | **Date**: 2026-05-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-job-scraper-agent/spec.md`

## Summary

Build a command-line job search agent using the Microsoft Agent Framework and a local OpenAI-compatible LLM. The agent fetches Indeed.com listings for user-specified keywords, analyzes each posting against the user's resume and optional career narrative, and persists results with match scores, pros/cons, and apply recommendations in a local database. Multiple users are supported, keyed by email address.

## Technical Context

- **Language/Version**: C# with NET 10.0 LTS
- **Primary Dependencies**: Microsoft Agent Framework (Microsoft.Agents), HTML sanitizer (AngleSharp), SQLite provider, NSubstitute (testing), Polly (resilience)
- **Storage**: Local database via data access layer
- **Testing**: xUnit with NSubstitute for mocking, integration tests for tool contracts
- **Target Platform**: Windows (primary), Linux (secondary)
- **Project Type**: CLI console application
- **Performance Goals**: Complete full search cycle (fetch, analyze, store) across up to 100 job pages in under 10 minutes
- **Constraints**: Local LLM endpoint at http //192.168.1.21:8080/v1 must be reachable; all text fields in markdown format; HTML rendering elements stripped from fetched content
- **Scale/Scope**: Single Indeed.com job site initial support; multiple users keyed by email; runs as a one-shot CLI invocation per user

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The constitution file (` constitution.md`) is a template with placeholder content. No active governance rules to evaluate at this time. The project-level constraints from the spec apply:

- CLI console program (REQ-001)
- Microsoft Agent Framework (REQ-002)
- .NET LTS framework (REQ-003)
- Local OpenAI-compatible LLM (REQ-100)

**Gates**: PASS — No violations identified.

## Project Structure

### Documentation (this feature)

```text
specs/002-job-scraper-agent/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── fetch-tool.md
│   ├── save-job-tool.md
│   └── get-jobs-tool.md
└── tasks.md             # Phase 2 output (not created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Fidalgo.Agent/
│   ├── Agents/
│   │   └── JobSearchAgent.cs      # Main agent using Microsoft Agent Framework
│   ├── Tools/
│   │   ├── FetchTool.cs           # Web fetch + HTML sanitization
│   │   ├── SaveJobTool.cs         # Persist job record
│   │   └── GetJobsTool.cs         # Query stored jobs
│   ├── Configuration/
│   │   └── CliOptions.cs          # Command-line argument parsing
│   ├── Sanitization/
│   │   └── HtmlSanitizer.cs       # Strip rendering-only HTML elements
│   ├── Storage/
│   │   ├── JobDbContext.cs        # EF Core DbContext
│   │   ├── JobRepository.cs       # Data access for jobs
│   │   └── JobEntity.cs           # Job entity
│   ├── Models/
│   Prompts/
│   │   └── AgentPrompt.cs         # Agent instruction template with {email} and {query}
│   ├── Program.cs                 # CLI entry point
│   ├── appsettings.json           # LLM endpoint etc.
│   └── Fidalgo.Agent.csproj
├── Fidalgo.Agent.Tests/
│   ├── Tools/
│   │   ├── FetchToolTests.cs
│   │   ├── SaveJobToolTests.cs
│   │   └──   └── GetJobsToolTests.cs
│   ├── Integration/
│   │   └── EndToEndTests.cs
│   └── Fidalgo.Agent.Tests.csproj
Fidalgo.slnx
```

**Structure Decision**: Single CLI project (`Fidalgo.Agent`) with a separate test project. Tools implement the Microsoft Agent Framework's `AgentTool` pattern. The strategy keeps the agent self-contained, independently testable, and aligned with existing project organization.

## Complexity Tracking

> No complexity violations identified.

</parameter>
<｜DSML｜parameter name