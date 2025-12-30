using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Size = System.Windows.Size;
using Point = System.Windows.Point;

namespace DesktopOrganizer.UI.Controls
{
    public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {
        private TranslateTransform _trans = new TranslateTransform();
        private Size _extent = new Size(0, 0);
        private Size _viewport = new Size(0, 0);
        private Point _offset = new Point(0, 0);

        public VirtualizingWrapPanel()
        {
            // For use in the ItemsPanel of a ListBox, you allow the ScrollViewer to handle scrolling logic
            // via the IScrollInfo implementation.
        }

        /// <summary>
        /// アイテムの幅（マージン含む）。依存プロパティとして外部から設定可能。
        /// </summary>
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(
                nameof(ItemWidth),
                typeof(double),
                typeof(VirtualizingWrapPanel),
                new FrameworkPropertyMetadata(78.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        /// <summary>
        /// アイテムの高さ（マージン含む）。依存プロパティとして外部から設定可能。
        /// </summary>
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(
                nameof(ItemHeight),
                typeof(double),
                typeof(VirtualizingWrapPanel),
                new FrameworkPropertyMetadata(94.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        private void UpdateScrollInfo(Size availableSize, Size extent)
        {
            _viewport = availableSize;
            _extent = extent;
            ScrollOwner?.InvalidateScrollInfo();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            UpdateScrollInfo(availableSize, _extent);

            var itemsControl = ItemsControl.GetItemsOwner(this);
            int itemCount = itemsControl?.HasItems == true ? itemsControl.Items.Count : 0;

            if (itemCount == 0)
                return new Size(0, 0);

            double itemW = ItemWidth;
            double itemH = ItemHeight;

            int itemsPerRow = Math.Max(1, (int)(availableSize.Width / itemW));
            int rowCount = (int)Math.Ceiling((double)itemCount / itemsPerRow);
            double extentH = rowCount * itemH;

            Size extent = new Size(availableSize.Width, extentH);
            if (extent != _extent)
            {
                _extent = extent;
                UpdateScrollInfo(availableSize, extent);
            }

            int firstRow = (int)Math.Floor(_offset.Y / itemH);
            int lastRow = (int)Math.Ceiling((_offset.Y + availableSize.Height) / itemH);

            firstRow = Math.Max(0, firstRow);
            lastRow = Math.Min(rowCount - 1, lastRow);

            int firstIndex = firstRow * itemsPerRow;
            int lastIndex = Math.Min(itemCount - 1, ((lastRow + 1) * itemsPerRow) - 1);

            // 1. Cleanup first
            CleanUpItems(firstIndex, lastIndex);

            // 2. Generate
            IItemContainerGenerator generator = ItemContainerGenerator;
            GeneratorPosition startPos = generator.GeneratorPositionFromIndex(firstIndex);

            using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
            {
                for (int i = firstIndex; i <= lastIndex; i++)
                {
                    bool isNewlyRealized;
                    UIElement? child = generator.GenerateNext(out isNewlyRealized) as UIElement;
                    if (child == null) continue;

                    bool isChildInView = InternalChildren.Contains(child);

                    if (isNewlyRealized || !isChildInView)
                    {
                        if (i - firstIndex < InternalChildren.Count)
                            InsertInternalChild(i - firstIndex, child);
                        else
                            AddInternalChild(child);

                        if (isNewlyRealized)
                            generator.PrepareItemContainer(child);
                    }
                    else
                    {
                        // Child is already in view, but we need to ensure it's at the correct visual index
                        // This might be redundant if cleanup preserved order, but good for robustness
                    }

                    child.Measure(new Size(itemW, itemH));
                }
            }

            return extent;
        }

        private void CleanUpItems(int minIndex, int maxIndex)
        {
            // Iterate backwards to safely remove
            for (int i = InternalChildren.Count - 1; i >= 0; i--)
            {
                var child = InternalChildren[i];
                var itemIndex = ((ItemContainerGenerator)ItemContainerGenerator).IndexFromContainer(child);

                // If the item is outside the visible range, remove it from the visual tree.
                // This reduces the visual tree size significantly, improving performance.
                // Note: We are not implementing container recycling here (reusing objects), 
                // just virtualization (loading/unloading). This is sufficient for minimizing rendering cost.
                if (itemIndex < minIndex || itemIndex > maxIndex)
                {
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double itemW = ItemWidth;
            double itemH = ItemHeight;
            double availableW = finalSize.Width;
            int itemsPerRow = Math.Max(1, (int)(availableW / itemW));

            UpdateScrollInfo(finalSize, _extent);

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i];

                // We need the index of this child in the data source to position it.
                // Since InternalChildren only contains realized items, we can't just use `i`.
                // We need `ItemContainerGenerator.IndexFromContainer`.
                int itemIndex = ((ItemContainerGenerator)ItemContainerGenerator).IndexFromContainer(child);

                if (itemIndex < 0) continue;

                int row = itemIndex / itemsPerRow;
                int col = itemIndex % itemsPerRow;

                Rect curRect = new Rect(col * itemW, row * itemH, itemW, itemH);

                // Adjust for scrolling offset
                curRect.Offset(-_offset.X, -_offset.Y);

                child.Arrange(curRect);
            }

            return finalSize;
        }

        //
        // IScrollInfo members
        //

        public bool CanHorizontallyScroll { get; set; } = false;
        public bool CanVerticallyScroll { get; set; } = true;

        public double ExtentWidth => _extent.Width;
        public double ExtentHeight => _extent.Height;
        public double ViewportWidth => _viewport.Width;
        public double ViewportHeight => _viewport.Height;
        public double HorizontalOffset => _offset.X;
        public double VerticalOffset => _offset.Y;
        public ScrollViewer? ScrollOwner { get; set; }

        public void LineUp() => SetVerticalOffset(VerticalOffset - 10);
        public void LineDown() => SetVerticalOffset(VerticalOffset + 10);
        public void LineLeft() => SetHorizontalOffset(HorizontalOffset - 10);
        public void LineRight() => SetHorizontalOffset(HorizontalOffset + 10);
        public void PageUp() => SetVerticalOffset(VerticalOffset - _viewport.Height);
        public void PageDown() => SetVerticalOffset(VerticalOffset + _viewport.Height);
        public void PageLeft() => SetHorizontalOffset(HorizontalOffset - _viewport.Width);
        public void PageRight() => SetHorizontalOffset(HorizontalOffset + _viewport.Width);
        public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 30);
        public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 30);
        public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - 30);
        public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + 30);

        public void SetHorizontalOffset(double offset)
        {
            if (offset < 0 || _viewport.Width >= _extent.Width)
            {
                offset = 0;
            }
            else
            {
                if (offset + _viewport.Width >= _extent.Width)
                {
                    offset = _extent.Width - _viewport.Width;
                }
            }
            _offset.X = offset;
            if (ScrollOwner != null) ScrollOwner.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public void SetVerticalOffset(double offset)
        {
            if (offset < 0 || _viewport.Height >= _extent.Height)
            {
                offset = 0;
            }
            else
            {
                if (offset + _viewport.Height >= _extent.Height)
                {
                    offset = _extent.Height - _viewport.Height;
                }
            }
            _offset.Y = offset;
            if (ScrollOwner != null) ScrollOwner.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            // Simplified
            return rectangle;
        }

        // 
        // Cleanup Implementation
        // Override MeasureOverride again to include proper cleanup 
        //

    }
}
