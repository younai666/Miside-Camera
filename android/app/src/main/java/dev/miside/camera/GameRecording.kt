package dev.miside.camera

import android.content.ContentValues
import android.content.Context
import android.media.MediaCodec
import android.media.MediaFormat
import android.media.MediaMuxer
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.os.SystemClock
import android.provider.MediaStore
import android.util.Log
import androidx.media3.common.Format
import androidx.media3.common.MimeTypes
import androidx.media3.decoder.DecoderInputBuffer
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.io.File
import java.nio.ByteBuffer
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.TimeUnit

enum class RecordingPhase { Idle, Starting, Recording, Saving, Saved, Failed }
data class RecordingState(val phase: RecordingPhase = RecordingPhase.Idle, val startedAt: Long = 0,
    val uri: Uri? = null, val error: Int = 0) {
    val active get() = phase == RecordingPhase.Starting || phase == RecordingPhase.Recording
    val saving get() = phase == RecordingPhase.Saving
}

/** Copies compressed game samples before decode. Neither Surface pixels nor Compose UI enter this path. */
@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
class GameRecording {
    private val mutableState = MutableStateFlow(RecordingState())
    val state = mutableState.asStateFlow()
    private var format: Format? = null
    private var session: Session? = null

    @Synchronized fun onFormat(value: Format) {
        val previous = format
        if (previous != null && (previous.width != value.width || previous.height != value.height ||
                previous.sampleMimeType != value.sampleMimeType || previous.initializationData.size != value.initializationData.size ||
                previous.initializationData.zip(value.initializationData).any { !it.first.contentEquals(it.second) })) stop()
        format = value
    }

    @Synchronized fun start(context: Context) {
        if (session != null) return
        val input = format
        if (input == null || input.sampleMimeType != MimeTypes.VIDEO_H264 || input.width != 1920 || input.height != 1080 || input.initializationData.isEmpty()) {
            mutableState.value = RecordingState(RecordingPhase.Failed, error = R.string.recording_requires_video)
            return
        }
        mutableState.value = RecordingState(RecordingPhase.Starting)
        val recording = Session(context.applicationContext, input)
        session = recording
        Thread({ recording.write() }, "Game MP4 writer").start()
    }

    @Synchronized fun onSample(buffer: DecoderInputBuffer) {
        val recording = session ?: return
        if (!recording.accepting || buffer.isEndOfStream) return
        val bytes = buffer.data?.duplicate() ?: return
        if (!bytes.hasRemaining()) return
        val copy = ByteArray(bytes.remaining()); bytes.get(copy)
        if (!recording.queue.offer(Sample(copy, buffer.timeUs, buffer.isKeyFrame))) {
            // Dropping an encoded P-frame corrupts later frames: finish instead of blocking playback.
            recording.failure = R.string.recording_save_failed
            stop()
        }
    }

    @Synchronized fun stop() {
        session?.let {
            if (!it.accepting) return
            it.accepting = false
            mutableState.value = mutableState.value.copy(phase = RecordingPhase.Saving)
        }
    }

    @Synchronized fun onStreamEnded() { format = null; stop() }

    private data class Sample(val bytes: ByteArray, val timeUs: Long, val key: Boolean)

    private inner class Session(private val context: Context, private val input: Format) {
        val queue = ArrayBlockingQueue<Sample>(60)
        @Volatile var accepting = true
        @Volatile var failure = 0

        fun write() {
            val resolver = context.contentResolver
            var uri: Uri? = null
            var descriptor: android.os.ParcelFileDescriptor? = null
            var muxer: MediaMuxer? = null
            var started = false
            var samples = 0
            try {
                val name = "Miside-${System.currentTimeMillis()}.mp4"
                val values = ContentValues().apply {
                    put(MediaStore.Video.Media.DISPLAY_NAME, name)
                    put(MediaStore.Video.Media.MIME_TYPE, "video/mp4")
                    if (Build.VERSION.SDK_INT >= 29) {
                        put(MediaStore.Video.Media.RELATIVE_PATH, "Movies/Miside Camera")
                        put(MediaStore.Video.Media.IS_PENDING, 1)
                    } else {
                        @Suppress("DEPRECATION") val directory = File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MOVIES), "Miside Camera")
                        if (!directory.isDirectory && !directory.mkdirs()) error("Cannot create Movies directory")
                        @Suppress("DEPRECATION") put(MediaStore.Video.Media.DATA, File(directory, name).absolutePath)
                    }
                }
                uri = checkNotNull(resolver.insert(MediaStore.Video.Media.EXTERNAL_CONTENT_URI, values))
                descriptor = checkNotNull(resolver.openFileDescriptor(uri, "rw"))
                muxer = MediaMuxer(descriptor.fileDescriptor, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4)
                val trackFormat = MediaFormat.createVideoFormat(MimeTypes.VIDEO_H264, input.width, input.height)
                input.initializationData.forEachIndexed { index, data -> trackFormat.setByteBuffer("csd-$index", ByteBuffer.wrap(data)) }
                // Preserve the bitstream's SPS/VUI. RTSP Format.colorInfo may default to BT.709
                // even when the encoded game stream uses a BT.601 matrix.
                val track = muxer.addTrack(trackFormat)
                var origin = 0L
                var lastTime = -1L
                val waitingAt = SystemClock.elapsedRealtime()
                val info = MediaCodec.BufferInfo()
                while (accepting || queue.isNotEmpty()) {
                    val sample = queue.poll(100, TimeUnit.MILLISECONDS)
                    if (sample == null) {
                        if (!started && SystemClock.elapsedRealtime() - waitingAt > 5000) {
                            failure = R.string.recording_no_frames; break
                        }
                        continue
                    }
                    if (!started) {
                        if (!sample.key) continue
                        muxer.start(); started = true; origin = sample.timeUs
                        synchronized(this@GameRecording) {
                            if (session === this && accepting) mutableState.value = RecordingState(RecordingPhase.Recording, SystemClock.elapsedRealtime())
                        }
                    }
                    val time = sample.timeUs - origin
                    check(time > lastTime) { "Game stream timestamp changed during recording" }
                    info.set(0, sample.bytes.size, time, if (sample.key) MediaCodec.BUFFER_FLAG_KEY_FRAME else 0)
                    muxer.writeSampleData(track, ByteBuffer.wrap(sample.bytes), info)
                    lastTime = time; samples++
                }
                if (samples == 0 && failure == 0) failure = R.string.recording_no_frames
            } catch (error: Exception) {
                failure = R.string.recording_save_failed
                Log.w("GameRecording", "Cannot write game recording", error)
            } finally {
                accepting = false
                try { if (started) muxer?.stop() } catch (error: Exception) { failure = R.string.recording_save_failed; Log.w("GameRecording", "Cannot finalize MP4", error) }
                try { muxer?.release() } catch (error: Exception) { failure = R.string.recording_save_failed; Log.w("GameRecording", "Cannot release MP4", error) }
                try { descriptor?.close() } catch (error: Exception) { failure = R.string.recording_save_failed; Log.w("GameRecording", "Cannot close MP4", error) }
                uri?.let { output ->
                    try {
                        if (failure != 0 || samples == 0) resolver.delete(output, null, null)
                        else if (Build.VERSION.SDK_INT >= 29) check(resolver.update(output, ContentValues().apply { put(MediaStore.Video.Media.IS_PENDING, 0) }, null, null) == 1)
                    } catch (error: Exception) {
                        failure = R.string.recording_save_failed
                        Log.w("GameRecording", "Cannot publish recording", error)
                        try { resolver.delete(output, null, null) } catch (cleanup: Exception) { Log.w("GameRecording", "Cannot remove incomplete recording", cleanup) }
                    }
                }
                queue.clear()
                synchronized(this@GameRecording) {
                    if (session === this) {
                        session = null
                        mutableState.value = if (failure == 0 && samples > 0) RecordingState(RecordingPhase.Saved, uri = uri)
                            else RecordingState(RecordingPhase.Failed, error = failure.takeIf { it != 0 } ?: R.string.recording_save_failed)
                    }
                }
            }
        }
    }
}
