import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import { ApplicationPacketModal } from './components/ApplicationPacketModal';
import type {
  ApplicationPacketResultDto,
  ApplicationPacketStatusDto,
  ProfileDefaultsDto,
} from './api/packets';

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers({
      'content-type': status >= 400 ? 'application/problem+json' : 'application/json',
    }),
    json: async () => body,
    text: async () => (typeof body === 'string' ? body : JSON.stringify(body)),
  } as Response;
}

function textResponse(body: string, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers({ 'content-type': 'text/markdown; charset=utf-8' }),
    json: async () => ({}),
    text: async () => body,
  } as Response;
}

const sampleDefaults: ProfileDefaultsDto = {
  defaultEmail: 'test@example.com',
  hasResume: true,
  hasCareerStory: true,
  careerStoryLength: 1200,
  hasCareerGoals: false,
  careerGoalsLength: 0,
  baseResumeMarkdown: '# Jane Doe\n\nSenior Engineer',
  careerStoryMarkdown: '# Career Story\n\nLed platform migration.',
  careerGoalsMarkdown: null,
  outputDirectory: 'C:\\Users\\test\\Documents\\Fidalgo\\applications',
};

const emptyPacketStatus: ApplicationPacketStatusDto = {
  exists: false,
  folderName: 'tech-corp_software-engineer_11111111',
  directoryPath:
    'C:\\Users\\test\\Documents\\Fidalgo\\applications\\tech-corp_software-engineer_11111111',
  generatedAtUtc: null,
  files: {
    resumePdfUrl: '/api/jobs/job-1/packet/files/resume.pdf',
    coverLetterPdfUrl: '/api/jobs/job-1/packet/files/cover-letter.pdf',
    jobPostingUrl: '/api/jobs/job-1/packet/files/job-posting.md',
  },
  resumeMarkdown: null,
  coverLetterMarkdown: null,
};

const generatedPacketResult: ApplicationPacketResultDto = {
  outcome: 'Generated',
  jobId: 'job-1',
  folderName: 'tech-corp_software-engineer_11111111',
  directoryPath:
    'C:\\Users\\test\\Documents\\Fidalgo\\applications\\tech-corp_software-engineer_11111111',
  generatedAtUtc: '2026-11-22T12:00:00Z',
  files: {
    resumePdfUrl: '/api/jobs/job-1/packet/files/resume.pdf',
    coverLetterPdfUrl: '/api/jobs/job-1/packet/files/cover-letter.pdf',
    jobPostingUrl: '/api/jobs/job-1/packet/files/job-posting.md',
  },
  resumeMarkdown: '# Tailored Resume',
  coverLetterMarkdown: '# Cover Letter',
};

describe('App', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    globalThis.fetch = vi.fn();
  });

  it('renders title and loading state initially', async () => {
    vi.mocked(globalThis.fetch).mockResolvedValueOnce(jsonResponse([]));

    render(<App />);
    expect(screen.getByText('Fidalgo')).toBeInTheDocument();
    expect(screen.getByText('AI-Powered Job Triage')).toBeInTheDocument();
    await waitFor(() => expect(globalThis.fetch).toHaveBeenCalled());
  });

  it('fetches tenants and then fetches jobs for the first tenant', async () => {
    vi.mocked(globalThis.fetch)
      .mockResolvedValueOnce(jsonResponse([{ email: 'test@example.com', jobCount: 2 }]))
      .mockResolvedValueOnce(
        jsonResponse({
          items: [
            {
              internalId: 'job-1',
              title: 'Software Engineer',
              employer: 'Tech Corp',
              location: 'Remote',
              recommendation: 'Apply',
              pay: '$100k',
            },
            {
              internalId: 'job-2',
              title: 'Backend Developer',
              employer: 'Data Inc',
              location: 'New York',
              recommendation: 'Do not apply',
              pay: '',
            },
          ],
        }),
      );

    render(<App />);

    await waitFor(() => {
      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    expect(screen.getByText('test@example.com (2 jobs)')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText('Software Engineer')).toBeInTheDocument();
    });
    expect(screen.getByText('Backend Developer')).toBeInTheDocument();
    expect(screen.getByText('Tech Corp')).toBeInTheDocument();
  });

  it('opens Application Packet modal from job details and generates packet without pasting', async () => {
    vi.mocked(globalThis.fetch).mockImplementation(async (input, init) => {
      const url = typeof input === 'string' ? input : input.toString();
      if (url === '/api/tenants') {
        return jsonResponse([{ email: 'test@example.com', jobCount: 1 }]);
      }
      if (url.startsWith('/api/jobs?')) {
        return jsonResponse({
          items: [
            {
              internalId: 'job-1',
              title: 'Software Engineer',
              employer: 'Tech Corp',
              location: 'Remote',
              recommendation: 'Apply',
              pay: '$150k',
              description: 'Build distributed systems.',
              aiReasoning: 'Strong match.',
              skills: ['TypeScript'],
              url: 'https://example.com/job/1',
              score: 92,
              postedDate: '2026-11-20T00:00:00Z',
            },
          ],
          totalItems: 1,
        });
      }
      if (url === '/api/profile/defaults') {
        return jsonResponse(sampleDefaults);
      }
      if (url === '/api/jobs/job-1/packet' && (!init?.method || init.method === 'GET')) {
        return jsonResponse(emptyPacketStatus);
      }
      if (url === '/api/jobs/job-1/packet' && init?.method === 'POST') {
        return jsonResponse(generatedPacketResult);
      }
      if (url === '/api/jobs/job-1/packet/files/job-posting.md') {
        return textResponse('# Software Engineer at Tech Corp\n\nSnapshot body.');
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });

    render(<App />);

    await waitFor(() => expect(screen.getByText('Software Engineer')).toBeInTheDocument());
    fireEvent.click(screen.getByText('Software Engineer'));

    const openPacketBtn = await screen.findByRole('button', { name: 'Application Packet' });
    fireEvent.click(openPacketBtn);

    expect(await screen.findByText('resume.md: Loaded from server')).toBeInTheDocument();
    expect(screen.getByText(/career-story\.md: Loaded \(1,200 chars\)/)).toBeInTheDocument();
    expect(screen.getByText('career-goals.md: Not found')).toBeInTheDocument();

    const generateBtn = screen.getByRole('button', { name: 'Generate Application Packet' });
    fireEvent.click(generateBtn);

    await waitFor(() => {
      expect(screen.getByText('tech-corp_software-engineer_11111111')).toBeInTheDocument();
    });

    // Verify POST body did not require or send overrides when unmodified
    const postCall = vi
      .mocked(globalThis.fetch)
      .mock.calls.find(([u, i]) => u === '/api/jobs/job-1/packet' && i?.method === 'POST');
    expect(postCall).toBeDefined();
    expect(JSON.parse(String(postCall?.[1]?.body))).toEqual({ force: false });

    // Resume PDF tab active by default
    const resumeIframe = screen.getByTitle('Resume PDF Preview');
    expect(resumeIframe).toHaveAttribute('src', '/api/jobs/job-1/packet/files/resume.pdf');
    expect(screen.getByRole('link', { name: 'Download resume.pdf' })).toHaveAttribute(
      'href',
      '/api/jobs/job-1/packet/files/resume.pdf?download=true',
    );

    // Switch to Cover Letter tab
    fireEvent.click(screen.getByRole('tab', { name: 'Cover Letter (cover-letter.pdf)' }));
    const coverIframe = screen.getByTitle('Cover Letter PDF Preview');
    expect(coverIframe).toHaveAttribute(
      'src',
      '/api/jobs/job-1/packet/files/cover-letter.pdf',
    );
    expect(screen.getByRole('link', { name: 'Download cover-letter.pdf' })).toHaveAttribute(
      'href',
      '/api/jobs/job-1/packet/files/cover-letter.pdf?download=true',
    );

    // Switch to Job Posting tab
    fireEvent.click(screen.getByRole('tab', { name: 'Job Posting (job-posting.md)' }));
    await waitFor(() => {
      expect(screen.getByText('Software Engineer at Tech Corp')).toBeInTheDocument();
    });
    expect(screen.getByText('Snapshot body.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Download job-posting.md' })).toHaveAttribute(
      'href',
      '/api/jobs/job-1/packet/files/job-posting.md?download=true',
    );
  });
});

describe('ApplicationPacketModal', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    globalThis.fetch = vi.fn();
  });

  it('displays ProblemDetails validation banner when generation fails', async () => {
    const missingResumeDefaults: ProfileDefaultsDto = {
      ...sampleDefaults,
      hasResume: false,
      baseResumeMarkdown: null,
    };

    vi.mocked(globalThis.fetch).mockImplementation(async (input, init) => {
      const url = typeof input === 'string' ? input : input.toString();
      if (url === '/api/profile/defaults') return jsonResponse(missingResumeDefaults);
      if (url === '/api/jobs/job-1/packet' && (!init?.method || init.method === 'GET')) {
        return jsonResponse(emptyPacketStatus);
      }
      if (url === '/api/jobs/job-1/packet' && init?.method === 'POST') {
        return jsonResponse(
          {
            title: 'Validation Failed',
            status: 400,
            detail: 'A base resume is required to generate an application packet.',
            errors: {
              baseResume: ['No resume.md found on server and no override provided.'],
            },
          },
          400,
        );
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });

    render(
      <ApplicationPacketModal
        job={{ internalId: 'job-1', title: 'Software Engineer', employer: 'Tech Corp' }}
        onClose={vi.fn()}
      />,
    );

    expect(await screen.findByText('resume.md: Missing')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Generate Application Packet' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Validation Failed');
    expect(alert).toHaveTextContent(
      'A base resume is required to generate an application packet.',
    );
    expect(alert).toHaveTextContent('baseResume');
    expect(alert).toHaveTextContent(
      'No resume.md found on server and no override provided.',
    );
  });

  it('supports Force regenerate and optional overrides when packet already exists', async () => {
    const existingStatus: ApplicationPacketStatusDto = {
      exists: true,
      folderName: 'tech-corp_software-engineer_11111111',
      directoryPath:
        'C:\\Users\\test\\Documents\\Fidalgo\\applications\\tech-corp_software-engineer_11111111',
      generatedAtUtc: '2026-11-22T11:00:00Z',
      files: {
        resumePdfUrl: '/api/jobs/job-1/packet/files/resume.pdf',
        coverLetterPdfUrl: '/api/jobs/job-1/packet/files/cover-letter.pdf',
        jobPostingUrl: '/api/jobs/job-1/packet/files/job-posting.md',
      },
      resumeMarkdown: '# Existing Resume',
      coverLetterMarkdown: '# Existing Cover Letter',
    };

    vi.mocked(globalThis.fetch).mockImplementation(async (input, init) => {
      const url = typeof input === 'string' ? input : input.toString();
      if (url === '/api/profile/defaults') return jsonResponse(sampleDefaults);
      if (url === '/api/jobs/job-1/packet' && (!init?.method || init.method === 'GET')) {
        return jsonResponse(existingStatus);
      }
      if (url === '/api/jobs/job-1/packet' && init?.method === 'POST') {
        return jsonResponse({ ...generatedPacketResult, outcome: 'Generated' });
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });

    render(
      <ApplicationPacketModal
        job={{ internalId: 'job-1', title: 'Software Engineer', employer: 'Tech Corp' }}
        onClose={vi.fn()}
      />,
    );

    const forceCheckbox = await screen.findByLabelText('Force regenerate');
    expect(forceCheckbox).not.toBeChecked();
    expect(screen.getByTitle('Resume PDF Preview')).toHaveAttribute(
      'src',
      '/api/jobs/job-1/packet/files/resume.pdf',
    );

    // Open collapsible override inputs and customize base resume
    fireEvent.click(screen.getByRole('button', { name: 'Customize Profile Inputs (Optional)' }));
    const resumeTextarea = screen.getByLabelText('Base Resume Override (resume.md)');
    expect(resumeTextarea).toHaveValue(sampleDefaults.baseResumeMarkdown);
    fireEvent.change(resumeTextarea, { target: { value: '# Updated Resume Override' } });

    fireEvent.click(forceCheckbox);
    expect(forceCheckbox).toBeChecked();

    fireEvent.click(screen.getByRole('button', { name: 'Regenerate Packet' }));

    await waitFor(() => {
      const postCall = vi
        .mocked(globalThis.fetch)
        .mock.calls.find(([u, i]) => u === '/api/jobs/job-1/packet' && i?.method === 'POST');
      expect(postCall).toBeDefined();
      expect(JSON.parse(String(postCall?.[1]?.body))).toEqual({
        force: true,
        baseResumeOverride: '# Updated Resume Override',
      });
    });
  });
});
