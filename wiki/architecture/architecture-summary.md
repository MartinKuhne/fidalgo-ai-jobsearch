# Fidalgo JobSearch AI — Architecture Summary

## 1. System Overview

Fidalgo JobSearch AI is a .NET 10 job-search and application-preparation suite. It ingests job postings (`Fidalgo.Ingest` via Adzuna API or Playwright scraping), evaluates them against a candidate's resume, career story, and career goals using an OpenAI-compatible local LLM (`Fidalgo.Agent`), generates job-targeted resume and cover letter PDFs (`ApplicationPacketService` + `spawnia/md-to-pdf`), and exposes a React/Vite + ASP.NET Core Minimal API web interface (`Fidalgo.Host` + `Fidalgo.Frontend`) for triaging jobs and previewing/downloading application packets.

## 2. Technology Choices

| Concern | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 LTS | Long-term support runtime; nullable enabled, warnings as errors |
| Frontend | React 19 + TypeScript + Vite (`Fidalgo.Frontend`) | Served statically from `Fidalgo.Host/wwwroot` with SPA fallback |
| Web Host | ASP.NET Core Minimal API (`Fidalgo.Host`) | Lightweight REST endpoints, RFC 7807 `ProblemDetails` via `GlobalExceptionHandler` |
| Database | SQLite (EF Core 10) | Zero-dependency, single-file store at `%APPDATA%\fidalgo\jobs.db` |
| LLM | Local OpenAI-compatible at `http://192.168.1.21:8080/v1` | `OpenAI` SDK wrapped by `ITextGenerator` (`OpenAiTextGenerator`) with `Polly` retries and timeouts |
| Web scraping | `Microsoft.Playwright` (headless Firefox) | Handles JS-rendered job boards in `BrowserFetchTool` |
| PDF generation | `spawnia/md-to-pdf` HTTP API (`weasyprint`) | OSI-licensed (`MIT` + `BSD-3-Clause`), vector/ATS-friendly PDFs via `IPdfRenderer` (`MdToPdfRenderer` + `PdfCssProvider`) |
| Logging & Tracing | Serilog (console + daily `CompactJsonFormatter` file sink) + OpenTelemetry / `ActivitySource` | 7-day log retention at `%APPDATA%\fidalgo\logs\log-.json`; W3C Trace Context propagation via `TracingHttpMessageHandler` |

## 3. Architectural Layers

```
┌────────────────────────────────────────────────────────────────────────┐
│                              Presentation                              │
│  ┌──────────────────────────────┐  ┌────────────────────────────────┐  │
│  │ Fidalgo.Host + Frontend      │  │ Fidalgo.Agent & Fidalgo.Ingest │  │
│  │ (Minimal API + React/Vite)   │  │ (Zero-flag CLI / Batch Runners)│  │
│  └──────────────┬───────────────┘  └───────────────┬────────────────┘  │
├─────────────────┼──────────────────────────────────┼───────────────────┤
│                 └──────────────────┬───────────────┘                   │
│                          Application (`Fidalgo.Shared`)                │
│  ┌──────────────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │ JobEvaluationAgent   │  │ JobSearchAgent / │  │ Application      │  │
│  │ & Skill/Fit Triage   │  │ ApiModeRunner    │  │ PacketService    │  │
│  └──────────────────────┘  └──────────────────┘  └──────────────────┘  │
│  ┌──────────────────────┐  ┌──────────────────┐                        │
│  │ IUserProfileLoader   │  │ ITextGenerator   │                        │
│  └──────────────────────┘  └──────────────────┘                        │
├────────────────────────────────────────────────────────────────────────┤
│                           Domain & Storage                             │
│  JobEntity │ JobRepository │ ApplicationPacketFiles/Status/Result      │
├────────────────────────────────────────────────────────────────────────┤
│                            Infrastructure                              │
│  ┌────────────┐  ┌──────────────┐  ┌─────────────┐  ┌───────────────┐ │
│  │ SQLite     │  │ Playwright   │  │ OpenAI HTTP │  │ md-to-pdf     │ │
│  │ (EF Core)  │  │ (Scraping)   │  │ (Local LLM) │  │ (WeasyPrint)  │ │
│  └────────────┘  └──────────────┘  └─────────────┘  └───────────────┘ │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Filesystem: %APPDATA%\.config\Fidalgo\config.json,               │  │
│  │             Documents\Fidalgo\, Documents\Fidalgo\applications\  │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

## 4. Module Responsibilities

### 4.1 `Fidalgo.Ingest` (Job Discovery)
- Runs in API mode (`ApiModeRunner` via Adzuna REST API, default) or Playwright + LLM scrape mode (`JobSearchAgent`).
- Supports zero-flag unattended execution: `--help`/`-h` is handled pre-host, bare boolean switches (`--Api`, `--Scrape`, `--QueryJobs`) are normalized by `CliArgumentNormalizer`, and `Email`, `JobSearchKeywords` (folded into `Keywords`), and `ZipCode` default from `%APPDATA%\.config\Fidalgo\config.json`.
- Missing required settings catch `OptionsValidationException` and exit `1` with actionable setup guidance.

### 4.2 `Fidalgo.Agent` (Evaluation & Batch Packet Generation)
- `AgentRunner` dispatches async CLI modes (`--QueryJobs`, `--DiscardJobId`, `--ListDiscarded`, `--TargetJobId`, `--GeneratePackets`, and default untriaged evaluation).
- `JobEvaluationAgent` evaluates untriaged jobs against `resume.md`, optional `career-story.md`, and optional `career-goals.md`, persisting `Pros`, `Cons`, `ResumeHints`, `Score`, and `Recommendation`.
- Batch/single-job packet generation (`--GeneratePackets`, `--TargetJobId <guid>`, `--ForceRegenerate`, `--SkipExisting`, `--OutputDir`, `--RecommendationFilter`) calls `IApplicationPacketService` for matching non-deleted jobs (default `"Apply"`). When `ApplicationPacket:AutoGenerateForApply` is `true`, default evaluation mode chains automatically into packet generation.

### 4.3 `Fidalgo.Shared` User Profile & Application Packet Pipeline
- **Storage separation (`UserProfilePaths`, `UserProfileConfig`, `UserProfileResolver`, `IUserProfileLoader`):**
  - Configuration: `%APPDATA%\.config\Fidalgo\config.json` (`Email`, `ZipCode`, `JobSearchKeywords`, `ApplicationPacket`).
  - Application state & logs: `%APPDATA%\fidalgo\jobs.db` and `%APPDATA%\fidalgo\logs\log-.json` (daily rolling `CompactJsonFormatter`, 7-day retention).
  - User profile documents: `Documents\Fidalgo\resume.md`, `career-story.md`, `career-goals.md` via `GetDefaultDocumentsDir()`, with backward-compatible fallback to `%APPDATA%\.config\Fidalgo\` (logged at `Information`) when `ProfileDirectory` override is not set.
- **LLM Abstraction (`ITextGenerator` / `OpenAiTextGenerator`):**
  - Single-turn completion abstraction over `OpenAI.Chat.ChatClient` with linked `CancellationTokenSource` timeout (`LlmTimeoutSeconds`), `Polly` exponential-backoff retries, empty-content/refusal `AgentExecutionException` checks, and `Activity` child spans.
- **PDF Rendering (`IPdfRenderer` / `MdToPdfRenderer` + `PdfCssProvider`):**
  - Sends `POST` form-urlencoded (`markdown`, static server-owned `css` for `Resume` or `CoverLetter`, `engine=weasyprint`) to `ApplicationPacket:MdToPdfEndpoint` (default `http://localhost:8000`) via `HttpClient` + `TracingHttpMessageHandler`, with `Polly` retries, `PdfTimeoutSeconds`, and `%PDF` magic-header validation.
- **Packet Orchestration & Store (`ApplicationPacketService`, `FileSystemApplicationPacketStore`, `ApplicationPacketPaths`):**
  - Deterministic, path-traversal-safe folder `<sanitized-employer>_<sanitized-title>_<short-id>` under `Documents\Fidalgo\applications\` (or `--OutputDir` / `ApplicationPacket:OutputDirectory`).
  - Two grounded LLM calls (`ResumeTailoringPromptBuilder`, `CoverLetterPromptBuilder`) forbid fabricating unverified experience.
  - Atomically writes `resume.pdf`, `cover-letter.pdf`, `job-posting.md` (via `JobPostingSnapshotBuilder`), `resume.md`, `cover-letter.md`, and `packet.json` using `.tmp-<guid>` staging files.
  - Idempotent: skips generation when `resume.pdf`, `cover-letter.pdf`, and `job-posting.md` already exist and are non-empty unless `effectiveForce = ForceRegenerate || !SkipExisting`.

### 4.4 `Fidalgo.Host` & `Fidalgo.Frontend` (Web UI & REST API)
- `Fidalgo.Host` loads `config.json` via `AddUserProfileConfig()` (disableable via `UseUserProfileConfig=false` or `FIDALGO_DISABLE_USER_PROFILE_CONFIG=true` for tests) and configures `GlobalExceptionHandler` (`IExceptionHandler` + `ProblemDetails`) preserving `BadHttpRequestException` `400`s while mapping `ValidationException` (`400`), `NotFoundException` (`404`), `UnauthorizedException` (`401`), and `AgentExecutionException` (`502`).
- Endpoints:
  - `GET /api/jobs`, `GET /api/jobs/{id}`, `DELETE /api/jobs/{id}`, `GET /api/tenants`
  - `GET /api/profile/defaults` — exposes server-loaded `resume.md`, `career-story.md`, `career-goals.md` availability/content and `OutputDirectory`
  - `GET /api/jobs/{id}/packet` — returns packet status, saved directory path, and file URLs
  - `POST /api/jobs/{id}/packet` — generates/returns application packet using server defaults + optional overrides (`force` flag supported)
  - `GET /api/jobs/{id}/packet/files/{fileName}` — streams `resume.pdf`, `cover-letter.pdf`, `job-posting.md`, `resume.md`, or `cover-letter.md` (strict 5-file allowlist, inline preview or `?download=true`)
  - `POST /api/jobs/{id}/resume` — legacy endpoint refactored onto `ITextGenerator` + `IUserProfileLoader`
- `Fidalgo.Frontend` (`App.tsx`, `ApplicationPacketModal.tsx`, `src/api/packets.ts`) displays server profile readiness badges, optional overrides, `Force regenerate` toggle, `ProblemDetails` error banners, and tabbed inline previews + downloads for `resume.pdf`, `cover-letter.pdf`, and `job-posting.md`.

## 5. Data Flow

```
[Adzuna / Job Boards] ──▶ [Fidalgo.Ingest] ──▶ [SQLite: jobs.db]
                                                       │
[Documents\Fidalgo\ (resume, story, goals)] ───────────┤
                                                       ▼
                                           [Fidalgo.Agent: Evaluate]
                                                       │
                          ┌────────────────────────────┴───────────────────────────┐
                          ▼                                                        ▼
          [Fidalgo.Agent --GeneratePackets]                        [Fidalgo.Host + Frontend]
                          │                                                        │
                          └────────────────────▶ [ApplicationPacketService] ◀──────┘
                                                       │
                                  ┌────────────────────┼────────────────────┐
                                  ▼                    ▼                    ▼
                          [OpenAiTextGenerator] [MdToPdfRenderer]  [FileSystemStore]
                           (Local LLM HTTP)    (md-to-pdf HTTP)    (Documents\Fidalgo\
                                                                    applications\<job>\)
```

## 6. Key Design Decisions

### 6.1 `spawnia/md-to-pdf` Container HTTP API over QuestPDF and Local Chromium
- **Decision:** Implement `IPdfRenderer` via `MdToPdfRenderer` calling `spawnia/md-to-pdf` (`engine=weasyprint`) over `HttpClient`.
- **Rationale:** QuestPDF's Community License restricts commercial use above $1M revenue (non-OSI, disallowed by `AGENTS.md`). Client-side `html2pdf.js` produces rasterized non-ATS images and cannot run in CLI batch mode. `spawnia/md-to-pdf` (MIT) + `WeasyPrint` (BSD-3-Clause) are OSI-licensed, produce genuine vector/CSS-paged PDFs, require zero new NuGet packages or local browser installs in .NET hosts, and stay swappable behind `IPdfRenderer`.
- **Reversibility:** High — `IPdfRenderer` isolates all callers from the concrete PDF engine.

### 6.2 Separation of Config, App State, and User Documents
- **Decision:** Keep `config.json` in `%APPDATA%\.config\Fidalgo\`, `jobs.db` and `logs\` in `%APPDATA%\fidalgo\`, and user-facing documents (`resume.md`, `career-story.md`, `career-goals.md`, `applications\`) in `Documents\Fidalgo\`, with secondary fallback from `Documents\Fidalgo\` to `.config\Fidalgo\` when `ProfileDirectory` is not explicitly overridden.
- **Rationale:** Resumes and generated PDFs are user documents meant to be easily opened/reviewed in Explorer/Finder, whereas `config.json` is tool configuration and SQLite/JSON logs are application state.

### 6.3 Deterministic Filesystem Packet Addressing
- **Decision:** Name per-job packet folders `<sanitized-employer>_<sanitized-title>_<short-id>` keyed by the first 8 hex chars of `JobEntity.InternalId`.
- **Rationale:** Avoids title/employer collisions, enables atomic `.tmp-<guid>` staging and zero-DB-migration idempotency, and guards against Windows invalid characters, reserved device names (`CON`, `NUL`, etc.), and path traversal.

### 6.4 Continuous 4-Hour Pipeline & Docker Compose Hosting
- **Decision:** Extract reusable `AdzunaJobIngestionService`, `LlmJobEvaluationService`, `CloseFitSelector`, and `JobPipelineRunner` into `Fidalgo.Shared.Pipeline`, hosted inside `Fidalgo.Host` via `JobPipelineBackgroundService` (`PeriodicTimer` defaulting to `4` hours) alongside `GET /api/pipeline/status`, and enabled by default in `docker-compose.yml` (`Pipeline__Enabled=true`, `Pipeline__MinScore=70`).
- **Rationale:** Running ingestion → evaluation → close-fit packet generation sequentially in a single `BackgroundService` eliminates timing gaps between separate timers while keeping `Fidalgo.Ingest` and `Fidalgo.Agent` CLIs available for manual batch runs. Container mounts (`/data/config`, `/data/profile`, `/data/applications`, `/data/db`, `/data/logs`) and `.env` secrets keep all runtime artifacts off container layers.

## 7. Security Considerations

- **Path Traversal & File Allowlist:** `ApplicationPacketPaths.ResolveJobDirectory` enforces `Path.TrimEndingDirectorySeparator` root containment; `GET /api/jobs/{id}/packet/files/{fileName}` enforces a strict 5-file allowlist.
- **Renderer Isolation:** `MdToPdfRenderer` sends only static server-owned CSS (`PdfCssProvider`) to a server-configured `MdToPdfEndpoint`—never user-supplied CSS or endpoint URLs.
- **Prompt & PII Hygiene:** Prompts delimit `<job_posting>` as untrusted data and forbid experience fabrication. Logs never emit full resume/story/cover-letter content or `LLM:ApiKey` values.
- **Error Sanitization:** `GlobalExceptionHandler` returns RFC 7807 `ProblemDetails` with opaque trace IDs and never leaks stack traces to HTTP clients.
- **Web Auth Posture:** `Fidalgo.Host` remains unauthenticated for single-user local usage; in shared/networked deployments, add reverse-proxy or cookie/API-key authentication.
