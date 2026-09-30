import bpy
import bmesh
import math
from mathutils import Euler, Vector

SEGS = 10


def material(name, rgb, rough=0.9):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = rough
        m.diffuse_color = (*rgb, 1.0)
    return m


PAL = {
    "skin": (0.93, 0.70, 0.52),
    "shirt": (0.20, 0.45, 0.72),
    "stripe": (0.92, 0.92, 0.88),
    "pants": (0.24, 0.22, 0.30),
    "boot": (0.30, 0.18, 0.10),
    "belt": (0.35, 0.22, 0.12),
    "hat": (0.78, 0.20, 0.18),
    "eye": (0.97, 0.97, 0.97),
    "pupil": (0.08, 0.08, 0.10),
    "pill": (0.22, 0.60, 0.62),
    "straw": (0.86, 0.72, 0.40),
    "rope": (0.82, 0.74, 0.56),
}
MATS = {k: material("Char_" + k, v) for k, v in PAL.items()}


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


COLL = collection("Characters")


def finish(bm):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def make(name, bm, mat, parent=None, location=(0, 0, 0), rotation=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    finish(bm).to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    me.materials.append(mat)
    COLL.objects.link(ob)
    ob.parent = parent
    ob.location = location
    ob.rotation_euler = rotation
    return ob


def empty(name, location, parent=None):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "PLAIN_AXES"
    ob.empty_display_size = 0.15
    COLL.objects.link(ob)
    ob.parent = parent
    ob.location = location
    return ob


def ring(bm, radius, z, segs=SEGS):
    return [bm.verts.new((radius * math.cos(2 * math.pi * k / segs), radius * math.sin(2 * math.pi * k / segs), z)) for k in range(segs)]


def bridge(bm, a, b):
    n = len(a)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))


def fan(bm, pole, loop):
    n = len(loop)
    for k in range(n):
        bm.faces.new((pole, loop[k], loop[(k + 1) % n]))


def capsule_bm(radius, length, segs=SEGS, rings=3, z0=0.0, top_radius=None):
    top_radius = radius if top_radius is None else top_radius
    straight = max(length - radius - top_radius, 0.0)
    bm = bmesh.new()
    loops = []
    bottom = bm.verts.new((0, 0, z0))
    for i in range(1, rings + 1):
        phi = -math.pi / 2 + (math.pi / 2) * i / rings
        loops.append(ring(bm, radius * math.cos(phi), z0 + radius + radius * math.sin(phi), segs))
    zt = z0 + radius + straight
    loops.append(ring(bm, top_radius, zt, segs))
    for i in range(1, rings):
        phi = (math.pi / 2) * i / rings
        loops.append(ring(bm, top_radius * math.cos(phi), zt + top_radius * math.sin(phi), segs))
    top = bm.verts.new((0, 0, zt + top_radius))
    fan(bm, bottom, loops[0])
    for a, b in zip(loops, loops[1:]):
        bridge(bm, a, b)
    fan(bm, top, loops[-1])
    return bm


def cylinder_bm(r0, r1, length, segs=SEGS, z0=0.0, offset=(0, 0)):
    bm = bmesh.new()
    a = ring(bm, r0, z0, segs)
    b = ring(bm, r1, z0 + length, segs)
    for v in b:
        v.co.x += offset[0]
        v.co.y += offset[1]
    bridge(bm, a, b)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    return bm


def sphere_bm(radius, u=8, v=5):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=radius)
    return bm


def box_bm(w, d, h, z0=0.0, bevel=0.0, top_scale=1.0, bottom_scale=1.0, y_off=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        s = top_scale if v.co.z > 0 else bottom_scale
        v.co.x *= w * s
        v.co.y *= d * s
        v.co.z = v.co.z * h + z0 + h / 2
        v.co.y += y_off
    if bevel > 0:
        try:
            bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=bevel, offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
        except TypeError:
            bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=bevel, segments=1)
    return bm


def cap_bm(radius, cut, segs=SEGS, v=6):
    bm = sphere_bm(radius, segs, v)
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, cut), plane_no=(0, 0, 1), clear_inner=True)
    bmesh.ops.holes_fill(bm, edges=bm.edges, sides=0)
    return bm


def tube_bm(points, radius, segs=6, twist=0.0):
    bm = bmesh.new()
    n = len(points)
    loops = []
    for i, p in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, n - 1)]
        tangent = (Vector(b) - Vector(a)).normalized()
        side = tangent.cross(Vector((0, 1, 0))).normalized()
        up = side.cross(tangent).normalized()
        ph = twist * i
        loops.append([bm.verts.new(Vector(p) + radius * (math.cos(2 * math.pi * k / segs + ph) * side + math.sin(2 * math.pi * k / segs + ph) * up)) for k in range(segs)])
    for a, b in zip(loops, loops[1:]):
        bridge(bm, a, b)
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    return bm


def add_rope_loop(parent, mats, base_z, half_width=0.06, height=0.17, radius=0.022):
    pts = []
    for i in range(11):
        t = math.pi * i / 10
        pts.append((half_width * math.cos(t), 0.0, base_z - 0.02 + height * math.sin(t)))
    return make("RopeLoop", tube_bm(pts, radius, 6, 0.35), mats["rope"], parent, (0, 0, 0), (0, 0, math.radians(20)))


def add_eyes(head, z, x, y_surface, eye_r=0.05, pupil_scale=0.55, protrude=0.55):
    for side, tag in ((1, "L"), (-1, "R")):
        eye = make("Eye_" + tag, sphere_bm(eye_r), MATS["eye"], head, (x * side, y_surface + eye_r * (1 - protrude), z))
        make("Pupil_" + tag, sphere_bm(eye_r * pupil_scale), MATS["pupil"], eye, (0, -eye_r * 0.75, 0))


def hat_beanie(parent, mats):
    hat = make("Hat_Beanie", cap_bm(0.235, 0.07, 12, 6), mats["hat"], parent)
    make("Hat_BeanieBand", cylinder_bm(0.245, 0.245, 0.055, 12, 0.055), mats["hat"], hat)
    return hat


def hat_bandana(parent, mats):
    hat = make("Hat_Bandana", cap_bm(0.232, 0.04, 12, 6), mats["hat"], parent)
    make("Hat_BandanaKnot", box_bm(0.09, 0.12, 0.05, 0.0, 0.012), mats["hat"], hat, (0, 0.20, 0.06), (math.radians(-35), 0, 0))
    return hat


def hat_tricorn(parent, mats):
    hat = make("Hat_TricornCrown", cylinder_bm(0.20, 0.17, 0.15, 12, 0.10), mats["pants"], parent)
    brim = make("Hat_TricornBrim", cylinder_bm(0.40, 0.40, 0.025, 3, 0.10), mats["pants"], hat, (0, 0, 0), (0, 0, math.radians(90)))
    make("Hat_TricornTrim", cylinder_bm(0.205, 0.205, 0.03, 12, 0.10), mats["stripe"], hat)
    return hat


def hat_straw(parent, mats):
    hat = make("Hat_StrawCrown", cylinder_bm(0.23, 0.13, 0.15, 12, 0.09), mats["straw"], parent)
    make("Hat_StrawBrim", cylinder_bm(0.42, 0.42, 0.015, 12, 0.09), mats["straw"], hat)
    make("Hat_StrawBand", cylinder_bm(0.235, 0.235, 0.035, 12, 0.10), mats["hat"], hat)
    return hat


HATS = {"beanie": hat_beanie, "bandana": hat_bandana, "tricorn": hat_tricorn, "straw": hat_straw}


def add_mouth(head, z, y_surface, mat, width=0.08):
    seg = width / 3
    for i, tilt in ((-1, 25), (0, 0), (1, -25)):
        make("Mouth_%d" % (i + 1), box_bm(seg * 1.05, 0.018, 0.014, -0.007), mat, head, (i * seg, y_surface + 0.004, z + (0.006 if i else 0)), (0, math.radians(tilt), 0))


def build_pill(origin, prefix="Pill", hat="beanie", overrides=None, show_all_hats=True, body="pill"):
    mats = dict(MATS)
    if overrides:
        for k, rgb in overrides.items():
            mats[k] = material("Char_%s_%s" % (prefix, k), rgb)
    foot_h, shin, thigh = 0.08, 0.20, 0.22
    hip_z = foot_h + shin + thigh
    torso_z0 = hip_z - 0.06
    root = empty(prefix + "_Root", origin)
    if body == "sphere":
        head_r = 0.30
        center = torso_z0 + head_r
        bm = sphere_bm(head_r, 12, 8)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, center))
        torso = make(prefix + "_Body", bm, mats["pill"], root)
        shoulder = (0.27, center + 0.165)
        face_z, eye_x = center + 0.11, 0.09
        eye_y = -math.sqrt(head_r ** 2 - 0.11 ** 2 - eye_x ** 2)
        mouth = (center + 0.01, -math.sqrt(head_r ** 2 - 0.01 ** 2))
        belt_bm = cylinder_bm(math.sqrt(head_r ** 2 - 0.21 ** 2) + 0.012, math.sqrt(head_r ** 2 - 0.15 ** 2) + 0.012, 0.06, 12, center - 0.21)
        stripe_bm = cylinder_bm(math.sqrt(head_r ** 2 - 0.10 ** 2) + 0.006, math.sqrt(head_r ** 2 - 0.05 ** 2) + 0.006, 0.05, 12, center - 0.10)
        mount_z = center + 0.07
        rope_base = head_r - 0.07
        rope_h, k = 0.17, head_r / 0.22
    elif body == "brick":
        w, d, h = 0.40, 0.30, 0.62
        top = torso_z0 + h
        head_r = 0.24
        torso = make(prefix + "_Body", box_bm(w, d, h, torso_z0, 0.045), mats["pill"], root)
        shoulder = (w / 2 + 0.02, top - 0.18)
        face_z, eye_x = top - 0.16, 0.09
        eye_y = -d / 2
        mouth = (top - 0.245, -d / 2)
        belt_bm = box_bm(w + 0.024, d + 0.024, 0.06, torso_z0 + 0.08, 0.01)
        stripe_bm = box_bm(w + 0.012, d + 0.012, 0.05, torso_z0 + 0.22, 0.008)
        mount_z = top - 0.10
        rope_base = 0.10
        rope_h, k = 0.27, 1.0
    else:
        head_r = 0.22
        torso_h = 0.72
        center = torso_z0 + torso_h - head_r
        torso = make(prefix + "_Body", capsule_bm(head_r, torso_h, 12, 4, torso_z0), mats["pill"], root)
        shoulder = (head_r + 0.02, center - 0.02)
        face_z, eye_x = center, 0.08
        eye_y = -math.sqrt(head_r ** 2 - eye_x ** 2)
        mouth = (center - 0.085, -head_r)
        belt_bm = cylinder_bm(head_r + 0.012, head_r + 0.012, 0.06, 12, torso_z0 + 0.20)
        stripe_bm = cylinder_bm(head_r + 0.006, head_r + 0.006, 0.05, 12, torso_z0 + 0.36)
        mount_z = center
        rope_base = head_r
        rope_h, k = 0.17, 1.0
    make(prefix + "_Belt", belt_bm, mats["belt"], torso)
    make(prefix + "_Stripe", stripe_bm, mats["stripe"], torso)
    for side, tag in ((1, "L"), (-1, "R")):
        sh = empty(prefix + "_Shoulder_" + tag, (side * shoulder[0], 0, shoulder[1]), torso)
        upper = make(prefix + "_UpperArm_" + tag, capsule_bm(0.048, 0.26, 8, 2, -0.26), mats["pill"], sh, (0, 0, 0), (0, math.radians(-8 * side), 0))
        lower = make(prefix + "_LowerArm_" + tag, capsule_bm(0.042, 0.24, 8, 2, -0.24), mats["pill"], upper, (0, 0, -0.26))
        make(prefix + "_Hand_" + tag, sphere_bm(0.07, 8, 5), mats["skin"], lower, (0, 0, -0.24))
        hip = empty(prefix + "_Hip_" + tag, (side * 0.10, 0, hip_z), root)
        thigh_ob = make(prefix + "_UpperLeg_" + tag, capsule_bm(0.065, thigh + 0.04, 8, 2, -thigh), mats["pill"], hip)
        shin_ob = make(prefix + "_LowerLeg_" + tag, capsule_bm(0.058, shin + 0.03, 8, 2, -shin), mats["pants"], thigh_ob, (0, 0, -thigh))
        make(prefix + "_Foot_" + tag, box_bm(0.14, 0.24, foot_h, -foot_h, 0.03, y_off=-0.05), mats["boot"], shin_ob, (0, 0, -shin))
    add_eyes(torso, face_z, eye_x, eye_y, 0.048)
    add_mouth(torso, mouth[0], mouth[1], mats["pupil"])
    mount = empty(prefix + "_HatMount", (0, 0, mount_z), torso)
    rope = add_rope_loop(mount, mats, rope_base, height=rope_h)
    rope.name = prefix + "_RopeLoop"
    for name, builder in HATS.items():
        if name != hat and not show_all_hats:
            continue
        ob = builder(mount, mats)
        ob.scale = (k, k, k)
        hidden = name != hat
        for child in [ob] + list(ob.children_recursive):
            child.name = "%s_%s" % (prefix, child.name)
            child.hide_set(hidden)
            child.hide_render = hidden
    return root


def frame_view():
    for area in bpy.context.screen.areas:
        if area.type != "VIEW_3D":
            continue
        space = area.spaces.active
        space.shading.type = "SOLID"
        space.shading.color_type = "MATERIAL"
        space.shading.light = "STUDIO"
        space.overlay.show_floor = True
        region = next(r for r in area.regions if r.type == "WINDOW")
        space.region_3d.view_rotation = Euler((math.radians(72), 0, math.radians(-32))).to_quaternion()
        space.region_3d.view_perspective = "PERSP"
        with bpy.context.temp_override(area=area, region=region):
            bpy.ops.view3d.view_all(center=False)
        break


for ob in list(COLL.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
build_pill((-1.2, 0, 0))
build_pill((0, 0, 0), "Ball", body="sphere")
build_pill((1.2, 0, 0), "Brick", body="brick")
frame_view()
print("BUILT", len(COLL.objects), "objects")


VARIANTS = [
    ("bandana", {"pill": (0.82, 0.45, 0.20), "pants": (0.30, 0.25, 0.20), "hat": (0.20, 0.35, 0.60)}),
    ("tricorn", {"pill": (0.55, 0.25, 0.55), "pants": (0.15, 0.15, 0.18), "stripe": (0.95, 0.85, 0.40)}),
    ("straw", {"pill": (0.30, 0.55, 0.30), "pants": (0.40, 0.30, 0.20), "hat": (0.75, 0.20, 0.20)}),
]


def build_lineup(x0=-3.9, step=1.0):
    roots = []
    for i, (hat, colors) in enumerate(VARIANTS):
        roots.append(build_pill((x0 + i * step, 0, 0), "Variant%d" % i, hat, colors, show_all_hats=False))
    return roots


def remove_lineup(roots):
    for root in roots:
        for ob in [root] + list(root.children_recursive):
            bpy.data.objects.remove(ob, do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
