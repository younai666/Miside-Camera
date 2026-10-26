package dev.miside.camera

import android.view.KeyEvent
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Rule
import org.junit.Test

class CameraUiTest {
    @get:Rule val compose = createAndroidComposeRule<MainActivity>()

    @Test
    fun compactControlsAndLanguagesKeepTheirState() {
        compose.onNodeWithTag("Zoom").performClick()
        compose.onNodeWithTag("zoom_popover").assertIsDisplayed()
        compose.onNodeWithTag("close_sheet").assertDoesNotExist()
        compose.onNodeWithTag("zoom_2.0").performClick()
        back()
        compose.onNodeWithTag("zoom_popover").assertDoesNotExist()
        compose.onNodeWithTag("Zoom").assertTextContains("2.0×")

        compose.onNodeWithTag("Mode").performClick()
        compose.onNodeWithTag("mode_popover").assertIsDisplayed()
        compose.onNodeWithTag("manual_mode").performClick()
        compose.onNodeWithTag("mode_popover").assertDoesNotExist()

        compose.onNodeWithTag("connection_status").performClick()
        compose.onNodeWithTag("connection_popover").assertIsDisplayed()
        compose.onNodeWithTag("close_sheet").assertDoesNotExist()
        compose.onNodeWithTag("host_input").performTextClearance()
        compose.onNodeWithTag("connect").performClick()
        compose.onNodeWithTag("connection_error").assertIsDisplayed()
        compose.onNodeWithTag("host_input").performTextReplacement("10.0.2.2")
        compose.onNodeWithTag("host_input").performImeAction()
        back()
        compose.onNodeWithTag("connection_popover").assertDoesNotExist()

        compose.onNodeWithTag("More").performClick()
        for ((tag, title) in listOf("ru" to "Ещё", "en" to "More", "zh" to "更多")) {
            compose.onNodeWithTag("language_$tag").performScrollTo().performClick()
            compose.waitUntil(10_000) {
                compose.onAllNodesWithTag("More").fetchSemanticsNodes()
                    .any { title in it.config[SemanticsProperties.ContentDescription] }
            }
            compose.onNodeWithTag("language_$tag").assertIsSelected()
            compose.onNodeWithTag("Zoom").assertTextContains("2.0×")
        }
        compose.activityRule.scenario.recreate()
        compose.onNodeWithTag("language_zh").assertIsSelected()
        compose.onNodeWithTag("developer").performScrollTo().performClick()
        compose.onNodeWithTag("axis_X").assertExists()
        compose.onNodeWithTag("close_sheet").performClick()
        compose.onNodeWithTag("Zoom").performClick()
        compose.onNodeWithTag("zoom_1.0").performClick()
        back()
    }

    private fun back() = InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(KeyEvent.KEYCODE_BACK)
}
