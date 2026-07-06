using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KvantoWPF.Infrastructure;

namespace KvantoWPF.Models;

public sealed class TaskItem : ObservableObject
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _category = string.Empty;
    private TaskPriority _priority = TaskPriority.Medium;
    private bool _isCompleted;
    private int _estimatedPomodoroSessions = 4;
    private int _completedPomodoroSessions;
    private string _colorHex = "#EF4444";

    public TaskItem()
    {
        WorkLogs.CollectionChanged += OnWorkLogsChanged;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public ObservableCollection<WorkLogEntry> WorkLogs { get; } = new();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public TaskPriority Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    public int EstimatedPomodoroSessions
    {
        get => _estimatedPomodoroSessions;
        set => SetProperty(ref _estimatedPomodoroSessions, Math.Max(1, value));
    }

    public int CompletedPomodoroSessions
    {
        get => _completedPomodoroSessions;
        set
        {
            if (SetProperty(ref _completedPomodoroSessions, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(PomodoroProgress));
                OnPropertyChanged(nameof(RemainingPomodoros));
            }
        }
    }

    public string ColorHex
    {
        get => _colorHex;
        set => SetProperty(ref _colorHex, value);
    }

    public int TotalWorkedMinutes => WorkLogs.Sum(log => log.Minutes);

    public int DaysWorkedCount => WorkLogs.Select(log => log.StartedAt.Date).Distinct().Count();

    public DateTime? LastWorkedOn => WorkLogs.OrderByDescending(log => log.StartedAt).FirstOrDefault()?.StartedAt;

    public string WorkedTimeDisplay => TimeSpan.FromMinutes(TotalWorkedMinutes).ToString(@"hh\:mm");

    public string PomodoroProgress => $"{CompletedPomodoroSessions}/{EstimatedPomodoroSessions}";

    public int RemainingPomodoros => Math.Max(EstimatedPomodoroSessions - CompletedPomodoroSessions, 0);

    public void RecordCompletedSession(int minutes, DateTime startedAt)
    {
        CompletedPomodoroSessions++;
        WorkLogs.Add(new WorkLogEntry
        {
            TaskId = Id,
            TaskTitle = Title,
            ColorHex = ColorHex,
            StartedAt = startedAt,
            Minutes = minutes
        });
    }

    private void OnWorkLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TotalWorkedMinutes));
        OnPropertyChanged(nameof(DaysWorkedCount));
        OnPropertyChanged(nameof(LastWorkedOn));
        OnPropertyChanged(nameof(WorkedTimeDisplay));
    }
}
