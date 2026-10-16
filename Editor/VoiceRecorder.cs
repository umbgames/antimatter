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
        
        public static event Action<string> OnTranscriptionUpdated;
        public static event Action<string> OnTranscriptionFinished;

        private static double _lastApiCallTime;
        private static string _currentTranscription = "";
        private static int _currentRequestId = 0;

        public static void StartRecording()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[Antimatter Voice] No microphone devices found.");
                throw new Exception("No microphone devices found.");
            }

            _micDevice = Microphone.devices[0];
            Debug.Log($"[Antimatter Voice] Starting recording on: {_micDevice}");
            _recordingClip = Microphone.Start(_micDevice, false, 60, 16000);
            _isRecording = true;
            _currentTranscription = "";
            _lastApiCallTime = EditorApplication.timeSinceStartup;
            _currentRequestId = 0;

            EditorApplication.update += EditorUpdate;
        }

        public static void StopRecording()
        {
            if (!_isRecording) return;
            
            _isRecording = false;
            EditorApplication.update -= EditorUpdate;
            
            int lastPos = Microphone.GetPosition(_micDevice);
            Microphone.End(_micDevice);
            
            Debug.Log($"[Antimatter Voice] Stopped recording. Final position: {lastPos}");

            if (lastPos > 0)
            {
                _ = FinalizeTranscriptionAsync(lastPos);
            }
            else
            {
                OnTranscriptionFinished?.Invoke(_currentTranscription);
            }
        }

        private static void EditorUpdate()
        {
            if (!_isRecording) return;

            if (EditorApplication.timeSinceStartup - _lastApiCallTime > 1.5)
            {
                _lastApiCallTime = EditorApplication.timeSinceStartup;
                
                int currentPos = Microphone.GetPosition(_micDevice);
                
                if (currentPos > 8000)
                {
                    _ = TranscribeChunkAsync(currentPos);
                }
            }
        }

        private static async Task TranscribeChunkAsync(int length)
        {
            int reqId = ++_currentRequestId;
            float[] samples = new float[length];
            _recordingClip.GetData(samples, 0);

            if (IsSilent(samples)) 
            {
                Debug.Log("[Antimatter Voice] Chunk was silent. Skipping.");
                return;
            }

            samples = StripTrailingSilence(samples);
            Debug.Log($"[Antimatter Voice] Sending chunk {reqId} of size {samples.Length} to Whisper...");
            byte[] wavData = EncodeAsWav(samples, _recordingClip.channels, _recordingClip.frequency);
            
            try
            {
                string text = await SendToWhisper(wavData);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (reqId < _currentRequestId) 
                    {
                        Debug.Log($"[Antimatter Voice] Discarding stale chunk {reqId}.");
                        return; // Discard out-of-order response
                    }

                    Debug.Log($"[Antimatter Voice] Partial Transcript {reqId}: {text}");
                    _currentTranscription = text;
                    OnTranscriptionUpdated?.Invoke(text);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Antimatter Voice] Stream chunk error: {ex.Message}");
            }
        }

        private static async Task FinalizeTranscriptionAsync(int length)
        {
            int reqId = ++_currentRequestId;
            float[] samples = new float[length];
            _recordingClip.GetData(samples, 0);

            if (!IsSilent(samples))
            {
                samples = StripTrailingSilence(samples);
                Debug.Log($"[Antimatter Voice] Sending FINAL chunk {reqId} (size {samples.Length}) to Whisper...");
                byte[] wavData = EncodeAsWav(samples, _recordingClip.channels, _recordingClip.frequency);
                try
                {
                    string text = await SendToWhisper(wavData);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Debug.Log($"[Antimatter Voice] Final Transcript {reqId}: {text}");
                        _currentTranscription = text;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[Antimatter Voice] Whisper API Error: " + e.Message);
                }
            }
            else
            {
                Debug.Log("[Antimatter Voice] Final chunk was silent.");
            }
            
            OnTranscriptionFinished?.Invoke(_currentTranscription);
        }

        private static bool IsSilent(float[] samples)
        {
            float max = 0;
            foreach (var s in samples)
            {
                if (Mathf.Abs(s) > max) max = Mathf.Abs(s);
            }
            return max < 0.005f; 
        }

        private static float[] StripTrailingSilence(float[] samples)
        {
            int lastNonSilent = samples.Length - 1;
            for (int i = samples.Length - 1; i >= 0; i--)
            {
                if (Mathf.Abs(samples[i]) > 0.005f)
                {
                    lastNonSilent = i;
                    break;
                }
            }
            
            // Keep up to 0.25 seconds of silence padding at the end (4000 samples at 16khz)
            int pad = 4000;
            int newLength = Mathf.Min(samples.Length, lastNonSilent + pad);
            if (newLength == samples.Length) return samples;
            
            float[] stripped = new float[newLength];
            Array.Copy(samples, stripped, newLength);
            return stripped;
        }

        private static async Task<string> SendToWhisper(byte[] wavData)
        {
            string token = MarketplaceSettings.CurrentToken;
            if (string.IsNullOrWhiteSpace(token)) 
            {
                Debug.LogError("[Antimatter Voice] Not logged in. Cannot transcribe voice.");
                return null;
            }

            string endpoint = "https://api.umbtechnologies.com/v1/unity/transcribe";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var base64Wav = Convert.ToBase64String(wavData);
            var jsonBody = "{\"file\":\"" + base64Wav + "\"}";
            using var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(endpoint, content);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[Antimatter Voice] Backend Error ({response.StatusCode}): {responseText}");
                return null;
            }

            try
            {
                var json = Newtonsoft.Json.Linq.JToken.Parse(responseText);
                return json["text"]?.ToString() ?? json["data"]?["text"]?.ToString() ?? responseText;
            }
            catch
            {
                return responseText;
            }
        }

        private static byte[] EncodeAsWav(float[] samples, int channels, int frequency)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);
            
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);

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
