using System.Windows;
using System.Windows.Input;

namespace VSynthApp
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainViewModel();
            this.DataContext = viewModel;

            // Handle clicking on the ItemsControl's Canvas to add blocks
            // This is a bit tricky with ItemsControl, so we'll handle it on the Canvas itself
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition((IInputElement)sender);
                viewModel.AddBlock(pos.X, pos.Y);
            }
        }
    }
}