import bpy
import bmesh
import math
import random
from mathutils import Vector

SEED = 7
SIDES = 11
RADIUS = 3.0
HEIGHT = 9.0
RINGS = 5
JITTER = 0.14
LEAN = 0.3
TAPER = 0.88
RIM_INSET = 0.25
CAP_DOME = 0.45
GRASS_BAND = 0.5
ROCK = (0.55, 0.50, 0.45)
GRASS = (0.42, 0.62, 0.30)


def material(name, rgb):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.95
        m.diffuse_color = (*rgb, 1.0)
    return m


def ring(bm, rng, radius, z, drift, taper):
    verts = []
    for k in range(SIDES):
        t = 2 * math.pi * k / SIDES + rng.uniform(-0.08, 0.08)
        r = radius * taper * (1 + rng.uniform(-JITTER, JITTER))
        verts.append(bm.verts.new((r * math.cos(t) + drift.x, r * math.sin(t) + drift.y, z)))
    return verts


def bridge(bm, a, b):
    faces = []
    n = len(a)
    for k in range(n):
        faces.append(bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])))
    return faces


def build(name="Cliff", seed=SEED, height=HEIGHT, radius=RADIUS):
    rng = random.Random(seed)
    bm = bmesh.new()
    rock_faces = []
    grass_faces = []

    drift = Vector((0.0, 0.0))
    loops = []
    for i in range(RINGS + 1):
        f = i / RINGS
        z = f * height
        if i > 0:
            drift += Vector((rng.uniform(-LEAN, LEAN), rng.uniform(-LEAN, LEAN))) * (height / RINGS) * 0.3
        taper = 1.0 + (TAPER - 1.0) * f
        loops.append(ring(bm, rng, radius, z, drift, taper))

    band = ring(bm, rng, radius, height - GRASS_BAND, drift, TAPER)
    loops.insert(RINGS, band)

    bottom = bm.faces.new(list(reversed(loops[0])))
    rock_faces.append(bottom)
    for a, b in zip(loops, loops[1:]):
        faces = bridge(bm, a, b)
        if b is loops[-1]:
            grass_faces.extend(faces)
        else:
            rock_faces.extend(faces)

    top = loops[-1]
    center = Vector((0.0, 0.0, 0.0))
    for v in top:
        center += v.co
    center /= len(top)
    rim = []
    for v in top:
        inward = (center - v.co) * RIM_INSET
        rim.append(bm.verts.new(v.co + Vector((inward.x, inward.y, 0.12 + rng.uniform(0.0, 0.12)))))
    grass_faces.extend(bridge(bm, top, rim))
    peak = bm.verts.new(center + Vector((rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), CAP_DOME)))
    for k in range(len(rim)):
        grass_faces.append(bm.faces.new((rim[k], rim[(k + 1) % len(rim)], peak)))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in rock_faces:
        f.material_index = 0
    for f in grass_faces:
        f.material_index = 1

    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.materials.append(material("Cliff_Rock", ROCK))
    me.materials.append(material("Cliff_Grass", GRASS))
    colors = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    for poly in me.polygons:
        rgb = GRASS if poly.material_index == 1 else ROCK
        for li in poly.loop_indices:
            colors.data[li].color = (*rgb, 1.0)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


for ob in list(bpy.data.objects):
    if ob.type == "MESH":
        bpy.data.objects.remove(ob, do_unlink=True)

cliff = build()
print("BUILT", cliff.name, "verts", len(cliff.data.vertices), "faces", len(cliff.data.polygons))
