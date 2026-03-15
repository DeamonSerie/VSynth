using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Windows;

namespace VSynthApp
{
    public partial class MainViewModel : ObservableObject
    {
        private AudioEngine audioEngine;

        [ObservableProperty]
        private string statusText = "Ready. Please record your voice sample.";

        public ObservableCollection<BlockViewModel> Blocks { get; } = new ObservableCollection<BlockViewModel>();

        public MainViewModel()
        {
            audioEngine = new AudioEngine();
            
            // Add some initial blocks for demo or empty slots
            for (int i = 0; i < 8; i++)
            {
                // Blocks.Add(new BlockViewModel(i * 130, 60 * 4)); // Default to C row
            }
        }

        [RelayCommand]
        private void Record()
        {
            var recordWindow = new RecordWindow(audioEngine);
            if (recordWindow.ShowDialog() == true)
            {
                StatusText = "Voice sample recorded and divided.";
                // Refresh voice samples in blocks if needed
            }
        }

        [RelayCommand]
        private void Play()
        {
            StatusText = "Playing sequence...";
            // Logic to play blocks sequentially
            _ = PlaySequence();
        }

        private async System.Threading.Tasks.Task PlaySequence()
        {
            var sortedBlocks = Blocks.OrderBy(b => b.X).ToList();
            foreach (var block in sortedBlocks)
            {
                if (block.SelectedVoice != null && block.SelectedVoice != "None")
                {
                    char letter = block.SelectedVoice[0];
                    var pitches = GetChordPitches(block.Y, block.SelectedModifier, block.SelectedChordType);
                    
                    foreach (var pitch in pitches)
                    {
                        audioEngine.PlayLetter(letter, pitch);
                    }
                }
                await System.Threading.Tasks.Task.Delay(1000); // 1 sec per block (tempo)
            }
            StatusText = "Playback finished.";
        }

        private List<float> GetChordPitches(double y, string modifier, string chordType)
        {
            float root = GetPitch(y, modifier);
            var pitches = new List<float> { root };

            // Very simplified chord intervals (semitones)
            // Major: 0, 4, 7
            // Minor: 0, 3, 7
            // 7th: 0, 4, 7, 10
            float semitone = 1.05946f;

            if (chordType == "Major" || chordType == "7th" || chordType == "Major7")
                pitches.Add(root * (float)Math.Pow(semitone, 4)); // Major 3rd
            else if (chordType == "Minor" || chordType == "Diminished")
                pitches.Add(root * (float)Math.Pow(semitone, 3)); // Minor 3rd
            else if (chordType == "Augmented")
                pitches.Add(root * (float)Math.Pow(semitone, 4));

            if (chordType == "Major" || chordType == "Minor" || chordType == "7th" || chordType == "Major7")
                pitches.Add(root * (float)Math.Pow(semitone, 7)); // Perfect 5th
            else if (chordType == "Diminished")
                pitches.Add(root * (float)Math.Pow(semitone, 6)); // Tritone
            else if (chordType == "Augmented")
                pitches.Add(root * (float)Math.Pow(semitone, 8)); // Sharp 5th

            if (chordType == "7th")
                pitches.Add(root * (float)Math.Pow(semitone, 10)); // Minor 7th
            else if (chordType == "Major7")
                pitches.Add(root * (float)Math.Pow(semitone, 11)); // Major 7th

            // Limit to max 4 notes as per requirement
            return pitches.Take(4).ToList();
        }

        private float GetPitch(double y, string modifier)
        {
            // Calculate pitch based on Row and Sharp/Flat
            // A=440Hz baseline
            // Rows are at 0, 60, 120, 180, 240, 300, 360
            // G, F, E, D, C, B, A
            int row = (int)(y / 60);
            float basePitch = 1.0f;
            switch(row)
            {
                case 0: basePitch = 1.5f; break; // G
                case 1: basePitch = 1.4f; break; // F
                case 2: basePitch = 1.25f; break; // E
                case 3: basePitch = 1.2f; break; // D
                case 4: basePitch = 1.0f; break; // C
                case 5: basePitch = 0.9f; break; // B
                case 6: basePitch = 0.8f; break; // A
            }

            if (modifier == "#") basePitch *= 1.059f;
            if (modifier == "##") basePitch *= 1.122f;
            if (modifier == "b") basePitch *= 0.944f;
            if (modifier == "bb") basePitch *= 0.891f;

            return basePitch;
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task Export()
        {
            StatusText = "Exporting MP3...";
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputPath = Path.Combine(desktopPath, "VSynthTrack.mp3");
            
            var blockData = Blocks.Select(b => new SequenceBlock
            {
                BaseNote = 'C', // Placeholder
                Y = b.Y,
                Modifier = b.SelectedModifier,
                ChordType = b.SelectedChordType,
                Text = b.SelectedVoice,
                Position = (int)b.X
            }).ToList();

            await audioEngine.ExportProject(outputPath, blockData);
            StatusText = $"Project exported to {outputPath}";
        }

        [RelayCommand]
        private void ExportWavs()
        {
            StatusText = "Exporting divided wavs...";
            string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "VSynthExport");
            if (!Directory.Exists(exportPath)) Directory.CreateDirectory(exportPath);
            
            foreach (var file in Directory.GetFiles(audioEngine.SamplePath, "*.wav"))
            {
                File.Copy(file, Path.Combine(exportPath, Path.GetFileName(file)), true);
            }
            StatusText = $"Wavs exported to {exportPath}";
        }

        public void AddBlock(double x, double y)
        {
            // Snap to grid
            double snappedX = Math.Floor(x / 130) * 130;
            double snappedY = Math.Floor(y / 60) * 60;
            
            if (snappedY > 360) snappedY = 360; // Max row label A
            if (snappedY < 0) snappedY = 0;   // Max row label G

            Blocks.Add(new BlockViewModel(snappedX, snappedY));
        }
    }

    public partial class BlockViewModel : ObservableObject
    {
        public double X { get; set; }
        public double Y { get; set; }

        public string[] Modifiers { get; } = { "Natural", "#", "##", "b", "bb" };
        
        [ObservableProperty]
        private string selectedModifier = "Natural";

        public string[] ChordTypes { get; } = { "Major", "Minor", "Diminished", "Augmented", "7th", "Major7" };

        [ObservableProperty]
        private string selectedChordType = "Major";

        public List<string> VoiceSamples { get; } = "None,A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z".Split(',').ToList();

        [ObservableProperty]
        private string selectedVoice = "A";

        public string[] VoiceModes { get; } = { "Letter", "Word" };

        [ObservableProperty]
        private string selectedVoiceMode = "Letter";

        public BlockViewModel(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
