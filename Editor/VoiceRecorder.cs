using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace Antimatter
{
    public static class VoiceRecorder
    {
        private static AudioClip _recordingClip;
        private static bool _isRecording;
        private static string _micDevice;

        public static void StartRecording()
        {
            if (Microphone.devices.Length == 0)
                throw new Exception("No microphone devices found.");

            _micDevice = Microphone.devices[0];
            // Start recording: device, loop=false, length=60 seconds, frequency=16000
            _recordingClip = Microphone.Start(_micDevice, false, 60, 16000);
            _isRecording = true;
        }

        public static async Task<string> StopAndTranscribeAsync()
        {
            if (!_isRecording) return null;

            int lastPos = Microphone.GetPosition(_micDevice);
            Microphone.End(_micDevice);
            _isRecording = false;

            if (lastPos == 0) return null; // Nothing recorded

            string apiKey = AiProviderSettings.OpenAIKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("OpenAI API Key is required for Voice Transcription. Please set it in Settings.");

            // Extract the actual recorded data
            float[] samples = new float[lastPos];
            _recordingClip.GetData(samples, 0);

            // Convert to WAV
            byte[] wavData = EncodeAsWav(samples, _recordingClip.channels, _recordingClip.frequency);

            // Upload to Whisper
            return await TranscribeWithWhisperAsync(wavData, apiKey);
        }

        private static async Task<string> TranscribeWithWhisperAsync(byte[] wavData, string apiKey)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(wavData);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "file", "voice.wav");
            content.Add(new StringContent("whisper-1"), "model");

            var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Whisper API failed: ({response.StatusCode}) {responseText}");
            }

            var json = Newtonsoft.Json.Linq.JToken.Parse(responseText);
            return json["text"]?.ToString();
        }

        private static byte[] EncodeAsWav(float[] samples, int channels, int frequency)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);
            
            // Header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + samples.Length * 2);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(samples.Length * 2);

            // Payload
            int rescaleFactor = 32767;
            foreach (float sample in samples)
            {
                short data = (short)(sample * rescaleFactor);
                writer.Write(data);
            }

            return memoryStream.ToArray();
        }
    }
}
