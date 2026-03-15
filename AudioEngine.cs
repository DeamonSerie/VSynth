using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Lame;

namespace VSynthApp
{
    public class AudioEngine
    {
        private WasapiCapture capture;
        private WaveFileWriter writer;
        private string tempFile;
        public string SamplePath { get; private set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");

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

            capture.DataAvailable += (s, e) =>
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
            };

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
            using (var reader = new AudioFileReader(sourceFile))
            {
                var samples = new List<float>();
                float[] buffer = new float[reader.WaveFormat.SampleRate / 10]; // 100ms
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    samples.AddRange(buffer.Take(read));
                }

                var samplesArray = samples.ToArray();
                var segments = DetectSegments(samplesArray, reader.WaveFormat.SampleRate);
                
                // If we didn't get 26, maybe try splitting the longest ones or just use what we have
                char currentLetter = 'A';
                for (int i = 0; i < 26; i++)
                {
                    string letterFile = Path.Combine(SamplePath, $"{currentLetter}.wav");
                    if (i < segments.Count)
                    {
                        SaveSegment(samplesArray, segments[i].start, segments[i].end, reader.WaveFormat, letterFile);
                    }
                    else if (segments.Count > 0)
                    {
                        // Fallback: Copy the last segment if we run out
                        SaveSegment(samplesArray, segments.Last().start, segments.Last().end, reader.WaveFormat, letterFile);
                    }
                    currentLetter++;
                }
            }
        }

        private List<(int start, int end)> DetectSegments(float[] samples, int sampleRate)
        {
            var segments = new List<(int start, int end)>();
            float threshold = 0.02f; // Adjust as needed
            int minSilenceLength = sampleRate / 5; // 200ms
            int minSegmentLength = sampleRate / 10; // 100ms

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
                else
                {
                    if (inSegment)
                    {
                        silenceCount++;
                        if (silenceCount > minSilenceLength)
                        {
                            if (i - segmentStart - silenceCount > minSegmentLength)
                            {
                                segments.Add((segmentStart, i - silenceCount));
                            }
                            inSegment = false;
                        }
                    }
                }
            }

            if (inSegment && samples.Length - segmentStart > minSegmentLength)
            {
                segments.Add((segmentStart, samples.Length));
            }

            return segments;
        }

        private void SaveSegment(float[] samples, int start, int end, WaveFormat format, string path)
        {
            using (var writer = new WaveFileWriter(path, format))
            {
                writer.WriteSamples(samples, start, end - start);
            }
        }

        public void PlayLetter(char letter, float pitch = 1.0f)
        {
            string path = Path.Combine(SamplePath, $"{letter.ToString().ToUpper()}.wav");
            if (!File.Exists(path)) return;

            var reader = new AudioFileReader(path);
            var sampleProvider = new PitchShiftingSampleProvider(reader.ToSampleProvider());
            sampleProvider.PitchFactor = pitch;

            var output = new WasapiOut();
            output.Init(sampleProvider);
            output.Play();
            
            // Auto dispose after play
            output.PlaybackStopped += (s, e) => {
                output.Dispose();
                reader.Dispose();
            };
        }

        public async Task ExportProject(string outputPath, List<SequenceBlock> blocks)
        {
            string wavPath = outputPath.Replace(".mp3", ".wav");
            
            // Create a simple mixed wav file
            // For a production app, we'd mix samples at specific offsets
            // Here we just concatenate for demonstration
            using (var waveWriter = new WaveFileWriter(wavPath, new WaveFormat(44100, 16, 2)))
            {
                foreach (var block in blocks.OrderBy(b => b.Position))
                {
                    string path = Path.Combine(SamplePath, $"{block.Text}.wav");
                    if (File.Exists(path))
                    {
                        using (var reader = new AudioFileReader(path))
                        {
                            var buffer = new float[reader.WaveFormat.SampleRate];
                            int read;
                            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                waveWriter.WriteSamples(buffer, 0, read);
                            }
                        }
                    }
                }
            }

            if (outputPath.EndsWith(".mp3"))
            {
                await Task.Run(() => WaveToMp3(wavPath, outputPath));
            }
        }

        private void WaveToMp3(string waveFile, string mp3File)
        {
            using (var reader = new AudioFileReader(waveFile))
            using (var writer = new LameMP3FileWriter(mp3File, reader.WaveFormat, LAMEPreset.STANDARD))
            {
                reader.CopyTo(writer);
            }
        }
    }

    public class PitchShiftingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private float[] sourceBuffer;
        private double sourcePosition;
        public float PitchFactor { get; set; } = 1.0f;

        public PitchShiftingSampleProvider(ISampleProvider source)
        {
            this.source = source;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            if (sourceBuffer == null)
            {
                // Read the whole source into memory for simplicity in this demo
                var samples = new List<float>();
                float[] readBuffer = new float[WaveFormat.SampleRate];
                int read;
                while ((read = source.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    samples.AddRange(readBuffer.Take(read));
                }
                sourceBuffer = samples.ToArray();
            }

            int samplesRead = 0;
            while (samplesRead < count && sourcePosition < sourceBuffer.Length)
            {
                buffer[offset + samplesRead] = sourceBuffer[(int)sourcePosition];
                sourcePosition += PitchFactor;
                samplesRead++;
            }

            return samplesRead;
        }
    }

    public class SequenceBlock
    {
        public char BaseNote { get; set; } // A-G
        public double Y { get; set; }
        public string Modifier { get; set; } // #, b, ##, bb
        public string ChordType { get; set; }
        public string Text { get; set; } // Word or Letter
        public int Position { get; set; } // X coordinate
    }
}
