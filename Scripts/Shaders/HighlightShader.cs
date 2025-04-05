namespace Ceiro.Scripts.Shaders;

/// <summary>
/// Example highlight shader code to be saved as highlight.gdshader
/// </summary>
public static class HighlightShaderCode
{
	public static string GetShaderCode() => """

	                                        shader_type spatial;
	                                        render_mode blend_mix, depth_draw_always, cull_back, diffuse_burley, specular_schlick_ggx;

	                                        uniform sampler2D texture_albedo : source_color, hint_default_white;
	                                        uniform vec4 highlight_color : source_color = vec4(1.0, 1.0, 0.0, 0.3);
	                                        uniform float highlight_amount : hint_range(0.0, 1.0) = 0.0;
	                                        uniform float highlight_width : hint_range(0.0, 0.1) = 0.02;

	                                        void fragment() {
	                                            vec4 albedo_tex = texture(texture_albedo, UV);
	                                            
	                                            // Calculate distance from edge for outline effect
	                                            float edge_distance = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	                                            float edge_factor = smoothstep(0.0, highlight_width, edge_distance);
	                                            
	                                            // Apply highlight at edges and overall tint
	                                            vec3 final_color = mix(highlight_color.rgb, albedo_tex.rgb, edge_factor);
	                                            final_color = mix(albedo_tex.rgb, final_color, highlight_amount);
	                                            
	                                            ALBEDO = final_color;
	                                            ALPHA = albedo_tex.a;
	                                            
	                                            // Add some emission for the highlight effect
	                                            EMISSION = highlight_color.rgb * highlight_amount * (1.0 - edge_factor) * 0.5;
	                                        }

	                                        """;
}