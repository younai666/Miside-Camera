package dev.miside.camera

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

object CameraColors {
    val Black = Color(0xFF000000)
    val White = Color(0xFFFFFFFF)
    val Pink = Color(0xFFFFB7C5)
    val Muted = White.copy(alpha = 0.68f)
    val Outline = White.copy(alpha = 0.25f)
}

@Composable
fun CameraTheme(content: @Composable () -> Unit) {
    with(CameraColors) {
        MaterialTheme(
            colorScheme = darkColorScheme(
                primary = Pink, onPrimary = Black, primaryContainer = Pink, onPrimaryContainer = Black,
                inversePrimary = Pink,
                secondary = White, onSecondary = Black, secondaryContainer = Black, onSecondaryContainer = White,
                tertiary = Pink, onTertiary = Black, tertiaryContainer = Black, onTertiaryContainer = Pink,
                background = Black, onBackground = White,
                surface = Black, onSurface = White, surfaceVariant = Black, onSurfaceVariant = Muted,
                surfaceTint = Black, inverseSurface = White, inverseOnSurface = Black,
                error = Pink, onError = Black, errorContainer = Black, onErrorContainer = Pink,
                outline = Outline, outlineVariant = Outline, scrim = Black,
                surfaceBright = Black, surfaceDim = Black,
                surfaceContainer = Black, surfaceContainerHigh = Black, surfaceContainerHighest = Black,
                surfaceContainerLow = Black, surfaceContainerLowest = Black,
            ),
            content = content,
        )
    }
}
