# Clean a TRELLIS mesh: drop small floating islands, recalc normals, put feet on the ground, centre on X/Y,
# scale to 1.85 m tall; save as .blend; render ortho front and side views for landmark picking.
import bpy, bmesh, sys, os, math, json
from mathutils import Vector
a=sys.argv[sys.argv.index('--')+1:]; SRC,HID,OUT=a[0],a[1],a[2]; MINV=int(a[3]) if len(a)>3 else 120
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=SRC)
ms=[o for o in bpy.context.scene.objects if o.type=='MESH']
for x in bpy.context.selected_objects: x.select_set(False)
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active=ms[0]
if len(ms)>1: bpy.ops.object.join()
o=bpy.context.view_layer.objects.active; o.name='Body'
o.parent=None
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
for e in [x for x in bpy.context.scene.objects if x.type!='MESH']: bpy.data.objects.remove(e,do_unlink=True)
bm=bmesh.new(); bm.from_mesh(o.data)
bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-4)
bm.verts.ensure_lookup_table(); bm.verts.index_update()
seen=set(); isl=[]
for v in bm.verts:
    if v.index in seen: continue
    st=[v]; seen.add(v.index); cur=[]
    while st:
        u=st.pop(); cur.append(u)
        for e in u.link_edges:
            w=e.other_vert(u)
            if w.index not in seen: seen.add(w.index); st.append(w)
    isl.append(cur)
isl.sort(key=len,reverse=True)
kill=[v for c in isl if len(c)<MINV for v in c]
print('[prep] islands',len(isl),'sizes',[len(c) for c in isl[:8]],'killed',len(kill))
bmesh.ops.delete(bm,geom=kill,context='VERTS')
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bm.to_mesh(o.data); bm.free()
pts=[v.co for v in o.data.vertices]
mn=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts))); mx=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
s=1.85/(mx.z-mn.z)
for v in o.data.vertices: v.co=Vector(((v.co.x-(mn.x+mx.x)/2)*s,(v.co.y-(mn.y+mx.y)/2)*s,(v.co.z-mn.z)*s))
o.data.update()
pts=[v.co for v in o.data.vertices]
bb=[min(p.x for p in pts),max(p.x for p in pts),min(p.y for p in pts),max(p.y for p in pts),0,1.85]
print('[prep] tris',sum(len(p.vertices)-2 for p in o.data.polygons),'bbox',[round(x,3) for x in bb])
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=f'{OUT}/{HID}_clean.blend')
json.dump(bb,open(f'{OUT}/{HID}_bbox.json','w'))
sc=bpy.context.scene; sc.render.engine='BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
sc.render.resolution_x=800; sc.render.resolution_y=800
w=bpy.data.worlds.new('W'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(1,1,1,1); w.node_tree.nodes['Background'].inputs[1].default_value=1.2
for tag,loc,rot in (('front',(0,-6,0.925),(math.radians(90),0,0)),('side',(6,0,0.925),(math.radians(90),0,math.radians(90)))):
    bpy.ops.object.camera_add(location=loc,rotation=rot); cam=bpy.context.object; cam.data.type='ORTHO'; cam.data.ortho_scale=2.0; sc.camera=cam
    sc.render.filepath=f'{OUT}/{HID}_{tag}.png'; bpy.ops.render.render(write_still=True)
print('[prep] done',HID)
