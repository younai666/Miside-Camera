package dev.miside.camera

import android.os.SystemClock

enum class PoseSource { Manual, ARCore }

/** The same right-handed camera coordinates consumed by the PC Core. */
data class TrackedPose(
    val position: List<Float> = listOf(0f, 0f, 0f),
    val rotation: List<Float> = listOf(0f, 0f, 0f, 1f),
    val tracking: Boolean = false,
    val sampledAtNs: Long = 0,
    val detail: String = "",
    val sessionId: Long = 0,
) {
    fun isCurrent() = tracking && SystemClock.elapsedRealtimeNanos() - sampledAtNs < 500_000_000L
}
