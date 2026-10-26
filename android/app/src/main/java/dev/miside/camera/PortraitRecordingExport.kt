package dev.miside.camera

import android.content.Context
import android.os.Handler
import android.os.HandlerThread
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.effect.Presentation
import androidx.media3.transformer.*
import java.io.File
import java.util.concurrent.CountDownLatch

/** The same centered zoom-fill geometry as PlayerView. No Android Surface or UI pixels are read. */
@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
internal object PortraitRecordingExport {
    const val WIDTH = 1080
    const val HEIGHT = 1920
    const val ASPECT = 9f / 16f

    // Runs on the recording writer, after its private source MP4 is finalized.
    fun export(context: Context, input: File, output: File) {
        val thread = HandlerThread("Portrait video export").apply { start() }
        val finished = CountDownLatch(1)
        var failure: Exception? = null
        var transformer: Transformer? = null
        Handler(thread.looper).post {
            try {
                val item = EditedMediaItem.Builder(MediaItem.fromUri(input.toURI().toString()))
                    .setRemoveAudio(true)
                    .setEffects(Effects(emptyList(), listOf(Presentation.createForWidthAndHeight(
                        WIDTH, HEIGHT, Presentation.LAYOUT_SCALE_TO_FIT_WITH_CROP))))
                    .build()
                transformer = Transformer.Builder(context).setLooper(thread.looper)
                    .setVideoMimeType(MimeTypes.VIDEO_H264)
                    .setEncoderFactory(DefaultEncoderFactory.Builder(context)
                        .setRequestedVideoEncoderSettings(VideoEncoderSettings.Builder().setBitrate(12_000_000).build()).build())
                    .addListener(object : Transformer.Listener {
                        override fun onCompleted(composition: Composition, result: ExportResult) { finished.countDown() }
                        override fun onError(composition: Composition, result: ExportResult, error: ExportException) {
                            failure = error; finished.countDown()
                        }
                    }).build().also { it.start(item, output.absolutePath) }
            } catch (error: Exception) { failure = error; finished.countDown() }
        }
        try {
            finished.await()
            failure?.let { throw it }
        } finally {
            Handler(thread.looper).post { transformer?.cancel(); thread.quitSafely() }
            thread.join()
        }
    }
}
