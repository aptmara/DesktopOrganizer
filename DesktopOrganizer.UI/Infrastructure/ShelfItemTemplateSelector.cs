using System.Windows;
using System.Windows.Controls;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Infrastructure;

public class ShelfItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? FileItemTemplate { get; set; }
    public DataTemplate? MemoItemTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is ShelfItemViewModel vm)
        {
            // メモ型、またはメモコンテンツがある場合はメモテンプレートを使用
            if (vm.Type == DesktopOrganizer.Core.Models.ShelfItemType.Memo || vm.MemoContent != null)
            {
                return MemoItemTemplate;
            }
        }

        return FileItemTemplate;
    }
}
