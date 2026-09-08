shader_type canvas_item;
render_mode unshaded;
// Procedural fire aura for Flame-special gems (docs: DECISIONS.md "Flame aura
// (visual)"). Replaces the v3 edge-ring annulus: fbm noise ("cracks"/veins
// across the body) plus a brightening rim, hard-bounded to the gem's own
// circle via dist > 1.0 so the effect structurally cannot bleed into
// neighboring cells. render_mode unshaded keeps it out of the 2D lighting
// pass — with no Light2D in the scene a lit canvas_item would multiply by
// zero light and render black. No texture is stamped, so there is no
// repetition artifact. Pure View-layer effect.
//
// The final color is multiplied by the incoming COLOR varying (the canvas-item
// modulate chain) so VanishAsync's parent modulate:a fade cascades through the
// shader output instead of being dropped by the custom fragment().

uniform float time_offset = 0.0;
uniform vec4 core_color : source_color = vec4(1.0, 0.85, 0.35, 1.0); // warm inner glow
uniform vec4 rim_color  : source_color = vec4(1.0, 0.35, 0.05, 1.0); // hot rim
uniform float intensity = 0.35; // hard cap on alpha — keeps it non-obstructive

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
    vec2 centered = (UV - 0.5) * 2.0;
    float dist = length(centered);

    if (dist > 1.0) {
        // Hard-bounded to the gem's own circle — cannot bleed into neighboring
        // cells, by construction rather than by tuning a radius. (Godot's
        // canvas_item fragment() does not allow early `return`, hence else.)
        COLOR = vec4(0.0);
    } else {
        vec2 p = centered * 2.5 + vec2(0.0, -TIME * 0.4 + time_offset);
        float cracks = smoothstep(0.35, 0.65, fbm(p));  // mottled veins across the body
        float rim = smoothstep(0.55, 1.0, dist);         // brightens toward the edge

        float glow = clamp(cracks * 0.6 + rim, 0.0, 1.0);
        vec4 col = mix(core_color, rim_color, rim);
        col.a = glow * intensity;

        COLOR = col * COLOR;
    }
}
