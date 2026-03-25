using System.Windows;
using System.IO;

namespace VSynthApp
{
    public partial class RecordWindow : Window
    {
        private IAudioEngine engine;
        private string tempFile = "voice_input.wav";

        public RecordWindow(IAudioEngine engine)
        {
            InitializeComponent();
            this.engine = engine;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            engine.StartRecording(tempFile);
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            StatusText.Text = "Recording... Speak now!";
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            engine.StopRecording();
            StatusText.Text = "Processing letters...";
            
            // Allow some time for file to close
            System.Threading.Tasks.Task.Run(() => {
                System.Threading.Thread.Sleep(500);
                engine.SplitRecording(Path.Combine(engine.SamplePath, tempFile));
                Dispatcher.Invoke(() => {
                    this.DialogResult = true;
                    this.Close();
                });
            });
        }
    }
}
