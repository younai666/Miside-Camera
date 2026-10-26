package androidx.media3.exoplayer.rtsp

import org.junit.Assert.*
import org.junit.Test

/** Exercises the actual instrumented dependency packaged in the APK. */
@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
class RtpSequenceRolloverTest {
    private fun packet(sequence: Int) = RtpPacket.Builder().setSequenceNumber(sequence).build()

    @Test fun consecutivePacketsSurviveThreeFullSequenceCycles() {
        val queue = RtpPacketReorderingQueue()
        repeat(3 * 65536 + 4) { index ->
            val sequence = (65534 + index) and 65535
            assertTrue("Rejected sequence $sequence", queue.offer(packet(sequence), index.toLong()))
            assertEquals(sequence, queue.poll(index.toLong())!!.sequenceNumber)
        }
    }

    @Test fun reorderingAndDuplicateRejectionAcrossZero() {
        val queue = RtpPacketReorderingQueue()
        assertTrue(queue.offer(packet(65534), 1))
        assertEquals(65534, queue.poll(1)!!.sequenceNumber)
        assertTrue(queue.offer(packet(0), 3))
        assertTrue(queue.offer(packet(65535), 2))
        assertEquals(65535, queue.poll(0)!!.sequenceNumber)
        assertEquals(0, queue.poll(0)!!.sequenceNumber)
        assertFalse(queue.offer(packet(65535), 4))
        assertTrue(queue.offer(packet(1), 5))
        assertEquals(1, queue.poll(5)!!.sequenceNumber)
    }
}
