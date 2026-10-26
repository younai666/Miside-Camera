using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Attributes;
using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Miside.Camera.Local;

namespace Miside.Camera.Streaming;

[BepInPlugin("dev.miside.camera.streaming", "Miside Camera Video", "0.2.0")]
public sealed class StreamPlugin : BasePlugin
{
    private GameStream? stream;
    public override void Load()
    {
        bool enabled = Config.Bind("Video", "Enabled", false, "Opt in to game capture. Desktop can also enable capture.").Value;
        string executable = Config.Bind("Video", "FFmpegPath", "", "Absolute path to a separately installed FFmpeg with h264_nvenc.").Value;
        stream = AddComponent<GameStream>();
        stream.Initialize(executable, message => Log.LogInfo(message), enabled);
    }
    public override bool Unload() { stream?.Stop(); if (stream != null) UnityEngine.Object.Destroy(stream); stream = null; return true; }
}

/// <summary>Only capture and hand off raw frames. FFmpeg owns encoding; MediaMTX owns transport.</summary>
public sealed class GameStream : MonoBehaviour
{
    public GameStream(IntPtr pointer) : base(pointer) { }
    private const int Fps = 30;
    private int width, height, frameBytes;
    private string executable = "";
    private Action<string>? log;
    private Process? encoder;
    private readonly CancellationTokenSource stop = new();
    private Task? writer;
    private byte[]? latest;
    private RenderTexture? screen;
    private AsyncGPUReadbackRequest request;
    private bool pending, stopped;
    private float nextCapture, nextRestart;
    private long frames, dropped;
    private long writeStarted;
    private float nextStatus;
    private LocalControlServer? desktop;
    private volatile bool desiredEnabled;
    private VideoState state = new(false, "idle");
    private string lastError = "";
    private long sampledFrames, sampledAt = Environment.TickCount64;
    private sealed record VideoState(bool Enabled, string Phase, int Width = 0, int Height = 0, double Fps = 0,
        long Frames = 0, long Dropped = 0, int EncoderPid = 0, string Error = "");

    public void Update()
    {
        // A publisher can stay alive with stdin blocked while its RTSP peer is gone.
        // Bound that specific stall so recovery does not depend on process exit.
        long writing = Interlocked.Read(ref writeStarted);
        if (writing != 0 && Environment.TickCount64 - writing > 4000 && encoder != null && !encoder.HasExited)
        {
            log?.Invoke("Video publisher stalled; restarting encoder");
            encoder.Kill();
        }
        if (Time.unscaledTime >= nextStatus)
        {
            nextStatus = Time.unscaledTime + 1;
            long now = Environment.TickCount64;
            double fps = (frames - sampledFrames) * 1000.0 / Math.Max(1, now - sampledAt);
            sampledAt = now; sampledFrames = frames;
            bool alive = encoder != null && !encoder.HasExited;
            Volatile.Write(ref state, new(desiredEnabled, !desiredEnabled ? "idle" : alive ? "publishing" : "recovering",
                width, height, Math.Round(fps, 1), frames, dropped, alive ? encoder!.Id : 0, Volatile.Read(ref lastError)));
        }
    }

    [HideFromIl2Cpp]
    public void Initialize(string path, Action<string> logger, bool enabled)
    {
        executable = path; log = logger;
        desiredEnabled = enabled;
        desktop = new LocalControlServer("Miside.Camera.Video.v1", () => Volatile.Read(ref state), operation => {
            if (operation == "enable") {
                if (!File.Exists(executable) || !Path.IsPathFullyQualified(executable)) throw new FormatException("Configure a valid FFmpeg path and restart the host");
                desiredEnabled = true;
            }
            else if (operation == "disable") desiredEnabled = false;
            else throw new FormatException("Unknown video operation");
        }, logger);
        if (enabled && (!File.Exists(path) || !Path.IsPathFullyQualified(path))) { desiredEnabled = false; lastError = "Configure the separate FFmpeg executable"; }
        if (!SystemInfo.supportsAsyncGPUReadback) throw new NotSupportedException("Asynchronous GPU capture is required.");
        BepInEx.Unity.IL2CPP.Utils.MonoBehaviourExtensions.StartCoroutine(this, Capture());
    }

    [HideFromIl2Cpp]
    private IEnumerator Capture()
    {
        var end = new WaitForEndOfFrame();
        while (!stopped)
        {
            yield return end;
            if (stopped) yield break;
            if (!desiredEnabled)
            {
                if (encoder != null || screen != null) PauseCapture();
                continue;
            }
            if (pending)
            {
                if (!request.done) continue;
                pending = false;
                if (request.hasError) { log?.Invoke("GPU readback failed; frame dropped"); continue; }
                var bytes = ArrayPool<byte>.Shared.Rent(frameBytes);
                Marshal.Copy(request.GetDataRaw(0), bytes, 0, frameBytes);
                var old = Interlocked.Exchange(ref latest, bytes);
                if (old != null) { ArrayPool<byte>.Shared.Return(old); dropped++; }
                frames++;
            }
            if (Screen.width < 2 || Screen.height < 2) continue;
            if (screen == null || width != Screen.width || height != Screen.height)
            {
                // A raw-video pipe has fixed geometry. Finish the old writer before changing it.
                if (encoder != null && !encoder.HasExited) encoder.Kill();
                if (writer != null && !writer.IsCompleted) continue;
                encoder?.Dispose(); encoder = null;
                var old = Interlocked.Exchange(ref latest, null);
                if (old != null) ArrayPool<byte>.Shared.Return(old);
                FreeTexture(screen);
                width = Screen.width; height = Screen.height; frameBytes = checked(width * height * 4);
                screen = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                screen.Create();
            }
            if (encoder == null || encoder.HasExited)
            {
                if (Time.unscaledTime >= nextRestart) { nextRestart = Time.unscaledTime + 2; StartEncoder(); }
                continue;
            }
            if (Time.unscaledTime < nextCapture) continue;
            nextCapture = Time.unscaledTime + 1f / Fps;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
            request = AsyncGPUReadback.Request(screen, 0, TextureFormat.RGBA32);
            pending = true;
        }
    }

    private void StartEncoder()
    {
        if (writer != null && !writer.IsCompleted) return;
        encoder?.Dispose();
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true };
        // Raw GPU frames can arrive below the target rate. Wall-clock PTS prevents the
        // receiver consuming a fictitious 30 fps timeline and periodically underrunning.
        string[] arguments = {
            "-hide_banner", "-loglevel", "warning", "-nostats",
            "-filter_threads", "1", "-thread_queue_size", "1", "-probesize", "32", "-analyzeduration", "0", "-fflags", "nobuffer",
            "-f", "rawvideo", "-pixel_format", "rgba", "-video_size", $"{width}x{height}",
            "-framerate", Fps.ToString(), "-use_wallclock_as_timestamps", "1", "-i", "pipe:0", "-an",
            // Preserve sRGB primaries/transfer. BT.601 YUV and limited range also round-trip
            // correctly through the emulator's decoder, whose BT.709/full-range path does not.
            // No resize: only pad an odd edge pixel and perform this RGB→YUV conversion.
            "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2,scale=in_range=full:out_range=limited:out_color_matrix=bt601:flags=accurate_rnd+full_chroma_int,format=nv12,setparams=range=limited:color_primaries=bt709:color_trc=iec61966-2-1:colorspace=smpte170m",
            "-color_range", "tv", "-colorspace", "smpte170m", "-color_primaries", "bt709", "-color_trc", "iec61966-2-1",
            "-fps_mode", "vfr", "-c:v", "h264_nvenc", "-profile:v", "high",
            "-preset", "p4", "-tune", "ull", "-zerolatency", "1", "-delay", "0", "-rc-lookahead", "0",
            "-rc", "vbr", "-cq", "17", "-b:v", "20M", "-maxrate", "30M", "-bufsize", "1M",
            "-spatial-aq", "1", "-aq-strength", "8",
            "-g", "15", "-bf", "0", "-flush_packets", "1", "-muxdelay", "0",
            // Local UDP publishing avoids the measured TCP publishing backlog; the relay
            // still serves Android over RTSP/TCP through the emulator NAT.
            "-f", "rtsp", "-rtsp_transport", "udp", "-pkt_size", "1200", "rtsp://127.0.0.1:8554/miside"
        };
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        encoder = Process.Start(start) ?? throw new IOException("FFmpeg failed to start");
        lastError = "";
        var process = encoder;
        int bytesPerFrame = frameBytes;
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) { Volatile.Write(ref lastError, e.Data); log?.Invoke("FFmpeg: " + e.Data); } };
        process.BeginErrorReadLine();
        writer = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested && !process.HasExited)
                {
                    var frame = Interlocked.Exchange(ref latest, null);
                    if (frame == null) { await Task.Delay(2, stop.Token); continue; }
                    try
                    {
                        Interlocked.Exchange(ref writeStarted, Environment.TickCount64);
                        await process.StandardInput.BaseStream.WriteAsync(frame.AsMemory(0, bytesPerFrame), stop.Token);
                    }
                    finally { Interlocked.Exchange(ref writeStarted, 0); ArrayPool<byte>.Shared.Return(frame); }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (IOException e) { log?.Invoke("Video encoder disconnected: " + e.Message); }
            finally { process.StandardInput.Close(); }
        });
        log?.Invoke($"Video capture → NVENC → local RTSP started ({width}x{height} native RGB, {Fps} fps target, BT.601 matrix / sRGB primaries and transfer / limited range, CQ17 / 20–30 Mbps)");
    }

    private static void FreeTexture(RenderTexture? texture) { if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); } }
    [HideFromIl2Cpp]
    private void PauseCapture()
    {
        if (encoder != null && !encoder.HasExited) encoder.Kill();
        writer?.Wait(1500);
        var frame = Interlocked.Exchange(ref latest, null); if (frame != null) ArrayPool<byte>.Shared.Return(frame);
        if (pending) { request.WaitForCompletion(); pending = false; }
        FreeTexture(screen); screen = null;
        encoder?.Dispose(); encoder = null;
    }
    public void OnApplicationQuit() => Stop();
    public void OnDestroy() => Stop();
    [HideFromIl2Cpp]
    public void Stop()
    {
        if (stopped) return;
        stopped = true; stop.Cancel();
        desktop?.Dispose(); desktop = null;
        PauseCapture();
        log?.Invoke($"Video stopped: captured={frames}, dropped={dropped}");
    }
}
