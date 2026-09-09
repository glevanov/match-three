shader_type canvas_item;
render_mode unshaded, blend_add;
// Procedural pulsing glow for Star-special gems. The static star_06.png
// overlay (Kenney particle pack; GemActor's Overlay/Art, 30% alpha) stays
// as the crisp, readable glint shape; this shader draws a soft ADDITIVE
// bloom behind/around it that
// breathes slowly, echoing Kenney's particle-pack "star flare" look (bright
// core, soft falling-off rays, gentle swell) instead of a flat static image.
//
// Same technique as gem_fire.gdshader: mask_texture's alpha IS the star
// shape (star_06.png, sampled at StarGlowRectScale x the sprite's own
// footprint so there's margin for the halo to bleed outward without being
// clipped (GemActor keeps the rect size and this scale in sync, same as
// FireRing/AuraRectScale). blend_add + unshaded for the same reasons as the
// flame aura: no Light2D in the scene, and additive glow only ever adds
// brightness on top of the gem underneath.
//
// The "breath": a single sine wave (TIME * pulse_speed + time_offset) drives
// both the glow's intensity (pulse_min..pulse_max) and a small zoom in/out
// of the sampled UV (scale_pulse) — the glint grows brighter and very
// slightly bigger at the same time, like a real glint catching light, then
// eases back down. time_offset is set per-gem (GemId-derived) by GemActor so
// a board full of Star gems doesn't pulse in unison.

uniform float time_offset = 0.0;
uniform vec4 glow_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float pulse_speed = 1.1;   // ~5.7s per full breath cycle at 1.1
uniform float pulse_min = 0.12;    // additive alpha at the low point of the breath
uniform float pulse_max = 0.33;    // additive alpha at the high point (kept under
                                   // the art's own 0.30 overlay so the bigger,
                                   // gem-filling star doesn't re-white the gem)
uniform float scale_pulse = 0.06;  // +/- fraction the glint swells with the breath
uniform float core_boost = 1.0;    // extra gain right on the sparkle's own silhouette
uniform float rect_scale = 1.5;    // glow rect vs sparkle footprint; matches GemActor
uniform sampler2D mask_texture : repeat_disable, filter_linear;

// Mask alpha at a texUV, treating anything outside the texture as transparent
// (same reasoning as gem_fire.gdshader: the art fills its border, so relying
// on sampler clamp-to-edge would leak the border's soft AA into the halo).
float maskAt(vec2 uv) {
    float inside = step(0.0, uv.x) * step(uv.x, 1.0)
                 * step(0.0, uv.y) * step(uv.y, 1.0);
    return texture(mask_texture, uv).a * inside;
}

// Average mask over a ring of `radius` texels — softens the sparkle's own
// edges at small radii, builds an outer bloom at larger ones.
float blur(vec2 uv, vec2 texel, float radius) {
    float m = maskAt(uv) * 0.4;
    for (int i = 0; i < 8; i++) {
        float angle = float(i) / 8.0 * 6.2831853;
        vec2 off = vec2(cos(angle), sin(angle)) * texel * radius;
        m += maskAt(uv + off) * 0.075;
    }
    return m;
}

void fragment() {
    vec2 texel = 1.0 / vec2(textureSize(mask_texture, 0));

    float phase = TIME * pulse_speed + time_offset;
    float breath = sin(phase) * 0.5 + 0.5; // 0..1, slow ease via the sine itself
    float intensity = mix(pulse_min, pulse_max, breath);

    // Rect is rect_scale x the sparkle's footprint (margin for the halo), and
    // on top of that we zoom slightly in/out with the same breath: the SPARKLE
    // renders at rectpx / s, so s must dip at the bright peak for the glint to
    // swell as it brightens (review fix: the original plus sign shrank it).
    float s = rect_scale * (1.0 - scale_pulse * (breath - 0.5) * 2.0);
    vec2 texUV = (UV - 0.5) * s + 0.5;

    float core = maskAt(texUV);
    float haloNear = blur(texUV, texel, 6.0);
    float haloFar  = blur(texUV, texel, 16.0);
    float glow = clamp(core * core_boost + haloNear * 0.6 + haloFar * 0.3, 0.0, 1.0);

    vec4 col = glow_color;
    col.a = glow * intensity;
    COLOR = col * COLOR;
}