using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace VSynthApp
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;
        private BlockViewModel draggingBlock = null;
        private Point clickOffset;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainViewModel();
            this.DataContext = viewModel;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && draggingBlock == null)
            {
                var pos = e.GetPosition((IInputElement)sender);
                viewModel.AddBlock(pos.X, pos.Y);
            }
        }

        private void Block_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border != null && border.DataContext is BlockViewModel block)
            {
                draggingBlock = block;
                clickOffset = e.GetPosition(border);
                border.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Block_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggingBlock != null && sender is FrameworkElement border)
            {
                var canvas = FindParent<Canvas>(border);
                if (canvas != null)
                {
                    var pos = e.GetPosition(canvas);
                    draggingBlock.X = pos.X - clickOffset.X;
                    draggingBlock.Y = pos.Y - clickOffset.Y;
                }
            }
        }

        private void Block_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (draggingBlock != null && sender is FrameworkElement border)
            {
                border.ReleaseMouseCapture();

                double snappedX = Math.Floor(draggingBlock.X / viewModel.SnapPixels) * viewModel.SnapPixels;
                double snappedY = Math.Floor(draggingBlock.Y / 60) * 60;

                if (snappedY > 360) snappedY = 360; 
                if (snappedY < 0) snappedY = 0;   
                if (snappedX < 0) snappedX = 0;

                draggingBlock.X = snappedX;
                draggingBlock.Y = snappedY;
                
                draggingBlock = null;
                e.Handled = true;
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
