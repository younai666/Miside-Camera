# Application icons

The project owner supplied a transparent PNG containing the Desktop artwork on
the left and Android artwork on the right. `desktop.png` and `android.png` are
lossless crops split at the exact vertical midpoint. The original artwork,
colors, aspect ratio and alpha are retained; no artwork was redrawn or plated.

For platform exports only, almost-transparent outlying pixels (alpha=1) are
excluded from the layout bounds, with a 16 px margin around visible artwork.
Pixels inside those bounds are preserved before proportional resampling.

## Regenerate

Requires Python and Pillow as development tools, not application dependencies:

```sh
python tools/generate_app_icons.py
# To replace the supplied two-part source:
python tools/generate_app_icons.py --source /path/to/combined-transparent.png
```

- Windows: `windows/desktop.ico` contains 16, 20, 24, 30, 32, 36, 40, 48,
  60, 64, 72, 80, 96, 128 and 256 px images. WPF embeds it in the executable
  and sets the window icon; the custom caption uses `windows/desktop-256.png`.
  A standalone ICO is also copied into build and publish directories.
- Android: the APK targets API 26+, so it uses adaptive icons on every supported
  Android version. Unused legacy PNG exports are not retained.
  Adaptive foregrounds use 108 dp canvases with proportionally fitted 66 dp
  artwork and a transparent background layer. The manifest's `icon` uses the
  adaptive resource; no separate round silhouette or monochrome redesign is
  introduced. Device launchers control the final mask, backing and system
  theming. The verified Pixel launcher adds its own circular backing even
  though the supplied background layer is transparent. No square or circular
  plate is baked into the resources.
- ZIP archives have no per-file embedded Windows icon. The executable and
  standalone ICO inside the release directory carry the Desktop branding.

References: [Windows icon sizes](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction)
and [Android adaptive icons](https://developer.android.com/develop/ui/compose/system/icon_design_adaptive).

## Artwork rights

User-supplied branding artwork; original rights are retained by its rights
holder. The source-code MIT license does not independently grant rights to
this artwork. No third-party authorship or license grant is inferred.
