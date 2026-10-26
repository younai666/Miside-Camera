package dev.miside.camera

import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.sp

internal val CameraEase = CubicBezierEasing(0.23f, 1f, 0.32f, 1f)
internal enum class Glyph { Recenter, Record, Stop, Zoom, Mode, More, Link, Close, Camera, Plus, Minus, Code, Grid }

@Composable
internal fun CameraGlyph(glyph: Glyph, modifier: Modifier = Modifier, color: Color = CameraColors.White) {
    Canvas(modifier.size(24.dp)) {
        val u = size.width / 24f
        val stroke = 1.6f * u
        fun line(x1: Float, y1: Float, x2: Float, y2: Float) = drawLine(
            color, Offset(x1 * u, y1 * u), Offset(x2 * u, y2 * u), stroke, StrokeCap.Round,
        )
        when (glyph) {
            Glyph.Record -> drawCircle(color, 7f * u, center)
            Glyph.Stop -> drawRoundRect(color, Offset(5f * u, 5f * u), Size(14f * u, 14f * u), androidx.compose.ui.geometry.CornerRadius(2f * u))
            Glyph.Recenter -> {
                line(3f, 8f, 3f, 3f); line(3f, 3f, 8f, 3f)
                line(16f, 3f, 21f, 3f); line(21f, 3f, 21f, 8f)
                line(21f, 16f, 21f, 21f); line(21f, 21f, 16f, 21f)
                line(8f, 21f, 3f, 21f); line(3f, 21f, 3f, 16f)
                drawCircle(color, 3f * u, center, style = Stroke(stroke))
            }
            Glyph.Zoom -> {
                drawCircle(color, 6.5f * u, Offset(10.5f * u, 10.5f * u), style = Stroke(stroke))
                line(15.5f, 15.5f, 21f, 21f); line(7.5f, 10.5f, 13.5f, 10.5f); line(10.5f, 7.5f, 10.5f, 13.5f)
            }
            Glyph.Mode -> {
                val path = Path().apply { moveTo(12f * u, 3f * u); lineTo(22f * u, 9f * u); lineTo(12f * u, 15f * u); lineTo(2f * u, 9f * u); close() }
                drawPath(path, color, style = Stroke(stroke))
                line(3f, 14f, 12f, 20f); line(12f, 20f, 21f, 14f)
            }
            Glyph.More -> listOf(5f, 12f, 19f).forEach { drawCircle(color, 1.5f * u, Offset(it * u, 12f * u)) }
            Glyph.Link -> {
                drawArc(color, 0f, 270f, false, Offset(2f * u, 9f * u), Size(13f * u, 9f * u), style = Stroke(stroke))
                drawArc(color, 180f, 270f, false, Offset(9f * u, 6f * u), Size(13f * u, 9f * u), style = Stroke(stroke))
                line(9f, 14f, 15f, 10f)
            }
            Glyph.Close -> { line(6f, 6f, 18f, 18f); line(6f, 18f, 18f, 6f) }
            Glyph.Camera -> {
                val path = Path().apply {
                    moveTo(2f * u, 7f * u); lineTo(7f * u, 7f * u); lineTo(9f * u, 4f * u)
                    lineTo(15f * u, 4f * u); lineTo(17f * u, 7f * u); lineTo(22f * u, 7f * u)
                    lineTo(22f * u, 20f * u); lineTo(2f * u, 20f * u); close()
                }
                drawPath(path, color, style = Stroke(stroke))
                drawCircle(color, 4f * u, Offset(12f * u, 13f * u), style = Stroke(stroke))
            }
            Glyph.Plus -> { line(5f, 12f, 19f, 12f); line(12f, 5f, 12f, 19f) }
            Glyph.Minus -> line(5f, 12f, 19f, 12f)
            Glyph.Code -> { line(8f, 6f, 2f, 12f); line(2f, 12f, 8f, 18f); line(16f, 6f, 22f, 12f); line(22f, 12f, 16f, 18f); line(14f, 3f, 10f, 21f) }
            Glyph.Grid -> {
                drawRect(color, Offset(3f * u, 3f * u), Size(18f * u, 18f * u), style = Stroke(stroke))
                line(9f, 3f, 9f, 21f); line(15f, 3f, 15f, 21f); line(3f, 9f, 21f, 9f); line(3f, 15f, 21f, 15f)
            }
        }
    }
}

@Composable
internal fun CameraAction(
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    glyph: Glyph? = null,
    primary: Boolean = false,
    enabled: Boolean = true,
    tag: String = label,
    horizontalPadding: Dp = 16.dp,
) {
    val interactions = remember { MutableInteractionSource() }
    val pressed by interactions.collectIsPressedAsState()
    val scale = animateFloatAsState(if (pressed) 0.97f else 1f, tween(110, easing = CameraEase), label = "press")
    val foreground = if (primary) CameraColors.Black else CameraColors.White
    Row(
        modifier.graphicsLayer { scaleX = scale.value; scaleY = scale.value }
            .heightIn(min = 48.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(if (primary) CameraColors.Pink else CameraColors.Black)
            .border(1.dp, if (enabled) CameraColors.Outline else CameraColors.White.copy(alpha = 0.12f), RoundedCornerShape(16.dp))
            .clickable(interactionSource = interactions, indication = null, enabled = enabled, role = Role.Button, onClick = onClick)
            .testTag(tag).padding(horizontal = horizontalPadding, vertical = 12.dp),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (glyph != null) {
            CameraGlyph(glyph, color = foreground.copy(alpha = if (enabled) 1f else 0.38f))
            Spacer(Modifier.width(8.dp))
        }
        Text(label, color = foreground.copy(alpha = if (enabled) 1f else 0.38f), fontSize = 14.sp, fontWeight = FontWeight.Medium)
    }
}

@Composable
internal fun CameraIconButton(glyph: Glyph, description: String, onClick: () -> Unit, modifier: Modifier = Modifier) {
    Box(
        modifier.size(48.dp).clip(CircleShape).clickable(role = Role.Button, onClick = onClick)
            .semantics { contentDescription = description },
        contentAlignment = Alignment.Center,
    ) { CameraGlyph(glyph) }
}
