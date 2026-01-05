using System.Windows.Threading;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// アナログ時計ウィジェットシェルのViewModel。
/// 時針・分針・秒針の角度をリアルタイムで更新する。
/// </summary>
public class AnalogClockShelfViewModel : ShelfViewModelBase
{
    private DispatcherTimer _timer;
    private double _hourAngle;
    private double _minuteAngle;
    private double _secondAngle;

    public AnalogClockShelfViewModel(Shelf model, Action? saveLayoutAction = null)
        : base(model, saveLayoutAction)
    {
        UpdateAngles();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100) // スムーズな秒針アニメーション
        };
        _timer.Tick += (s, e) => UpdateAngles();
        _timer.Start();
    }

    /// <summary>時針の角度 (0-360度)</summary>
    public double HourAngle
    {
        get => _hourAngle;
        private set { _hourAngle = value; OnPropertyChanged(); }
    }

    /// <summary>分針の角度 (0-360度)</summary>
    public double MinuteAngle
    {
        get => _minuteAngle;
        private set { _minuteAngle = value; OnPropertyChanged(); }
    }

    /// <summary>秒針の角度 (0-360度)</summary>
    public double SecondAngle
    {
        get => _secondAngle;
        private set { _secondAngle = value; OnPropertyChanged(); }
    }

    /// <summary>アナログ時計かどうかを示すフラグ（XAMLバインディング用）</summary>
    public bool IsAnalogClock => true;

    private void UpdateAngles()
    {
        var now = DateTime.Now;

        // 時針: 12時間で360度、分の影響も加味
        HourAngle = (now.Hour % 12) * 30 + now.Minute * 0.5;

        // 分針: 60分で360度、秒の影響も加味
        MinuteAngle = now.Minute * 6 + now.Second * 0.1;

        // 秒針: 60秒で360度（スムーズなアニメーション）
        SecondAngle = now.Second * 6 + now.Millisecond * 0.006;
    }

    public override void Dispose()
    {
        _timer.Stop();
        base.Dispose();
    }
}
