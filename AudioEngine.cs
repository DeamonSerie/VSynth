using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VSynthApp
{
    public interface IAudioEngine
    {
        string SamplePath { get; }
        void StartRecording(string fileName);
        void StopRecording();
        void SplitRecording(string sourceFile);
        void PlayLetter(char letter, float pitch = 1.0f);
        Task ExportProject(string outputPath, List<SequenceBlock> blocks, IPhonemeParser phonemeParser);
    }

    public class AudioEngine : IAudioEngine
    {
        private WasapiCapture? capture;
        private WaveFileWriter? writer;
        private string tempFile = string.Empty;
        public string SamplePath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");

        public AudioEngine()
        {
            if (!Directory.Exists(SamplePath))
                Directory.CreateDirectory(SamplePath);
        }

        public void StartRecording(string fileName)
        {
            tempFile = Path.Combine(SamplePath, fileName);
            capture = new WasapiCapture();
            writer = new WaveFileWriter(tempFile, capture.WaveFormat);

            capture.DataAvailable += (s, e) => writer?.Write(e.Buffer, 0, e.BytesRecorded);
            capture.RecordingStopped += (s, e) =>
            {
                writer?.Dispose();
                writer = null;
                capture?.Dispose();
                capture = null;
            };

            capture.StartRecording();
        }

        public void StopRecording()
        {
            capture?.StopRecording();
        }

        public void SplitRecording(string sourceFile)
        {
            using var reader = new AudioFileReader(sourceFile);
            var samples = new List<float>();
            float[] buffer = new float[reader.WaveFormat.SampleRate / 10];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                samples.AddRange(buffer.Take(read));
            }

            var samplesArray = samples.ToArray();
            var segments = DetectSegments(samplesArray, reader.WaveFormat.SampleRate);
            if (segments.Count == 0) return;

            string pangram = "THEQUICKBROWNFOXJUMPSOVERTHELAZYDOG";
            var lettersCaptured = new HashSet<char>();

            int pIndex = 0;
            for (int i = 0; i < segments.Count && pIndex < pangram.Length; i++)
            {
                char letter = pangram[pIndex];
                if (!lettersCaptured.Contains(letter))
                {
                    string letterFile = Path.Combine(SamplePath, $"{letter}.wav");
                    SaveSegment(samplesArray, segments[i].start, segments[i].end, reader.WaveFormat, letterFile);
                    lettersCaptured.Add(letter);
                }
                pIndex++;
            }

            for (int c = 0; c < 26; c++)
            {
                char letter = (char)('A' + c);
                if (!lettersCaptured.Contains(letter) && lettersCaptured.Count > 0)
                {
                    string letterFile = Path.Combine(SamplePath, $"{letter}.wav");
                    SaveSegment(samplesArray, segments[^1].start, segments[^1].end, reader.WaveFormat, letterFile);
                }
            }
        }

        private static List<(int start, int end)> DetectSegments(float[] samples, int sampleRate)
        {
            var segments = new List<(int start, int end)>();
            float threshold = 0.02f;
            int minSilenceLength = sampleRate / 5;
            int minSegmentLength = sampleRate / 10;

            bool inSegment = false;
            int segmentStart = 0;
            int silenceCount = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > threshold)
                {
                    if (!inSegment)
                    {
                        inSegment = true;
                        segmentStart = i;
                    }
                    silenceCount = 0;
                }
                else if (inSegment)
                {
                    silenceCount++;
                    if (silenceCount > minSilenceLength)
                    {
                        if (i - segmentStart - silenceCount > minSegmentLength)
                            segments.Add((segmentStart, i - silenceCount));
                        inSegment = false;
                    }
                }
            }

            if (inSegment && samples.Length - segmentStart > minSegmentLength)
                segments.Add((segmentStart, samples.Length));

            return segments;
        }

        private static void SaveSegment(float[] samples, int start, int end, WaveFormat format, string path)
        {
            using var waveWriter = new WaveFileWriter(path, format);
            waveWriter.WriteSamples(samples, start, end - start);
        }

        public void PlayLetter(char letter, float pitch = 1.0f)
        {
            string path = Path.Combine(SamplePath, $"{char.ToUpperInvariant(letter)}.wav");
            if (!File.Exists(path)) return;

            var reader = new AudioFileReader(path);
            var pitchProvider = new PitchShiftingSampleProvider(reader.ToSampleProvider()) { PitchFactor = pitch };

            var output = new WasapiOut();
            output.Init(pitchProvider);
            output.Play();

            output.PlaybackStopped += (s, e) =>
            {
                output.Dispose();
                reader.Dispose();
            };
        }

        public async Task ExportProject(string outputPath, List<SequenceBlock> blocks, IPhonemeParser phonemeParser)
        {
            string wavPath = outputPath.Replace(".mp3", ".wav", StringComparison.OrdinalIgnoreCase);
            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
            {
                ReadFully = true
            };

            bool hasValidInput = false;
            var openReaders = new List<IDisposable>();

            try
            {
                foreach (var block in blocks.OrderBy(b => b.Position))
                {
                    var letters = phonemeParser.ParseToLetters(block.Text ?? string.Empty);
                    if (letters.Count == 0) continue;

                    var pitches = (block.Pitches == null || block.Pitches.Count == 0)
                        ? new List<float> { 1.0f }
                        : block.Pitches;

                    for (int i = 0; i < letters.Count; i++)
                    {
                        string path = Path.Combine(SamplePath, $"{letters[i]}.wav");
                        if (!File.Exists(path)) continue;

                        double totalDelayMs = (block.Position * (1000.0 / 130.0)) + (i * 150.0);
                        TimeSpan delay = TimeSpan.FromMilliseconds(totalDelayMs);

                        foreach (float pitch in pitches)
                        {
                            var reader = new AudioFileReader(path);
                            openReaders.Add(reader);

                            ISampleProvider provider = new PitchShiftingSampleProvider(reader.ToSampleProvider()) { PitchFactor = pitch };
                            provider = new WdlResamplingSampleProvider(provider, 44100);
                            if (provider.WaveFormat.Channels == 1)
                                provider = new MonoToStereoSampleProvider(provider);

                            var offsetProvider = new OffsetSampleProvider(provider) { DelayBy = delay };
                            mixer.AddMixerInput(offsetProvider);
                            hasValidInput = true;
                        }
                    }
                }

                if (!hasValidInput) return;

                WaveFileWriter.CreateWaveFile16(wavPath, mixer);

                if (outputPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    await Task.Run(() => WaveToMp3(wavPath, outputPath));
            }
            finally
            {
                foreach (var reader in openReaders)
                    reader.Dispose();
            }
        }

        private static void WaveToMp3(string waveFile, string mp3File)
        {
            using var reader = new AudioFileReader(waveFile);
            using var writer = new LameMP3FileWriter(mp3File, reader.WaveFormat, LAMEPreset.STANDARD);
            reader.CopyTo(writer);
        }
    }

    public class PitchShiftingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private float[]? sourceBuffer;
        private double sourcePosition;
        public float PitchFactor { get; set; } = 1.0f;

        public PitchShiftingSampleProvider(ISampleProvider source)
        {
            this.source = source;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            EnsureBuffer();
            if (sourceBuffer == null || sourceBuffer.Length == 0) return 0;

            int channels = WaveFormat.Channels;
            int framesRead = 0;
            int framesToRead = count / channels;

            while (framesRead < framesToRead)
            {
                int frameIndex = (int)sourcePosition;
                int nextFrameIndex = frameIndex + 1;
                double frac = sourcePosition - frameIndex;

                int baseIndex = frameIndex * channels;
                int nextBaseIndex = nextFrameIndex * channels;
                if (nextBaseIndex + (channels - 1) >= sourceBuffer.Length) break;

                for (int c = 0; c < channels; c++)
                {
                    float a = sourceBuffer[baseIndex + c];
                    float b = sourceBuffer[nextBaseIndex + c];
                    buffer[offset + framesRead * channels + c] = (float)(a + ((b - a) * frac));
                }

                sourcePosition += PitchFactor;
                framesRead++;
            }

            return framesRead * channels;
        }

        private void EnsureBuffer()
        {
            if (sourceBuffer != null) return;

            var samples = new List<float>();
            float[] readBuffer = new float[WaveFormat.SampleRate * WaveFormat.Channels];
            int read;
            while ((read = source.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                samples.AddRange(readBuffer.Take(read));
            }

            sourceBuffer = samples.ToArray();
        }
    }

    public class SequenceBlock
    {
        public char BaseNote { get; set; }
        public double Y { get; set; }
        public string Modifier { get; set; } = "Natural";
        public string ChordType { get; set; } = "Major";
        public string Text { get; set; } = string.Empty;
        public int Position { get; set; }
        public List<float> Pitches { get; set; } = new();
    }
}
