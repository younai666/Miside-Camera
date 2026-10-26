package dev.miside.camera

import android.graphics.Color
import android.media.MediaMetadataRetriever
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Assume.assumeTrue
import org.junit.Test
import java.io.File

class PortraitRecordingExportTest {
    /** Opt-in synthetic MP4: red sides, center quadrants white/pink/pink/black. No game/network needed. */
    @Test fun exportsOnlyTheCenteredPortraitRegion() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val input = File(context.cacheDir, "portrait-test-input.mp4")
        val output = File(context.cacheDir, "portrait-test-output.mp4")
        assumeTrue(input.isFile)
        try {
            PortraitRecordingExport.export(context, input, output)
            MediaMetadataRetriever().let { reader ->
                try {
                    reader.setDataSource(output.absolutePath)
                    val frame = checkNotNull(reader.getFrameAtTime(0, MediaMetadataRetriever.OPTION_CLOSEST_SYNC))
                    assertEquals(1080, frame.width); assertEquals(1920, frame.height)
                    fun matches(x: Int, y: Int, expected: Int) {
                        val actual = frame.getPixel(frame.width * x / 4, frame.height * y / 4)
                        assertTrue("Portrait crop must retain the center quadrants, excluding side panels",
                            kotlin.math.abs(Color.red(actual) - Color.red(expected)) < 25 &&
                            kotlin.math.abs(Color.green(actual) - Color.green(expected)) < 25 &&
                            kotlin.math.abs(Color.blue(actual) - Color.blue(expected)) < 25)
                    }
                    matches(1, 1, Color.WHITE); matches(3, 1, Color.rgb(255,183,197))
                    matches(1, 3, Color.rgb(255,183,197)); matches(3, 3, Color.BLACK)
                    frame.recycle()
                } finally { reader.release() }
            }
        } finally { input.delete(); output.delete() }
    }
}
