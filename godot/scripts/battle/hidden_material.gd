## A material that draws nothing: hides one surface of a skinned mesh (a hood, a crown) the way
## the web game sets material.visible = false.
class_name HiddenMaterial

static var _mat: ShaderMaterial


static func get_material() -> ShaderMaterial:
	if _mat == null:
		var sh := Shader.new()
		sh.code = "shader_type spatial;\nrender_mode unshaded, cull_disabled, shadows_disabled;\nvoid vertex() { POSITION = vec4(2.0, 2.0, 2.0, 1.0); }\nvoid fragment() { discard; }\n"
		_mat = ShaderMaterial.new()
		_mat.shader = sh
	return _mat
