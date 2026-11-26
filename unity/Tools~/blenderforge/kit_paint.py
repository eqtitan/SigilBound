"""Painted details (docs/sigilbound/PLAN.md phase E steps 51-52) for the high-detail heroes.

Run by ubc.bake() right after the albedo bake, on the same atlas UVs. The stylised "hand-painted" pass:
  - crevices darken: a baked ambient-occlusion pass multiplies the albedo (folds, plate gaps, under the
    chin and belt, where a painter shades);
  - worn edges lighten: Cycles' pointiness (convex edges) brightens raised edges toward a warm highlight;
  - painted light: surfaces facing up read slightly lighter than those facing down (object-space normal Z),
    the soft top light hand-painted textures carry.
Strengths are gentle; the game's own lighting and rim still apply on top.
"""
import bpy
import numpy as np

AO_STRENGTH, EDGE_STRENGTH, TOP_STRENGTH = 0.42, 0.16, 0.10

def _bake_emit(obj, img, make_shader):
    """Temporarily give every material slot one emission material (built by make_shader), bake EMIT to img."""
    m = bpy.data.materials.new("M_PaintPass"); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], out.inputs[0]); make_shader(nt, em)
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = img
    uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = "Atlas"; nt.links.new(uvn.outputs[0], t.inputs[0]); nt.nodes.active = t
    saved = list(obj.data.materials)
    for i in range(len(obj.data.materials)): obj.data.materials[i] = m
    try: bpy.ops.object.bake(type="EMIT")
    finally:
        for i, mm in enumerate(saved): obj.data.materials[i] = mm
        bpy.data.materials.remove(m)

def _px(img):
    a = np.empty(len(img.pixels), dtype=np.float32); img.pixels.foreach_get(a); return a.reshape(-1, 4)

def painted_pass(obj, albedo, size):
    sc = bpy.context.scene; bk = sc.render.bake
    samples = sc.cycles.samples; sc.cycles.samples = 16
    ao = bpy.data.images.new("T_ao", size, size, alpha=False); ao.colorspace_settings.name = "Non-Color"
    edge = bpy.data.images.new("T_edge", size, size, alpha=False); edge.colorspace_settings.name = "Non-Color"
    top = bpy.data.images.new("T_top", size, size, alpha=False); top.colorspace_settings.name = "Non-Color"
    try:
        def ao_shader(nt, em):
            a = nt.nodes.new("ShaderNodeAmbientOcclusion"); a.inputs["Distance"].default_value = 0.06; a.samples = 12
            nt.links.new(a.outputs["AO"], em.inputs["Color"])
        def edge_shader(nt, em):
            g = nt.nodes.new("ShaderNodeNewGeometry"); r = nt.nodes.new("ShaderNodeValToRGB")
            r.color_ramp.elements[0].position = 0.52; r.color_ramp.elements[1].position = 0.62
            nt.links.new(g.outputs["Pointiness"], r.inputs[0]); nt.links.new(r.outputs[0], em.inputs["Color"])
        def top_shader(nt, em):
            g = nt.nodes.new("ShaderNodeNewGeometry"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
            vt = nt.nodes.new("ShaderNodeVectorTransform"); vt.convert_from = "WORLD"; vt.convert_to = "OBJECT"; vt.vector_type = "NORMAL"
            mp = nt.nodes.new("ShaderNodeMapRange"); mp.inputs["From Min"].default_value = -1; mp.inputs["From Max"].default_value = 1
            nt.links.new(g.outputs["Normal"], vt.inputs[0]); nt.links.new(vt.outputs[0], sep.inputs[0])
            nt.links.new(sep.outputs["Z"], mp.inputs["Value"]); nt.links.new(mp.outputs[0], em.inputs["Color"])
        _bake_emit(obj, ao, ao_shader); _bake_emit(obj, edge, edge_shader); _bake_emit(obj, top, top_shader)
        A = _px(albedo); O = _px(ao)[:, :1]; E = _px(edge)[:, :1]; T = _px(top)[:, :1]
        rgb = A[:, :3]
        rgb *= (1 - AO_STRENGTH) + AO_STRENGTH * O                                   # crevice shading
        warm = np.array([1.0, 0.93, 0.8], dtype=np.float32)
        rgb += (warm - rgb) * (EDGE_STRENGTH * E) * (0.4 + 0.6 * O)                  # worn edges (not inside crevices)
        rgb *= 1 + TOP_STRENGTH * (T * 2 - 1)                                        # soft painted top light
        A[:, :3] = np.clip(rgb, 0, 1)
        albedo.pixels.foreach_set(A.ravel())
        albedo.update()
    finally:
        sc.cycles.samples = samples
        for im in (ao, edge, top): bpy.data.images.remove(im)
    return albedo
