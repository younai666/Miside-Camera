package dev.miside.camera

import android.os.SystemClock
import android.view.LayoutInflater
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.viewinterop.AndroidView
import androidx.media3.common.MediaItem
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.VideoSize
import androidx.media3.exoplayer.DefaultLoadControl
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.analytics.AnalyticsListener
import androidx.media3.exoplayer.rtsp.RtspMediaSource
import androidx.media3.ui.PlayerView
import kotlinx.coroutines.delay
import java.util.concurrent.atomic.AtomicLong

data class VideoMetrics(val phase: String = "idle", val frames: Long = 0, val decoder: String = "", val firstFrameMs: Long = 0,
    val reconnects: Int = 0, val error: String = "", val hasRenderedFrame: Boolean = false, val bufferedMs: Long = 0,
    val width: Int = 0, val height: Int = 0)

/** Media3 owns RTSP/RTP, MediaCodec decode and presentation. No Pose or Camera API here. */
@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
@Composable
fun GameVideo(host: String, onMetrics: (VideoMetrics) -> Unit, modifier: Modifier = Modifier, accessCode: String = "", recording: GameRecording) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val frameCount = remember(host, accessCode) { AtomicLong() }
    var metrics by remember(host, accessCode) { mutableStateOf(VideoMetrics(phase = "connecting")) }
    var retryAt by remember(host, accessCode) { mutableLongStateOf(0L) }
    var retryDelay by remember(host, accessCode) { mutableLongStateOf(500L) }
    var waitingSince by remember(host, accessCode) { mutableLongStateOf(SystemClock.elapsedRealtime()) }
    val started = remember(host, accessCode) { SystemClock.elapsedRealtime() }
    val player = remember(host, accessCode, recording) {
        ExoPlayer.Builder(context, RecordingRenderersFactory(context, recording)).setLoadControl(DefaultLoadControl.Builder()
            .setBufferDurationsMs(0, 100, 0, 0).setPrioritizeTimeOverSizeThresholds(true).build()).build().apply {
            setMediaSource(RtspMediaSource.Factory().setForceUseRtpTcp(true).setTimeoutMs(2500)
                .createMediaSource(MediaItem.fromUri("rtsp://${if (accessCode.isEmpty()) "" else "camera:$accessCode@"}${host.trim()}:8554/miside")))
            setVideoFrameMetadataListener { _, _, _, _ -> frameCount.incrementAndGet() }
        }
    }
    DisposableEffect(player) {
        val listener = object : Player.Listener {
            override fun onVideoSizeChanged(size: VideoSize) {
                // Reset/buffering can report UNKNOWN while the same Surface retains its last frame.
                if (size.width > 0 && size.height > 0) metrics = metrics.copy(width = size.width, height = size.height)
            }
            override fun onRenderedFirstFrame() {
                metrics = metrics.copy(phase = "playing", firstFrameMs = if (metrics.firstFrameMs == 0L) SystemClock.elapsedRealtime() - started else metrics.firstFrameMs, error = "", hasRenderedFrame = true)
                retryDelay = 500
            }
            override fun onPlaybackStateChanged(state: Int) {
                if (state == Player.STATE_READY) metrics = metrics.copy(phase = "playing", error = "")
                if (state == Player.STATE_BUFFERING || state == Player.STATE_ENDED) {
                    waitingSince = SystemClock.elapsedRealtime()
                    metrics = metrics.copy(phase = "buffering")
                }
            }
            override fun onPlayerError(error: PlaybackException) {
                recording.onStreamEnded()
                metrics = metrics.copy(phase = "reconnecting", error = error.errorCodeName)
                retryAt = SystemClock.elapsedRealtime() + retryDelay
                retryDelay = (retryDelay * 2).coerceAtMost(5000)
            }
        }
        val analytics = object : AnalyticsListener {
            override fun onVideoDecoderInitialized(eventTime: AnalyticsListener.EventTime, decoderName: String, initializedTimestampMs: Long, initializationDurationMs: Long) {
                metrics = metrics.copy(decoder = decoderName)
            }
        }
        player.addListener(listener); player.addAnalyticsListener(analytics)
        player.playWhenReady = true; player.prepare()
        onDispose { recording.onStreamEnded(); player.removeListener(listener); player.removeAnalyticsListener(analytics); player.release(); onMetrics(VideoMetrics()) }
    }
    LaunchedEffect(player) {
        while (true) {
            delay(250)
            val now = SystemClock.elapsedRealtime()
            if ((retryAt > 0 && now >= retryAt) || (metrics.phase == "buffering" && now - waitingSince > 4000)) {
                retryAt = 0; waitingSince = now
                metrics = metrics.copy(phase = "connecting", reconnects = metrics.reconnects + 1)
                recording.onStreamEnded()
                player.stop()
                // A new RTSP publisher starts a new timeline. stop() retains the old position.
                player.seekToDefaultPosition()
                player.prepare(); player.playWhenReady = true
            }
            // RTSP has no Media3 live timeline/automatic live-offset controller. Decoder
            // startup can leave old frames queued even with a small LoadControl budget.
            val buffered = if (player.playbackState == Player.STATE_READY) player.totalBufferedDuration else 0L
            val speed = when { buffered > 500 -> 1.5f; buffered > 120 -> 1.25f; buffered > 80 -> 1.08f; else -> 1f }
            if (player.playbackParameters.speed != speed) player.setPlaybackSpeed(speed)
            onMetrics(metrics.copy(frames = frameCount.get(), bufferedMs = buffered))
        }
    }
    AndroidView(factory = {
        (LayoutInflater.from(it).inflate(R.layout.game_video, null) as PlayerView).apply {
            setShutterBackgroundColor(android.graphics.Color.TRANSPARENT)
            setKeepContentOnPlayerReset(true)
            this.player = player
        }
    }, modifier = modifier, update = { if (it.player !== player) it.player = player }, onRelease = { it.player = null })
}
