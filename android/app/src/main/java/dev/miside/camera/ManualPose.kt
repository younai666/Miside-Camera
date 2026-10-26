package dev.miside.camera

data class ManualPose(
    val x: Float = 0f,
    val y: Float = 0f,
    val z: Float = 0f,
    val pitch: Float = 0f,
    val yaw: Float = 0f,
    val roll: Float = 0f,
) {
    fun withAxis(axis: PoseAxis, value: Float): ManualPose {
        require(value.isFinite() && value in axis.range)
        return when (axis) {
            PoseAxis.X -> copy(x = value)
            PoseAxis.Y -> copy(y = value)
            PoseAxis.Z -> copy(z = value)
            PoseAxis.Pitch -> copy(pitch = value)
            PoseAxis.Yaw -> copy(yaw = value)
            PoseAxis.Roll -> copy(roll = value)
        }
    }

    fun value(axis: PoseAxis): Float = when (axis) {
        PoseAxis.X -> x
        PoseAxis.Y -> y
        PoseAxis.Z -> z
        PoseAxis.Pitch -> pitch
        PoseAxis.Yaw -> yaw
        PoseAxis.Roll -> roll
    }

    fun values() = floatArrayOf(x, y, z, pitch, yaw, roll)

    /** Rz(roll) * Ry(yaw) * Rx(pitch), right-handed camera coordinates, XYZW. */
    fun quaternion(): FloatArray {
        val k = Math.PI / 360.0
        val sx = kotlin.math.sin(pitch * k); val cx = kotlin.math.cos(pitch * k)
        val sy = kotlin.math.sin(yaw * k); val cy = kotlin.math.cos(yaw * k)
        val sz = kotlin.math.sin(roll * k); val cz = kotlin.math.cos(roll * k)
        return floatArrayOf((cz * cy * sx - sz * sy * cx).toFloat(), (cz * sy * cx + sz * cy * sx).toFloat(),
            (sz * cy * cx - cz * sy * sx).toFloat(), (cz * cy * cx + sz * sy * sx).toFloat())
    }

    companion object {
        fun restore(values: FloatArray?): ManualPose = if (values == null) ManualPose()
            else ManualPose(values[0], values[1], values[2], values[3], values[4], values[5])
    }
}

enum class PoseAxis(val label: String, val unit: String, val range: ClosedFloatingPointRange<Float>, val step: Float) {
    X("X", "m", -5f..5f, 0.1f),
    Y("Y", "m", -5f..5f, 0.1f),
    Z("Z", "m", -5f..5f, 0.1f),
    Pitch("Pitch", "°", -180f..180f, 5f),
    Yaw("Yaw", "°", -180f..180f, 5f),
    Roll("Roll", "°", -180f..180f, 5f),
}
