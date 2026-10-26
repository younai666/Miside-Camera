package dev.miside.camera

import java.io.ByteArrayInputStream
import java.io.IOException
import org.junit.Assert.*
import org.junit.Test

class PoseAcknowledgementTest {
    @Test fun utf8AndAdjacentFramesPreserveTheirBoundaries() {
        val frame = """{"version":1,"ack":1,"host":{"scene":"米塔的房间"}}"""
        val input = ByteArrayInputStream((frame + "\n" + frame + "\n").toByteArray(Charsets.UTF_8))
        repeat(2) {
            assertEquals("米塔的房间", readPoseAcknowledgement(input).getJSONObject("host").getString("scene"))
        }
        assertEquals(-1, input.read())
    }

    @Test fun truncatedAndOversizeAcknowledgementsAreRejected() {
        for (bytes in listOf("{\"ack\":1}".toByteArray(), ByteArray(4097) { ' '.code.toByte() })) {
            try {
                readPoseAcknowledgement(ByteArrayInputStream(bytes))
                fail("Invalid acknowledgement accepted")
            } catch (_: IOException) { }
        }
    }
}
