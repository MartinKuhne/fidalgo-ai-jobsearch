import { render, screen, waitFor } from '@testing-library/react';
import App from './App';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock the global fetch
globalThis.fetch = vi.fn() as any;

describe('App', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('renders title and loading state initially', async () => {
    (globalThis.fetch as any).mockResolvedValueOnce({
      json: async () => []
    });

    render(<App />);
    expect(screen.getByText('Fidalgo')).toBeInTheDocument();
    expect(screen.getByText('AI-Powered Job Triage')).toBeInTheDocument();
    await waitFor(() => expect(globalThis.fetch).toHaveBeenCalled());
  });

  it('fetches tenants and then fetches jobs for the first tenant', async () => {
    // 1st fetch: /api/tenants
    (globalThis.fetch as any).mockResolvedValueOnce({
      json: async () => [{ email: 'test@example.com', jobCount: 2 }]
    });

    // 2nd fetch: /api/jobs
    (globalThis.fetch as any).mockResolvedValueOnce({
      json: async () => ({
        items: [
          {
            internalId: '1',
            title: 'Software Engineer',
            employer: 'Tech Corp',
            location: 'Remote',
            recommendation: 'Apply',
            pay: '$100k',
          },
          {
            internalId: '2',
            title: 'Backend Developer',
            employer: 'Data Inc',
            location: 'New York',
            recommendation: 'Do not apply',
            pay: '',
          }
        ]
      })
    });

    render(<App />);

    // Check if tenant selector is populated
    await waitFor(() => {
      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });
    
    // Check if options are correct
    expect(screen.getByText('test@example.com (2 jobs)')).toBeInTheDocument();

    // Check if jobs are rendered
    await waitFor(() => {
      expect(screen.getByText('Software Engineer')).toBeInTheDocument();
    });
    expect(screen.getByText('Backend Developer')).toBeInTheDocument();
    expect(screen.getByText('Tech Corp')).toBeInTheDocument();
  });
});
