package dev.miside.camera

import android.media.MediaExtractor
import android.media.MediaFormat
import android.media.MediaMetadataRetriever
import android.net.Uri
import android.os.SystemClock
import android.provider.MediaStore
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.lifecycle.ViewModelProvider
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Assume.assumeNotNull
import org.junit.Rule
import org.junit.Test

/** Opt-in short recording check against an existing 1080p stream; no Camera/Pose acceptance loop. */
class RecordingViewfinderTest {
    @get:Rule val compose = createAndroidComposeRule<MainActivity>()

    @Test fun original1080pRecordingPublishesToGalleryAndReleasesOnDisconnect() {
        val args = InstrumentationRegistry.getArguments()
        val host = args.getString("recordingHost")
        assumeNotNull(host)
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        lateinit var model: CameraViewModel
        compose.activityRule.scenario.onActivity {
            model = ViewModelProvider(it)[CameraViewModel::class.java]
            model.setHost(host!!); model.setAccessCode(args.getString("accessCode") ?: ""); model.connect()
        }
        compose.waitUntil(30_000) { model.video.value.phase == "playing" && model.video.value.width == 1920 }
        assertEquals(1080, model.video.value.height)
        val recordWidth = compose.onNodeWithTag("record_circle", useUnmergedTree = true).fetchSemanticsNode().boundsInRoot.width
        val zoomWidth = compose.onNodeWithTag("Zoom_circle", useUnmergedTree = true).fetchSemanticsNode().boundsInRoot.width
        assertEquals(1f, recordWidth / zoomWidth, 0.01f)
        compose.onNodeWithTag("recenter").assertDoesNotExist()
        compose.onNodeWithTag("More").performClick()
        compose.onNodeWithTag("recenter").assertIsDisplayed().performClick()
        val connection = model.link.value.generation
        val frames = model.video.value.frames
        compose.onNodeWithTag("record").assertIsEnabled().performClick()
        compose.waitUntil(8_000) { model.recording.state.value.phase == RecordingPhase.Recording }
        compose.onNodeWithTag("recording_status").assertIsDisplayed()
        val start = SystemClock.elapsedRealtime()
        compose.waitUntil(4_000) { SystemClock.elapsedRealtime() - start >= 1600 }
        assertTrue("Viewfinder must keep rendering while recording", model.video.value.frames > frames)
        assertEquals("Recording must not replace the Pose connection", connection, model.link.value.generation)
        compose.onNodeWithTag("record").performClick()
        compose.waitUntil(8_000) { model.recording.state.value.phase == RecordingPhase.Saved || model.recording.state.value.phase == RecordingPhase.Failed }
        assertEquals(RecordingPhase.Saved, model.recording.state.value.phase)
        val output = checkNotNull(model.recording.state.value.uri)
        verifyFile(output)
        context.contentResolver.delete(output, null, null)
        compose.onNodeWithTag("recording_status").assertDoesNotExist()

        // A stream ending must finalize the existing movie, without keeping a recording thread alive.
        compose.onNodeWithTag("record").performClick()
        compose.waitUntil(8_000) { model.recording.state.value.phase == RecordingPhase.Recording }
        val second = SystemClock.elapsedRealtime()
        compose.waitUntil(2_000) { SystemClock.elapsedRealtime() - second > 300 }
        compose.runOnUiThread { model.disconnect() }
        compose.waitUntil(8_000) { model.recording.state.value.phase == RecordingPhase.Saved }
        val disconnected = checkNotNull(model.recording.state.value.uri)
        verifyFile(disconnected)
        context.contentResolver.delete(disconnected, null, null)
        compose.onNodeWithTag("record").assertIsNotEnabled()
    }

    private fun verifyFile(uri: Uri) {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        MediaExtractor().let { extractor ->
            try {
                extractor.setDataSource(context, uri, null)
                assertEquals("The current game stream is video only", 1, extractor.trackCount)
                val format = extractor.getTrackFormat(0)
                assertEquals("video/avc", format.getString(MediaFormat.KEY_MIME))
                assertEquals(1920, format.getInteger(MediaFormat.KEY_WIDTH))
                assertEquals(1080, format.getInteger(MediaFormat.KEY_HEIGHT))
                extractor.selectTrack(0)
                assertTrue("The MP4 must start at a keyframe", extractor.sampleFlags and MediaExtractor.SAMPLE_FLAG_SYNC != 0)
                assertTrue(format.getLong(MediaFormat.KEY_DURATION) > 0)
            } finally { extractor.release() }
        }
        context.contentResolver.query(uri, arrayOf(MediaStore.Video.Media.IS_PENDING, MediaStore.Video.Media.RELATIVE_PATH), null, null, null)!!.use {
            assertTrue(it.moveToFirst()); assertEquals(0, it.getInt(0)); assertTrue(it.getString(1).startsWith("Movies/Miside Camera"))
        }
        MediaMetadataRetriever().let { retriever ->
            try {
                retriever.setDataSource(context, uri)
                val bitmap = checkNotNull(retriever.getFrameAtTime(0, MediaMetadataRetriever.OPTION_CLOSEST_SYNC))
                assertEquals(1920, bitmap.width); assertEquals(1080, bitmap.height)
                bitmap.recycle()
            } finally { retriever.release() }
        }
    }
}
