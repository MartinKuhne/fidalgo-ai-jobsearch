# Implementation Plan: Job Scraper Agent

**Branch**: `001-job-scraper-agent` | **Date**: 2026-05-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-job-scraper-agent/spec.md`

## Summary

This feature implements the initial AI agent for automated job searching. The agent reads a user configuration specifying which websites to monitor and what keywords to search for. It periodically scrapes those sites for job postings, extracts relevant details, stores them in a local SQLite database with deduplication, and logs all activity. The initial scope focuses on the core scraping and storage pipeline without advanced AI analysis.

## Technical Context

**Language/Version**: C# with .NET 10.0 LTS

**Primary Dependencies**: Microsoft Agent Framework (Microsoft.Agents), SQLite (Microsoft.Data.Sqlite), HttpClient for web scraping, AngleSharp for HTML parsing, NSubstitute for mocking in tests

**Storage**: SQLite local database via Entity Framework Core

**Testing**: xUnit with NSubstitute for mocking, integration tests for scraping contracts

**Target Platform**: Windows (primary), Linux container (secondary)

**Project Type**: CLI background service / containerized agent

**Performance Goals**: Complete a full search cycle across all configured websites within 30 minutes; process at least 100 job pages per cycle

**Constraints**: Respect website rate limits and robots.txt; handle anti-bot measures gracefully; no authentication required for scraping; offline-capable database storage

**Scale/Scope**: Initial scope covers 6 job search sites (governmentjobs.com, google, glassdoor.com, monster.com, indeed.com, linkedin.com) with 2 default keywords; supports multiple users keyed by email

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The constitution file (`constitution.md`) is a template with placeholder content. No active governance rules to evaluate at this time. All design decisions will follow the project-level constraints defined in `spec/SPEC.md`:

- Uses Microsoft Agent Framework (REQ-002)
- Uses .NET LTS (REQ-003)
- Uses SQLite for job storage (REQ-007)
- Uses local OpenAI-compatible LLM at http://192.168.1.21:8080/v1 (REQ-100)

**Gates**: PASS — No violations identified. Design aligns with project constitution.

## Project Structure

### Documentation (this feature)

```text
specs/001-job-scraper-agent/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Fidalgo.Agent/
│   ├── Agents/
│   │   └── JobSearchAgent.cs
│   ├── Configuration/
│   │   └── SearchConfig.cs
│   ├── Scraping/
│   │   ├── IWebsiteScraper.cs
│   │   ├── WebsiteScraperRegistry.cs
│   │   └── Scrapers/
│   │       ├── GovernmentJobsScraper.cs
│   │       ├── GoogleScraper.cs
│   │       ├── GlassdoorScraper.cs
│   │       ├── MonsterScraper.cs
│   │       ├── IndeedScraper.cs
│   │       └── LinkedInScraper.cs
│   ├── Storage/
│   │   ├── JobDbContext.cs
│   │   ├── JobRepository.cs
│   │   └── DuplicateDetector.cs
│   └── Fidalgo.Agent.csproj
├── Fidalgo.Agent.Tests/
│   ├── Agents/
│   ├── Scraping/
│   ├── Storage/
│   └── Fidalgo.Agent.Tests.csproj
tests/
├── contract/
│   └── scraper-contracts/
└── integration/
    └── scraping-integration/

```

**Structure Decision**: Single library project (`Fidalgo.Agent`) containing the agent, scrapers, and storage. Tests in a separate test project. This aligns with the project's Library-First principle and keeps the agent self-contained and independently testable. The strategy pattern for scrapers (`IWebsiteScraper`) allows each website to implement its own parsing logic without affecting the core pipeline.

## Complexity Tracking

> No complexity violations identified. The design uses straightforward patterns (repository, strategy for scrapers) that are justified by the need to support multiple websites with different HTML structures.
