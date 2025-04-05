shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 water_color : source_color = vec4(0.2, 0.5, 0.8, 0.7);
uniform float wave_speed : hint_range(0.0, 10.0) = 1.0;
uniform float wave_height : hint_range(0.0, 1.0) = 0.1;
uniform float time;

varying vec3 vertex_pos;

void vertex() {
    vertex_pos = VERTEX;
    
    // Apply wave effect
    float wave = sin(VERTEX.x * 2.0 + time * wave_speed) * cos(VERTEX.z * 2.0 + time * wave_speed) * wave_height;
    VERTEX.y += wave;
}

void fragment() {
    // Base water color
    ALBEDO = water_color.rgb;
    
    // Transparency
    ALPHA = water_color.a;
    
    // Add some specular reflection
    SPECULAR = 0.5;
    ROUGHNESS = 0.1;
    
    // Add refraction
    REFRACTION = 0.05;
}