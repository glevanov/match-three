package com.matchthree.ui.game

import android.content.res.AssetManager
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.asAndroidPath
import com.matchthree.game.model.GemType
import com.matchthree.game.model.Special

/**
 * Loads the PNG art bundled in app/src/main/assets (tiny, already-compressed
 * sprites; decode is cheap and synchronous). Decoded with no density scaling,
 * so a 256px PNG stays a 256px sprite.
 *
 * - One sprite per [GemType]; Hypercube is colorless (MECHANICS.md) so it has
 *   its own sprite instead of tinting a color.
 * - Orange renders with white.png — the orange art reads almost the same as
 *   yellow on the board; the gem type stays ORANGE logically.
 * - [Special.FLAME] / [Special.STAR] are layered over the base color sprite by
 *   BoardCanvas. For each an alpha-tracing [outlineFor] path is built once so
 *   the outline follows the sprite silhouette rather than its bounding rect.
 */
object GemSprites {
    /** Star overlay alpha (see-through sparkle over the gem). */
    const val STAR_OVERLAY_ALPHA = 0.55f

    /** Star outline alpha — readable on any base color. */
    const val STAR_OUTLINE_ALPHA = 0.7f

    /** Flame overlay size as a fraction of the gem (inset keeps it inside). */
    const val FLAME_OVERLAY_FRACTION = 0.45f

    /** Fraction of the cell a gem sprite spans; the rest is gutter between gems. */
    const val GEM_WIDTH_FRACTION = 0.94f

    private var loaded = false

    private lateinit var color: Map<GemType, ImageBitmap>
    private var hypercube: ImageBitmap? = null
    private var specialSprite: Map<Special, ImageBitmap> = emptyMap()
    private var outline: Map<Special, Path> = emptyMap()

    /** Base sprite to draw for a gem of this appearance. */
    fun spriteFor(special: Special?, type: GemType): ImageBitmap? = when (special) {
        Special.HYPERCUBE -> hypercube
        Special.FLAME, Special.STAR, null -> color[type]
    }

    /** Center overlay stacked on [spriteFor], if any. */
    fun overlayFor(special: Special?): ImageBitmap? = when (special) {
        Special.FLAME, Special.STAR -> specialSprite[special]
        Special.HYPERCUBE, null -> null
    }

    /** Alpha-traced outline of that overlay, sized to the sprite's own pixels. */
    fun outlineFor(special: Special?): Path? = when (special) {
        Special.FLAME, Special.STAR -> outline[special]
        Special.HYPERCUBE, null -> null
    }

    /** Size of the overlay as a fraction of the base sprite. */
    fun overlayFraction(special: Special?): Float = when (special) {
        Special.FLAME -> FLAME_OVERLAY_FRACTION
        else -> 1f
    }

    /** Alpha of the overlay (Star see-through, Flame solid). */
    fun overlayAlpha(special: Special?): Float = when (special) {
        Special.STAR -> STAR_OVERLAY_ALPHA
        else -> 1f
    }

    /** Alpha of the silhouette outline. */
    fun outlineAlpha(special: Special?): Float = when (special) {
        Special.STAR -> STAR_OUTLINE_ALPHA
        else -> 1f
    }

    fun load(assets: AssetManager) {
        if (loaded) return
        loaded = true
        color = mapOf(
            GemType.RED to decode(assets, "red.png").asImageBitmap(),
            GemType.GREEN to decode(assets, "green.png").asImageBitmap(),
            GemType.BLUE to decode(assets, "blue.png").asImageBitmap(),
            GemType.YELLOW to decode(assets, "yellow.png").asImageBitmap(),
            GemType.PURPLE to decode(assets, "purple.png").asImageBitmap(),
            GemType.ORANGE to decode(assets, "white.png").asImageBitmap(),
        )
        hypercube = decode(assets, "hypercube.png").asImageBitmap()
        val flameBitmap = decode(assets, "flame.png")
        val sparkleBitmap = decode(assets, "sparkle.png")
        specialSprite = mapOf(
            Special.FLAME to flameBitmap.asImageBitmap(),
            Special.STAR to sparkleBitmap.asImageBitmap(),
        )
        outline = mapOf(
            Special.FLAME to traceAlpha(flameBitmap),
            Special.STAR to traceAlpha(sparkleBitmap),
        )
    }

    /** Decode a bundled PNG; raw [Bitmap] so callers can composite or path-trace. */
    private fun decode(assets: AssetManager, name: String): Bitmap =
        assets.open(name).use { input ->
            BitmapFactory.decodeStream(input) ?: error("Failed to decode asset: $name")
        }

    /**
     * Traces the outer boundary where the bitmap's alpha channel transitions
     * from transparent to opaque, returning a [Path] in image pixel coordinates
     * that BoardCanvas can scale to whatever draw size it picks.
     */
    private fun traceAlpha(bitmap: Bitmap): Path {
        val w = bitmap.width
        val h = bitmap.height
        val pixels = IntArray(w * h)
        bitmap.getPixels(pixels, 0, w, 0, 0, w, h)
        val path = android.graphics.Path()
        for (y in 0 until h) {
            for (x in 0 until w) {
                val i = y * w + x
                val opaque = (pixels[i] ushr 24) != 0
                if (!opaque) continue
                val isBoundary = x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                    // Any transparent 4-neighbour makes this pixel part of the outline.
                    (pixels[i - 1] ushr 24) == 0 ||
                    (pixels[i + 1] ushr 24) == 0 ||
                    (pixels[i - w] ushr 24) == 0 ||
                    (pixels[i + w] ushr 24) == 0
                if (isBoundary) {
                    // Collect 1px squares; we merge them by union later if needed,
                    // but for sprites <300px a per-pixel moveTo/lineTo is ~40k ops
                    // and still cheap enough at load time.
                    path.addRect(x.toFloat(), y.toFloat(), x + 1f, y + 1f, android.graphics.Path.Direction.CW)
                }
            }
        }
        // Union the per-pixel squares into a single silhouette so the outline is
        // one continuous stroke and not a noisy set of self-intersecting squares.
        val unified = android.graphics.Path()
        unified.op(path, android.graphics.Path.Op.UNION)
        return Path().apply { asAndroidPath().set(unified) }
    }
}
