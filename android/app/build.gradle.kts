plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

androidComponents.onVariants { variant ->
    variant.instrumentation.transformClassesWith(
        dev.miside.tooling.RtpSequenceFix::class.java,
        com.android.build.api.instrumentation.InstrumentationScope.ALL,
    ) {}
}

val media3Notices = tasks.register<Copy>("copyMedia3Notices") {
    from(rootProject.file("../licenses/media3-LICENSE.txt"), rootProject.file("../licenses/media3-NOTICE.txt"))
    into(layout.buildDirectory.dir("generated/media3Notices/licenses"))
}

tasks.named("preBuild") { dependsOn(media3Notices) }

android {
    namespace = "dev.miside.camera"
    compileSdk = 36
    buildToolsVersion = "36.0.0"

    defaultConfig {
        applicationId = "dev.miside.camera"
        minSdk = 26
        targetSdk = 36
        versionCode = 4
        versionName = "0.2.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures { compose = true }
    sourceSets.getByName("main").assets.srcDir(layout.buildDirectory.dir("generated/media3Notices"))

    signingConfigs {
        if (providers.environmentVariable("MISIDE_SIGNING_STORE_FILE").isPresent) {
            create("release") {
                storeFile = file(providers.environmentVariable("MISIDE_SIGNING_STORE_FILE").get())
                storePassword = providers.environmentVariable("MISIDE_SIGNING_STORE_PASSWORD").get()
                keyAlias = providers.environmentVariable("MISIDE_SIGNING_KEY_ALIAS").get()
                keyPassword = providers.environmentVariable("MISIDE_SIGNING_KEY_PASSWORD").get()
            }
        }
    }
    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = signingConfigs.findByName("release")
        }
    }
}

dependencies {
    implementation(platform("androidx.compose:compose-bom:2025.10.01"))
    implementation("androidx.activity:activity-compose:1.11.0")
    implementation("androidx.appcompat:appcompat:1.7.1")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.9.4")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.9.4")
    implementation("com.google.ar:core:1.57.0")
    implementation("androidx.media3:media3-exoplayer:1.8.0")
    implementation("androidx.media3:media3-exoplayer-rtsp:1.8.0")
    implementation("androidx.media3:media3-ui:1.8.0")
    debugImplementation("androidx.compose.ui:ui-tooling")
    debugImplementation("androidx.compose.ui:ui-test-manifest")
    androidTestImplementation(platform("androidx.compose:compose-bom:2025.10.01"))
    androidTestImplementation("androidx.compose.ui:ui-test-junit4")
    androidTestImplementation("androidx.test:runner:1.6.2")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
}

kotlin {
    jvmToolchain(17)
}
