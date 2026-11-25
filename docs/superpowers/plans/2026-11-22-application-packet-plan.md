# Fidalgo Application Packet Extension Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend Fidalgo so user profile documents (`resume.md`, `career-story.md`, `career-goals.md`) and generated per-job application packets (`resume.pdf`, `cover-letter.pdf`, `job-posting.md`) live in `Documents\Fidalgo\` with `config.json` in `%APPDATA%\.config\Fidalgo\`, enabling zero-flag batch/service execution and dual-PDF generation across `Fidalgo.Agent`, `Fidalgo.Ingest`, `Fidalgo.Host`, and `Fidalgo.Frontend`.

**Architecture:** Extract shared abstractions (`ITextGenerator`, `IPdfRenderer`, `IUserProfileLoader`, `IApplicationPacketStore`, `IApplicationPacketService`) and pure helpers (`ApplicationPacketPaths`, `JobPostingSnapshotBuilder`, `JobUrlFormatter`, prompt builders, `CliArgumentNormalizer`) into `Fidalgo.Shared`. Wire them into an injectable `AgentRunner` (batch/CLI), zero-flag `Fidalgo.Ingest`, Minimal API packet endpoints + `GlobalExceptionHandler` in `Fidalgo.Host`, and a refactored React packet modal in `Fidalgo.Frontend`.

**Tech Stack:** .NET 10 (`TreatWarningsAsErrors=true`, nullable enabled), `spawnia/md-to-pdf` HTTP API (`weasyprint`, MIT/BSD-3-Clause, zero new NuGet packages), Polly, Serilog, OpenAI SDK, xUnit + NSubstitute, React 19 + TypeScript + Vite + Vitest.

**Spec:** [docs/superpowers/specs/2026-11-22-application-packet-design.md](../specs/2026-11-22-application-packet-design.md)

## Global Constraints

- Target .NET 10 LTS with `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (`src/Directory.Build.props`); build command `dotnet build src/Fidalgo.slnx` must produce 0 warnings.
- One class, struct, record, or interface per file; `Async` suffix and `CancellationToken` propagation on all async methods.
- Use `NSubstitute` for mocking in `.NET` tests; follow `coverlet.runsettings` exclusions by placing DTOs/records in `Models/` and options classes in `Configuration/`.
- Use only OSI-approved open-source libraries (`spawnia/md-to-pdf` MIT + `WeasyPrint` BSD-3-Clause over standard `HttpClient`; zero new NuGet packages; no QuestPDF).
- Keep configuration (`%APPDATA%\.config\Fidalgo\config.json`), app state/logs (`%APPDATA%\fidalgo\`), and user documents/packets (`Documents\Fidalgo\` and `Documents\Fidalgo\applications\`) cleanly separated, with legacy fallback from `UserProfileResolver` when no explicit override is provided.
- No live LLM calls or external HTTP/browser launches in hermetic unit/API tests.

## Review Focus

- Passing bare boolean CLI flags (`--GeneratePackets --OutputDir ...`) to `AddCommandLine` throws `FormatException` unless `CliArgumentNormalizer` rewrites them before configuration binding; verify with `CliArgumentNormalizerTests` and `AgentRunnerTests`.
- `OptionsValidationException` thrown by `.ValidateOnStart()` when `args.Length == 0` and `config.json` lacks `Email`/`ZipCode`/`Keywords` must be caught in `Program.cs` and exit `1` cleanly instead of crashing; verify with `IngestStartupTests` and `AgentRunnerTests`.
- `GlobalExceptionHandler` in `Fidalgo.Host` must preserve `BadHttpRequestException.StatusCode` (`400`) when Minimal API query/body binding fails (`ProgramTests.GetJobs_WithMissingEmail_ReturnsBadRequest`) while mapping `NotFoundException` (`404`), `ValidationException` (`400`), `UnauthorizedException` (`401`), and `AgentExecutionException` (`502` for LLM or `md-to-pdf` failures); verify in `PacketEndpointsTests`.
- `JobRepository.GetByIdAsync` does not filter `IsDeleted == true`; both `AgentRunner --TargetJobId` and `ApplicationPacketService` / `Fidalgo.Host` endpoints must explicitly treat `job is null || job.IsDeleted` as not found (`404` / exit `1`).
- `MdToPdfRenderer` must send static server-owned CSS (`PdfCssProvider`), propagate W3C Trace Context via `TracingHttpMessageHandler`, apply `PdfTimeoutSeconds` + `Polly` retries on transient failures, and validate the `%PDF` magic header; verify with `MdToPdfRendererTests` using `HttpMessageHandlerStub`.

---

### Task 1: Shared Core Abstractions, Profile Paths, Prompt Builders, PDF & Packet Store (`Fidalgo.Shared` + `Fidalgo.Shared.Tests`)

**Files:**
- Modify: `src/Fidalgo.Shared/UserProfile/UserProfilePaths.cs`, `src/Fidalgo.Shared/UserProfile/UserProfileResolver.cs`, `src/Fidalgo.Shared/UserProfile/UserProfileConfig.cs`
- Create: `src/Fidalgo.Shared/Configuration/LlmConfiguration.cs`, `src/Fidalgo.Shared/Configuration/ApplicationPacketOptions.cs`, `src/Fidalgo.Shared/Configuration/CliArgumentNormalizer.cs`
- Create: `src/Fidalgo.Shared/Models/TextGenerationOptions.cs`, `UserProfileLoadOverrides.cs`, `ProfileSourceMetadata.cs`, `UserProfileContent.cs`, `PdfDocumentKind.cs`, `PdfRenderOptions.cs`, `PacketGenerationOutcome.cs`, `ApplicationPacketFiles.cs`, `ApplicationPacketStatus.cs`, `ApplicationPacketWriteRequest.cs`, `ApplicationPacketGenerationRequest.cs`, `ApplicationPacketResult.cs`
- Create: `src/Fidalgo.Shared/Llm/ITextGenerator.cs`, `OpenAiTextGenerator.cs`, `LlmServiceCollectionExtensions.cs`
- Create: `src/Fidalgo.Shared/UserProfile/IUserProfileLoader.cs`, `UserProfileLoader.cs`
- Create: `src/Fidalgo.Shared/Pdf/IPdfRenderer.cs`, `PdfCssProvider.cs`, `MdToPdfRenderer.cs`
- Create: `src/Fidalgo.Shared/ApplicationPackets/ApplicationPacketPaths.cs`, `JobUrlFormatter.cs`, `JobPostingSnapshotBuilder.cs`, `IApplicationPacketStore.cs`, `FileSystemApplicationPacketStore.cs`, `IApplicationPacketService.cs`, `ApplicationPacketService.cs`, `ApplicationPacketServiceCollectionExtensions.cs`
- Create: `src/Fidalgo.Shared/ApplicationPackets/Prompts/ResumeTailoringPromptBuilder.cs`, `CoverLetterPromptBuilder.cs`, `MarkdownResponseNormalizer.cs`
- Test: `src/Fidalgo.Shared.Tests/UserProfile/UserProfilePathsTests.cs`, `UserProfileResolverTests.cs`, `UserProfileLoaderTests.cs`, `ApplicationPackets/ApplicationPacketPathsTests.cs`, `ApplicationPackets/PromptBuildersTests.cs`, `ApplicationPackets/FileSystemApplicationPacketStoreTests.cs`, `ApplicationPackets/ApplicationPacketServiceTests.cs`, `Configuration/CliArgumentNormalizerTests.cs`, `Llm/OpenAiTextGeneratorTests.cs`, `Pdf/MdToPdfRendererTests.cs`

**Interfaces:**
- Consumes: `JobEntity`, `JobRepository`, `ValidationException`, `NotFoundException`, `AgentExecutionException`, `ITraceContextProvider`, `TracingHttpMessageHandler`.
- Produces: Exact public signatures specified in Spec Section 4.1 (`ITextGenerator`, `IUserProfileLoader`, `IPdfRenderer`, `IApplicationPacketStore`, `IApplicationPacketService`, `CliArgumentNormalizer.NormalizeBooleanFlags`, `JobUrlFormatter.FormatUrl`, `LlmServiceCollectionExtensions.AddFidalgoLlmClient`, `ApplicationPacketServiceCollectionExtensions.AddApplicationPacketServices`).

- [ ] **Step 1: Extend `UserProfilePaths` + `UserProfileResolver` (no new NuGet packages needed)**
  - Add `GetDefaultDocumentsDir()` (`Documents\Fidalgo\`), `GetDefaultApplicationsDir()` (`Documents\Fidalgo\applications\`), and secondary fallback in `UserProfileResolver` from `Documents\Fidalgo\` to `%APPDATA%\.config\Fidalgo\` when `baseDir` is null.
- [ ] **Step 2: Write unit tests for `UserProfilePathsTests`, `UserProfileResolverTests` (legacy fallback), `UserProfileLoaderTests`, and `CliArgumentNormalizerTests`**
  - Assert `Documents\Fidalgo\` primary resolution, legacy fallback logging, test-isolated `baseDir` skipping legacy fallback, `ValidationException("baseResume")` when missing, and bare `--Flag` rewrite without corrupting `--Flag true/false`.
- [ ] **Step 3: Implement `Models/*.cs`, `Configuration/*.cs` (including `MdToPdfEndpoint`, `MdToPdfEngine`, `PdfTimeoutSeconds`), `UserProfileLoader`, and `CliArgumentNormalizer`**
- [ ] **Step 4: Write unit tests for `ApplicationPacketPathsTests`, `PromptBuildersTests`, `FileSystemApplicationPacketStoreTests`, `OpenAiTextGeneratorTests`, `MdToPdfRendererTests`, and `ApplicationPacketServiceTests`**
  - Cover invalid Windows path chars, reserved device names (`CON`, `NUL`), drive-root containment, nullable `JobEntity.Title`/`EmployerJobId`, prompt sections + anti-fabrication guardrails, atomic temp-staging saves, allowlist enforcement, `MdToPdfRenderer` form fields (`markdown`, `css`, `engine`) + `%PDF-` validation + Polly retry on `503` + timeout/`500` `AgentExecutionException` via `HttpMessageHandlerStub`, LLM Polly retries/empty-content error, idempotent skip vs `ForceRegenerate`, and `IsDeleted` check.
- [ ] **Step 5: Implement `Llm/*`, `Pdf/*` (`PdfCssProvider`, `MdToPdfRenderer` wired with `TracingHttpMessageHandler`), and `ApplicationPackets/*` services and DI extensions**
- [ ] **Step 6: Verify build & tests**
  - Run: `dotnet test src/Fidalgo.Shared.Tests/Fidalgo.Shared.Tests.csproj --filter "FullyQualifiedName!~BrowserFetchToolTests"`
  - Expected: All hermetic `Fidalgo.Shared.Tests` pass with 0 warnings.
- [ ] **Step 7: Commit `Fidalgo.Shared` and `Fidalgo.Shared.Tests`**

---

### Task 2: Frontend Application Packet Modal, API Client & PDF Preview (`Fidalgo.Frontend`)

**Files:**
- Modify: `src/Fidalgo.Frontend/package.json`, `src/Fidalgo.Frontend/package-lock.json` (remove `html2pdf.js`)
- Create: `src/Fidalgo.Frontend/src/api/packets.ts`
- Create: `src/Fidalgo.Frontend/src/components/ApplicationPacketModal.tsx`
- Modify: `src/Fidalgo.Frontend/src/App.tsx`, `src/Fidalgo.Frontend/src/App.test.tsx`, `src/Fidalgo.Frontend/src/index.css`

**Interfaces:**
- Consumes: Host REST API contracts from Spec Section 4.4 (`GET /api/profile/defaults`, `GET /api/jobs/{id}/packet`, `POST /api/jobs/{id}/packet`, `GET /api/jobs/{id}/packet/files/{fileName}`).
- Produces: Static SPA bundle compiled into `src/Fidalgo.Host/wwwroot` via `npm run build`.

- [ ] **Step 1: Install frontend dependencies (`npm ci` in `src/Fidalgo.Frontend`) and uninstall `html2pdf.js`**
- [ ] **Step 2: Write/update Vitest component tests in `src/Fidalgo.Frontend/src/App.test.tsx`**
  - Verify modal loads `/api/profile/defaults` and `/api/jobs/{id}/packet`, displays server readiness badges, generates packet without requiring manual resume input, renders folder path, switches preview tabs (`resume.pdf`, `cover-letter.pdf`, `job-posting.md`), and renders download links (`?download=true`).
- [ ] **Step 3: Implement `src/api/packets.ts`, `src/components/ApplicationPacketModal.tsx`, and update `src/App.tsx`**
- [ ] **Step 4: Run `npm run lint && npm run build && npm test` inside `src/Fidalgo.Frontend`**
  - Expected: 0 lint errors, clean TypeScript + Vite build into `src/Fidalgo.Host/wwwroot`, all Vitest tests pass.
- [ ] **Step 5: Commit `src/Fidalgo.Frontend`**

---

### Task 3: Batch CLI Runner & Zero-Flag Startup (`Fidalgo.Agent`, `Fidalgo.Ingest`, `Fidalgo.Agent.Tests`, `Fidalgo.Ingest.Tests`)

**Files:**
- Modify: `src/Fidalgo.Agent/Configuration/CliOptions.cs`, `src/Fidalgo.Agent/Program.cs`, `src/Fidalgo.Agent/DependencyInjection/ServiceCollectionExtensions.cs`
- Delete: `src/Fidalgo.Agent/Configuration/LlmConfiguration.cs`, `src/Fidalgo.Ingest/Configuration/LlmConfiguration.cs` (use `Fidalgo.Shared.Configuration.LlmConfiguration`)
- Create: `src/Fidalgo.Agent/AgentRunner.cs`
- Modify: `src/Fidalgo.Ingest/Program.cs`, `src/Fidalgo.Ingest/DependencyInjection/ServiceCollectionExtensions.cs`
- Create: `src/Fidalgo.Agent.Tests/AgentRunnerTests.cs`, `src/Fidalgo.Ingest.Tests/IngestStartupTests.cs`

**Interfaces:**
- Consumes: Task 1 (`IApplicationPacketService`, `CliArgumentNormalizer`, `ApplicationPacketOptions`, `AddFidalgoLlmClient`, `AddApplicationPacketServices`).
- Produces: `AgentRunner.RunAsync(CliOptions, CancellationToken)`, zero-arg `Program.cs` bootstrap in both CLIs, rolling Serilog file sink (`%APPDATA%\fidalgo\logs\log-.json`).

- [ ] **Step 1: Write unit tests in `AgentRunnerTests.cs` and `IngestStartupTests.cs`**
  - Test `--GeneratePackets` filtering `Recommendation == "Apply"`, skipping soft-deleted jobs, honoring `ForceRegenerate` / `SkipExisting`, per-job error isolation, `--TargetJobId` missing/soft-deleted exit `1`, zero-arg execution from `config.json`, and `OptionsValidationException` exit `1`.
- [ ] **Step 2: Implement `AgentRunner.cs`, update `CliOptions.cs`, `Program.cs`, and DI in `Fidalgo.Agent` & `Fidalgo.Ingest`, and remove duplicate `LlmConfiguration` files**
- [ ] **Step 3: Run `dotnet test src/Fidalgo.Agent.Tests/Fidalgo.Agent.Tests.csproj` and `dotnet test src/Fidalgo.Ingest.Tests/Fidalgo.Ingest.Tests.csproj`**
  - Expected: All tests pass with 0 warnings.
- [ ] **Step 4: Commit `Fidalgo.Agent`, `Fidalgo.Ingest`, and their test projects**

---

### Task 4: Web Host Endpoints, Server Defaults & Global Exception Handler (`Fidalgo.Host`, `Fidalgo.Host.Tests`)

**Files:**
- Create: `src/Fidalgo.Host/ErrorHandling/GlobalExceptionHandler.cs`
- Create: `src/Fidalgo.Host/Models/ProfileDefaultsDto.cs`, `PacketFilesDto.cs`, `ApplicationPacketStatusDto.cs`, `GeneratePacketRequest.cs`, `ApplicationPacketResultDto.cs`, `ResumeRequest.cs`
- Modify: `src/Fidalgo.Host/Program.cs`, `src/Fidalgo.Host/Services/JobsService.cs`
- Create: `src/Fidalgo.Host.Tests/PacketEndpointsTests.cs`
- Modify: `src/Fidalgo.Host.Tests/ProgramTests.cs`, `src/Fidalgo.Host.Tests/JobsServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 (`AddFidalgoLlmClient`, `AddApplicationPacketServices`, `IApplicationPacketService`, `IApplicationPacketStore`, `IUserProfileLoader`, `ITextGenerator`, `JobUrlFormatter`).
- Produces: `/api/profile/defaults`, `/api/jobs/{id}/packet`, `/api/jobs/{id}/packet/files/{fileName}`, refactored `/api/jobs/{id}/resume`, and `GlobalExceptionHandler`.

- [ ] **Step 1: Write `PacketEndpointsTests.cs` using `WebApplicationFactory<Program>`**
  - Configure `UseUserProfileConfig=false`, temp `ProfileDirectory`/`OutputDirectory`, and NSubstitute `ITextGenerator`/`IPdfRenderer`.
  - Assert `/api/profile/defaults`, `/api/jobs/{id}/packet` GET/POST, `404` on missing/soft-deleted job, `400` on missing `baseResume` and invalid `fileName` (`../appsettings.json`, `packet.json`), PDF/Markdown bytes + `Content-Disposition`, `502` on `AgentExecutionException` without stack traces, and `ProgramTests.GetJobs_WithMissingEmail_ReturnsBadRequest` preserving `400`.
- [ ] **Step 2: Implement `GlobalExceptionHandler.cs`, `Models/*.cs`, `JobsService` (`JobUrlFormatter`), and `Program.cs` updates**
- [ ] **Step 3: Run `dotnet test src/Fidalgo.Host.Tests/Fidalgo.Host.Tests.csproj`**
  - Expected: All Host tests pass with 0 warnings.
- [ ] **Step 4: Commit `Fidalgo.Host` and `Fidalgo.Host.Tests`**

---

### Task 5: Architecture Memory, README, Full Verification, Code Review & PR Proposal

**Files:**
- Modify: `wiki/architecture/architecture-summary.md`, `wiki/architecture/c4-container.puml`, `wiki/architecture/c4-component.puml`, `wiki/architecture/er-diagram.md`, `README.md`

- [ ] **Step 1: Update Architecture Memory (`wiki/architecture/*`) and `README.md` (`Documents\Fidalgo\`, `Documents\Fidalgo\applications\`, CLI flags, `spawnia/md-to-pdf` container HTTP API)**
- [ ] **Step 2: Rebuild frontend (`npm run build` in `src/Fidalgo.Frontend`) and run full `.NET` build + test + coverage (`dotnet build src/Fidalgo.slnx`, `dotnet test ... --settings ./coverlet.runsettings`)**
- [ ] **Step 3: Run dedicated Swarm `code-reviewer` over `git diff main..HEAD` and address every blocking/medium finding**
- [ ] **Step 4: Re-run verification gates after final fixes, commit docs/fixes, and propose the Pull Request**
