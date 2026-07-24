using System.Text.Json;

namespace ScreenStudio.Core.Transcription;

/// <summary>A single transcript segment: text + start/end timestamps (ms).</summary>
public readonly record struct TranscriptSegment(double StartMs, double EndMs, string Text);

/// <summary>PRD P2 #6 — "Local speech-to-text; subtitle/caption generation; export transcripts."
/// This is the contract for local speech-to-text. The actual recognition engine (e.g.
/// Whisper.net or Windows.Media.SpeechRecognition) is pluggable; this provides the interface
/// + a simple JSON export format (SRT + raw JSON).</summary>
public sealed class TranscriptResult
{
    public List<TranscriptSegment> Segments { get; } = new();
    public string Language { get; set; } = "en";
    public double DurationMs { get; set; }

    /// <summary>Export as SRT subtitle format.</summary>
    public string ToSrt()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Segments.Count; i++)
        {
            var seg = Segments[i];
            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatSrtTime(seg.StartMs)} --> {FormatSrtTime(seg.EndMs)}");
            sb.AppendLine(seg.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Export as raw JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    private static string FormatSrtTime(double ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
    }
}

/// <summary>Interface for a speech-to-text engine. Implementations wrap Whisper.net,
/// Windows.Media.SpeechRecognition, or any local STT provider.</summary>
public interface ITranscriptionEngine
{
    /// <summary>Transcribe audio samples to text segments with timestamps.</summary>
    TranscriptResult Transcribe(float[] samples, int sampleRate, CancellationToken ct = default);
}

/// <summary>Stub transcription engine that produces empty results. Real implementation
/// would call a local STT model (Whisper.net is the recommended path — it's a .NET binding
/// for OpenAI's Whisper model, runs fully locally, no cloud dependency).</summary>
public sealed class StubTranscriptionEngine : ITranscriptionEngine
{
    public TranscriptResult Transcribe(float[] samples, int sampleRate, CancellationToken ct = default)
    {
        return new TranscriptResult
        {
            DurationMs = sampleRate > 0 ? (double)samples.Length / sampleRate * 1000 : 0,
            Language = "en",
        };
    }
}
