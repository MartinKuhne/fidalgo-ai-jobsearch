import { useEffect, useState } from 'react';
import Markdown from 'react-markdown';
import { ApplicationPacketModal } from './components/ApplicationPacketModal';

interface Job {
  internalId: string;
  employerJobId: string;
  sourceWebsite: string;
  title: string;
  employer: string;
  description: string;
  pay: string;
  location: string;
  skills: string[];
  recommendation: string;
  aiReasoning: string;
  url: string;
  email: string;
  score: number;
  postedDate: string;
}

interface Tenant {
  email: string;
  jobCount: number;
}

function App() {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [selectedEmail, setSelectedEmail] = useState<string>('');

  // Table state
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(false);
  const [totalItems, setTotalItems] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState('score');
  const [sortDir, setSortDir] = useState('desc');
  const [searchInput, setSearchInput] = useState(''); // debounced search

  const [selectedJob, setSelectedJob] = useState<Job | null>(null);
  const [showPacketModal, setShowPacketModal] = useState(false);

  useEffect(() => {
    fetch('/api/tenants')
      .then((res) => res.json())
      .then((data: Tenant[]) => {
        setTenants(data);
        if (data.length > 0) {
          setSelectedEmail(data[0].email);
        }
      })
      .catch((err) => console.error('Error fetching tenants:', err));
  }, []);

  useEffect(() => {
    const delayDebounceFn = setTimeout(() => {
      setSearch(searchInput);
      setPage(1);
    }, 500);
    return () => clearTimeout(delayDebounceFn);
  }, [searchInput]);

  useEffect(() => {
    if (!selectedEmail) return;
    setLoading(true);
    const query = new URLSearchParams({
      email: selectedEmail,
      page: page.toString(),
      pageSize: pageSize.toString(),
      sortBy,
      sortDir,
    });
    if (search) query.append('search', search);

    fetch(`/api/jobs?${query.toString()}`)
      .then((res) => res.json())
      .then((data) => {
        setJobs(data.items || []);
        setTotalItems(data.totalItems || 0);
        setLoading(false);
      })
      .catch((err) => {
        console.error('Error fetching jobs:', err);
        setLoading(false);
      });
  }, [selectedEmail, page, pageSize, search, sortBy, sortDir]);

  const deleteJob = async (id: string) => {
    await fetch(`/api/jobs/${id}`, { method: 'DELETE' });
    setJobs(jobs.filter((j) => j.internalId !== id));
    setTotalItems((prev) => prev - 1);
    setShowPacketModal(false);
    setSelectedJob(null);
  };

  const handleSort = (col: string) => {
    if (sortBy === col) {
      setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(col);
      setSortDir(col === 'score' || col === 'date' ? 'desc' : 'asc');
    }
    setPage(1);
  };

  const totalPages = Math.ceil(totalItems / pageSize);

  const getSortIcon = (col: string) => {
    if (sortBy !== col) return '↕️';
    return sortDir === 'asc' ? '↑' : '↓';
  };

  return (
    <>
      <header className="header">
        <h1>Fidalgo</h1>
        <p style={{ color: 'var(--text-muted)' }}>AI-Powered Job Triage</p>
      </header>

      <div className="table-controls">
        {tenants.length > 0 && (
          <select
            className="tenant-selector"
            style={{ margin: 0 }}
            value={selectedEmail}
            onChange={(e) => {
              setSelectedEmail(e.target.value);
              setPage(1);
            }}
          >
            {tenants.map((t) => (
              <option key={t.email} value={t.email}>
                {t.email} ({t.jobCount} jobs)
              </option>
            ))}
          </select>
        )}

        <input
          type="text"
          className="search-input"
          placeholder="Filter by title or employer..."
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
      </div>

      <div className="jobs-table-container">
        {loading && jobs.length === 0 ? (
          <div className="loader"></div>
        ) : (
          <table className="jobs-table">
            <thead>
              <tr>
                <th onClick={() => handleSort('score')}>Score {getSortIcon('score')}</th>
                <th onClick={() => handleSort('title')}>Title {getSortIcon('title')}</th>
                <th onClick={() => handleSort('employer')}>Employer {getSortIcon('employer')}</th>
                <th>Pay</th>
                <th onClick={() => handleSort('recommendation')}>
                  Recommendation {getSortIcon('recommendation')}
                </th>
                <th onClick={() => handleSort('date')}>Date {getSortIcon('date')}</th>
              </tr>
            </thead>
            <tbody>
              {jobs.map((job) => (
                <tr key={job.internalId} onClick={() => setSelectedJob(job)}>
                  <td>
                    <span
                      className="badge"
                      style={{ background: 'rgba(255,255,255,0.1)', color: 'var(--text-main)' }}
                    >
                      {job.score}
                    </span>
                  </td>
                  <td style={{ fontWeight: 600 }}>{job.title}</td>
                  <td>{job.employer}</td>
                  <td style={{ color: 'var(--text-muted)' }}>{job.pay || 'N/A'}</td>
                  <td>
                    <span
                      className="badge"
                      style={{
                        background:
                          job.recommendation === 'Apply'
                            ? 'rgba(34, 197, 94, 0.2)'
                            : job.recommendation === 'Do not apply'
                              ? 'rgba(239, 68, 68, 0.2)'
                              : 'rgba(234, 179, 8, 0.2)',
                        color:
                          job.recommendation === 'Apply'
                            ? '#4ade80'
                            : job.recommendation === 'Do not apply'
                              ? '#f87171'
                              : '#facc15',
                      }}
                    >
                      {job.recommendation}
                    </span>
                  </td>
                  <td style={{ color: 'var(--text-muted)' }}>
                    {job.postedDate ? new Date(job.postedDate).toLocaleDateString() : 'Unknown'}
                  </td>
                </tr>
              ))}
              {jobs.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '2rem' }}>
                    No jobs found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>

      <div className="pagination">
        <button disabled={page <= 1 || loading} onClick={() => setPage((p) => p - 1)}>
          Previous
        </button>
        <span style={{ color: 'var(--text-muted)' }}>
          Page {page} of {totalPages || 1} ({totalItems} total)
        </span>
        <button disabled={page >= totalPages || loading} onClick={() => setPage((p) => p + 1)}>
          Next
        </button>
      </div>

      {selectedJob && (
        <div
          className="modal-overlay"
          onClick={() => {
            setShowPacketModal(false);
            setSelectedJob(null);
          }}
        >
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h2 style={{ fontSize: '1.5rem', marginBottom: '0.5rem' }}>{selectedJob.title}</h2>
            <p style={{ color: 'var(--text-muted)', marginBottom: '0.5rem' }}>
              {selectedJob.employer} • {selectedJob.location}
            </p>
            <p style={{ color: 'var(--text-muted)', marginBottom: '1.5rem' }}>
              <strong>Pay:</strong> {selectedJob.pay || 'No salary info'}
            </p>

            <div style={{ marginBottom: '1.5rem' }}>
              <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#818cf8' }}>
                AI Reasoning:
              </strong>
              <p style={{ lineHeight: '1.6', fontSize: '0.95rem' }}>
                {selectedJob.aiReasoning || 'No reasoning provided.'}
              </p>
            </div>

            <div
              style={{
                marginBottom: '1.5rem',
                maxHeight: '300px',
                overflowY: 'auto',
                background: 'rgba(255,255,255,0.05)',
                padding: '1rem',
                borderRadius: '4px',
              }}
            >
              <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#4ade80' }}>
                Job Description:
              </strong>
              <div style={{ lineHeight: '1.6', fontSize: '0.9rem' }}>
                <Markdown>{selectedJob.description || 'No description available.'}</Markdown>
              </div>
            </div>

            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
              {selectedJob.skills?.map((skill) => (
                <span
                  key={skill}
                  className="badge"
                  style={{ background: 'rgba(255,255,255,0.1)', color: 'white' }}
                >
                  {skill}
                </span>
              ))}
            </div>

            <div className="modal-actions">
              <button
                type="button"
                className="btn btn-primary"
                style={{ background: '#818cf8', color: '#fff' }}
                onClick={() => setShowPacketModal(true)}
              >
                Application Packet
              </button>
              <button
                type="button"
                className="btn btn-danger"
                onClick={() => void deleteJob(selectedJob.internalId)}
              >
                Delete
              </button>
              <a
                href={selectedJob.url}
                target="_blank"
                rel="noreferrer"
                className="btn"
                style={{ textDecoration: 'none' }}
              >
                View Source
              </a>
              <button
                type="button"
                className="btn"
                onClick={() => setSelectedJob(null)}
                style={{ background: 'transparent', border: '1px solid var(--border)' }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {showPacketModal && selectedJob && (
        <ApplicationPacketModal
          job={selectedJob}
          onClose={() => setShowPacketModal(false)}
        />
      )}
    </>
  );
}

export default App;
