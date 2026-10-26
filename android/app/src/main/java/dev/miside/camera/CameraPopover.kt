package dev.miside.camera

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.MutableTransitionState
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.TransformOrigin
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.*
import androidx.compose.ui.window.Popup
import androidx.compose.ui.window.PopupPositionProvider
import androidx.compose.ui.window.PopupProperties

/** Place inside the trigger's Box: Popup receives that Box's window bounds. */
@Composable
internal fun CameraPopover(
    expanded: Boolean,
    above: Boolean,
    tag: String,
    onDismiss: () -> Unit,
    width: Dp = 280.dp,
    content: @Composable ColumnScope.() -> Unit,
) {
    val visibility = remember { MutableTransitionState(false) }
    LaunchedEffect(expanded) { visibility.targetState = expanded }
    val density = LocalDensity.current
    val margin = with(density) { 12.dp.roundToPx() }
    val gap = with(density) { 8.dp.roundToPx() }
    val top = WindowInsets.safeDrawing.getTop(density)
    val bottom = WindowInsets.safeDrawing.getBottom(density)
    var origin by remember { mutableStateOf(TransformOrigin(0.5f, if (above) 1f else 0f)) }
    val position = remember(above, margin, gap, top, bottom) {
        object : PopupPositionProvider {
            override fun calculatePosition(anchorBounds: IntRect, windowSize: IntSize, layoutDirection: LayoutDirection, popupContentSize: IntSize): IntOffset {
                val x = (anchorBounds.center.x - popupContentSize.width / 2)
                    .coerceIn(margin, (windowSize.width - margin - popupContentSize.width).coerceAtLeast(margin))
                val minY = top + margin
                val maxY = (windowSize.height - bottom - margin - popupContentSize.height).coerceAtLeast(minY)
                val upper = anchorBounds.top - gap - popupContentSize.height
                val lower = anchorBounds.bottom + gap
                val preferred = if (above) upper else lower
                val alternative = if (above) lower else upper
                val y = when {
                    preferred in minY..maxY -> preferred
                    alternative in minY..maxY -> alternative
                    else -> preferred.coerceIn(minY, maxY)
                }
                origin = TransformOrigin(
                    ((anchorBounds.center.x - x).toFloat() / popupContentSize.width.coerceAtLeast(1)).coerceIn(0f, 1f),
                    if (y < anchorBounds.top) 1f else 0f,
                )
                return IntOffset(x, y)
            }
        }
    }
    if (visibility.currentState || visibility.targetState) Popup(
        popupPositionProvider = position,
        onDismissRequest = onDismiss,
        properties = PopupProperties(focusable = true),
    ) {
        AnimatedVisibility(
            visibleState = visibility,
            enter = fadeIn(tween(140)) + scaleIn(tween(160, easing = CameraEase), initialScale = 0.96f, transformOrigin = origin),
            exit = fadeOut(tween(90)) + scaleOut(tween(110, easing = CameraEase), targetScale = 0.98f, transformOrigin = origin),
        ) {
            Column(
                Modifier.width(width).padding(1.dp).clip(RoundedCornerShape(24.dp))
                    .background(CameraColors.Black).border(1.dp, CameraColors.Outline, RoundedCornerShape(24.dp))
                    .testTag(tag).verticalScroll(rememberScrollState()).padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
                content = content,
            )
        }
    }
}
