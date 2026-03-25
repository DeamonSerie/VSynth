using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSynthApp
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IAudioEngine audioEngine;
        private readonly IPhonemeParser phonemeParser;
        private readonly IProjectStorage projectStorage;
        private readonly IPathProvider pathProvider;
        private CancellationTokenSource? playbackCancellation;

        [ObservableProperty]
        private string statusText = "Ready. Please record your voice sample.";

        [ObservableProperty]
        private double playheadX;

        [ObservableProperty]
        private bool isPlaying;

        [ObservableProperty]
        private int bpm = 130;

        [ObservableProperty]
        private int snapPixels = 130;

        public ObservableCollection<BlockViewModel> Blocks { get; } = new();

        public MainViewModel()
            : this(new AudioEngine(), new SimplePhonemeParser(), new JsonProjectStorage(), new DesktopPathProvider())
        {
        }

        public MainViewModel(IAudioEngine audioEngine, IPhonemeParser phonemeParser, IProjectStorage projectStorage, IPathProvider pathProvider)
        {
            this.audioEngine = audioEngine;
            this.phonemeParser = phonemeParser;
            this.projectStorage = projectStorage;
            this.pathProvider = pathProvider;
        }

        [RelayCommand]
        private void Record()
        {
            var recordWindow = new RecordWindow(audioEngine);
            if (recordWindow.ShowDialog() == true)
            {
                StatusText = "Voice sample recorded and divided.";
            }
        }

        [RelayCommand]
        private void Play()
        {
            if (IsPlaying) return;
            StatusText = "Playing sequence...";
            playbackCancellation = new CancellationTokenSource();
            _ = PlaySequence(playbackCancellation.Token);
        }

        [RelayCommand]
        private void Stop()
        {
            playbackCancellation?.Cancel();
            IsPlaying = false;
            PlayheadX = 0;
            StatusText = "Playback stopped.";
        }

        private async Task PlaySequence(CancellationToken cancellationToken)
        {
            IsPlaying = true;
            PlayheadX = 0;
            var sortedBlocks = Blocks.OrderBy(b => b.X).ToList();
            if (sortedBlocks.Count == 0)
            {
                IsPlaying = false;
                StatusText = "No blocks to play.";
                return;
            }

            double currentX = 0;
            double lastBlockX = sortedBlocks.Last().X + sortedBlocks.Last().DurationPixels;

            var playheadTask = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && PlayheadX < lastBlockX)
                {
                    await Task.Delay(16, cancellationToken);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PlayheadX += PixelsPerMs() * 16;
                    });
                }
            }, cancellationToken);

            try
            {
                foreach (var block in sortedBlocks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (block.X > currentX)
                    {
                        int delayMs = (int)((block.X - currentX) / PixelsPerMs());
                        await Task.Delay(delayMs, cancellationToken);
                        currentX = block.X;
                    }

                    var letters = phonemeParser.ParseToLetters(block.SelectedVoice);
                    if (letters.Count == 0) continue;

                    var pitches = GetChordPitches(block.Y, block.SelectedModifier, block.SelectedChordType);
                    foreach (char letter in letters)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        foreach (var pitch in pitches)
                        {
                            audioEngine.PlayLetter(letter, pitch);
                        }

                        await Task.Delay(150, cancellationToken);
                    }
                }

                StatusText = "Playback finished.";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Playback cancelled.";
            }
            finally
            {
                IsPlaying = false;
                await SafeAwait(playheadTask);
            }
        }

        private static async Task SafeAwait(Task task)
        {
            try
            {
                await task;
            }
            catch
            {
            }
        }

        private double PixelsPerMs()
        {
            if (Bpm <= 0) return 0.13;
            // 130 px = quarter note
            return SnapPixels / (60000.0 / Bpm);
        }

        private List<float> GetChordPitches(double y, string modifier, string chordType)
        {
            float root = GetPitch(y, modifier);
            var pitches = new List<float> { root };
            float semitone = 1.05946f;

            if (chordType == "Major" || chordType == "7th" || chordType == "Major7")
                pitches.Add(root * (float)Math.Pow(semitone, 4));
            else if (chordType == "Minor" || chordType == "Diminished")
                pitches.Add(root * (float)Math.Pow(semitone, 3));
            else if (chordType == "Augmented")
                pitches.Add(root * (float)Math.Pow(semitone, 4));

            if (chordType == "Major" || chordType == "Minor" || chordType == "7th" || chordType == "Major7")
                pitches.Add(root * (float)Math.Pow(semitone, 7));
            else if (chordType == "Diminished")
                pitches.Add(root * (float)Math.Pow(semitone, 6));
            else if (chordType == "Augmented")
                pitches.Add(root * (float)Math.Pow(semitone, 8));

            if (chordType == "7th")
                pitches.Add(root * (float)Math.Pow(semitone, 10));
            else if (chordType == "Major7")
                pitches.Add(root * (float)Math.Pow(semitone, 11));

            return pitches.Take(4).ToList();
        }

        private float GetPitch(double y, string modifier)
        {
            int row = (int)(y / 60);
            float basePitch = row switch
            {
                0 => 1.5f,
                1 => 1.4f,
                2 => 1.25f,
                3 => 1.2f,
                4 => 1.0f,
                5 => 0.9f,
                6 => 0.8f,
                _ => 1.0f
            };

            if (modifier == "#") basePitch *= 1.059f;
            if (modifier == "##") basePitch *= 1.122f;
            if (modifier == "b") basePitch *= 0.944f;
            if (modifier == "bb") basePitch *= 0.891f;

            return basePitch;
        }

        [RelayCommand]
        private async Task Export()
        {
            StatusText = "Exporting MP3...";
            string outputPath = pathProvider.GetDefaultExportPath();

            var blockData = Blocks.Select(b => new SequenceBlock
            {
                BaseNote = 'C',
                Y = b.Y,
                Modifier = b.SelectedModifier,
                ChordType = b.SelectedChordType,
                Text = b.SelectedVoice,
                Position = (int)b.X,
                Pitches = GetChordPitches(b.Y, b.SelectedModifier, b.SelectedChordType)
            }).ToList();

            await audioEngine.ExportProject(outputPath, blockData, phonemeParser);
            StatusText = $"Project exported to {outputPath}";
        }

        public void AddBlock(double x, double y)
        {
            double snappedX = Math.Floor(x / SnapPixels) * SnapPixels;
            double snappedY = Math.Floor(y / 60) * 60;

            if (snappedY > 360) snappedY = 360;
            if (snappedY < 0) snappedY = 0;

            Blocks.Add(new BlockViewModel(snappedX, snappedY));
        }

        [RelayCommand]
        private void RemoveBlock(BlockViewModel block)
        {
            Blocks.Remove(block);
        }

        [RelayCommand]
        private void Clear()
        {
            Blocks.Clear();
            StatusText = "Sequence cleared.";
        }

        [RelayCommand]
        private async Task Save()
        {
            StatusText = "Saving Project...";
            string path = pathProvider.GetDefaultProjectPath();
            await projectStorage.SaveAsync(path, Blocks.ToList());
            StatusText = $"Project saved to {path}";
        }

        [RelayCommand]
        private async Task Load()
        {
            StatusText = "Loading Project...";
            string path = pathProvider.GetDefaultProjectPath();
            if (!File.Exists(path))
            {
                StatusText = "Project file not found.";
                return;
            }

            try
            {
                var loadedBlocks = await projectStorage.LoadAsync(path);
                Blocks.Clear();
                foreach (var b in loadedBlocks) Blocks.Add(b);
                StatusText = $"Project loaded from {path}";
            }
            catch
            {
                StatusText = "Failed to load project file.";
            }
        }
    }

    public partial class BlockViewModel : ObservableObject
    {
        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        [ObservableProperty]
        private double durationPixels = 130;

        public string[] Modifiers { get; } = { "Natural", "#", "##", "b", "bb" };

        [ObservableProperty]
        private string selectedModifier = "Natural";

        public string[] ChordTypes { get; } = { "Major", "Minor", "Diminished", "Augmented", "7th", "Major7" };

        [ObservableProperty]
        private string selectedChordType = "Major";

        public List<string> VoiceSamples { get; } = "None,A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z".Split(',').ToList();

        [ObservableProperty]
        private string selectedVoice = "A";

        public BlockViewModel()
        {
        }

        public BlockViewModel(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
