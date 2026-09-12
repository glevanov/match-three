shader_type canvas_item;
render_mode unshaded, blend_add;

uniform float time_offset = 0.0;
uniform vec4 core_color : source_color = vec4(0.9, 0.9, 0.95, 1.0);
uniform vec4 rim_color  : source_color = vec4(0.7, 0.7, 0.8, 1.0);
uniform float intensity = 0.85;
uniform float halo_intensity = 0.5;
uniform float rim_distance = 5.0;
uniform float band_distance = 14.0;
uniform float rect_scale = 1.35;
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

float maskAt(vec2 uv) {
    float inside = step(0.0, uv.x) * step(uv.x, 1.0)
                 * step(0.0, uv.y) * step(uv.y, 1.0);
    return texture(mask_texture, uv).a * inside;
}

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
    vec2 texUV = (UV - 0.5) * rect_scale + 0.5;
    float mask = maskAt(texUV);

    float rim = clamp((mask - erode(texUV, texel, rim_distance)) * 3.0, 0.0, 1.0);
    float band = clamp((mask - erode(texUV, texel, band_distance)) * 2.0, 0.0, 1.0);

    float avg = mask * 0.5;
    for (int r = 1; r <= 3; r++) {
        float weight = r == 1 ? 0.3 : (r == 2 ? 0.2 : 0.1);
        for (int i = 0; i < 8; i++) {
            float angle = float(i) / 8.0 * 6.2831853;
            vec2 off = vec2(cos(angle), sin(angle)) * texel * (8.0 * float(r));
            avg += maskAt(texUV + off) * weight;
        }
    }
    avg /= 5.3;
    float halo = clamp(avg - mask, 0.0, 1.0);

    vec2 p = (texUV - 0.5) * 2.5 + vec2(0.0, -TIME * 0.4 + time_offset);
    float cracks = smoothstep(0.35, 0.65, fbm(p));
    float glow = clamp(cracks * band + rim, 0.0, 1.0);

    vec4 col = mix(core_color, rim_color, rim);
    col.a = glow * intensity * mask + halo * halo_intensity;

    COLOR = col * COLOR;
}
