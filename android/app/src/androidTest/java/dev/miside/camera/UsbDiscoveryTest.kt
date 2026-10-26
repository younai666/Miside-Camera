package dev.miside.camera

import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import org.junit.Assert.*
import org.junit.Test

class UsbDiscoveryTest {
    @Test fun interfaceBoundDiscoveryUsesItsSourceAndRetainsAuthenticationMetadata() {
        val loopback = InetAddress.getByName("127.0.0.1")
        DatagramSocket(0, loopback).use { server ->
            server.soTimeout = 1500
            val worker = Executors.newSingleThreadExecutor()
            try {
                val response = worker.submit {
                    val query = DatagramPacket(ByteArray(256), 256)
                    server.receive(query)
                    assertEquals(loopback, query.address)
                    assertEquals("MISIDE_CAMERA_DISCOVER_V1", String(query.data, 0, query.length, Charsets.UTF_8))
                    val bytes = """{"service":"miside-camera","version":1,"name":"USB test","controlPort":39570,"videoPort":8554,"requiresCode":true,"ready":true,"transport":"usb"}""".toByteArray()
                    server.send(DatagramPacket(bytes, bytes.size, query.socketAddress))
                }
                val peers = probeDiscovery(loopback, listOf(loopback), emptyList(), server.localPort)
                response.get(2, TimeUnit.SECONDS)
                assertEquals(1, peers.size)
                assertEquals("127.0.0.1", peers.single().host)
                assertEquals("usb", peers.single().transport)
                assertTrue(peers.single().requiresCode)
                assertTrue(peers.single().ready)
            } finally { worker.shutdownNow() }
        }
    }
}
