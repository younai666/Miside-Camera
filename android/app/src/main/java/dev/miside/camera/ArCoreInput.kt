package dev.miside.camera

import android.Manifest
import android.app.Activity
import android.content.pm.PackageManager
import android.opengl.GLES11Ext
import android.opengl.GLES20
import android.opengl.GLSurfaceView
import android.os.SystemClock
import androidx.activity.compose.LocalActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.google.ar.core.ArCoreApk
import com.google.ar.core.Config
import com.google.ar.core.Session
import com.google.ar.core.TrackingState
import javax.microedition.khronos.egl.EGLConfig
import javax.microedition.khronos.opengles.GL10

/** AR owns its camera/GL lifecycle, independently of the game video surface. */
@Composable
fun ArCoreInput(model: CameraViewModel) {
    val activity = LocalActivity.current ?: return
    val owner = LocalLifecycleOwner.current
    val configuration = LocalConfiguration.current
    var granted by remember { mutableStateOf(ContextCompat.checkSelfPermission(activity, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) }
    val permission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) {
        granted = it
        if (!it) model.updateTrackedPose(TrackedPose(detail = "Camera permission required"))
    }
    LaunchedEffect(Unit) { if (!granted) permission.launch(Manifest.permission.CAMERA) }
    if (!granted) return
    val view = remember(activity) { ArCoreSurface(activity) { value -> activity.runOnUiThread { model.updateTrackedPose(value) } } }
    // ARCore needs a current GL context and camera texture, but the Viewfinder displays PC video.
    AndroidView(factory = { view }, modifier = Modifier.size(1.dp), update = {
        it.rotationForDisplay = activity.window.decorView.display?.rotation ?: 0
        it.viewportWidth = configuration.screenWidthDp
        it.viewportHeight = configuration.screenHeightDp
    })
    DisposableEffect(owner, view) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) view.start()
            if (event == Lifecycle.Event.ON_PAUSE) view.stop()
        }
        owner.lifecycle.addObserver(observer)
        if (owner.lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) view.start()
        onDispose { owner.lifecycle.removeObserver(observer); view.close() }
    }
}

private class ArCoreSurface(private val activity: Activity, private val sample: (TrackedPose) -> Unit) : GLSurfaceView(activity), GLSurfaceView.Renderer {
    @Volatile private var session: Session? = null
    @Volatile var rotationForDisplay = 0
    @Volatile var viewportWidth = 1
    @Volatile var viewportHeight = 1
    private var texture = 0
    private var installRequested = false
    private var lastTimestamp = 0L
    private var lastPose = TrackedPose()
    private val sessionId = SystemClock.elapsedRealtimeNanos()
    init {
        setEGLContextClientVersion(2)
        setEGLConfigChooser(8, 8, 8, 8, 16, 0)
        preserveEGLContextOnPause = true
        setRenderer(this)
    }

    fun start() {
        try {
            if (session == null) {
                if (ArCoreApk.getInstance().requestInstall(activity, !installRequested) != ArCoreApk.InstallStatus.INSTALLED) {
                    installRequested = true
                    sample(TrackedPose(detail = "Install Google Play Services for AR"))
                    return
                }
                val created = Session(activity)
                try {
                    created.configure(Config(created).apply {
                        updateMode = Config.UpdateMode.LATEST_CAMERA_IMAGE
                        planeFindingMode = Config.PlaneFindingMode.DISABLED
                        lightEstimationMode = Config.LightEstimationMode.DISABLED
                        focusMode = Config.FocusMode.AUTO
                    })
                } catch (error: Exception) {
                    created.close()
                    throw error
                }
                session = created
            }
            session!!.resume()
            onResume()
        } catch (error: Exception) {
            sample(TrackedPose(detail = error.javaClass.simpleName + ": " + (error.message ?: "ARCore unavailable")))
        }
    }
    fun stop() { onPause(); session?.pause(); sample(lastPose.copy(tracking = false, detail = "Paused")) }
    fun close() { stop(); session?.close(); session = null }
    override fun onSurfaceCreated(gl: GL10?, config: EGLConfig?) {
        val ids = IntArray(1); GLES20.glGenTextures(1, ids, 0); texture = ids[0]
        GLES20.glBindTexture(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, texture)
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_LINEAR)
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_LINEAR)
    }
    override fun onSurfaceChanged(gl: GL10?, width: Int, height: Int) = Unit
    override fun onDrawFrame(gl: GL10?) {
        val current = session ?: return
        try {
            current.setCameraTextureName(texture)
            current.setDisplayGeometry(rotationForDisplay, viewportWidth, viewportHeight)
            val frame = current.update()
            if (frame.timestamp == 0L || frame.timestamp == lastTimestamp) return
            lastTimestamp = frame.timestamp
            val camera = frame.camera
            val pose = camera.displayOrientedPose
            lastPose = TrackedPose(pose.translation.toList(), pose.rotationQuaternion.toList(), camera.trackingState == TrackingState.TRACKING,
                SystemClock.elapsedRealtimeNanos(), if (camera.trackingState == TrackingState.TRACKING) "TRACKING" else camera.trackingFailureReason.name, sessionId)
            sample(lastPose)
        } catch (error: com.google.ar.core.exceptions.SessionPausedException) {
            // Lifecycle pause can race one final GL frame; stop() publishes the paused state.
        } catch (error: Exception) {
            sample(lastPose.copy(tracking = false, detail = error.javaClass.simpleName))
        }
    }
}
