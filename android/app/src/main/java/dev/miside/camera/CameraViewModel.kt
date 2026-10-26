package dev.miside.camera

import android.os.SystemClock
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import java.io.IOException
import java.io.InputStream
import java.net.InetSocketAddress
import java.net.Socket
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import org.json.JSONException
import org.json.JSONObject

enum class LinkPhase { Disconnected, Connecting, Connected, Error }
enum class LinkFailure { MissingAddress, Transport, AccessCode }
private class DesktopAccessCodeException : IOException("Desktop access code does not match")

data class LinkState(
    val generation: Long = 0,
    val phase: LinkPhase = LinkPhase.Disconnected,
    val acknowledged: Long = 0,
    val roundTripMs: Long = 0,
    val error: String = "",
    val failure: LinkFailure? = null,
    val cameraPhase: String = "",
    val appliedSequence: Long = 0,
    val cameraId: Int = 0,
    val scene: String = "",
    val fieldOfView: Float = 0f,
    val cameraPosition: List<Float> = emptyList(),
    val cameraRotation: List<Float> = emptyList(),
    val recenter: Long = 0,
)

class CameraViewModel(private val saved: SavedStateHandle) : ViewModel() {
    val recording = GameRecording()
    private val mutablePose = MutableStateFlow(ManualPose.restore(saved["pose"]))
    val pose = mutablePose.asStateFlow()
    private val mutableZoom = MutableStateFlow(saved["zoom"] ?: 1f)
    val zoom = mutableZoom.asStateFlow()
    private val mutableHost = MutableStateFlow(saved["host"] ?: "10.0.2.2")
    val host = mutableHost.asStateFlow()
    private val mutableAccessCode = MutableStateFlow(saved["accessCode"] ?: "")
    val accessCode = mutableAccessCode.asStateFlow()
    private val mutableVideoCode = MutableStateFlow("")
    val videoCode = mutableVideoCode.asStateFlow()
    private val mutableDesktops = MutableStateFlow(emptyList<DiscoveredDesktop>())
    val desktops = mutableDesktops.asStateFlow()
    private val mutableDiscovering = MutableStateFlow(false)
    val discovering = mutableDiscovering.asStateFlow()
    fun setAccessCode(value: String) { mutableAccessCode.value = value.filter(Char::isDigit).take(6); saved["accessCode"] = mutableAccessCode.value }
    fun discover() {
        if (mutableDiscovering.value) return
        viewModelScope.launch {
            mutableDiscovering.value = true
            try { mutableDesktops.value = discoverDesktops(host.value.trim()) }
            catch (_: IOException) { mutableDesktops.value = emptyList() }
            finally { mutableDiscovering.value = false }
        }
    }
    fun chooseDesktop(value: DiscoveredDesktop) {
        setHost(value.host)
        if (!value.requiresCode) setAccessCode("")
        if (value.ready && (!value.requiresCode || accessCode.value.length == 6)) connect()
    }
    private val mutableLink = MutableStateFlow(LinkState())
    val link = mutableLink.asStateFlow()
    private val mutableSource = MutableStateFlow(PoseSource.Manual)
    val source = mutableSource.asStateFlow()
    private val mutableTrackedPose = MutableStateFlow(TrackedPose())
    val trackedPose = mutableTrackedPose.asStateFlow()
    private val mutableVideo = MutableStateFlow(VideoMetrics())
    val video = mutableVideo.asStateFlow()
    private val mutableVideoHost = MutableStateFlow<String?>(null)
    val videoHost = mutableVideoHost.asStateFlow()
    fun updateVideo(value: VideoMetrics) { mutableVideo.value = value }
    private var connection: Job? = null
    private var socket: Socket? = null
    private var retry: Job? = null
    private var connectionWanted = false
    private var retryDelay = 500L
    private val controlLock = Any()
    private var recenterGeneration = saved["recenter"] ?: 0L
    private var trackingSession = 0L

    fun selectSource(value: PoseSource) = synchronized(controlLock) {
        mutableSource.value = value
        if (value == PoseSource.ARCore) mutableTrackedPose.value = TrackedPose()
    }

    fun updateTrackedPose(value: TrackedPose) = synchronized(controlLock) {
        if (source.value == PoseSource.ARCore && value.tracking && value.sessionId != trackingSession) {
            trackingSession = value.sessionId
            saved["recenter"] = ++recenterGeneration
        }
        mutableTrackedPose.value = value
    }

    fun setAxis(axis: PoseAxis, value: Float) = synchronized(controlLock) {
        val next = mutablePose.value.withAxis(axis, value)
        saved["pose"] = next.values()
        mutablePose.value = next
    }

    fun recenter() = synchronized(controlLock) {
        saved["recenter"] = ++recenterGeneration
        saved["pose"] = ManualPose().values()
        mutablePose.value = ManualPose()
    }

    fun setZoom(value: Float) = synchronized(controlLock) {
        require(value.isFinite() && value in 1f..3f)
        saved["zoom"] = value
        mutableZoom.value = value
    }

    fun setHost(value: String) {
        saved["host"] = value
        mutableHost.value = value
    }

    fun disconnect(preserveVideo: Boolean = false) {
        if (!preserveVideo) recording.stop()
        // Closing the socket interrupts a blocked connect/read immediately.
        mutableLink.update { LinkState(generation = it.generation + 1) }
        connection?.cancel()
        socket?.close()
        socket = null
        retry?.cancel(); retry = null
        if (!preserveVideo) { connectionWanted = false; mutableVideoHost.value = null }
    }

    fun connect() { retryDelay = 500; openConnection() }

    private fun openConnection() {
        disconnect(preserveVideo = true)
        connectionWanted = true
        val destination = host.value.trim()
        val generation = link.value.generation
        if (destination.isBlank()) {
            mutableLink.update { it.copy(phase = LinkPhase.Error, error = "PC address is required", failure = LinkFailure.MissingAddress) }
            return
        }
        mutableVideoHost.value = destination
        mutableVideoCode.value = accessCode.value
        val client = Socket()
        socket = client
        mutableLink.update { it.copy(phase = LinkPhase.Connecting) }
        connection = viewModelScope.launch {
            try {
                withContext(Dispatchers.IO) {
                    client.tcpNoDelay = true
                    client.soTimeout = 1500
                    client.connect(InetSocketAddress(destination, PORT), 2500)
                    val input = client.getInputStream().buffered()
                    val output = client.getOutputStream().bufferedWriter(Charsets.UTF_8)
                    var sequence = 0L
                    while (currentCoroutineContext().isActive) {
                        val start = SystemClock.elapsedRealtime()
                        val packet = synchronized(controlLock) {
                            val p = pose.value
                            val tracked = trackedPose.value
                            val ar = source.value == PoseSource.ARCore
                            val position = if (ar) tracked.position else listOf(p.x, p.y, p.z)
                            val q = if (ar) tracked.rotation else p.quaternion().toList()
                            JSONObject()
                            .put("version", 2)
                            .put("sequence", ++sequence)
                            .put("timestampNs", SystemClock.elapsedRealtimeNanos())
                            .put("source", if (ar) "arcore" else "manual")
                            .put("position", JSONObject().put("x", position[0]).put("y", position[1]).put("z", position[2]))
                            .put("rotation", JSONObject().put("x", q[0]).put("y", q[1]).put("z", q[2]).put("w", q[3]))
                            .put("zoom", zoom.value)
                            .put("recenter", recenterGeneration).put("tracking", !ar || tracked.isCurrent())
                            .put("accessToken", accessCode.value)
                            .put("client", JSONObject().put("name", android.os.Build.MODEL.take(80))
                                .put("roundTripMs", link.value.roundTripMs).put("videoPhase", video.value.phase)
                                .put("width", video.value.width).put("height", video.value.height)
                                .put("frames", video.value.frames).put("bufferedMs", video.value.bufferedMs.coerceAtLeast(0)))
                        }
                        output.write(packet.toString())
                        output.newLine()
                        output.flush()
                        val ack = readPoseAcknowledgement(input)
                        if (ack.optString("error") == "access-code") throw DesktopAccessCodeException()
                        if (ack.getInt("version") != 1 || ack.getLong("ack") != sequence) {
                            throw IOException("PC 确认序号不匹配")
                        }
                        currentCoroutineContext().ensureActive()
                        val hostState = ack.optJSONObject("host")
                        retryDelay = 500
                        mutableLink.update {
                            if (it.generation != generation) it else it.copy(
                                phase = LinkPhase.Connected,
                                acknowledged = sequence,
                                roundTripMs = SystemClock.elapsedRealtime() - start,
                                cameraPhase = hostState?.optString("phase") ?: "",
                                appliedSequence = hostState?.optLong("appliedSequence") ?: 0,
                                cameraId = hostState?.optInt("cameraId") ?: 0,
                                scene = hostState?.optString("scene") ?: "",
                                fieldOfView = hostState?.optDouble("fieldOfView", 0.0)?.toFloat() ?: 0f,
                                cameraPosition = hostState?.let { h -> listOf("x", "y", "z").map { h.optDouble(it, 0.0).toFloat() } } ?: emptyList(),
                                cameraRotation = hostState?.let { h -> listOf("qx", "qy", "qz", "qw").map { h.optDouble(it, 0.0).toFloat() } } ?: emptyList(),
                                recenter = hostState?.optLong("recenter") ?: 0,
                            )
                        }
                        // Sample the latest pose; never queue obsolete slider positions.
                        delay((16L - (SystemClock.elapsedRealtime() - start)).coerceAtLeast(0L))
                    }
                }
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (failure: Exception) {
                if (failure !is IOException && failure !is JSONException && failure !is IllegalArgumentException) throw failure
                mutableLink.update {
                    if (it.generation != generation) it else it.copy(
                        phase = LinkPhase.Error,
                        failure = if (failure is DesktopAccessCodeException) LinkFailure.AccessCode else LinkFailure.Transport,
                        error = failure.message ?: "无法连接 PC",
                    )
                }
                if (connectionWanted && link.value.generation == generation && failure is IOException && failure !is DesktopAccessCodeException) {
                    val wait = retryDelay; retryDelay = (retryDelay * 2).coerceAtMost(5000)
                    retry = viewModelScope.launch {
                        delay(wait)
                        if (connectionWanted && link.value.generation == generation) { retry = null; openConnection() }
                    }
                }
            } finally {
                client.close()
            }
        }
    }

    override fun onCleared() {
        disconnect()
        super.onCleared()
    }

    companion object { const val PORT = 39570 }
}

internal fun readPoseAcknowledgement(input: InputStream): JSONObject {
    val bytes = ByteArray(4096)
    var length = 0
    while (true) {
        val next = input.read()
        if (next == -1) throw IOException("PC 已关闭连接")
        if (next == 10) return JSONObject(String(bytes, 0, length, Charsets.UTF_8))
        if (length == bytes.size) throw IOException("PC acknowledgement exceeds 4096 bytes")
        bytes[length++] = next.toByte()
    }
}
