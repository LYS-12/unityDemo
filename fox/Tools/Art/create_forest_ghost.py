"""Blender 4.4: original skinned ghost, baked animation and a static title diorama.
Run: blender --background --factory-startup --python Tools/Art/create_forest_ghost.py
All distances are metres. The editable .blend deliberately lives outside Assets.
"""
import bpy, math, os, random
from mathutils import Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'SourceArt/Ghost'
EXPORT = ROOT / 'Assets/_Project/Art/Models/Ghost'
IMAGES = ROOT / 'Assets/_Project/Art/Textures/ForestParty'
for path in (SOURCE, EXPORT, IMAGES, ROOT / 'Docs/Images'):
    path.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(42)

def material(name, color, roughness=.55, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bs = mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = roughness
    bs.inputs['Emission Color'].default_value = (*color, 1)
    bs.inputs['Emission Strength'].default_value = emission
    return mat

cloth = material('Ghost_PearlescentCloth', (.83,.78,.98), .3)
nodes = cloth.node_tree.nodes
links = cloth.node_tree.links
geo = nodes.new('ShaderNodeNewGeometry')
separate = nodes.new('ShaderNodeSeparateXYZ')
ramp = nodes.new('ShaderNodeValToRGB')
remap = nodes.new('ShaderNodeMapRange')
remap.inputs['From Min'].default_value = .15
remap.inputs['From Max'].default_value = 2.5
links.new(geo.outputs['Position'], separate.inputs[0])
links.new(separate.outputs['Z'], remap.inputs['Value'])
links.new(remap.outputs[0], ramp.inputs[0])
ramp.color_ramp.elements[0].color = (.30,.30,.68,1)
ramp.color_ramp.elements[1].color = (1,.91,.94,1)
ramp.color_ramp.elements.new(.5).color = (.76,.65,.94,1)
links.new(ramp.outputs[0], nodes.get('Principled BSDF').inputs['Base Color'])
dark = material('Ghost_DeepPlumFace', (.075,.018,.095), .3)
blush = material('Ghost_RosyCheeks', (.98,.35,.55), .6)
hem = material('Ghost_RoseHem', (.86,.48,.77), .3)
shine = material('Ghost_EyeGlints', (1,.92,.94), .2, .4)

# One continuous dome and skirt. Side folds stretch out into little cloth wings.
verts, faces = [], []
RINGS, SIDES = 49, 96
for row in range(RINGS):
    t = row / (RINGS-1)
    z = 1.70 + .92 * math.cos((t/.43)*math.pi/2) if t < .43 else 1.70 - 1.26*(t-.43)/.57
    if t < .43:
        radius = .86 * math.sin((t/.43)*math.pi/2)
    else:
        u = (t-.43)/.57
        radius = .86 + .21*u + .23*u*u
    for column in range(SIDES):
        angle = 2*math.pi*column/SIDES
        skirt = max(0,(t-.44)/.56)
        wave = math.cos(7*angle + .35) * .105 * skirt**1.6
        wings = abs(math.cos(angle))**12 * .72 * math.exp(-((t-.72)/.17)**2)
        r = radius + wave + wings
        x = r * math.cos(angle)
        y = r * math.sin(angle) * .75
        zz = z + (.18*math.sin(7*angle+.4)+.13*math.cos(3*angle))*skirt**3
        verts.append((x,y,zz))
for row in range(RINGS-1):
    for col in range(SIDES):
        a = row*SIDES+col; b=row*SIDES+(col+1)%SIDES
        faces.append((a,b,b+SIDES,a+SIDES))
mesh=bpy.data.meshes.new('GhostDrapedClothMesh'); mesh.from_pydata(verts,[],faces); mesh.update()
body=bpy.data.objects.new('GhostCloth',mesh); bpy.context.collection.objects.link(body); body.data.materials.append(cloth)
for p in mesh.polygons: p.use_smooth=True
solid = body.modifiers.new('Fabric thickness','SOLIDIFY'); solid.thickness=.025
bpy.context.view_layer.objects.active=body
bpy.ops.object.modifier_apply(modifier=solid.name)

def uv(name, loc, scale, mat, segments=32):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=16, location=loc)
    obj=bpy.context.object; obj.name=name; obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.append(mat)
    for p in obj.data.polygons: p.use_smooth=True
    return obj

parts=[body]
for x in (-.32,.32):
    eye=uv('EyeLeft' if x<0 else 'EyeRight',(x,-.623,1.99),(.135,.06,.225),dark)
    eye.rotation_euler[1]=-.13 if x<0 else .13
    parts.append(eye)
    parts.append(uv('EyeGlint',(x-.035,-.676,2.07),(.035,.014,.047),shine,16))
    parts.append(uv('RosyCheek',(x*1.68,-.55,1.70),(.13,.025,.058),blush,24))
# A broad happy U-mouth, embedded slightly into the cloth.
mouthverts=[(0,-.80,1.56)]
for i in range(49):
    a=2*math.pi*i/48
    x=.265*math.cos(a)
    z=1.58+.27*math.sin(a)
    if math.sin(a)>0: z=1.76-.085*math.sin(a)**.7
    mouthverts.append((x,-.80+abs(x)*.06,z))
mouthfaces=[(0,i+1,i+2) for i in range(48)]
mm=bpy.data.meshes.new('HappyMouthMesh'); mm.from_pydata(mouthverts,[],mouthfaces); mm.materials.append(dark)
mouth=bpy.data.objects.new('HappyMouth',mm); bpy.context.collection.objects.link(mouth); parts.append(mouth)

# Thin rose piping following the irregular scalloped lower hem.
curve=bpy.data.curves.new('HemPiping','CURVE'); curve.dimensions='3D'; curve.bevel_depth=.012; curve.bevel_resolution=2
spline=curve.splines.new('POLY'); spline.points.add(SIDES)
for i in range(SIDES+1): spline.points[i].co=(*verts[(RINGS-1)*SIDES+i%SIDES],1)
edge=bpy.data.objects.new('RoseHem',curve); bpy.context.collection.objects.link(edge); edge.data.materials.append(hem)
bpy.context.view_layer.objects.active=edge; edge.select_set(True); body.select_set(False)
bpy.ops.object.convert(target='MESH'); edge=bpy.context.object; parts.append(edge)

# Real deforming skeleton, smooth weights and baked bone motion.
bpy.ops.object.armature_add(location=(0,0,0))
rig=bpy.context.object; rig.name='GhostRig'; rig.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT')
root=rig.data.edit_bones[0]; root.name='FloatRoot'; root.head=(0,0,0); root.tail=(0,0,.5)
spine=rig.data.edit_bones.new('Body'); spine.head=(0,0,.5); spine.tail=(0,0,2.3); spine.parent=root
for name,point in [('SkirtLeft',(-1.2,0,.55)),('SkirtRight',(1.2,0,.55)),('SkirtFront',(0,-.8,.4)),('SkirtBack',(0,.8,.4))]:
    b=rig.data.edit_bones.new(name); b.head=(0,0,1.25); b.tail=point; b.parent=spine
bpy.ops.object.mode_set(mode='OBJECT')
for obj in parts:
    groups={name:obj.vertex_groups.new(name=name) for name in ['Body','SkirtLeft','SkirtRight','SkirtFront','SkirtBack']}
    for vertex in obj.data.vertices:
        world=obj.matrix_world @ vertex.co
        if obj in (body,edge):
            amount=max(0,min(.88,(1.65-world.z)/1.3))
            weights={'SkirtLeft':max(0,-world.x), 'SkirtRight':max(0,world.x), 'SkirtFront':max(0,-world.y), 'SkirtBack':max(0,world.y)}
            total=sum(weights.values()) or 1
            for name,w in weights.items():
                if w: groups[name].add([vertex.index],amount*w/total,'REPLACE')
            groups['Body'].add([vertex.index],1-amount,'REPLACE')
        else: groups['Body'].add([vertex.index],1,'REPLACE')
    modifier=obj.modifiers.new('Ghost skeleton','ARMATURE'); modifier.object=rig
    obj.parent=rig

scene=bpy.context.scene; scene.render.fps=30
for name,length in [('IdleHover',90),('MoveFloat',36),('Wave',60),('Startle',36),('Bow',60)]:
    rig.animation_data_create(); action=bpy.data.actions.new(name); action.use_fake_user=True; rig.animation_data.action=action
    for f in range(1,length+1):
        phase=(f-1)/(length-1)*math.tau
        for bone in rig.pose.bones:
            bone.rotation_mode='XYZ'; bone.location=(0,0,0); bone.rotation_euler=(0,0,0)
        b=rig.pose.bones['Body']
        b.location.z=.09*math.sin(phase) if name=='IdleHover' else .12*math.sin(phase*2)
        b.rotation_euler.y=.035*math.sin(phase)
        if name=='MoveFloat': b.rotation_euler.x=.16; b.location.y=-.04*math.sin(phase)
        if name=='Bow': b.rotation_euler.x=-.42*math.sin(phase/2)**2
        if name=='Startle': b.location.z=.35*math.sin(phase/2)**2; b.rotation_euler.z=.13*math.sin(phase*3)
        for side,sign in [('SkirtLeft',1),('SkirtRight',-1),('SkirtFront',1),('SkirtBack',-1)]:
            bone=rig.pose.bones[side]
            bone.rotation_euler.y=.10*math.sin(phase+sign)*sign
            bone.rotation_euler.x=.06*math.cos(phase+sign)
            if name=='Wave' and side=='SkirtLeft': bone.rotation_euler.y=.55+.35*math.sin(phase*3)
            bone.keyframe_insert('rotation_euler',frame=f,group=side)
        b.keyframe_insert('location',frame=f,group='Body'); b.keyframe_insert('rotation_euler',frame=f,group='Body')
    action['purpose']=name
rig.animation_data.action=bpy.data.actions.get('IdleHover'); scene.frame_start=1; scene.frame_end=90; scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for obj in parts+[rig]: obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ForestGhost.blend'))
bpy.ops.export_scene.fbx(filepath=str(EXPORT/'ForestGhost.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},
    axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='AUTO',use_mesh_modifiers=True)

# Render the actual model for review, then an original forest-party title illustration.
def point_camera(camera, point): camera.rotation_euler=(Vector(point)-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.world.color=(.28,.25,.35)
bpy.ops.object.camera_add(location=(5,-8,4)); camera=bpy.context.object; point_camera(camera,(0,0,1.35)); scene.camera=camera
camera.data.type='ORTHO'; camera.data.ortho_scale=4.2
for name,loc,color,power,size in [('PinkKey',(-3,-4,5),(1,.66,.72),550,4),('BlueRim',(3,1,4),(.48,.74,1),750,3),('SoftFill',(0,-5,2),(1,.94,.84),120,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc); l=bpy.context.object; l.name=name; l.data.color=color; l.data.energy=power; l.data.shape='DISK'; l.data.size=size; point_camera(l,(0,0,1))
scene.render.resolution_x=900; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.render.film_transparent=True; scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(ROOT/'Docs/Images/ghost-blender.png'); bpy.ops.render.render(write_still=True)

scene.render.film_transparent=False
green=material('Moss',(.18,.36,.24)); leaf=material('Sage',(.31,.50,.36)); wood=material('Bark',(.30,.17,.12)); cream=material('PicnicCream',(.97,.79,.58)); orange=material('FoxOrange',(.86,.34,.14)); white=material('FoxCream',(.98,.87,.66))
def cube(name,loc,scale,mat,bevel=.1):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; o.name=name; o.scale=scale; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(mat)
    m=o.modifiers.new('Soft edges','BEVEL'); m.width=bevel; m.segments=3; o.modifiers.new('Normals','WEIGHTED_NORMAL'); return o
uv('ForestIsland',(0,1,-.35),(8,6,.65),green,64)
for i in range(16):
    a=math.tau*i/16; x=math.cos(a)*random.uniform(5,7); y=2+math.sin(a)*random.uniform(3.5,5)
    if y<1.0: continue
    cube('TreeTrunk',(x,y,1.1),(.28,.28,2.6),wood)
    for h,r in [(2.1,1.1),(2.9,.9),(3.6,.65)]: uv('RoundCanopy',(x,y,h),(r,r*.85,r*.85),leaf,24)
cube('PicnicTable',(0,2,.65),(2.7,1.5,.22),cream)
for x in (-.9,.9): cube('TableLeg',(x,2,.25),(.18,1,.8),wood)
for i,(x,y) in enumerate([(-1.8,1.4),(1.8,2.3),(.5,3.4)]):
    uv('FoxBody',(x,y,.55),(.42,.35,.55),orange)
    uv('FoxHead',(x,y-.05,1.08),(.46,.38,.4),orange)
    uv('FoxMuzzle',(x,y-.36,.98),(.28,.14,.19),white)
    for sx in (-1,1):
        bpy.ops.mesh.primitive_cone_add(vertices=3,radius1=.23,radius2=0,depth=.52,location=(x+sx*.28,y,1.5)); bpy.context.object.data.materials.append(orange)
        uv('FoxEye',(x+sx*.17,y-.397,1.15),(.035,.025,.052),dark,16)
    uv('FoxNose',(x,y-.5,1.03),(.065,.025,.045),dark,16)
for i in range(30):
    x=random.uniform(-4.5,4.5); y=random.uniform(-2,5)
    uv('GoldenLeaf',(x,y,.08),(.07,.17,.025),cream,12)
for x in [-3,-1.5,0,1.5,3]:
    uv('PartyLantern',(x,3.8,3.1+abs(x)*.12),(.16,.16,.2),material('WarmLight'+str(x),(1,.68,.26),.5,2),16)
    bpy.ops.object.light_add(type='POINT',location=(x,3.8,3)); bpy.context.object.data.energy=12; bpy.context.object.data.color=(1,.58,.26)
# Keep the ghost at the front-left, leaving the right for the menu's text panel.
rig.location=(-1.8,-.8,0)
camera.location=(9,-14,11); point_camera(camera,(1,1,1)); camera.data.ortho_scale=17.5
scene.render.resolution_x=1920; scene.render.resolution_y=1080
scene.render.filepath=str(IMAGES/'ForestPartyTitle.png'); scene.cycles.samples=48
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ForestPartyTitle.blend'))
bpy.ops.render.render(write_still=True)
print('FOREST_GHOST_EXPORT_COMPLETE')
