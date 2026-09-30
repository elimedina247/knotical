import bpy
import bmesh
import math
import random
from mathutils import Vector

SEED = 3
TRUNK_HEIGHT = 2.0
TRUNK_RADIUS = 0.28
TRUNK_TAPER = 0.7
TRUNK_SIDES = 6
CANOPY_RADIUS = 1.4
CANOPY_BLOBS = 3
CANOPY_JITTER = 0.14
BUSH_RADIUS = 0.75
BUSH_SQUASH = 0.65
BARK = (0.40, 0.28, 0.18)
LEAF_DARK = (0.30, 0.52, 0.26)
LEAF_LIGHT = (0.46, 0.66, 0.32)


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


def blob(bm, rng, center, radius, scale, jitter, subdivisions=2):
    new = bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)["verts"]
    for v in new:
        direction = v.co.normalized()
        r = radius * (1 + rng.uniform(-jitter, jitter))
        v.co = Vector((direction.x * r * scale.x, direction.y * r * scale.y, direction.z * r * scale.z)) + center
    return new


def trunk(bm, rng, height, radius, taper, sides, lean):
    rings = []
    drift = Vector((0.0, 0.0))
    for i in range(3):
        f = i / 2
        z = -0.3 + (height + 0.3) * f
        if i > 0:
            drift += Vector((rng.uniform(-lean, lean), rng.uniform(-lean, lean)))
        r = radius * (1 + (taper - 1) * f)
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / sides) + drift.x, r * math.sin(2 * math.pi * k / sides) + drift.y, z)) for k in range(sides)])
    faces = [bm.faces.new(list(reversed(rings[0])))]
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            faces.append(bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k])))
    faces.append(bm.faces.new(rings[-1]))
    top = Vector((drift.x, drift.y, height))
    return faces, top


def finish(name, bm, colored):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.materials.append(material("Foliage_Bark", BARK))
    me.materials.append(material("Foliage_LeafDark", LEAF_DARK))
    me.materials.append(material("Foliage_LeafLight", LEAF_LIGHT))
    colors = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    palette = [BARK, LEAF_DARK, LEAF_LIGHT]
    for poly in me.polygons:
        poly.material_index = colored[poly.index]
        rgb = palette[poly.material_index]
        for li in poly.loop_indices:
            colors.data[li].color = (*rgb, 1.0)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def leaf_index(rng):
    return 2 if rng.random() < 0.4 else 1


def build_tree(name="Tree", seed=SEED, location=(0, 0, 0)):
    rng = random.Random(seed)
    bm = bmesh.new()
    bark_faces, top = trunk(bm, rng, TRUNK_HEIGHT, TRUNK_RADIUS, TRUNK_TAPER, TRUNK_SIDES, 0.08)
    bark = set(bark_faces)
    center = top + Vector((0, 0, CANOPY_RADIUS * 0.55))
    for i in range(CANOPY_BLOBS):
        angle = 2 * math.pi * i / CANOPY_BLOBS + rng.uniform(-0.5, 0.5)
        offset = Vector((math.cos(angle), math.sin(angle), rng.uniform(-0.25, 0.35))) * (CANOPY_RADIUS * 0.45)
        scale = Vector((rng.uniform(0.85, 1.1), rng.uniform(0.85, 1.1), rng.uniform(0.8, 1.0)))
        blob(bm, rng, center + offset, CANOPY_RADIUS * rng.uniform(0.7, 0.9), scale, CANOPY_JITTER)
    bm.faces.ensure_lookup_table()
    colored = [0 if f in bark else leaf_index(rng) for f in bm.faces]
    ob = finish(name, bm, colored)
    ob.location = location
    return ob


def build_bush(name="Bush", seed=SEED, location=(0, 0, 0)):
    rng = random.Random(seed)
    bm = bmesh.new()
    blob(bm, rng, Vector((0, 0, BUSH_RADIUS * BUSH_SQUASH * 0.8)), BUSH_RADIUS, Vector((1.0, rng.uniform(0.8, 1.1), BUSH_SQUASH)), 0.16)
    blob(bm, rng, Vector((BUSH_RADIUS * 0.55, BUSH_RADIUS * 0.2, BUSH_RADIUS * 0.35)), BUSH_RADIUS * 0.6, Vector((1.0, 1.0, 0.7)), 0.16)
    bm.faces.ensure_lookup_table()
    colored = [leaf_index(rng) for f in bm.faces]
    ob = finish(name, bm, colored)
    ob.location = location
    return ob


for ob in list(bpy.data.objects):
    if ob.type == "MESH":
        bpy.data.objects.remove(ob, do_unlink=True)

tree = build_tree()
bush = build_bush(location=(3.0, 0, 0))
print("BUILT tree faces", len(tree.data.polygons), "bush faces", len(bush.data.polygons))
