shader_type canvas_item;
render_mode unshaded, blend_add;

uniform float time_offset = 0.0;
uniform vec4 glow_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float pulse_speed = 1.1;
uniform float pulse_min = 0.12;
uniform float pulse_max = 0.33;
uniform float scale_pulse = 0.06;
uniform float core_boost = 1.0;
uniform float rect_scale = 1.5;
uniform sampler2D mask_texture : repeat_disable, filter_linear;

float maskAt(vec2 uv) {
    float inside = step(0.0, uv.x) * step(uv.x, 1.0)
                 * step(0.0, uv.y) * step(uv.y, 1.0);
    return texture(mask_texture, uv).a * inside;
}

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
    float breath = sin(phase) * 0.5 + 0.5;
    float intensity = mix(pulse_min, pulse_max, breath);

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
