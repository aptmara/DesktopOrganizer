using System.Windows.Threading;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// 時計ウィジェットシェルのViewModel。
/// リアルタイムで現在時刻を表示する。
/// </summary>
public class ClockShelfViewModel : ShelfViewModelBase
{
    private DispatcherTimer _timer;
    private string _timeDisplay = string.Empty;
    private string _dateDisplay = string.Empty;

    public ClockShelfViewModel(Shelf model, Action? saveLayoutAction = null)
        : base(model, saveLayoutAction)
    {
        UpdateTime();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (s, e) => UpdateTime();
        _timer.Start();
    }

    public string TimeDisplay
    {
        get => _timeDisplay;
        private set { _timeDisplay = value; OnPropertyChanged(); }
    }

    public string DateDisplay
    {
        get => _dateDisplay;
        private set { _dateDisplay = value; OnPropertyChanged(); }
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        TimeDisplay = now.ToString("HH:mm:ss");
        DateDisplay = now.ToString("yyyy/MM/dd (ddd)");
    }

    public override void Dispose()
    {
        _timer.Stop();
        base.Dispose();
    }
}
