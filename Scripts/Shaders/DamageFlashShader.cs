namespace Ceiro.Scripts.Shaders;

/// <summary>
/// Example damage flash shader code to be saved as damage_flash.gdshader
/// </summary>
public static class DamageFlashShaderCode
{
	public static string GetShaderCode() => """

	                                        shader_type spatial;
	                                        render_mode blend_mix, depth_draw_always, cull_back, diffuse_burley, specular_schlick_ggx;

	                                        uniform sampler2D texture_albedo : source_color, hint_default_white;
	                                        uniform vec4 flash_color : source_color = vec4(1.0, 0.0, 0.0, 0.5);
	                                        uniform float flash_amount : hint_range(0.0, 1.0) = 0.0;

	                                        void fragment() {
	                                            vec4 albedo_tex = texture(texture_albedo, UV);
	                                            
	                                            // Mix the texture with the flash color based on flash amount
	                                            ALBEDO = mix(albedo_tex.rgb, flash_color.rgb, flash_amount * flash_color.a);
	                                            ALPHA = albedo_tex.a;
	                                        }

	                                        """;
}