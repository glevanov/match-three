shader_type canvas_item;
render_mode unshaded, blend_add;
// Procedural fire aura for Flame-special gems (docs: DECISIONS.md "Flame aura
// (visual)"). v8 (user-directed): the flame effect now lives in an outer band
// around the gem's silhouette — the gem's CENTER stays clean (no flame, no
// tint) — plus a SOFT halo of light bleeding a little way past the silhouette
// into the surrounding cells (the earlier hard no-bleed boundary was relaxed
// deliberately at the user's request; the halo intensity is low so it reads
// as glow, not obstruction).
//
// The gem's own base-texture alpha is the shape: every GemType has a different
// silhouette (round, teardrop, diamond, squircle, ...). The FireRing rect is
// AuraRectScale x Base's drawn footprint (GemActor keeps these in sync), and
// texUV = (UV - 0.5) / rect_scale + 0.5 maps the rect onto the texture 1:1
// with margin for the halo. Distance from the silhouette edge is approximated
// by eroding the mask (min over a ring of neighboring texels):
//   - rim        : crisp edge ring (rim_distance)
//   - band       : wider erode (band_distance) — flame veins live here only
//   - halo       : average-of-mask minus mask — 0 in the center, soft outside
// Per-gem tint: core_color/rim_color are set from the gem art by GemActor
// (GemSprites.AuraColors). Additive so the glow brightens, never obscures.
// render_mode unshaded keeps it out of the 2D lighting pass — with no Light2D
// in the scene a lit canvas_item would multiply by zero light and render
// black. Pure View-layer effect.
//
// The final color is multiplied by the incoming COLOR varying (the canvas-item
// modulate chain) so VanishAsync's parent modulate:a fade cascades through the
// shader output: with blend_add the add amount scales with col.a, which drops
// to zero as the parent fades.

uniform float time_offset = 0.0;
// core/rim are set per gem by GemActor (GemSprites.AuraColors). The defaults
// below are only a neutral fallback for materials created outside that path.
uniform vec4 core_color : source_color = vec4(0.9, 0.9, 0.95, 1.0);
uniform vec4 rim_color  : source_color = vec4(0.7, 0.7, 0.8, 1.0);
uniform float intensity = 0.85;      // additive gain for the flame band/rim
uniform float halo_intensity = 0.5;  // additive gain for the soft outer halo
uniform float rim_distance = 5.0;    // texels: crisp edge ring erosion radius
uniform float band_distance = 14.0;  // texels: flame-band extent (center stays clean)
uniform float rect_scale = 1.35;     // FireRing rect vs base footprint; texUV maps via `(UV - 0.5) / rect_scale + 0.5`
// The gem's own base texture — its alpha IS the shape. Samples are gated to
// the [0,1] bounds via maskAt() (outside the texture reads as transparent),
// so repeat/filter hints only matter inside the texture; repeat_disable keeps
// any in-bounds sampling near the border from wrapping to the far edge.
uniform sampler2D mask_texture : repeat_disable, filter_linear;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 4; i++) {
        v += amp * noise(p);
        p *= 2.0;
        amp *= 0.5;
    }
    return v;
}

// Mask alpha at a texUV, treating ANYTHING outside the texture bounds as
// transparent. The art fills its whole texture (silhouettes touch the border),
// so relying on the sampler's clamp-to-edge would read the border's soft AA
// (~0.7-0.8) instead of 0 outside the gem — silently killing the erosion
// rim/band and the halo at every edge that touches the border.
float maskAt(vec2 uv) {
    float inside = step(0.0, uv.x) * step(uv.x, 1.0)
                 * step(0.0, uv.y) * step(uv.y, 1.0);
    return texture(mask_texture, uv).a * inside;
}

// Minimum mask alpha over a ring of `radius` texels (erosion).
float erode(vec2 uv, vec2 texel, float radius) {
    float m = maskAt(uv);
    for (int i = 0; i < 8; i++) {
        float angle = float(i) / 8.0 * 6.2831853;
        vec2 off = vec2(cos(angle), sin(angle)) * texel * radius;
        m = min(m, maskAt(uv + off));
    }
    return m;
}

void fragment() {
    vec2 texel = 1.0 / vec2(textureSize(mask_texture, 0));
    // The rect is rect_scale x the texture footprint, so its UVs map to a
    // ZOOMED-IN texture space: texUV = (UV - 0.5) * rect_scale + 0.5 (the gem
    // occupies the central 1/rect_scale of the rect, with margin around it
    // for the halo).
    vec2 texUV = (UV - 0.5) * rect_scale + 0.5;
    float mask = maskAt(texUV);

    // Crisp edge ring: follows the actual silhouette for any shape.
    float rim = clamp((mask - erode(texUV, texel, rim_distance)) * 3.0, 0.0, 1.0);

    // Flame band: veins only within band_distance of the edge; the gem's
    // center stays clean (band -> 0 where the mask holds up over the radius).
    float band = clamp((mask - erode(texUV, texel, band_distance)) * 2.0, 0.0, 1.0);

    // Soft outer halo: blurred mask minus mask. 0 in the center (avg == mask),
    // a soft positive field outside the silhouette that falls off with
    // distance, and a gentle soft edge inside.
    float avg = mask * 0.5;
    for (int r = 1; r <= 3; r++) {
        float weight = r == 1 ? 0.3 : (r == 2 ? 0.2 : 0.1);
        for (int i = 0; i < 8; i++) {
            float angle = float(i) / 8.0 * 6.2831853;
            vec2 off = vec2(cos(angle), sin(angle)) * texel * (8.0 * float(r));
            avg += maskAt(texUV + off) * weight;
        }
    }
    avg /= 5.3; // 0.5 center + 8*0.3 + 8*0.2 + 8*0.1 (weights sum to 5.3)
    float halo = clamp(avg - mask, 0.0, 1.0);

    // Veins: mottled fbm, confined to the flame band (not the clean center).
    vec2 p = (texUV - 0.5) * 2.5 + vec2(0.0, -TIME * 0.4 + time_offset);
    float cracks = smoothstep(0.35, 0.65, fbm(p));
    float glow = clamp(cracks * band + rim, 0.0, 1.0);

    vec4 col = mix(core_color, rim_color, rim);
    col.a = glow * intensity * mask + halo * halo_intensity;

    COLOR = col * COLOR;
}