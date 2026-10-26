package dev.miside.camera

import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.toPixelMap
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class RecordButtonUiTest {
    @get:Rule val compose = createComposeRule()

    @Test fun recordKeepsItsColorAndSizeAcrossAvailabilityAndPressStates() {
        val enabled = mutableStateOf(true)
        val active = mutableStateOf(false)
        compose.setContent {
            CameraTheme {
                Row(Modifier.fillMaxWidth()) {
                    ToolbarControl("Record", if (active.value) Glyph.Stop else Glyph.Record, {}, Modifier.weight(1f),
                        primary = true, tag = "record", enabled = enabled.value, steady = true)
                    ToolbarControl("Zoom", Glyph.Zoom, {}, Modifier.weight(1f), tag = "Zoom")
                    ToolbarControl("Mode", Glyph.Mode, {}, Modifier.weight(1f), tag = "Mode")
                    ToolbarControl("More", Glyph.More, {}, Modifier.weight(1f), tag = "More")
                }
            }
        }
        val circle = compose.onNodeWithTag("record_circle", useUnmergedTree = true)
        val bounds = circle.fetchSemanticsNode().boundsInRoot
        for (tag in listOf("Zoom", "Mode", "More")) {
            val other = compose.onNodeWithTag("${tag}_circle", useUnmergedTree = true).fetchSemanticsNode().boundsInRoot
            assertEquals(other.width, bounds.width, 0.1f)
            assertEquals(other.height, bounds.height, 0.1f)
        }
        fun checkAppearance() {
            val pixels = circle.captureToImage().toPixelMap()
            assertEquals(CameraColors.Pink.toArgb(), pixels[pixels.width / 2, pixels.height / 4].toArgb())
            val current = circle.fetchSemanticsNode().boundsInRoot
            assertEquals(bounds.width, current.width, 0.1f)
            assertEquals(bounds.height, current.height, 0.1f)
        }
        checkAppearance()
        compose.onNodeWithTag("record").performTouchInput { down(center) }
        compose.mainClock.advanceTimeBy(250)
        checkAppearance()
        compose.onNodeWithTag("record").performTouchInput { up() }
        for ((available, recording) in listOf(false to false, true to false, true to true, false to true, false to false)) {
            compose.runOnIdle { enabled.value = available; active.value = recording }
            compose.mainClock.advanceTimeBy(300)
            checkAppearance()
        }
    }
}
