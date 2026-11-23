import { useEffect, useState } from 'react';
import Markdown from 'react-markdown';
import {
  ApiProblemError,
  fetchPacketTextFile,
  generateJobPacket,
  getJobPacketStatus,
  getPacketFileUrl,
  getProfileDefaults,
  withDownloadParam,
  withPreviewVersion,
  type ApplicationPacketResultDto,
  type ApplicationPacketStatusDto,
  type GeneratePacketRequest,
  type PacketFilesDto,
  type ProblemDetails,
  type ProfileDefaultsDto,
} from '../api/packets';

export interface PacketModalJob {
  internalId: string;
  title: string;
  employer: string;
}

interface ApplicationPacketModalProps {
  job: PacketModalJob;
  onClose: () => void;
}

type PreviewTab = 'resume' | 'cover-letter' | 'job-posting';

interface ErrorBannerState {
  title: string;
  detail: string;
  fieldErrors: Array<[string, string[]]>;
}

function toErrorBanner(err: unknown): ErrorBannerState {
  if (err instanceof ApiProblemError) {
    const problem: ProblemDetails | null = err.problem;
    const fieldErrors = problem?.errors
      ? Object.entries(problem.errors).filter(([, msgs]) => msgs.length > 0)
      : [];
    return {
      title: problem?.title || `Request failed (${err.status})`,
      detail: problem?.detail || err.message,
      fieldErrors,
    };
  }

  if (err instanceof Error) {
    return {
      title: 'Request Error',
      detail: err.message,
      fieldErrors: [],
    };
  }

  return {
    title: 'Request Error',
    detail: 'An unexpected error occurred.',
    fieldErrors: [],
  };
}

export function ApplicationPacketModal({ job, onClose }: ApplicationPacketModalProps) {
  const [defaults, setDefaults] = useState<ProfileDefaultsDto | null>(null);
  const [packetStatus, setPacketStatus] = useState<ApplicationPacketStatusDto | null>(null);
  const [lastResult, setLastResult] = useState<ApplicationPacketResultDto | null>(null);
  const [initialLoading, setInitialLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<ErrorBannerState | null>(null);

  const [showOverrides, setShowOverrides] = useState(false);
  const [baseResumeInput, setBaseResumeInput] = useState('');
  const [careerStoryInput, setCareerStoryInput] = useState('');
  const [careerGoalsInput, setCareerGoalsInput] = useState('');
  const [forceRegenerate, setForceRegenerate] = useState(false);

  const [activeTab, setActiveTab] = useState<PreviewTab>('resume');
  const [previewVersion, setPreviewVersion] = useState(0);
  const [jobPostingMarkdown, setJobPostingMarkdown] = useState<string | null>(null);
  const [loadingJobPosting, setLoadingJobPosting] = useState(false);
  const [jobPostingError, setJobPostingError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setInitialLoading(true);
    setError(null);
    setLastResult(null);
    setJobPostingMarkdown(null);
    setJobPostingError(null);
    setForceRegenerate(false);

    Promise.all([
      getProfileDefaults(controller.signal),
      getJobPacketStatus(job.internalId, controller.signal),
    ])
      .then(([profileDefaults, status]) => {
        setDefaults(profileDefaults);
        setPacketStatus(status);
        setBaseResumeInput(profileDefaults.baseResumeMarkdown ?? '');
        setCareerStoryInput(profileDefaults.careerStoryMarkdown ?? '');
        setCareerGoalsInput(profileDefaults.careerGoalsMarkdown ?? '');
        setInitialLoading(false);
      })
      .catch((err: unknown) => {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(toErrorBanner(err));
        setInitialLoading(false);
      });

    return () => controller.abort();
  }, [job.internalId]);

  const packetExists = Boolean(lastResult || packetStatus?.exists);
  const activeFiles: PacketFilesDto | null = packetExists
    ? (lastResult?.files ??
        packetStatus?.files ?? {
          resumePdfUrl: getPacketFileUrl(job.internalId, 'resume.pdf'),
          coverLetterPdfUrl: getPacketFileUrl(job.internalId, 'cover-letter.pdf'),
          jobPostingUrl: getPacketFileUrl(job.internalId, 'job-posting.md'),
        })
    : null;

  const activeFolderName = lastResult?.folderName ?? packetStatus?.folderName ?? '';
  const activeDirectoryPath = lastResult?.directoryPath ?? packetStatus?.directoryPath ?? '';
  const activeGeneratedAt = lastResult?.generatedAtUtc ?? packetStatus?.generatedAtUtc ?? null;

  useEffect(() => {
    if (activeTab !== 'job-posting' || !packetExists || !activeFiles?.jobPostingUrl) {
      return;
    }
    if (jobPostingMarkdown !== null) {
      return;
    }

    const controller = new AbortController();
    setLoadingJobPosting(true);
    setJobPostingError(null);

    fetchPacketTextFile(activeFiles.jobPostingUrl, controller.signal)
      .then((text) => {
        setJobPostingMarkdown(text);
        setLoadingJobPosting(false);
      })
      .catch((err: unknown) => {
        if (err instanceof Error && err.name === 'AbortError') return;
        setJobPostingError(err instanceof Error ? err.message : 'Failed to load job posting.');
        setLoadingJobPosting(false);
      });

    return () => controller.abort();
  }, [activeTab, packetExists, activeFiles?.jobPostingUrl, jobPostingMarkdown, previewVersion]);

  const hasBaseResumeOverride = baseResumeInput !== (defaults?.baseResumeMarkdown ?? '');
  const hasCareerStoryOverride = careerStoryInput !== (defaults?.careerStoryMarkdown ?? '');
  const hasCareerGoalsOverride = careerGoalsInput !== (defaults?.careerGoalsMarkdown ?? '');
  const hasAnyOverride =
    hasBaseResumeOverride || hasCareerStoryOverride || hasCareerGoalsOverride;

  const handleResetOverrides = () => {
    setBaseResumeInput(defaults?.baseResumeMarkdown ?? '');
    setCareerStoryInput(defaults?.careerStoryMarkdown ?? '');
    setCareerGoalsInput(defaults?.careerGoalsMarkdown ?? '');
  };

  const handleGenerate = async () => {
    setGenerating(true);
    setError(null);

    const request: GeneratePacketRequest = {
      force: packetExists ? forceRegenerate : false,
      ...(hasBaseResumeOverride ? { baseResumeOverride: baseResumeInput } : {}),
      ...(hasCareerStoryOverride ? { careerStoryOverride: careerStoryInput } : {}),
      ...(hasCareerGoalsOverride ? { careerGoalsOverride: careerGoalsInput } : {}),
    };

    try {
      const result = await generateJobPacket(job.internalId, request);
      setLastResult(result);
      setPacketStatus({
        exists: true,
        folderName: result.folderName,
        directoryPath: result.directoryPath,
        generatedAtUtc: result.generatedAtUtc,
        files: result.files,
        resumeMarkdown: result.resumeMarkdown,
        coverLetterMarkdown: result.coverLetterMarkdown,
      });
      setJobPostingMarkdown(null);
      setJobPostingError(null);
      setPreviewVersion((v) => v + 1);
    } catch (err: unknown) {
      setError(toErrorBanner(err));
    } finally {
      setGenerating(false);
    }
  };

  const handleFileUpload = async (
    file: File | undefined,
    setter: (value: string) => void,
  ) => {
    if (!file) return;
    const text = await file.text();
    setter(text);
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-content packet-modal-content"
        role="dialog"
        aria-modal="true"
        aria-labelledby="packet-modal-title"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="packet-modal-header">
          <div>
            <h2 id="packet-modal-title">Application Packet</h2>
            <p className="packet-subtitle">
              {job.title} — {job.employer}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-ghost"
            onClick={onClose}
            aria-label="Close application packet modal"
          >
            Close
          </button>
        </div>

        {initialLoading ? (
          <div className="packet-loading-state">
            <div className="loader" aria-label="Loading profile defaults and packet status" />
            <p>Loading server profile defaults and packet status...</p>
          </div>
        ) : (
          <>
            {defaults && (
              <section className="packet-section" aria-label="Server profile readiness">
                <div className="readiness-badges">
                  <span
                    className={`badge ${defaults.hasResume ? 'badge-success' : 'badge-danger'}`}
                  >
                    {defaults.hasResume
                      ? 'resume.md: Loaded from server'
                      : 'resume.md: Missing'}
                  </span>
                  <span
                    className={`badge ${defaults.hasCareerStory ? 'badge-success' : 'badge-warning'}`}
                  >
                    {defaults.hasCareerStory
                      ? `career-story.md: Loaded (${defaults.careerStoryLength.toLocaleString()} chars)`
                      : 'career-story.md: Not found'}
                  </span>
                  <span
                    className={`badge ${defaults.hasCareerGoals ? 'badge-success' : 'badge-warning'}`}
                  >
                    {defaults.hasCareerGoals
                      ? `career-goals.md: Loaded (${defaults.careerGoalsLength.toLocaleString()} chars)`
                      : 'career-goals.md: Not found'}
                  </span>
                </div>
                {defaults.outputDirectory && (
                  <p className="packet-meta-line">
                    <strong>Output directory:</strong> <code>{defaults.outputDirectory}</code>
                  </p>
                )}
              </section>
            )}

            {error && (
              <div role="alert" className="packet-error-banner">
                <strong>{error.title}</strong>
                <p>{error.detail}</p>
                {error.fieldErrors.length > 0 && (
                  <ul>
                    {error.fieldErrors.map(([field, messages]) => (
                      <li key={field}>
                        <code>{field}</code>: {messages.join(', ')}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}

            <section className="packet-section">
              <div className="override-header-row">
                <button
                  type="button"
                  className="btn btn-ghost btn-sm"
                  aria-expanded={showOverrides}
                  onClick={() => setShowOverrides((prev) => !prev)}
                >
                  {showOverrides ? 'Hide' : 'Customize'} Profile Inputs (Optional)
                </button>
                {hasAnyOverride && (
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    onClick={handleResetOverrides}
                  >
                    Reset to server defaults
                  </button>
                )}
              </div>

              {showOverrides && (
                <div className="override-panel">
                  <div className="override-field">
                    <div className="override-label-row">
                      <label htmlFor="override-base-resume">
                        Base Resume Override (resume.md)
                      </label>
                      <label className="upload-chip">
                        Upload File
                        <input
                          type="file"
                          accept=".md,.txt"
                          style={{ display: 'none' }}
                          onChange={(e) =>
                            void handleFileUpload(e.target.files?.[0], setBaseResumeInput)
                          }
                        />
                      </label>
                    </div>
                    <textarea
                      id="override-base-resume"
                      value={baseResumeInput}
                      onChange={(e) => setBaseResumeInput(e.target.value)}
                      placeholder="Optional override for server resume.md..."
                      rows={5}
                    />
                  </div>

                  <div className="override-field">
                    <div className="override-label-row">
                      <label htmlFor="override-career-story">
                        Career Story Override (career-story.md)
                      </label>
                      <label className="upload-chip">
                        Upload File
                        <input
                          type="file"
                          accept=".md,.txt"
                          style={{ display: 'none' }}
                          onChange={(e) =>
                            void handleFileUpload(e.target.files?.[0], setCareerStoryInput)
                          }
                        />
                      </label>
                    </div>
                    <textarea
                      id="override-career-story"
                      value={careerStoryInput}
                      onChange={(e) => setCareerStoryInput(e.target.value)}
                      placeholder="Optional override for server career-story.md..."
                      rows={4}
                    />
                  </div>

                  <div className="override-field">
                    <div className="override-label-row">
                      <label htmlFor="override-career-goals">
                        Career Goals Override (career-goals.md)
                      </label>
                      <label className="upload-chip">
                        Upload File
                        <input
                          type="file"
                          accept=".md,.txt"
                          style={{ display: 'none' }}
                          onChange={(e) =>
                            void handleFileUpload(e.target.files?.[0], setCareerGoalsInput)
                          }
                        />
                      </label>
                    </div>
                    <textarea
                      id="override-career-goals"
                      value={careerGoalsInput}
                      onChange={(e) => setCareerGoalsInput(e.target.value)}
                      placeholder="Optional override for server career-goals.md..."
                      rows={4}
                    />
                  </div>
                </div>
              )}
            </section>

            <section className="packet-controls-bar">
              <div className="packet-controls-left">
                <button
                  type="button"
                  className="btn"
                  onClick={() => void handleGenerate()}
                  disabled={generating || initialLoading}
                >
                  {generating
                    ? 'Generating...'
                    : packetExists
                      ? 'Regenerate Packet'
                      : 'Generate Application Packet'}
                </button>

                {packetExists && (
                  <label className="force-toggle">
                    <input
                      type="checkbox"
                      checked={forceRegenerate}
                      onChange={(e) => setForceRegenerate(e.target.checked)}
                      disabled={generating}
                    />
                    Force regenerate
                  </label>
                )}
              </div>

              {lastResult?.outcome === 'SkippedExisting' && (
                <span className="packet-outcome-note">
                  Existing packet returned (enable &quot;Force regenerate&quot; to overwrite).
                </span>
              )}
            </section>

            {packetExists && activeFiles && (
              <>
                <section className="packet-saved-banner" aria-label="Saved packet location">
                  <div>
                    <strong>Folder:</strong> <code>{activeFolderName}</code>
                  </div>
                  <div>
                    <strong>Saved path:</strong> <code>{activeDirectoryPath}</code>
                  </div>
                  {activeGeneratedAt && (
                    <div className="packet-meta-line">
                      <strong>Generated:</strong>{' '}
                      {new Date(activeGeneratedAt).toLocaleString()}
                    </div>
                  )}
                </section>

                <section className="packet-preview-section">
                  <div className="packet-tabs-header">
                    <div className="packet-tabs" role="tablist" aria-label="Packet documents">
                      <button
                        type="button"
                        role="tab"
                        aria-selected={activeTab === 'resume'}
                        className={`packet-tab ${activeTab === 'resume' ? 'active' : ''}`}
                        onClick={() => setActiveTab('resume')}
                      >
                        Resume (resume.pdf)
                      </button>
                      <button
                        type="button"
                        role="tab"
                        aria-selected={activeTab === 'cover-letter'}
                        className={`packet-tab ${activeTab === 'cover-letter' ? 'active' : ''}`}
                        onClick={() => setActiveTab('cover-letter')}
                      >
                        Cover Letter (cover-letter.pdf)
                      </button>
                      <button
                        type="button"
                        role="tab"
                        aria-selected={activeTab === 'job-posting'}
                        className={`packet-tab ${activeTab === 'job-posting' ? 'active' : ''}`}
                        onClick={() => setActiveTab('job-posting')}
                      >
                        Job Posting (job-posting.md)
                      </button>
                    </div>

                    <div className="packet-download-action">
                      {activeTab === 'resume' && (
                        <a
                          href={withDownloadParam(activeFiles.resumePdfUrl)}
                          download="resume.pdf"
                          className="btn btn-secondary btn-sm"
                        >
                          Download resume.pdf
                        </a>
                      )}
                      {activeTab === 'cover-letter' && (
                        <a
                          href={withDownloadParam(activeFiles.coverLetterPdfUrl)}
                          download="cover-letter.pdf"
                          className="btn btn-secondary btn-sm"
                        >
                          Download cover-letter.pdf
                        </a>
                      )}
                      {activeTab === 'job-posting' && (
                        <a
                          href={withDownloadParam(activeFiles.jobPostingUrl)}
                          download="job-posting.md"
                          className="btn btn-secondary btn-sm"
                        >
                          Download job-posting.md
                        </a>
                      )}
                    </div>
                  </div>

                  <div className="packet-tabpanel" role="tabpanel">
                    {activeTab === 'resume' && (
                      <iframe
                        key={`resume-${previewVersion}`}
                        title="Resume PDF Preview"
                        src={withPreviewVersion(activeFiles.resumePdfUrl, previewVersion)}
                        className="packet-pdf-frame"
                      />
                    )}

                    {activeTab === 'cover-letter' && (
                      <iframe
                        key={`cover-letter-${previewVersion}`}
                        title="Cover Letter PDF Preview"
                        src={withPreviewVersion(activeFiles.coverLetterPdfUrl, previewVersion)}
                        className="packet-pdf-frame"
                      />
                    )}

                    {activeTab === 'job-posting' && (
                      <div className="packet-markdown-preview">
                        {loadingJobPosting ? (
                          <p>Loading job-posting.md...</p>
                        ) : jobPostingError ? (
                          <p className="packet-error-text">{jobPostingError}</p>
                        ) : (
                          <Markdown>{jobPostingMarkdown ?? ''}</Markdown>
                        )}
                      </div>
                    )}
                  </div>
                </section>
              </>
            )}
          </>
        )}
      </div>
    </div>
  );
}
