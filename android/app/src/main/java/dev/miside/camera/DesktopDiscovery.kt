package dev.miside.camera

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.*
import java.io.ByteArrayOutputStream
import java.io.IOException

data class DiscoveredDesktop(val name: String, val host: String, val requiresCode: Boolean, val ready: Boolean, val transport: String = "lan")

internal data class DiscoveryRoute(val local: InetAddress, val broadcast: InetAddress)

/** Includes tethering downstream interfaces, which need not be Android's default network. */
internal fun discoveryRoutes(): List<DiscoveryRoute> = NetworkInterface.getNetworkInterfaces().toList()
    .filter { it.isUp && !it.isLoopback }
    .flatMap { adapter -> adapter.interfaceAddresses.mapNotNull { value ->
        val local = value.address
        val broadcast = value.broadcast
        if (local is Inet4Address && broadcast is Inet4Address) DiscoveryRoute(local, broadcast) else null
    } }

/** IP discovery only: no USB debugging, ADB tunnel, subnet scan, or Internet service. */
suspend fun discoverDesktops(knownHost: String = "10.0.2.2"): List<DiscoveredDesktop> = withContext(Dispatchers.IO) {
    coroutineScope {
        val direct = async { probeDiscovery(null, listOf(InetAddress.getByName("10.0.2.2"), InetAddress.getByName("255.255.255.255")),
            listOf("10.0.2.2", knownHost).filter { it.isNotBlank() }.distinct()) }
        val routes = try { discoveryRoutes() } catch (_: SocketException) { emptyList() }
        val interfaces = routes.map { route -> async { probeDiscovery(route.local, listOf(route.broadcast), emptyList()) } }
        (direct.await() + interfaces.flatMap { it.await() }).distinctBy { it.host }
    }
}

internal fun probeDiscovery(local: InetAddress?, destinations: List<InetAddress>, knownHosts: List<String>, port: Int = 39569): List<DiscoveredDesktop> {
    val found = linkedMapOf<String, DiscoveredDesktop>()
    fun accept(bytes: ByteArray, host: String) {
        try {
            val data = JSONObject(String(bytes, Charsets.UTF_8))
            if (data.getString("service") == "miside-camera" && data.getInt("version") == 1 &&
                data.getInt("controlPort") == CameraViewModel.PORT && data.getInt("videoPort") == 8554) {
                found[host] = DiscoveredDesktop(data.getString("name").take(80), host, data.getBoolean("requiresCode"),
                    data.getBoolean("ready"), data.optString("transport", "lan"))
            }
        } catch (_: org.json.JSONException) { /* Not our discovery reply. */ }
    }
    try {
        DatagramSocket(InetSocketAddress(local, 0)).use { socket ->
            socket.broadcast = true
            socket.soTimeout = 200
            val query = "MISIDE_CAMERA_DISCOVER_V1".toByteArray(Charsets.UTF_8)
            for (address in destinations) {
                try { socket.send(DatagramPacket(query, query.size, address, port)) }
                catch (_: IOException) { /* An unavailable interface must not suppress the remaining routes. */ }
            }
            for (address in knownHosts) {
                try {
                    Socket().use { client ->
                        client.connect(InetSocketAddress(address, port), 350)
                        client.soTimeout = 500
                        client.getOutputStream().write(query + byteArrayOf(10))
                        val reply = ByteArrayOutputStream()
                        val input = client.getInputStream()
                        while (reply.size() < 1024) {
                            val next = input.read()
                            if (next == 10) { accept(reply.toByteArray(), address); break }
                            if (next < 0) break
                            reply.write(next)
                        }
                    }
                } catch (_: IOException) { /* The interface-specific UDP request can still find this peer. */ }
            }
            val deadline = android.os.SystemClock.elapsedRealtime() + 1000
            while (android.os.SystemClock.elapsedRealtime() < deadline) {
                val packet = DatagramPacket(ByteArray(1024), 1024)
                try { socket.receive(packet) } catch (_: SocketTimeoutException) { continue }
                val host = packet.address.hostAddress ?: continue
                accept(packet.data.copyOfRange(packet.offset, packet.offset + packet.length), host)
            }
        }
    } catch (_: SocketException) { /* USB can disappear between interface enumeration and socket binding. */ }
    return found.values.toList()
}
