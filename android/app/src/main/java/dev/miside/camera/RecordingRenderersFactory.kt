package dev.miside.camera

import android.content.Context
import android.os.Handler
import androidx.media3.decoder.DecoderInputBuffer
import androidx.media3.exoplayer.DecoderReuseEvaluation
import androidx.media3.exoplayer.DefaultRenderersFactory
import androidx.media3.exoplayer.FormatHolder
import androidx.media3.exoplayer.Renderer
import androidx.media3.exoplayer.mediacodec.MediaCodecSelector
import androidx.media3.exoplayer.video.MediaCodecVideoRenderer
import androidx.media3.exoplayer.video.VideoRendererEventListener

@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
internal class RecordingRenderersFactory(context: Context, private val recording: GameRecording) : DefaultRenderersFactory(context) {
    override fun buildVideoRenderers(context: Context, extensionRendererMode: Int, mediaCodecSelector: MediaCodecSelector,
        enableDecoderFallback: Boolean, eventHandler: Handler, eventListener: VideoRendererEventListener,
        allowedVideoJoiningTimeMs: Long, out: ArrayList<Renderer>) {
        out.add(object : MediaCodecVideoRenderer(Builder(context).setCodecAdapterFactory(codecAdapterFactory)
            .setMediaCodecSelector(mediaCodecSelector).setAllowedJoiningTimeMs(allowedVideoJoiningTimeMs)
            .setEnableDecoderFallback(enableDecoderFallback).setEventHandler(eventHandler).setEventListener(eventListener)
            .setMaxDroppedFramesToNotify(50)) {
            override fun onInputFormatChanged(formatHolder: FormatHolder): DecoderReuseEvaluation? {
                val evaluation = super.onInputFormatChanged(formatHolder)
                formatHolder.format?.let(recording::onFormat)
                return evaluation
            }
            override fun onQueueInputBuffer(buffer: DecoderInputBuffer) {
                recording.onSample(buffer)
                super.onQueueInputBuffer(buffer)
            }
        })
    }
}
