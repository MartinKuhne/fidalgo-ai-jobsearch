# Tasks: Job Scraper Agent

**Input**: Design documents from `/specs/001-job-scraper-agent/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/scraper-interface.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create solution file `Fidalgo.sln` at repository root
- [x] T002 [P] Create Fidalgo.Agent project `src/Fidalgo.Agent/Fidalgo.Agent.csproj` with .NET 10.0 target framework
- [x] T003 [P] Create Fidalgo.Agent.Tests project `src/Fidalgo.Agent.Tests/Fidalgo.Agent.Tests.csproj` with xUnit, NSubstitute, and Microsoft.NET.Test.Sdk packages
- [x] T004 [P] Create directory structure: `src/Fidalgo.Agent/Agents/`, `src/Fidalgo.Agent/Configuration/`, `src/Fidalgo.Agent/Scraping/Scrapers/`, `src/Fidalgo.Agent/Storage/`, `src/Fidalgo.Agent.Tests/Agents/`, `src/Fidalgo.Agent.Tests/Scraping/`, `src/Fidalgo.Agent.Tests/Storage/`, `specs/001-job-scraper-agent/contracts/`
- [x] T005 Configure .editorconfig at repository root with C# formatting rules and nullable enabled

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Create `ScraperException` class in `src/Fidalgo.Agent/Scraping/ScraperException.cs` with WebsiteName, Message, and InnerException properties
- [x] T007 Create `JobPosting` record in `src/Fidalgo.Agent/Scraping/JobPosting.cs` with SourceUrl, Title, Company, Description, PostedDate, SalaryLow, SalaryHigh, Currency, SourceWebsite properties
- [x] T008 [P] Add NuGet packages to Fidalgo.Agent: Microsoft.Data.Sqlite, Microsoft.EntityFrameworkCore.Sqlite, AngleSharp, Microsoft.Extensions.Hosting, Microsoft.Extensions.Configuration.Json
- [x] T009 [P] Add NuGet packages to Fidalgo.Agent.Tests: xUnit, NSubstitute, Microsoft.NET.Test.Sdk, Moq
- [x] T010 Create `JobDbContext` in `src/Fidalgo.Agent/Storage/JobDbContext.cs` with DbSet<JobPosting>, DbSet<SearchConfiguration>, DbSet<ScrapeResult> and OnModelCreating configuration
- [x] T011 Implement database migration setup in `src/Fidalgo.Agent/Storage/DatabaseInitializer.cs` with method to ensure database is created and migrated
- [x] T012 Configure logging infrastructure using Microsoft.Extensions.Logging in `src/Fidalgo.Agent/Logging/LoggingExtensions.cs` with ILoggerFactory setup

---

## Phase 3: User Story 1 - Configure Job Search Sources (Priority: P1)

**Goal**: Allow users to configure websites and keywords via JSON, persist and retrieve the configuration

**Independent Test**: Can be fully tested by loading a JSON config file, verifying websites and keywords are correctly parsed, and confirming the configuration is persisted to the database.

### Implementation for User Story 1

- [x] T013 [P] [US1] Create `SearchConfiguration` entity in `src/Fidalgo.Agent/Configuration/SearchConfiguration.cs` with Id, UserEmail, Websites, Keywords, IsActive, CreatedAt, UpdatedAt properties
- [x] T014 [P] [US1] Create `SearchConfigDto` in `src/Fidalgo.Agent/Configuration/SearchConfigDto.cs` for deserializing JSON configuration
- [x] T015 [US1] Create `ConfigRepository` in `src/Fidalgo.Agent/Storage/ConfigRepository.cs` with methods: GetConfigByEmail, UpsertConfig, GetActiveConfig
- [x] T016 [US1] Create `ConfigurationService` in `src/Fidalgo.Agent/Configuration/ConfigurationService.cs` with methods: LoadFromFile, SaveToDatabase, GetConfig, ValidateWebsites, ValidateKeywords
- [x] T017 [US1] Create `SupportedWebsites` constant class in `src/Fidalgo.Agent/Configuration/SupportedWebsites.cs` listing all 6 supported websites
- [x] T018 [US1] Create CLI command handler in `src/Fidalgo.Agent/Agents/ConfigCommandHandler.cs` that reads JSON config file path from command-line argument and persists it

**Checkpoint**: User Story 1 is fully functional - users can configure and retrieve search sources

---

## Phase 4: User Story 2 - Search Websites for Job Postings (Priority: P1)

**Goal**: Search configured websites for job postings matching keywords, extract details, handle failures gracefully

**Independent Test**: Can be fully tested by triggering a search against a configured website and verifying job data is extracted and returned correctly.

### Implementation for User Story 2

- [x] T019 [P] [US2] Create `IWebsiteScraper` interface in `src/Fidalgo.Agent/Scraping/IWebsiteScraper.cs` with SearchAsync(string keyword, CancellationToken ct) method
- [x] T020 [P] [US2] Create `RateLimitingHandler` in `src/Fidalgo.Agent/Scraping/RateLimitingHandler.cs` (DelegatingHandler) that enforces 1 request per 3 seconds per website
- [x] T021 [P] [US2] Create `HttpClientFactoryService` in `src/Fidalgo.Agent/Scraping/HttpClientFactoryService.cs` that registers HttpClient with RateLimitingHandler for each scraper
- [x] T022 [US2] Create `GovernmentJobsScraper` in `src/Fidalgo.Agent/Scraping/Scrapers/GovernmentJobsScraper.cs` implementing IWebsiteScraper with AngleSharp-based HTML parsing for governmentjobs.com
- [x] T023 [US2] Create `GoogleScraper` in `src/Fidalgo.Agent/Scraping/Scrapers/GoogleScraper.cs` implementing IWebsiteScraper with AngleSharp-based HTML parsing for Google job search
- [x] T024 [US2] Create `GlassdoorScraper` in `src/Fidalgo.Agent/Scraping/Scrapers/GlassdoorScraper.cs` implementing IWebsiteScraper with AngleSharp-based HTML parsing for glassdoor.com
- [x] T025 [US2] Create `MonsterScraper` in `src/Fidalgo.Agent/Scraping/Scrapers/MonsterScraper.cs` implementing IWebsiteScraper with AngleSharp-based HTML parsing for monster.com
- [x] T026 [US2] Create `IndeedScraper` in `src/Fidalgo.Agent/Scraping/Scrapers/IndeedScraper.cs` implementing IWebsiteScraper with AngleSharp-based HTML parsing for indeed.com
- [x] T027 [US2] Create `LinkedInScraper` in `src/Fidalgo.Agent/Scraping/Scrapers/LinkedInScraper.cs` implementing IWebsiteScraper with AngleSharp-based HTML parsing for linkedin.com
- [x] T028 [US2] Create `WebsiteScraperRegistry` in `src/Fidalgo.Agent/Scraping/WebsiteScraperRegistry.cs` with Register, Get, GetAll methods and automatic registration of all 6 scrapers
- [x] T029 [US2] Create `ScrapeResult` entity in `src/Fidalgo.Agent/Storage/ScrapeResult.cs` with Id, ConfigurationId, Website, Status, JobsFound, JobsSkipped, ErrorMessage, StartedAt, CompletedAt, DurationSeconds properties
- [x] T030 [US2] Create `SearchOrchestrator` in `src/Fidalgo.Agent/Scraping/SearchOrchestrator.cs` that iterates over configured websites and keywords, calls each scraper, collects results, and logs outcomes

**Checkpoint**: User Story 2 is fully functional - agent can search all 6 websites and extract job postings

---

## Phase 5: User Story 3 - Store Jobs in Database (Priority: P1)

**Goal**: Store discovered job postings in SQLite with deduplication, enable querying stored jobs

**Independent Test**: Can be fully tested by triggering a search, then querying the database to verify jobs were stored correctly and duplicates are prevented.

### Implementation for User Story 3

- [x] T031 [US3] Create `JobRepository` in `src/Fidalgo.Agent/Storage/JobRepository.cs` with methods: AddJobAsync, AddJobsAsync, GetJobsByDateRange, GetJobsByWebsite, GetJobsByKeyword, UpsertJobsAsync
- [x] T032 [US3] Create `DuplicateDetector` in `src/Fidalgo.Agent/Storage/DuplicateDetector.cs` with method: GetDuplicateUrls(IEnumerable<string> urls) that queries the database for existing SourceUrl values
- [x] T033 [US3] Integrate duplicate detection into SearchOrchestrator: filter discovered jobs through DuplicateDetector before storage, track skipped count in ScrapeResult
- [x] T034 [US3] Create `JobQueryService` in `src/Fidalgo.Agent/Storage/JobQueryService.cs` with methods: GetAllJobs, GetJobsByDateRange, GetJobsByWebsite, GetJobsByKeyword, GetNewJobsSince
- [x] T035 [US3] Wire up DbContext and repositories in `src/Fidalgo.Agent/DependencyInjection/ServiceCollectionExtensions.cs` with AddAgentServices() extension method registering all services

### Agent Main Entry Point

- [x] T036 [US3] Create `JobSearchAgent` in `src/Fidalgo.Agent/Agents/JobSearchAgent.cs` (BackgroundService) that: loads config, runs SearchOrchestrator, stores results, logs summary
- [x] T037 [US3] Create `Program.cs` in `src/Fidalgo.Agent/Program.cs` with CLI argument parsing for --config, service host builder, and graceful shutdown
- [x] T038 [US3] Create `appsettings.json` template in `src/Fidalgo.Agent/appsettings.json` with sample configuration structure

**Checkpoint**: User Story 3 is fully functional - all discovered jobs are stored in the database with deduplication

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T039 Add XML documentation comments to all public classes and methods in Fidalgo.Agent
- [ ] T040 [P] Create unit tests for ConfigurationService in `src/Fidalgo.Agent.Tests/Configuration/ConfigurationServiceTests.cs`
- [ ] T041 [P] Create unit tests for DuplicateDetector in `src/Fidalgo.Agent.Tests/Storage/DuplicateDetectorTests.cs` using mocked DbContext
- [ ] T042 [P] Create unit tests for RateLimitingHandler in `src/Fidalgo.Agent.Tests/Scraping/RateLimitingHandlerTests.cs`
- [ ] T043 Create integration test for full search cycle in `src/Fidalgo.Agent.Tests/Integration/FullSearchCycleTest.cs` with in-memory SQLite
- [ ] T044 Validate quickstart.md instructions work by running the agent end-to-end
- [ ] T045 Run `dotnet build` and verify zero warnings, run `dotnet test` and verify all tests pass
- [ ] T046 Add .gitignore entries for jobs.db, bin/, obj/, and IDE files

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - Stories can proceed in parallel (if staffed) or sequentially (P1 -> P2 -> P3)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Independent from US1 and US3
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Integrates with US2 components (SearchOrchestrator, JobPosting) but is independently testable

### Within Each User Story

- Models before services
- Services before orchestration
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- T002, T003, T004 (Setup): All create different files, can run in parallel
- T006-T012 (Foundational): T008, T009, T010, T011 run in parallel after T007
- T013, T014 (US1): Different files, can run in parallel
- T019-T021 (US2): Different files, can run in parallel
- T022-T027 (US2): Each scraper is a separate file, can run in parallel after T019
- T031-T033 (US3): T031, T032 run in parallel, T033 depends on both

---

## Parallel Example: User Story 2 Scrapers

```bash
# Launch all 6 scraper implementations in parallel (different files, same interface):
Task: "Create GovernmentJobsScraper in src/Fidalgo.Agent/Scraping/Scrapers/GovernmentJobsScraper.cs"
Task: "Create GoogleScraper in src/Fidalgo.Agent/Scraping/Scrapers/GoogleScraper.cs"
Task: "Create GlassdoorScraper in src/Fidalgo.Agent/Scraping/Scrapers/GlassdoorScraper.cs"
Task: "Create MonsterScraper in src/Fidalgo.Agent/Scraping/Scrapers/MonsterScraper.cs"
Task: "Create IndeedScraper in src/Fidalgo.Agent/Scraping/Scrapers/IndeedScraper.cs"
Task: "Create LinkedInScraper in src/Fidalgo.Agent/Scraping/Scrapers/LinkedInScraper.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1-3 Complete)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (configuration)
4. Complete Phase 4: User Story 2 (search)
5. Complete Phase 5: User Story 3 (storage)
6. **STOP and VALIDATE**: Run the agent end-to-end with a test config
7. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational -> Foundation ready
2. Add US1: Config -> Test config load/save independently
3. Add US2: Search -> Test scraping against one website
4. Add US3: Storage -> Test full pipeline: config -> search -> store
5. Polish: Tests, docs, cleanup

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (configuration)
   - Developer B: User Story 2 (scrapers - all 6 can be split)
   - Developer C: User Story 3 (storage + agent)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify the agent builds and runs after each phase
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Each scraper implementation follows the same interface contract defined in `contracts/scraper-interface.md`
- All entities follow the data model defined in `data-model.md`
