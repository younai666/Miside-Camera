package dev.miside.camera

import androidx.activity.compose.LocalActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.SystemClock
import android.widget.Toast
import androidx.appcompat.app.AppCompatDelegate
import androidx.compose.animation.*
import androidx.compose.animation.core.MutableTransitionState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.PlatformTextStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.core.os.LocaleListCompat
import java.util.Locale
import kotlinx.coroutines.delay

private enum class Sheet { Connection, Zoom, Mode, More, Developer }

@Composable
fun CameraApp(model: CameraViewModel) {
    val link by model.link.collectAsStateWithLifecycle()
    val zoom by model.zoom.collectAsStateWithLifecycle()
    val source by model.source.collectAsStateWithLifecycle()
    val video by model.video.collectAsStateWithLifecycle()
    val videoHost by model.videoHost.collectAsStateWithLifecycle()
    val videoCode by model.videoCode.collectAsStateWithLifecycle()
    val recording by model.recording.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val storagePermission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted) model.recording.start(context)
        else Toast.makeText(context, R.string.recording_storage_permission, Toast.LENGTH_SHORT).show()
    }
    LaunchedEffect(recording.phase, recording.uri, recording.error) {
        if (recording.phase == RecordingPhase.Saved) Toast.makeText(context, R.string.recording_saved, Toast.LENGTH_SHORT).show()
        if (recording.phase == RecordingPhase.Failed) Toast.makeText(context, recording.error, Toast.LENGTH_LONG).show()
    }
    val uri = LocalUriHandler.current
    if (source == PoseSource.ARCore) ArCoreInput(model)
    var sheet by rememberSaveable { mutableStateOf<Sheet?>(null) }
    var grid by rememberSaveable { mutableStateOf(true) }
    val owner = LocalLifecycleOwner.current
    val activity = LocalActivity.current
    DisposableEffect(owner, activity, model) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_STOP && activity?.isChangingConfigurations != true) model.disconnect()
        }
        owner.lifecycle.addObserver(observer)
        onDispose { owner.lifecycle.removeObserver(observer) }
    }
    ViewfinderScreen(
        phase = link.phase, zoom = zoom, grid = grid,
        videoContent = { videoHost?.let { GameVideo(it, model::updateVideo, Modifier.fillMaxSize(), videoCode, model.recording) } },
        videoPlaying = video.hasRenderedFrame,
        recording = recording, canRecord = video.phase == "playing" && video.width == 1920 && video.height == 1080,
        modeValue = if (source == PoseSource.ARCore) "AR" else null,
        selected = sheet, onDismiss = { sheet = null },
        connectionContent = { ConnectionPanel(model) },
        zoomContent = { ZoomPanel(zoom, model::setZoom) },
        modeContent = {
            FlowRow(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp, Alignment.CenterHorizontally), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                CameraAction(stringResource(R.string.manual), { model.selectSource(PoseSource.Manual); sheet = null }, Modifier.semantics { selected = source == PoseSource.Manual }, primary = source == PoseSource.Manual, tag = "manual_mode")
                CameraAction("ARCore", { model.selectSource(PoseSource.ARCore); sheet = null }, Modifier.semantics { selected = source == PoseSource.ARCore }, primary = source == PoseSource.ARCore, tag = "arcore_mode")
            }
            Text(stringResource(R.string.ar_privacy_notice), color = CameraColors.Muted, fontSize = 14.sp)
            CameraAction(stringResource(R.string.google_privacy), { uri.openUri("https://policies.google.com/privacy") }, Modifier.fillMaxWidth())
        },
        onConnect = { sheet = if (sheet == Sheet.Connection) null else Sheet.Connection },
        onRecord = {
            if (recording.active) model.recording.stop()
            else if (Build.VERSION.SDK_INT < 29 && context.checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED)
                storagePermission.launch(Manifest.permission.WRITE_EXTERNAL_STORAGE)
            else model.recording.start(context)
        },
        onZoom = { sheet = if (sheet == Sheet.Zoom) null else Sheet.Zoom },
        onMode = { sheet = if (sheet == Sheet.Mode) null else Sheet.Mode }, onMore = { sheet = Sheet.More },
    )
    CameraSheetHost(sheet?.takeIf { it == Sheet.More || it == Sheet.Developer }, onDismiss = { sheet = null }) { displayed ->
        when (displayed) {
            Sheet.More -> {
                CameraAction(stringResource(R.string.recenter), { model.recenter(); sheet = null }, Modifier.fillMaxWidth(), Glyph.Recenter, tag = "recenter")
                CameraAction(stringResource(R.string.developer), { sheet = Sheet.Developer }, Modifier.fillMaxWidth(), Glyph.Code, tag = "developer")
                Row(Modifier.fillMaxWidth().heightIn(min = 48.dp).clip(RoundedCornerShape(16.dp))
                    .toggleable(grid, role = Role.Switch, onValueChange = { grid = it }).padding(horizontal = 16.dp),
                    verticalAlignment = Alignment.CenterVertically) {
                    CameraGlyph(Glyph.Grid)
                    Text(stringResource(R.string.grid), Modifier.weight(1f).padding(start = 12.dp), color = CameraColors.White, fontSize = 14.sp)
                    Switch(grid, onCheckedChange = null)
                }
                LanguagePicker()
            }
            Sheet.Developer -> DeveloperPanel(model)
            else -> Unit
        }
    }
}

@Composable
private fun ViewfinderScreen(
    phase: LinkPhase, zoom: Float, grid: Boolean,
    videoContent: @Composable () -> Unit = {}, videoPlaying: Boolean = false,
    recording: RecordingState = RecordingState(), canRecord: Boolean = false,
    modeValue: String? = null,
    selected: Sheet? = null, onDismiss: () -> Unit = {},
    connectionContent: @Composable ColumnScope.() -> Unit = {},
    zoomContent: @Composable ColumnScope.() -> Unit = {},
    modeContent: @Composable ColumnScope.() -> Unit = {},
    onConnect: () -> Unit, onRecord: () -> Unit, onZoom: () -> Unit, onMode: () -> Unit, onMore: () -> Unit,
) {
    BoxWithConstraints(Modifier.fillMaxSize().background(CameraColors.Black).safeDrawingPadding().padding(horizontal = 20.dp, vertical = 4.dp)) {
        val compact = maxWidth / LocalDensity.current.fontScale < 280.dp
        val expandedLabel = stringResource(R.string.expanded)
        val collapsedLabel = stringResource(R.string.collapsed)
        Column(Modifier.fillMaxSize(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(Modifier.fillMaxWidth().heightIn(min = 52.dp), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text("MISIDE", color = CameraColors.Pink, fontSize = 10.sp, lineHeight = 12.sp, letterSpacing = 1.8.sp,
                        fontWeight = FontWeight.SemiBold, style = TextStyle(platformStyle = PlatformTextStyle(includeFontPadding = false)))
                    Text("Camera", color = CameraColors.White, fontSize = 26.sp, lineHeight = 28.sp, fontWeight = FontWeight.Medium,
                        letterSpacing = (-0.6).sp, style = TextStyle(platformStyle = PlatformTextStyle(includeFontPadding = false)))
                }
                Spacer(Modifier.width(8.dp))
                val label = stringResource(when (phase) {
                    LinkPhase.Connected -> R.string.pc_connected
                    LinkPhase.Connecting -> R.string.connecting
                    LinkPhase.Error -> R.string.reconnect
                    LinkPhase.Disconnected -> R.string.connect_pc
                })
                Box {
                    Row(
                        Modifier.heightIn(min = 48.dp).clip(RoundedCornerShape(24.dp))
                            .border(1.dp, if (selected == Sheet.Connection) CameraColors.Pink else CameraColors.Outline, RoundedCornerShape(24.dp))
                            .clickable(role = Role.Button, onClick = onConnect).testTag("connection_status")
                            .semantics { contentDescription = label; stateDescription = if (selected == Sheet.Connection) expandedLabel else collapsedLabel }
                            .padding(horizontal = 12.dp, vertical = 8.dp),
                        verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Box(Modifier.size(6.dp).background(if (phase == LinkPhase.Connected) CameraColors.Pink else CameraColors.White.copy(alpha = 0.6f), CircleShape))
                        if (compact) CameraGlyph(Glyph.Link) else Text(label, color = CameraColors.White, fontSize = 12.sp, fontWeight = FontWeight.Medium)
                    }
                    CameraPopover(selected == Sheet.Connection, above = false, tag = "connection_popover", onDismiss, content = connectionContent)
                }
            }
            BoxWithConstraints(Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.Center) {
                val frameWidth = minOf(maxWidth, maxHeight * PortraitRecordingExport.ASPECT)
                Viewfinder(Modifier.width(frameWidth).aspectRatio(PortraitRecordingExport.ASPECT), grid, videoContent, videoPlaying, recording)
            }
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.Bottom) {
                ToolbarControl(stringResource(if (recording.active) R.string.stop_recording else R.string.record_video),
                    if (recording.active) Glyph.Stop else Glyph.Record, onRecord, Modifier.weight(1f), primary = true,
                    tag = "record", showLabel = !compact, enabled = !recording.saving && (canRecord || recording.active), steady = true)
                Box(Modifier.weight(1f)) {
                    ToolbarControl(stringResource(R.string.zoom), Glyph.Zoom, onZoom, Modifier.fillMaxWidth(), value = String.format(Locale.ROOT, "%.1f×", zoom), tag = "Zoom", showLabel = !compact, expanded = selected == Sheet.Zoom)
                    CameraPopover(selected == Sheet.Zoom, above = true, tag = "zoom_popover", onDismiss, content = zoomContent)
                }
                Box(Modifier.weight(1f)) {
                    ToolbarControl(stringResource(R.string.mode), Glyph.Mode, onMode, Modifier.fillMaxWidth(), value = modeValue, tag = "Mode", showLabel = !compact, expanded = selected == Sheet.Mode)
                    CameraPopover(selected == Sheet.Mode, above = true, tag = "mode_popover", onDismiss, width = 224.dp, content = modeContent)
                }
                ToolbarControl(stringResource(R.string.more), Glyph.More, onMore, Modifier.weight(1f), tag = "More", showLabel = !compact)
            }
        }
    }
}

@Composable
internal fun ToolbarControl(
    label: String, glyph: Glyph, onClick: () -> Unit, modifier: Modifier,
    primary: Boolean = false, value: String? = null, tag: String = label,
    showLabel: Boolean = true, expanded: Boolean? = null,
    enabled: Boolean = true, steady: Boolean = false,
) {
    val interactions = remember { MutableInteractionSource() }
    val feedback = if (steady) Modifier else {
        val pressed by interactions.collectIsPressedAsState()
        val scale = animateFloatAsState(if (pressed) 0.97f else 1f, tween(110, easing = CameraEase), label = "toolbar press")
        Modifier.graphicsLayer { scaleX = scale.value; scaleY = scale.value; alpha = if (enabled) 1f else 0.4f }
    }
    val diameter = 56.dp * LocalDensity.current.fontScale.coerceIn(1f, 1.15f)
    val expandedLabel = expanded?.let { stringResource(if (it) R.string.expanded else R.string.collapsed) }
    Column(
        modifier.then(feedback)
            .clip(RoundedCornerShape(16.dp)).clickable(enabled = enabled, interactionSource = interactions, indication = null, role = Role.Button, onClick = onClick)
            .testTag(tag).padding(vertical = 4.dp).semantics(mergeDescendants = true) {
                contentDescription = label
                if (expandedLabel != null) stateDescription = expandedLabel
            },
        horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Box(
            Modifier.size(diameter).testTag("${tag}_circle").background(if (primary) CameraColors.Pink else CameraColors.Black, CircleShape)
                .border(1.dp, if (primary || expanded == true) CameraColors.Pink else CameraColors.Outline, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            if (value != null) Text(value, color = CameraColors.White, fontSize = 14.sp, fontWeight = FontWeight.Medium, maxLines = 1, softWrap = false)
            else CameraGlyph(glyph, color = if (primary) CameraColors.Black else CameraColors.White)
        }
        if (showLabel) Text(label, color = if (primary) CameraColors.Pink else CameraColors.White, fontSize = 12.sp, textAlign = TextAlign.Center)
    }
}

@Composable
private fun Viewfinder(modifier: Modifier, grid: Boolean, videoContent: @Composable () -> Unit, videoPlaying: Boolean, recording: RecordingState) {
    val description = stringResource(R.string.viewfinder_description)
    BoxWithConstraints(modifier.clip(RoundedCornerShape(20.dp)).border(1.dp, CameraColors.Outline, RoundedCornerShape(20.dp))
        .testTag("viewfinder").semantics { contentDescription = description }) {
        videoContent()
        Canvas(Modifier.fillMaxSize()) {
            if (grid) for (part in 1..2) {
                val x = size.width * part / 3
                val y = size.height * part / 3
                drawLine(CameraColors.White.copy(alpha = 0.09f), Offset(x, 0f), Offset(x, size.height), 1.dp.toPx())
                drawLine(CameraColors.White.copy(alpha = 0.09f), Offset(0f, y), Offset(size.width, y), 1.dp.toPx())
            }
            val inset = 20.dp.toPx()
            val arm = 12.dp.toPx()
            for (x in listOf(inset, size.width - inset)) for (y in listOf(inset, size.height - inset)) {
                drawLine(CameraColors.White.copy(alpha = 0.65f), Offset(x, y), Offset(x + if (x < size.width / 2) arm else -arm, y), 1.dp.toPx())
                drawLine(CameraColors.White.copy(alpha = 0.65f), Offset(x, y), Offset(x, y + if (y < size.height / 2) arm else -arm), 1.dp.toPx())
            }
        }
        if (!videoPlaying) CameraGlyph(Glyph.Camera, Modifier.align(Alignment.Center).size(if (maxHeight < 220.dp) 28.dp else 40.dp), CameraColors.Pink)
        RecordingIndicator(recording, Modifier.align(Alignment.BottomCenter).padding(bottom = 12.dp))
    }
}

@Composable
private fun RecordingIndicator(recording: RecordingState, modifier: Modifier) {
    if (!recording.active && !recording.saving) return
    var seconds by remember(recording.startedAt) { mutableLongStateOf(0L) }
    LaunchedEffect(recording.phase, recording.startedAt) {
        while (recording.phase == RecordingPhase.Recording) {
            seconds = (SystemClock.elapsedRealtime() - recording.startedAt) / 1000
            delay(250)
        }
    }
    Row(modifier.clip(RoundedCornerShape(16.dp)).background(CameraColors.Black.copy(alpha = 0.72f))
        .padding(horizontal = 10.dp, vertical = 6.dp).testTag("recording_status"),
        verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        Box(Modifier.size(5.dp).background(CameraColors.Pink, CircleShape))
        Text(when (recording.phase) {
            RecordingPhase.Starting -> stringResource(R.string.recording_starting)
            RecordingPhase.Saving -> stringResource(R.string.recording_saving)
            else -> stringResource(R.string.recording_timer, String.format(Locale.ROOT, "%02d:%02d", seconds / 60, seconds % 60))
        }, color = CameraColors.White, fontSize = 11.sp, fontFamily = FontFamily.Monospace)
    }
}

@Composable
private fun CameraSheetHost(selected: Sheet?, onDismiss: () -> Unit, content: @Composable ColumnScope.(Sheet) -> Unit) {
    val visibility = remember { MutableTransitionState(false) }
    var displayed by remember { mutableStateOf(selected ?: Sheet.More) }
    LaunchedEffect(selected) {
        if (selected != null) displayed = selected
        visibility.targetState = selected != null
    }
    if (visibility.currentState || visibility.targetState) Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false, decorFitsSystemWindows = false),
    ) {
        BoxWithConstraints(Modifier.fillMaxSize().imePadding()) {
            Box(Modifier.fillMaxSize().background(CameraColors.Black.copy(alpha = 0.8f)).clickable(
                interactionSource = remember { MutableInteractionSource() }, indication = null, onClick = onDismiss,
            ))
            val height = maxHeight * 0.92f
            AnimatedVisibility(
                visibleState = visibility,
                enter = slideInVertically(tween(220, easing = CameraEase)) { it / 5 } + fadeIn(tween(180)),
                exit = slideOutVertically(tween(160, easing = CameraEase)) { it / 5 } + fadeOut(tween(140)),
                modifier = Modifier.align(Alignment.BottomCenter),
            ) {
                Column(
                    Modifier.widthIn(max = 600.dp).fillMaxWidth().heightIn(max = height)
                        .clip(RoundedCornerShape(topStart = 28.dp, topEnd = 28.dp))
                        .background(CameraColors.Black).border(1.dp, CameraColors.Outline, RoundedCornerShape(topStart = 28.dp, topEnd = 28.dp))
                        .pointerInput(Unit) { detectTapGestures(onTap = {}) }
                        .windowInsetsPadding(WindowInsets.navigationBars).padding(horizontal = 24.dp),
                ) {
                    Box(Modifier.align(Alignment.CenterHorizontally).padding(top = 12.dp).width(32.dp).height(3.dp).background(CameraColors.Outline, CircleShape))
                    Row(Modifier.fillMaxWidth().padding(top = 8.dp, bottom = 8.dp), verticalAlignment = Alignment.CenterVertically) {
                        Text(stringResource(if (displayed == Sheet.Developer) R.string.developer else R.string.more),
                            Modifier.weight(1f), color = CameraColors.White, fontSize = 24.sp, fontWeight = FontWeight.Medium)
                        CameraIconButton(Glyph.Close, stringResource(R.string.close_panel), onDismiss, Modifier.testTag("close_sheet"))
                    }
                    Column(
                        Modifier.fillMaxWidth().testTag(if (displayed == Sheet.Developer) "developer_sheet" else "more_sheet")
                            .verticalScroll(rememberScrollState()).padding(bottom = 24.dp),
                        verticalArrangement = Arrangement.spacedBy(16.dp),
                    ) { content(displayed) }
                }
            }
        }
    }
}

@Composable
private fun ConnectionPanel(model: CameraViewModel) {
    val host by model.host.collectAsStateWithLifecycle()
    val link by model.link.collectAsStateWithLifecycle()
    val code by model.accessCode.collectAsStateWithLifecycle()
    val desktops by model.desktops.collectAsStateWithLifecycle()
    val discovering by model.discovering.collectAsStateWithLifecycle()
    LaunchedEffect(link.phase) {
        if (link.phase == LinkPhase.Disconnected || link.phase == LinkPhase.Error) {
            while (true) { model.discover(); kotlinx.coroutines.delay(3000) }
        }
    }
    val focus = LocalFocusManager.current
    val keyboard = LocalSoftwareKeyboardController.current
    if (link.phase != LinkPhase.Connected && link.phase != LinkPhase.Connecting) {
        desktops.forEach { desktop ->
            CameraAction(desktop.name + if (desktop.transport == "usb") " · USB" else "", { model.chooseDesktop(desktop) }, Modifier.fillMaxWidth(), enabled = desktop.ready, tag = "desktop_${desktop.host}")
        }
        CameraAction(stringResource(if (discovering) R.string.discovering_desktop else R.string.find_desktop), model::discover,
            Modifier.fillMaxWidth(), enabled = !discovering, tag = "discover_desktop")
        Text(stringResource(R.string.usb_connection_hint), color = CameraColors.Muted, fontSize = 12.sp)
    }
    OutlinedTextField(
        value = host, onValueChange = model::setHost, label = { Text(stringResource(R.string.pc_address)) }, singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri, autoCorrectEnabled = false, imeAction = ImeAction.Done),
        keyboardActions = KeyboardActions(onDone = { keyboard?.hide(); focus.clearFocus() }),
        modifier = Modifier.fillMaxWidth().testTag("host_input"), enabled = link.phase != LinkPhase.Connecting && link.phase != LinkPhase.Connected,
    )
    if (link.phase != LinkPhase.Connected && link.phase != LinkPhase.Connecting) OutlinedTextField(
        value = code, onValueChange = model::setAccessCode, label = { Text(stringResource(R.string.desktop_code)) }, singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword, imeAction = ImeAction.Done),
        modifier = Modifier.fillMaxWidth().testTag("access_code"),
    )
    if (link.phase == LinkPhase.Error) Text(stringResource(when (link.failure) {
        LinkFailure.MissingAddress -> R.string.address_required; LinkFailure.AccessCode -> R.string.access_code_error; else -> R.string.connection_error
    }),
        color = CameraColors.Pink, fontSize = 13.sp, modifier = Modifier.testTag("connection_error"))
    if (link.phase == LinkPhase.Connected || link.phase == LinkPhase.Connecting) {
        Text(stringResource(if (link.phase == LinkPhase.Connected) R.string.connected else R.string.connecting), color = CameraColors.Muted,
            fontSize = 14.sp, modifier = Modifier.testTag("connection_phase").semantics { stateDescription = link.phase.name })
        CameraAction(stringResource(R.string.disconnect), { model.disconnect() }, Modifier.fillMaxWidth(), tag = "disconnect")
    } else CameraAction(stringResource(R.string.connect), model::connect, Modifier.fillMaxWidth(), Glyph.Link, primary = true, tag = "connect")
}

@Composable
private fun ZoomPanel(zoom: Float, onZoom: (Float) -> Unit) {
    val description = stringResource(R.string.zoom_description)
    Slider(zoom, onZoom, valueRange = 1f..3f, colors = cameraSliderColors(), modifier = Modifier.testTag("zoom_slider").semantics { contentDescription = description })
    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        listOf(1f, 1.5f, 2f, 3f).forEach { value -> CameraAction(String.format(Locale.ROOT, "%.1f×", value), { onZoom(value) }, Modifier.widthIn(min = 48.dp), tag = "zoom_$value", horizontalPadding = 8.dp) }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun LanguagePicker() {
    val current = LocalConfiguration.current.locales[0].language
    Text(stringResource(R.string.language), color = CameraColors.Muted, fontSize = 14.sp)
    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        listOf("zh" to R.string.language_zh, "en" to R.string.language_en, "ru" to R.string.language_ru).forEach { (tag, name) ->
            CameraAction(stringResource(name), { AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(tag)) },
                Modifier.semantics { selected = current == tag }, primary = current == tag, tag = "language_$tag", horizontalPadding = 12.dp)
        }
    }
}

@Composable
private fun DeveloperPanel(model: CameraViewModel) {
    var network by rememberSaveable { mutableStateOf(false) }
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        CameraAction(stringResource(R.string.pose), { network = false }, Modifier.weight(1f), primary = !network, tag = "pose_tab")
        CameraAction(stringResource(R.string.network), { network = true }, Modifier.weight(1f), primary = network, tag = "network_tab")
    }
    if (network) {
        val link by model.link.collectAsStateWithLifecycle()
        val host by model.host.collectAsStateWithLifecycle()
        Metric(stringResource(R.string.target), "$host:${CameraViewModel.PORT}")
        Metric(stringResource(R.string.transport), stringResource(R.string.transport_value))
        Metric(stringResource(R.string.status), stringResource(when (link.phase) {
            LinkPhase.Connected -> R.string.connected; LinkPhase.Connecting -> R.string.connecting
            LinkPhase.Disconnected -> R.string.disconnected; LinkPhase.Error -> R.string.connection_failed
        }))
        Metric(stringResource(R.string.acknowledgements), link.acknowledged.toString(), "network_ack")
        Metric(stringResource(R.string.round_trip), "${link.roundTripMs} ms")
        val video by model.video.collectAsStateWithLifecycle()
        Metric("Video", "${video.phase} / ${video.width}×${video.height} / ${video.frames}", "video_frames")
        Metric("Decoder", video.decoder)
        Metric("First frame / reconnects / buffer", "${video.firstFrameMs} ms / ${video.reconnects} / ${video.bufferedMs} ms")
        if (video.error.isNotEmpty()) Text(video.error, color = CameraColors.Pink, fontSize = 13.sp)
        if (link.cameraPhase.isNotEmpty()) {
            Metric(stringResource(R.string.host_camera), "${link.cameraId} / ${link.cameraPhase}")
            Metric(stringResource(R.string.game_scene), link.scene)
            Metric(stringResource(R.string.applied_frame), link.appliedSequence.toString())
            Metric("FOV", String.format(Locale.ROOT, "%.2f°", link.fieldOfView))
        }
        if (link.error.isNotEmpty()) Text(link.error, color = CameraColors.Pink, fontSize = 13.sp)
    } else {
        val source by model.source.collectAsStateWithLifecycle()
        val tracked by model.trackedPose.collectAsStateWithLifecycle()
        if (source == PoseSource.ARCore) {
            Metric("ARCore", tracked.detail, "tracking_status")
            Metric("XYZ", tracked.position.joinToString { String.format(Locale.ROOT, "%.3f", it) })
            Metric("Quaternion XYZW", tracked.rotation.joinToString { String.format(Locale.ROOT, "%.3f", it) })
            CameraAction(stringResource(R.string.manual), { model.selectSource(PoseSource.Manual) }, Modifier.fillMaxWidth(), tag = "manual_fallback")
            return
        }
        val pose by model.pose.collectAsStateWithLifecycle()
        SectionLabel(stringResource(R.string.translation))
        PoseAxis.entries.take(3).forEach { AxisControl(it, pose.value(it), model::setAxis) }
        SectionLabel(stringResource(R.string.rotation))
        PoseAxis.entries.drop(3).forEach { AxisControl(it, pose.value(it), model::setAxis) }
        CameraAction(stringResource(R.string.reset_axes), model::recenter, Modifier.fillMaxWidth(), Glyph.Recenter)
    }
}

@Composable
private fun AxisControl(axis: PoseAxis, value: Float, onChange: (PoseAxis, Float) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text(axis.label, Modifier.weight(1f), color = CameraColors.White, fontSize = 15.sp)
            Text(String.format(Locale.ROOT, if (axis.unit == "m") "%+.2f m" else "%+.1f°", value), color = CameraColors.Pink, fontFamily = FontFamily.Monospace, fontSize = 14.sp)
        }
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            CameraIconButton(Glyph.Minus, stringResource(R.string.decrease_axis, axis.label), { onChange(axis, (value - axis.step).coerceIn(axis.range)) })
            Slider(value, { onChange(axis, it) }, valueRange = axis.range, colors = cameraSliderColors(),
                modifier = Modifier.weight(1f).testTag("axis_${axis.name}").semantics { contentDescription = "${axis.label} ${axis.unit}" })
            CameraIconButton(Glyph.Plus, stringResource(R.string.increase_axis, axis.label), { onChange(axis, (value + axis.step).coerceIn(axis.range)) })
        }
    }
}

@Composable
private fun cameraSliderColors() = SliderDefaults.colors(
    thumbColor = CameraColors.Pink,
    activeTrackColor = CameraColors.Pink,
    inactiveTrackColor = CameraColors.White.copy(alpha = 0.4f),
)

@Composable
private fun SectionLabel(text: String) = Text(text, color = CameraColors.Muted, fontSize = 11.sp, letterSpacing = 1.3.sp)

@Composable
private fun Metric(label: String, value: String, tag: String = label) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(label, color = CameraColors.Muted, fontSize = 12.sp)
        Text(value, Modifier.testTag(tag), color = CameraColors.White, fontSize = 16.sp, fontFamily = FontFamily.Monospace)
    }
}

@Preview(name = "Portrait", widthDp = 393, heightDp = 852)
@Preview(name = "Compact", widthDp = 375, heightDp = 667)
@Preview(name = "Landscape", widthDp = 852, heightDp = 393)
@Composable
private fun ViewfinderPreview() {
    CameraTheme { ViewfinderScreen(LinkPhase.Disconnected, 1f, true, onConnect = {}, onRecord = {}, onZoom = {}, onMode = {}, onMore = {}) }
}
