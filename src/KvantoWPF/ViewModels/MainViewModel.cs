using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using KvantoWPF.Infrastructure;
using KvantoWPF.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace KvantoWPF.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly string[] TaskColors =
    {
        "#EF4444",
        "#3B82F6",
        "#10B981",
        "#8B5CF6",
        "#F59E0B",
        "#EC4899"
    };

    private readonly DispatcherTimer _timer;
    private readonly RelayCommand _saveTaskCommand;
    private readonly RelayCommand _deleteTaskCommand;
    private readonly RelayCommand _toggleTaskStateCommand;
    private readonly RelayCommand _startPauseCommand;
    private readonly RelayCommand _stopCommand;
    private readonly RelayCommand _skipCommand;
    private readonly RelayCommand _newTaskCommand;
    private readonly RelayCommand _openCompactViewCommand;
    private readonly RelayCommand _togglePinCommand;
    private readonly RelayCommand _previousMonthCommand;
    private readonly RelayCommand _nextMonthCommand;
    private RelayCommand<string>? _navigateToCommand;
    private RelayCommand<TaskItem>? _selectTaskAndStartTimerCommand;
    private RelayCommand<TaskItem>? _selectTaskForEditCommand;
    private TaskItem? _selectedTask;
    private string _selectedPage = "Dashboard";
    private string _taskTitle = string.Empty;
    private string _taskDescription = string.Empty;
    private string _taskCategory = string.Empty;
    private TaskPriority _taskPriority = TaskPriority.Medium;
    private int _estimatedPomodoros = 4;
    private int _workMinutes = 25;
    private int _shortBreakMinutes = 5;
    private int _longBreakMinutes = 15;
    private int _longBreakFrequency = 4;
    private int _dailyPomodoroGoal = 8;
    private PomodoroPhase _currentPhase;
    private TimeSpan _remainingTime = TimeSpan.FromMinutes(25);
    private TimeSpan _currentSessionLength = TimeSpan.FromMinutes(25);
    private bool _isTimerRunning;
    private int _completedWorkSessionsInCycle;
    private bool _isCompactPinned = true;
    private DateTime _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private string _selectedReportRange = "Last 7 Days";
    private DateTime _reportStartDate = DateTime.Today.AddDays(-6);
    private DateTime _reportEndDate = DateTime.Today;
    private int _reportTotalMinutes;
    private int _reportSessionCount;
    private int _reportActiveDays;
    private string _reportDailyAverage = "00:00";
    private int _reportCurrentStreak;
    private DateTime _currentSessionStartedAt = DateTime.Now;

    public MainViewModel()
    {
        PriorityOptions = Enum.GetValues<TaskPriority>();
        ReportRangeOptions = new[] { "Last 7 Days", "Last 30 Days", "This Year", "Custom" };
        ActiveTasks = new ObservableCollection<TaskItem>();
        ArchivedTasks = new ObservableCollection<TaskItem>();
        CalendarDays = new ObservableCollection<CalendarDayViewModel>();
        TaskReportItems = new ObservableCollection<ReportBarItem>();
        DailyReportItems = new ObservableCollection<ReportBarItem>();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;

        _saveTaskCommand = new RelayCommand(SaveTask);
        _deleteTaskCommand = new RelayCommand(DeleteSelectedTask, () => SelectedTask is not null);
        _toggleTaskStateCommand = new RelayCommand(ToggleTaskState, () => SelectedTask is not null);
        _startPauseCommand = new RelayCommand(StartOrPauseTimer, () => (SelectedTask is not null && !SelectedTask.IsCompleted) || IsTimerRunning || CurrentPhase != PomodoroPhase.Idle);
        _stopCommand = new RelayCommand(StopTimer, () => CurrentPhase != PomodoroPhase.Idle || IsTimerRunning);
        _skipCommand = new RelayCommand(SkipToNextSession, () => CurrentPhase != PomodoroPhase.Idle || SelectedTask is { IsCompleted: false });
        _newTaskCommand = new RelayCommand(ClearTaskDraft);
        _openCompactViewCommand = new RelayCommand(() => CompactViewRequested?.Invoke(this, EventArgs.Empty));
        _togglePinCommand = new RelayCommand(() => IsCompactPinned = !IsCompactPinned);
        _previousMonthCommand = new RelayCommand(() =>
        {
            DisplayMonth = DisplayMonth.AddMonths(-1);
            RefreshCalendar();
        });
        _nextMonthCommand = new RelayCommand(() =>
        {
            DisplayMonth = DisplayMonth.AddMonths(1);
            RefreshCalendar();
        });

        ResetToIdle();
        RefreshCalendar();
        RefreshReports();
    }

    public event EventHandler? CompactViewRequested;
    public event EventHandler<NotificationRequestEventArgs>? NotificationRequested;

    public Array PriorityOptions { get; }

    public string[] ReportRangeOptions { get; }

    public ObservableCollection<TaskItem> ActiveTasks { get; }

    public ObservableCollection<TaskItem> ArchivedTasks { get; }

    public ObservableCollection<CalendarDayViewModel> CalendarDays { get; }

    public ObservableCollection<ReportBarItem> TaskReportItems { get; }

    public ObservableCollection<ReportBarItem> DailyReportItems { get; }

    public RelayCommand SaveTaskCommand => _saveTaskCommand;
    public RelayCommand DeleteTaskCommand => _deleteTaskCommand;
    public RelayCommand ToggleTaskStateCommand => _toggleTaskStateCommand;
    public RelayCommand StartPauseCommand => _startPauseCommand;
    public RelayCommand StopCommand => _stopCommand;
    public RelayCommand SkipCommand => _skipCommand;
    public RelayCommand NewTaskCommand => _newTaskCommand;
    public RelayCommand OpenCompactViewCommand => _openCompactViewCommand;
    public RelayCommand TogglePinCommand => _togglePinCommand;
    public RelayCommand PreviousMonthCommand => _previousMonthCommand;
    public RelayCommand NextMonthCommand => _nextMonthCommand;

    public RelayCommand<string> NavigateToCommand =>
        _navigateToCommand ??= new RelayCommand<string>(page => SelectedPage = page ?? "Dashboard");

    public RelayCommand<TaskItem> SelectTaskAndStartTimerCommand =>
        _selectTaskAndStartTimerCommand ??= new RelayCommand<TaskItem>(task =>
        {
            if (task is null) return;
            SelectedTask = task;
            StartOrPauseTimer();
        });

    public RelayCommand<TaskItem> SelectTaskForEditCommand =>
        _selectTaskForEditCommand ??= new RelayCommand<TaskItem>(task =>
        {
            if (task is null) return;
            SelectedTask = task;
            SelectedPage = "Tasks";
        });

    public string SelectedPage
    {
        get => _selectedPage;
        set => SetProperty(ref _selectedPage, value);
    }

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                PopulateTaskDraft(value);
                NotifyCommandStateChanged();
                OnPropertyChanged(nameof(SelectedTaskStats));
            }
        }
    }

    public string TaskTitle
    {
        get => _taskTitle;
        set => SetProperty(ref _taskTitle, value);
    }

    public string TaskDescription
    {
        get => _taskDescription;
        set => SetProperty(ref _taskDescription, value);
    }

    public string TaskCategory
    {
        get => _taskCategory;
        set => SetProperty(ref _taskCategory, value);
    }

    public TaskPriority TaskPriority
    {
        get => _taskPriority;
        set => SetProperty(ref _taskPriority, value);
    }

    public int EstimatedPomodoros
    {
        get => _estimatedPomodoros;
        set => SetProperty(ref _estimatedPomodoros, Math.Max(1, value));
    }

    public int WorkMinutes
    {
        get => _workMinutes;
        set
        {
            if (SetProperty(ref _workMinutes, Math.Max(1, value)) && CurrentPhase is PomodoroPhase.Idle)
            {
                ResetToIdle();
            }
        }
    }

    public int ShortBreakMinutes
    {
        get => _shortBreakMinutes;
        set => SetProperty(ref _shortBreakMinutes, Math.Max(1, value));
    }

    public int LongBreakMinutes
    {
        get => _longBreakMinutes;
        set => SetProperty(ref _longBreakMinutes, Math.Max(1, value));
    }

    public int LongBreakFrequency
    {
        get => _longBreakFrequency;
        set => SetProperty(ref _longBreakFrequency, Math.Max(1, value));
    }

    public int DailyPomodoroGoal
    {
        get => _dailyPomodoroGoal;
        set => SetProperty(ref _dailyPomodoroGoal, Math.Max(1, value));
    }

    public PomodoroPhase CurrentPhase
    {
        get => _currentPhase;
        private set
        {
            if (SetProperty(ref _currentPhase, value))
            {
                OnPropertyChanged(nameof(SessionLabel));
                OnPropertyChanged(nameof(SessionBrush));
                OnPropertyChanged(nameof(StatusBarText));
                OnPropertyChanged(nameof(IsBreakPhase));
                NotifyCommandStateChanged();
            }
        }
    }

    public TimeSpan RemainingTime
    {
        get => _remainingTime;
        private set
        {
            if (SetProperty(ref _remainingTime, value))
            {
                OnPropertyChanged(nameof(TimeRemainingText));
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(StatusBarText));
            }
        }
    }

    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        private set
        {
            if (SetProperty(ref _isTimerRunning, value))
            {
                OnPropertyChanged(nameof(StartPauseText));
                OnPropertyChanged(nameof(StatusBarText));
                NotifyCommandStateChanged();
            }
        }
    }

    public int CompletedWorkSessionsInCycle
    {
        get => _completedWorkSessionsInCycle;
        private set => SetProperty(ref _completedWorkSessionsInCycle, Math.Max(0, value));
    }

    public bool IsCompactPinned
    {
        get => _isCompactPinned;
        set
        {
            if (SetProperty(ref _isCompactPinned, value))
            {
                OnPropertyChanged(nameof(PinText));
            }
        }
    }

    public DateTime DisplayMonth
    {
        get => _displayMonth;
        private set
        {
            if (SetProperty(ref _displayMonth, value))
            {
                OnPropertyChanged(nameof(DisplayMonthLabel));
            }
        }
    }

    public string SelectedReportRange
    {
        get => _selectedReportRange;
        set
        {
            if (SetProperty(ref _selectedReportRange, value))
            {
                ApplyReportRangePreset();
                OnPropertyChanged(nameof(IsCustomRange));
                RefreshReports();
            }
        }
    }

    public DateTime ReportStartDate
    {
        get => _reportStartDate;
        set
        {
            if (SetProperty(ref _reportStartDate, value))
            {
                RefreshReports();
            }
        }
    }

    public DateTime ReportEndDate
    {
        get => _reportEndDate;
        set
        {
            if (SetProperty(ref _reportEndDate, value))
            {
                RefreshReports();
            }
        }
    }

    public int ReportTotalMinutes
    {
        get => _reportTotalMinutes;
        private set => SetProperty(ref _reportTotalMinutes, value);
    }

    public int ReportSessionCount
    {
        get => _reportSessionCount;
        private set => SetProperty(ref _reportSessionCount, value);
    }

    public int ReportActiveDays
    {
        get => _reportActiveDays;
        private set => SetProperty(ref _reportActiveDays, value);
    }

    public string ReportDailyAverage
    {
        get => _reportDailyAverage;
        private set => SetProperty(ref _reportDailyAverage, value);
    }

    public int ReportCurrentStreak
    {
        get => _reportCurrentStreak;
        private set => SetProperty(ref _reportCurrentStreak, value);
    }

    public string SessionLabel => CurrentPhase switch
    {
        PomodoroPhase.Work => "Focus session",
        PomodoroPhase.ShortBreak => "Short break",
        PomodoroPhase.LongBreak => "Long break",
        _ => "Ready for the next Pomodoro"
    };

    public string TimeRemainingText => RemainingTime.ToString(@"mm\:ss");

    public double ProgressValue
    {
        get
        {
            var totalSeconds = Math.Max(_currentSessionLength.TotalSeconds, 1);
            var elapsedSeconds = totalSeconds - RemainingTime.TotalSeconds;
            return Math.Clamp(elapsedSeconds / totalSeconds * 100d, 0d, 100d);
        }
    }

    public Brush SessionBrush => CurrentPhase switch
    {
        PomodoroPhase.Work => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
        PomodoroPhase.ShortBreak => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
        PomodoroPhase.LongBreak => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#047857")),
        _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
    };

    public string StartPauseText => IsTimerRunning ? "Pause" : CurrentPhase == PomodoroPhase.Idle ? "Start" : "Resume";

    public string StatusBarText => CurrentPhase == PomodoroPhase.Idle
        ? "Idle — select a task and start a Pomodoro."
        : $"{SessionLabel} {(IsTimerRunning ? "running" : "paused")} · {TimeRemainingText}";

    public string DisplayMonthLabel => DisplayMonth.ToString("MMMM yyyy");

    public bool IsCustomRange => SelectedReportRange == "Custom";

    public bool IsBreakPhase => CurrentPhase is PomodoroPhase.ShortBreak or PomodoroPhase.LongBreak;

    public string PinText => IsCompactPinned ? "Unpin overlay" : "Pin overlay";

    public string SelectedTaskStats => SelectedTask is null
        ? "No task selected"
        : $"{SelectedTask.WorkedTimeDisplay} tracked · {SelectedTask.DaysWorkedCount} active days · {SelectedTask.PomodoroProgress} sessions";

    public string GreetingText => DateTime.Now.Hour switch
    {
        < 12 => "Good morning, User",
        < 17 => "Good afternoon, User",
        _ => "Good evening, User"
    };

    public int ActiveTasksCount => ActiveTasks.Count;

    public bool HasFeaturedTask => ActiveTasks.Count > 0;

    public bool HasNoFeaturedTask => ActiveTasks.Count == 0;

    public TaskItem? FeaturedTask => SelectedTask is not null && !SelectedTask.IsCompleted
        ? SelectedTask
        : ActiveTasks.OrderByDescending(t => t.Priority).FirstOrDefault();

    public int TodayPomodoroCount => GetAllTasks()
        .SelectMany(t => t.WorkLogs)
        .Count(log => log.StartedAt.Date == DateTime.Today);

    public string TodayFocusedHoursText
    {
        get
        {
            var totalMinutes = GetAllTasks()
                .SelectMany(t => t.WorkLogs)
                .Where(log => log.StartedAt.Date == DateTime.Today)
                .Sum(log => log.Minutes);
            return $"{totalMinutes / 60.0:F1}h";
        }
    }

    public int TodayCompletedCount => ArchivedTasks
        .Count(t => t.LastWorkedOn?.Date == DateTime.Today);

    public IEnumerable<TaskItem> TodayCompletedItems => ArchivedTasks
        .Where(t => t.LastWorkedOn?.Date == DateTime.Today)
        .Take(5);

    public bool HasTodayCompletedItems => TodayCompletedCount > 0;

    public bool HasNoTodayCompletedItems => TodayCompletedCount == 0;

    public void StartNextPomodoroFromNotification()
    {
        if (SelectedTask is null || SelectedTask.IsCompleted)
        {
            return;
        }

        StartSession(PomodoroPhase.Work, WorkMinutes);
        StartTimer();
    }

    private void SaveTask()
    {
        if (string.IsNullOrWhiteSpace(TaskTitle))
        {
            return;
        }

        if (SelectedTask is null)
        {
            var task = new TaskItem
            {
                Title = TaskTitle.Trim(),
                Description = TaskDescription.Trim(),
                Category = string.IsNullOrWhiteSpace(TaskCategory) ? "General" : TaskCategory.Trim(),
                Priority = TaskPriority,
                EstimatedPomodoroSessions = EstimatedPomodoros,
                ColorHex = TaskColors[GetAllTasks().Count % TaskColors.Length]
            };

            ActiveTasks.Add(task);
            SelectedTask = task;
        }
        else
        {
            SelectedTask.Title = TaskTitle.Trim();
            SelectedTask.Description = TaskDescription.Trim();
            SelectedTask.Category = string.IsNullOrWhiteSpace(TaskCategory) ? "General" : TaskCategory.Trim();
            SelectedTask.Priority = TaskPriority;
            SelectedTask.EstimatedPomodoroSessions = EstimatedPomodoros;
        }

        RefreshCalendar();
        RefreshReports();
        OnPropertyChanged(nameof(SelectedTaskStats));
        RefreshDashboardStats();
    }

    private void DeleteSelectedTask()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var collection = SelectedTask.IsCompleted ? ArchivedTasks : ActiveTasks;
        collection.Remove(SelectedTask);
        SelectedTask = null;
        ClearTaskDraft();
        RefreshCalendar();
        RefreshReports();
        RefreshDashboardStats();
    }

    private void ToggleTaskState()
    {
        if (SelectedTask is null)
        {
            return;
        }

        if (SelectedTask.IsCompleted)
        {
            SelectedTask.IsCompleted = false;
            ArchivedTasks.Remove(SelectedTask);
            ActiveTasks.Add(SelectedTask);
        }
        else
        {
            SelectedTask.IsCompleted = true;
            ActiveTasks.Remove(SelectedTask);
            ArchivedTasks.Add(SelectedTask);
            if (CurrentPhase == PomodoroPhase.Work && !IsTimerRunning)
            {
                ResetToIdle();
            }
        }

        RefreshCalendar();
        RefreshReports();
        OnPropertyChanged(nameof(SelectedTaskStats));
        RefreshDashboardStats();
    }

    private void StartOrPauseTimer()
    {
        if (IsTimerRunning)
        {
            PauseTimer();
            return;
        }

        if (CurrentPhase == PomodoroPhase.Idle)
        {
            if (SelectedTask is null || SelectedTask.IsCompleted)
            {
                return;
            }

            StartSession(PomodoroPhase.Work, WorkMinutes);
        }

        StartTimer();
    }

    private void StopTimer()
    {
        PauseTimer();
        ResetToIdle();
    }

    private void SkipToNextSession()
    {
        if (CurrentPhase == PomodoroPhase.Idle)
        {
            if (SelectedTask is not null && !SelectedTask.IsCompleted)
            {
                StartNextPomodoroFromNotification();
            }

            return;
        }

        PauseTimer();

        if (CurrentPhase == PomodoroPhase.Work)
        {
            StartBreakSession();
            StartTimer();
            return;
        }

        ResetToIdle();
        NotificationRequested?.Invoke(this, new NotificationRequestEventArgs(
            "Break skipped",
            "Your next focus session is ready to begin.",
            NotificationAction.StartNextPomodoro));
    }

    private void ClearTaskDraft()
    {
        SelectedTask = null;
        TaskTitle = string.Empty;
        TaskDescription = string.Empty;
        TaskCategory = string.Empty;
        TaskPriority = TaskPriority.Medium;
        EstimatedPomodoros = 4;
    }

    private void PopulateTaskDraft(TaskItem? task)
    {
        if (task is null)
        {
            return;
        }

        TaskTitle = task.Title;
        TaskDescription = task.Description;
        TaskCategory = task.Category;
        TaskPriority = task.Priority;
        EstimatedPomodoros = task.EstimatedPomodoroSessions;
    }

    private void StartSession(PomodoroPhase phase, int durationMinutes)
    {
        CurrentPhase = phase;
        _currentSessionLength = TimeSpan.FromMinutes(durationMinutes);
        RemainingTime = _currentSessionLength;
        _currentSessionStartedAt = DateTime.Now;
    }

    private void StartBreakSession()
    {
        var breakPhase = CompletedWorkSessionsInCycle > 0 && CompletedWorkSessionsInCycle % LongBreakFrequency == 0
            ? PomodoroPhase.LongBreak
            : PomodoroPhase.ShortBreak;

        var duration = breakPhase == PomodoroPhase.LongBreak ? LongBreakMinutes : ShortBreakMinutes;
        StartSession(breakPhase, duration);
    }

    private void StartTimer()
    {
        IsTimerRunning = true;
        _timer.Start();
    }

    private void PauseTimer()
    {
        _timer.Stop();
        IsTimerRunning = false;
    }

    private void ResetToIdle()
    {
        CurrentPhase = PomodoroPhase.Idle;
        _currentSessionLength = TimeSpan.FromMinutes(WorkMinutes);
        RemainingTime = _currentSessionLength;
        IsTimerRunning = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (RemainingTime <= TimeSpan.Zero)
        {
            CompleteCurrentSession();
            return;
        }

        RemainingTime -= TimeSpan.FromSeconds(1);
    }

    private void CompleteCurrentSession()
    {
        PauseTimer();

        if (CurrentPhase == PomodoroPhase.Work)
        {
            CompletedWorkSessionsInCycle++;
            SelectedTask?.RecordCompletedSession(WorkMinutes, _currentSessionStartedAt);
            OnPropertyChanged(nameof(SelectedTaskStats));
            RefreshCalendar();
            RefreshReports();
            RefreshDashboardStats();
            StartBreakSession();
            NotificationRequested?.Invoke(this, new NotificationRequestEventArgs(
                "Pomodoro complete",
                $"{SelectedTask?.Title ?? "Task"} finished a focus session. Break started automatically.",
                NotificationAction.StartBreak));
            StartTimer();
            return;
        }

        ResetToIdle();
        NotificationRequested?.Invoke(this, new NotificationRequestEventArgs(
            "Break complete",
            "Your next Pomodoro is ready.",
            NotificationAction.StartNextPomodoro));
    }

    private void RefreshDashboardStats()
    {
        OnPropertyChanged(nameof(ActiveTasksCount));
        OnPropertyChanged(nameof(HasFeaturedTask));
        OnPropertyChanged(nameof(HasNoFeaturedTask));
        OnPropertyChanged(nameof(FeaturedTask));
        OnPropertyChanged(nameof(TodayPomodoroCount));
        OnPropertyChanged(nameof(TodayFocusedHoursText));
        OnPropertyChanged(nameof(TodayCompletedCount));
        OnPropertyChanged(nameof(TodayCompletedItems));
        OnPropertyChanged(nameof(HasTodayCompletedItems));
        OnPropertyChanged(nameof(HasNoTodayCompletedItems));
    }

    private void RefreshCalendar()
    {
        var workLogs = GetAllTasks()
            .SelectMany(task => task.WorkLogs)
            .GroupBy(log => log.StartedAt.Date)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(entry => entry.TaskId)
                    .Select(entries =>
                    {
                        var firstEntry = entries.First();
                        return new CalendarTaskEntryViewModel(
                            firstEntry.TaskTitle,
                            entries.Sum(item => item.Minutes),
                            firstEntry.ColorHex);
                    })
                    .OrderByDescending(entry => entry.Minutes)
                    .ToList());

        CalendarDays.Clear();
        var firstVisibleDay = DisplayMonth.AddDays(-(((int)DisplayMonth.DayOfWeek + 6) % 7));

        for (var index = 0; index < 42; index++)
        {
            var date = firstVisibleDay.AddDays(index);
            workLogs.TryGetValue(date.Date, out var entries);
            CalendarDays.Add(new CalendarDayViewModel(
                date,
                date.Month == DisplayMonth.Month,
                date.Date == DateTime.Today,
                entries ?? []));
        }
    }

    private void RefreshReports()
    {
        var filteredLogs = GetAllTasks()
            .SelectMany(task => task.WorkLogs)
            .Where(log => log.StartedAt.Date >= ReportStartDate.Date && log.StartedAt.Date <= ReportEndDate.Date)
            .OrderBy(log => log.StartedAt)
            .ToList();

        ReportTotalMinutes = filteredLogs.Sum(log => log.Minutes);
        ReportSessionCount = filteredLogs.Count;
        ReportActiveDays = filteredLogs.Select(log => log.StartedAt.Date).Distinct().Count();
        ReportDailyAverage = ReportActiveDays == 0
            ? "00:00"
            : TimeSpan.FromMinutes((double)ReportTotalMinutes / ReportActiveDays).ToString(@"hh\:mm");
        ReportCurrentStreak = CalculateCurrentStreak(filteredLogs.Select(log => log.StartedAt.Date).Distinct().OrderBy(date => date).ToList());

        var taskGroups = filteredLogs
            .GroupBy(log => log.TaskTitle)
            .Select(group => new
            {
                group.Key,
                Minutes = group.Sum(item => item.Minutes),
                ColorHex = group.First().ColorHex
            })
            .OrderByDescending(group => group.Minutes)
            .ToList();
        var maxTaskMinutes = Math.Max(taskGroups.FirstOrDefault()?.Minutes ?? 0, 1);

        TaskReportItems.Clear();
        foreach (var item in taskGroups)
        {
            TaskReportItems.Add(new ReportBarItem(item.Key, item.Minutes, item.ColorHex, item.Minutes * 260d / maxTaskMinutes));
        }

        var dailyGroups = filteredLogs
            .GroupBy(log => log.StartedAt.Date)
            .Select(group => new
            {
                Label = group.Key.ToString("dd MMM"),
                Minutes = group.Sum(item => item.Minutes)
            })
            .OrderBy(group => group.Label)
            .ToList();
        var maxDayMinutes = Math.Max(dailyGroups.FirstOrDefault()?.Minutes ?? 0, 1);

        DailyReportItems.Clear();
        foreach (var item in dailyGroups)
        {
            DailyReportItems.Add(new ReportBarItem(item.Label, item.Minutes, "#3B82F6", item.Minutes * 260d / maxDayMinutes));
        }
    }

    private void ApplyReportRangePreset()
    {
        if (SelectedReportRange == "Custom")
        {
            return;
        }

        var today = DateTime.Today;
        if (SelectedReportRange == "Last 30 Days")
        {
            _reportStartDate = today.AddDays(-29);
            _reportEndDate = today;
        }
        else if (SelectedReportRange == "This Year")
        {
            _reportStartDate = new DateTime(today.Year, 1, 1);
            _reportEndDate = today;
        }
        else
        {
            _reportStartDate = today.AddDays(-6);
            _reportEndDate = today;
        }

        OnPropertyChanged(nameof(ReportStartDate));
        OnPropertyChanged(nameof(ReportEndDate));
    }

    private int CalculateCurrentStreak(IReadOnlyList<DateTime> workedDays)
    {
        if (workedDays.Count == 0)
        {
            return 0;
        }

        var lastDay = workedDays[^1];
        if (lastDay.Date < DateTime.Today.AddDays(-1).Date)
        {
            return 0;
        }

        var streak = 1;
        for (var index = workedDays.Count - 1; index > 0; index--)
        {
            if (workedDays[index].Date.AddDays(-1) != workedDays[index - 1].Date)
            {
                break;
            }

            streak++;
        }

        return streak;
    }

    private IReadOnlyList<TaskItem> GetAllTasks() => [.. ActiveTasks, .. ArchivedTasks];

    private void NotifyCommandStateChanged()
    {
        _deleteTaskCommand.NotifyCanExecuteChanged();
        _toggleTaskStateCommand.NotifyCanExecuteChanged();
        _startPauseCommand.NotifyCanExecuteChanged();
        _stopCommand.NotifyCanExecuteChanged();
        _skipCommand.NotifyCanExecuteChanged();
    }
}

public enum NotificationAction
{
    None,
    StartBreak,
    StartNextPomodoro
}

public sealed class NotificationRequestEventArgs : EventArgs
{
    public NotificationRequestEventArgs(string title, string message, NotificationAction action)
    {
        Title = title;
        Message = message;
        Action = action;
    }

    public string Title { get; }

    public string Message { get; }

    public NotificationAction Action { get; }
}

public sealed record CalendarTaskEntryViewModel(string TaskTitle, int Minutes, string ColorHex)
{
    public string Display => $"{TaskTitle} · {Minutes}m";
}

public sealed class CalendarDayViewModel(DateTime date, bool isCurrentMonth, bool isToday, IReadOnlyList<CalendarTaskEntryViewModel> entries)
{
    public DateTime Date { get; } = date;
    public bool IsCurrentMonth { get; } = isCurrentMonth;
    public bool IsToday { get; } = isToday;
    public IReadOnlyList<CalendarTaskEntryViewModel> Entries { get; } = entries;
    public string DayLabel => Date.Day.ToString();
    public string BackgroundHex => IsToday ? "#DBEAFE" : IsCurrentMonth ? "#FFFFFF" : "#F9FAFB";
    public string DayTextColor => IsToday ? "#2563EB" : IsCurrentMonth ? "#111827" : "#9CA3AF";
    public string BorderColor => IsToday ? "#93C5FD" : "#F3F4F6";
}

public sealed class ReportBarItem(string label, int minutes, string colorHex, double barWidth)
{
    public string Label { get; } = label;
    public int Minutes { get; } = minutes;
    public string ColorHex { get; } = colorHex;
    public double BarWidth { get; } = Math.Max(barWidth, 12d);
    public string ValueLabel => TimeSpan.FromMinutes(Minutes).ToString(@"hh\:mm");
}
