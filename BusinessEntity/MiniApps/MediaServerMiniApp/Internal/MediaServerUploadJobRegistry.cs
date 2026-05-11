using System.Collections.Concurrent;
using BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

namespace BusinessEntity.MiniApps.MediaServerMiniApp.Internal;

public sealed class MediaServerUploadJobRegistry : IMediaServerUploadJobTracker
{
    private readonly ConcurrentDictionary<Guid, UploadJobRecord> _jobs = new();

    public MediaVideoUploadJobInfo RegisterVideoUploadJob(
        Guid jobId,
        string fileName,
        string contentType,
        long? totalBytes)
    {
        var record = new UploadJobRecord(jobId, fileName, contentType, totalBytes);
        var existing = _jobs.AddOrUpdate(
            jobId,
            record,
            (_, previous) =>
            {
                if (IsActive(previous.Snapshot().State))
                {
                    throw new InvalidOperationException($"Upload job '{jobId}' is already active.");
                }

                previous.Dispose();
                return record;
            });

        TrimOldTerminalJobs();
        return existing.Snapshot();
    }

    public void MarkVideoUploadJobUploading(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var record))
        {
            record.MarkUploading();
        }
    }

    public void ReportVideoUploadProgress(Guid jobId, long uploadedBytes)
    {
        if (_jobs.TryGetValue(jobId, out var record))
        {
            record.ReportProgress(uploadedBytes);
        }
    }

    public void CompleteVideoUploadJob(Guid jobId, MediaVideoInfo video)
    {
        if (_jobs.TryGetValue(jobId, out var record))
        {
            record.MarkCompleted(video);
        }
    }

    public void FailVideoUploadJob(Guid jobId, string errorMessage)
    {
        if (_jobs.TryGetValue(jobId, out var record))
        {
            record.MarkFailed(errorMessage);
        }
    }

    public void MarkVideoUploadJobCancelled(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var record))
        {
            record.MarkCancelled();
        }
    }

    public CancellationToken GetCancellationToken(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var record)
            ? record.CancellationToken
            : CancellationToken.None;
    }

    public IReadOnlyList<MediaVideoUploadJobInfo> GetVideoUploadJobs()
    {
        TrimOldTerminalJobs();
        return _jobs.Values
            .Select(x => x.Snapshot())
            .OrderByDescending(x => x.CreatedDate)
            .ToList();
    }

    public MediaVideoUploadJobInfo? GetVideoUploadJob(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var record) ? record.Snapshot() : null;
    }

    public bool CancelVideoUploadJob(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var record))
        {
            return false;
        }

        record.Cancel();
        return true;
    }

    private void TrimOldTerminalJobs()
    {
        var threshold = DateTime.UtcNow.AddHours(-2);
        foreach (var pair in _jobs)
        {
            var snapshot = pair.Value.Snapshot();
            if (!IsActive(snapshot.State) &&
                snapshot.CompletedDate.HasValue &&
                snapshot.CompletedDate.Value < threshold &&
                _jobs.TryRemove(pair.Key, out var removed))
            {
                removed.Dispose();
            }
        }
    }

    private static bool IsActive(MediaVideoUploadJobState state)
    {
        return state == MediaVideoUploadJobState.Queued ||
               state == MediaVideoUploadJobState.Uploading;
    }

    private sealed class UploadJobRecord : IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private long _uploadedBytes;
        private MediaVideoUploadJobState _state = MediaVideoUploadJobState.Queued;
        private string _errorMessage = string.Empty;
        private DateTime? _startedDate;
        private DateTime? _completedDate;
        private Guid? _videoId;
        private string _displayName = string.Empty;

        public UploadJobRecord(Guid jobId, string fileName, string contentType, long? totalBytes)
        {
            JobId = jobId;
            FileName = fileName;
            ContentType = contentType;
            TotalBytes = totalBytes;
            CreatedDate = DateTime.UtcNow;
        }

        public Guid JobId { get; }
        public string FileName { get; }
        public string ContentType { get; }
        public long? TotalBytes { get; }
        public DateTime CreatedDate { get; }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void MarkUploading()
        {
            lock (_syncRoot)
            {
                if (IsActive(_state))
                {
                    _state = MediaVideoUploadJobState.Uploading;
                    _startedDate ??= DateTime.UtcNow;
                }
            }
        }

        public void ReportProgress(long uploadedBytes)
        {
            lock (_syncRoot)
            {
                if (!IsActive(_state))
                {
                    return;
                }

                _uploadedBytes = Math.Max(_uploadedBytes, uploadedBytes);
                _state = MediaVideoUploadJobState.Uploading;
                _startedDate ??= DateTime.UtcNow;
            }
        }

        public void MarkCompleted(MediaVideoInfo video)
        {
            lock (_syncRoot)
            {
                _state = MediaVideoUploadJobState.Completed;
                _uploadedBytes = TotalBytes.HasValue && TotalBytes.Value > 0
                    ? TotalBytes.Value
                    : Math.Max(_uploadedBytes, video.OriginalSizeBytes);
                _completedDate = DateTime.UtcNow;
                _videoId = video.Id;
                _displayName = video.DisplayName;
                _errorMessage = string.Empty;
            }
        }

        public void MarkFailed(string errorMessage)
        {
            lock (_syncRoot)
            {
                if (_state == MediaVideoUploadJobState.Cancelled)
                {
                    return;
                }

                _state = MediaVideoUploadJobState.Failed;
                _errorMessage = errorMessage;
                _completedDate = DateTime.UtcNow;
            }
        }

        public void MarkCancelled()
        {
            lock (_syncRoot)
            {
                if (_state == MediaVideoUploadJobState.Completed)
                {
                    return;
                }

                _state = MediaVideoUploadJobState.Cancelled;
                _errorMessage = "Загрузка отменена.";
                _completedDate = DateTime.UtcNow;
            }
        }

        public void Cancel()
        {
            MarkCancelled();
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        public MediaVideoUploadJobInfo Snapshot()
        {
            lock (_syncRoot)
            {
                return new MediaVideoUploadJobInfo
                {
                    JobId = JobId,
                    FileName = FileName,
                    ContentType = ContentType,
                    TotalBytes = TotalBytes,
                    UploadedBytes = _uploadedBytes,
                    State = _state,
                    ErrorMessage = _errorMessage,
                    CreatedDate = CreatedDate,
                    StartedDate = _startedDate,
                    CompletedDate = _completedDate,
                    VideoId = _videoId,
                    DisplayName = _displayName
                };
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
        }
    }
}
