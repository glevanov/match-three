shader_type canvas_item;
render_mode unshaded, blend_add;
// Procedural fire aura for Flame-special gems (docs: DECISIONS.md "Flame aura
// (visual)"). v6: additive blend + higher intensity — on-device measurement of
// v5 showed the aura rendered but was nearly invisible (mean RGB delta 3-27/
// 255 vs the plain gem; ~8% effective mid-body alpha), worst on light gems.
// Additive glow brightens the gem instead of tinting it, so it reads on every
// color and never obscures the gem beneath (non-obstructive by construction).
//
// The mask is the gem's own base-texture alpha: every GemType has a different
// silhouette (round, teardrop, diamond, squircle, ...), and the alpha mask
// traces whatever shape the art actually has. The FireRing rect is sized to
// Base's exact drawn footprint (texture dims x the same scale GemActor applies
// to Base), so this shader's UVs line up 1:1 with baseTexture's pixels. rim is
// derived by eroding the mask (min over a ring of neighboring texels) rather
// than by distance from center, so the hot edge hugs the true silhouette of
// any shape. render_mode unshaded keeps it out of the 2D lighting pass — with
// no Light2D in the scene a lit canvas_item would multiply by zero light and
// render black. Pure View-layer effect.
//
// The final color is multiplied by the incoming COLOR varying (the canvas-item
// modulate chain) so VanishAsync's parent modulate:a fade cascades through the
// shader output: with blend_add the add amount scales with col.a, which drops
// to zero as the parent fades.

uniform float time_offset = 0.0;
uniform vec4 core_color : source_color = vec4(1.0, 0.75, 0.25, 1.0); // warm inner glow
uniform vec4 rim_color  : source_color = vec4(1.0, 0.45, 0.05, 1.0); // hot rim
uniform float intensity = 0.85; // additive gain — raised from 0.35 after on-device
                                // measurement showed the aura was nearly invisible
// The gem's own base texture — its alpha IS the shape. repeat_disable keeps
// erosion samples near the texture border from wrapping to the far edge; the
// art is AA-soft at the crop line, so clamped samples still read lower alpha
// than the body and the rim fires correctly at the border.
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

void fragment() {
    float mask = texture(mask_texture, UV).a;

    if (mask < 0.05) {
        // Outside the gem art: zero everything. The shape mask is the gem's
        // own silhouette, so the aura cannot extend past it. (Godot's
        // canvas_item fragment() does not allow early `return`, hence else.)
        COLOR = vec4(0.0);
    } else {
        // Cheap edge-distance proxy: erode the mask by sampling a small ring of
        // neighboring texels and taking the minimum. Where the eroded value
        // drops below the center value, we're near the silhouette's boundary —
        // works for any gem shape, since it reads the actual alpha shape.
        vec2 texel = 1.0 / vec2(textureSize(mask_texture, 0));
        float eroded = mask;
        for (int i = 0; i < 8; i++) {
            float angle = float(i) / 8.0 * 6.2831853;
            vec2 offset = vec2(cos(angle), sin(angle)) * texel * 6.0;
            eroded = min(eroded, texture(mask_texture, UV + offset).a);
        }
        float rim = clamp((mask - eroded) * 3.0, 0.0, 1.0);

        vec2 centered = (UV - 0.5) * 2.0;
        vec2 p = centered * 2.5 + vec2(0.0, -TIME * 0.4 + time_offset);
        float cracks = smoothstep(0.35, 0.65, fbm(p));  // mottled veins across the body

        float glow = clamp(cracks * 0.55 + rim, 0.0, 1.0);
        vec4 col = mix(core_color, rim_color, rim);
        // mask multiplies alpha directly: zero anywhere the gem itself is
        // transparent, belt-and-suspenders under the erosion-based rim.
        col.a = glow * intensity * mask;

        COLOR = col * COLOR;
    }
}
