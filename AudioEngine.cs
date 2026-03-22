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

                // Fallback for remaining letters if 35 words were not spoken
                char fallbackLetter = 'A';
                for (int c = 0; c < 26; c++)
                {
                    char l = (char)(fallbackLetter + c);
                    if (!lettersCaptured.Contains(l) && lettersCaptured.Count > 0)
                    {
                        // Copy the last recorded as fallback if needed for safety
                        string letterFile = Path.Combine(SamplePath, $"{l}.wav");
                        SaveSegment(samplesArray, segments.Last().start, segments.Last().end, reader.WaveFormat, letterFile);
                    }
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
            
            var mixer = new NAudio.Wave.SampleProviders.MixingSampleProvider(NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            bool hasValidInput = false;

            foreach (var block in blocks.OrderBy(b => b.Position))
            {
                if (string.IsNullOrEmpty(block.Text) || block.Text.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;
                
                string word = block.Text.ToUpper();
                double wordLetterDelayMs = 150.0;
                
                for (int i = 0; i < word.Length; i++)
                {
                    char letter = word[i];
                    if (letter < 'A' || letter > 'Z') continue;
                    
                    string path = Path.Combine(SamplePath, $"{letter}.wav");
                    if (File.Exists(path))
                    {
                        double totalDelayMs = (block.Position * (1000.0 / 130.0)) + (i * wordLetterDelayMs);
                        TimeSpan delay = TimeSpan.FromMilliseconds(totalDelayMs);
                        
                        if (block.Pitches == null || block.Pitches.Count == 0)
                            block.Pitches = new List<float> { 1.0f }; // Fallback

                        foreach (var pitch in block.Pitches)
                        {
                            var reader = new AudioFileReader(path);
                            var pitchShifter = new PitchShiftingSampleProvider(reader) { PitchFactor = pitch };
                            
                            var resampler = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(pitchShifter, 44100);
                            var stereoProvider = resampler.WaveFormat.Channels == 1 
                                ? (ISampleProvider)new NAudio.Wave.SampleProviders.MonoToStereoSampleProvider(resampler) 
                                : resampler;
                                
                            var offsetProvider = new NAudio.Wave.SampleProviders.OffsetSampleProvider(stereoProvider);
                            offsetProvider.DelayBy = delay;
                            // Avoid holding the file locks open forever. The MixingSampleProvider will read everything 
                            // internally until it completes.
                            mixer.AddMixerInput(offsetProvider);
                            hasValidInput = true;
                        }
                    }
                }
            }

            if (!hasValidInput) return; // Nothing to export

            WaveFileWriter.CreateWaveFile16(wavPath, mixer);

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

            int channels = WaveFormat.Channels;
            int framesRead = 0;
            int framesToRead = count / channels;
            
            while (framesRead < framesToRead && ((int)sourcePosition) * channels < sourceBuffer.Length)
            {
                int baseIndex = ((int)sourcePosition) * channels;
                for (int c = 0; c < channels; c++)
                {
                    if (baseIndex + c < sourceBuffer.Length)
                    {
                        buffer[offset + framesRead * channels + c] = sourceBuffer[baseIndex + c];
                    }
                }
                sourcePosition += PitchFactor;
                framesRead++;
            }

            return framesRead * channels;
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
        public List<float> Pitches { get; set; }

    }
}
