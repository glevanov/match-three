shader_type canvas_item;
// Procedural fire ring for Flame-special gems (Docs: DECISIONS.md "Flame fire
// ring (visual)"). Noise-driven annulus hugging the gem edge; no texture is
// stamped, so there is no repetition artifact. Pure View-layer effect.
//
// The final color is multiplied by the incoming COLOR varying (the canvas-item
// modulate chain) so VanishAsync's parent modulate:a fade cascades through the
// shader output instead of being dropped by the custom fragment().

uniform float time_offset = 0.0;   // per-gem desync (radians), set from GemActor
uniform float inner_radius = 0.5;  // normalized 0..1: gem edge within this rect
uniform float outer_radius = 1.0;  // normalized 0..1: outer fade limit
uniform vec4 core_color : source_color = vec4(1.0, 0.95, 0.55, 1.0);
uniform vec4 edge_color : source_color = vec4(1.0, 0.25, 0.0, 0.0);

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

void fragment()
{
    vec2 centered = (UV - 0.5) * 2.0; // -1..1, origin at node center
    float dist = length(centered);

    // Annulus just outside the gem edge, fading at both edges.
    float ring = smoothstep(inner_radius - 0.05, inner_radius, dist)
               * (1.0 - smoothstep(outer_radius - 0.2, outer_radius, dist));

    // Sample noise in polar-ish coords so flames "lick" outward and rotate.
    float angle = atan(centered.y, centered.x);
    vec2 noise_coord = vec2(angle * 3.0, dist * 4.0 - TIME * 2.5 + time_offset);
    float n = noise(noise_coord * 3.0) + 0.5 * noise(noise_coord * 6.0 + 10.0);

    float flame = ring * n;
    vec4 col = mix(edge_color, core_color, clamp(flame * 1.5, 0.0, 1.0));
    col.a *= clamp(flame * 1.8, 0.0, 1.0);

    // Multiply by the canvas-item modulate chain (parent fade on vanish).
    COLOR = col * COLOR;
}