import json
import math
import os
import sys

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
spec_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "models", "boat_toon.json")
out_path = sys.argv[2] if len(sys.argv) > 2 else os.path.join(root, "boat_toon.tscn")
spec = json.load(open(spec_path))

MASS = 4000.0
WALL = spec["wall"]
MAIN = spec["main_deck"]
QD = spec["quarter_deck"]
QD_Y = spec["qd_y"]
STERN = spec["stern_y"]
BOW = spec["bow_y"]
HULL_LENGTH = round(BOW - STERN, 2)
BEAM = round(spec["half_beam"] * 2.0, 2)
MAST_Y = spec["mast_y"]
HELM_Y = spec["helm_y"]
MAST_TOP = MAIN + 8.9
DROP = 6.4


def g(x, y_b, z_b):
    return (round(x, 3), round(z_b, 3), round(-y_b, 3))


def f(v):
    s = ("%.4f" % v).rstrip("0").rstrip(".")
    return s if s not in ("", "-0") else "0"


def vec3(p):
    return "Vector3(%s, %s, %s)" % (f(p[0]), f(p[1]), f(p[2]))


def xform(pos, basis=None):
    b = basis or (1, 0, 0, 0, 1, 0, 0, 0, 1)
    return "Transform3D(%s, %s, %s)" % (", ".join(f(v) for v in b[:3]), ", ".join(f(v) for v in b[3:9]) if False else ", ".join(f(v) for v in b[3:]), ", ".join(f(v) for v in pos))


def packed(points):
    return "PackedVector3Array(" + ", ".join(", ".join(f(c) for c in p) for p in points) + ")"


stations = spec["stations"]


def width_at(y_b):
    best = min(stations, key=lambda s: abs(s["y"] - y_b))
    return best["w"]


hull_points = [g(*p) for p in spec["hull_points"]]

deck_points = []
for x, y in spec["deck_outline"]:
    for sx in (-1, 1):
        for z in (MAIN, MAIN - 0.3):
            deck_points.append(g(sx * abs(x), y, z))
quarter_points = []
for x, y in spec["quarter_outline"]:
    for sx in (-1, 1):
        for z in (QD, MAIN - 0.1):
            quarter_points.append(g(sx * abs(x), y, z))

toe_y = QD_Y + 0.34 * 3 + 0.1
ramp_points = [g(sx * 0.72, toe_y, MAIN) for sx in (-1, 1)]
ramp_points += [g(sx * 0.72, QD_Y - 0.02, MAIN) for sx in (-1, 1)]
ramp_points += [g(sx * 0.72, QD_Y - 0.02, QD) for sx in (-1, 1)]

cabin = spec["cabin"]
cabin_centre = g(0.0, (cabin["y0"] + cabin["y1"]) * 0.5, MAIN + cabin["h"] * 0.5)
cabin_size = (round(cabin["half_w"] * 2.0, 3), round(cabin["h"], 3), round(cabin["y1"] - cabin["y0"], 3))

sub = []
nodes = []

sub.append('[sub_resource type="PhysicsMaterial" id="PhysicsMaterial_hull"]\nfriction = 0.7\n')
sub.append('[sub_resource type="ConvexPolygonShape3D" id="Convex_hull"]\npoints = %s\n' % packed(hull_points))
sub.append('[sub_resource type="ConvexPolygonShape3D" id="Convex_deck"]\npoints = %s\n' % packed(deck_points))
sub.append('[sub_resource type="ConvexPolygonShape3D" id="Convex_quarter"]\npoints = %s\n' % packed(quarter_points))
sub.append('[sub_resource type="ConvexPolygonShape3D" id="Convex_ramp"]\npoints = %s\n' % packed(ramp_points))
sub.append('[sub_resource type="BoxShape3D" id="Box_cabin"]\nsize = %s\n' % vec3(cabin_size))
sub.append('[sub_resource type="CylinderShape3D" id="Cylinder_grip"]\nradius = 0.62\nheight = 0.24\n')
sub.append('[sub_resource type="BoxMesh" id="BoxMesh_pedestal"]\nsize = Vector3(0.22, 0.95, 0.18)\n')

for i, w in enumerate(spec["walls"]):
    sub.append('[sub_resource type="BoxShape3D" id="Box_wall%d"]\nsize = %s\n' % (i, vec3((WALL, round(w["z1"] - w["z0"], 3), w["len"]))))

nodes.append('''[node name="BoatToon" type="RigidBody3D"]
mass = %s
physics_material_override = SubResource("PhysicsMaterial_hull")
center_of_mass_mode = 1
center_of_mass = Vector3(0, -0.35, 0.2)
can_sleep = false
contact_monitor = true
max_contacts_reported = 8
script = ExtResource("1_ctrl")
GovernorSpeed = 8.0
HullLength = %s
Beam = %s
HullBottom = %s
''' % (f(MASS), f(HULL_LENGTH), f(BEAM), f(-spec["draft"])))

nodes.append('[node name="HullCol" type="CollisionShape3D" parent="."]\nshape = SubResource("Convex_hull")\n')
nodes.append('[node name="DeckCol" type="CollisionShape3D" parent="."]\nshape = SubResource("Convex_deck")\n')
nodes.append('[node name="QuarterCol" type="CollisionShape3D" parent="."]\nshape = SubResource("Convex_quarter")\n')
nodes.append('[node name="RampCol" type="CollisionShape3D" parent="."]\nshape = SubResource("Convex_ramp")\n')
nodes.append('[node name="CabinCol" type="CollisionShape3D" parent="."]\ntransform = %s\nshape = SubResource("Box_cabin")\n' % xform(cabin_centre))

for i, w in enumerate(spec["walls"]):
    psi = w["yaw"]
    c, s = math.cos(psi), math.sin(psi)
    basis = (-c, 0, s, 0, 1, 0, -s, 0, -c)
    pos = g(w["x"], w["y"], (w["z0"] + w["z1"]) * 0.5)
    nodes.append('[node name="Wall%d" type="CollisionShape3D" parent="."]\ntransform = %s\nshape = SubResource("Box_wall%d")\n' % (i, xform(pos, basis), i))

nodes.append('[node name="Model" parent="." instance=ExtResource("10_model")]\n')
nodes.append('''[node name="Paint" type="Node" parent="."]
script = ExtResource("11_paint")
Target = NodePath("../Model")
PaintMaterial = ExtResource("12_mat_paint")
DeckMaterial = ExtResource("13_mat_deck")
''')

nodes.append('''[node name="Buoyancy" type="Node3D" parent="."]
script = ExtResource("2_buoy")
Radius = 1.0
Coefficient = 0.27
DampingFactor1 = 1.0
DampingFactor2 = 0.4
DragCoefficient = 0.35
DragCoefficient2 = 0.4
MaxDragSpeed = 12.0
AngularDrag = 1.0
TiltResponse = 0.45
ShortWaveFilter = 1.0
SnapToWaterOnActivation = true
''')
for name, t in (("Aft", 0.12), ("Mid", 0.38), ("Fore", 0.64), ("Bow", 0.88)):
    y_b = STERN + t * (BOW - STERN)
    w = width_at(y_b) * 0.6
    for side, label in ((-1, "L"), (1, "R")):
        nodes.append('[node name="%s%s" type="Marker3D" parent="Buoyancy"]\ntransform = %s\n' % (name, label, xform(g(side * w, y_b, -0.05))))

nodes.append('''[node name="Rudder" type="MeshInstance3D" parent="."]
transform = %s
material_override = ExtResource("14_mat_wood")
script = ExtResource("3_rudder")
Span = 1.4
HeadChord = 0.75
FootChord = 0.5
''' % xform(g(0.0, STERN - 0.15, -0.3)))

nodes.append('''[node name="Mast" type="MeshInstance3D" parent="."]
transform = Transform3D(0, -1, 0, 1, 0, 0, 0, 0, 1, 0, %s, %s)
material_override = ExtResource("14_mat_wood")
script = ExtResource("5_spar")
HalfLength = %s
EndRadius = 0.09
MidRadius = 0.16
''' % (f((MAIN + MAST_TOP + 0.3) * 0.5), f(-MAST_Y), f((MAST_TOP + 0.3 - MAIN) * 0.5)))

nodes.append('''[node name="BoomPivot" type="Node3D" parent="."]
transform = %s
''' % xform(g(0.0, MAST_Y, MAST_TOP)))
nodes.append('''[node name="Boom" type="MeshInstance3D" parent="BoomPivot"]
transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, %s, 0)
material_override = ExtResource("14_mat_wood")
script = ExtResource("5_spar")
HalfLength = 2.6
EndRadius = 0.07
MidRadius = 0.1
''' % f(-DROP))
nodes.append('''[node name="Sail" type="MeshInstance3D" parent="BoomPivot"]
script = ExtResource("4_sail")
HeadWidth = 2.9
FootWidth = 5.2
Drop = %s
PanelsAcross = 10
PanelsDown = 12
Deployment = 0.0
TargetDeployment = 0.0
Boom = NodePath("..")
''' % f(DROP))
nodes.append('''[node name="HalyardMain" type="MeshInstance3D" parent="."]
transform = %s
script = ExtResource("6_halyard")
SailPath = NodePath("../BoomPivot/Sail")
''' % xform(g(0.6, MAST_Y - 0.3, MAIN + 1.7)))

nodes.append('''[node name="Helm" type="Node3D" parent="."]
transform = %s
script = ExtResource("7_helm")
Wheel = NodePath("Mount/Wheel")
Rudder = NodePath("../Rudder")
''' % xform(g(0.0, HELM_Y, QD)))
nodes.append('''[node name="Pedestal" type="MeshInstance3D" parent="Helm"]
transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0.47, 0)
material_override = ExtResource("14_mat_wood")
mesh = SubResource("BoxMesh_pedestal")
''')
nodes.append('''[node name="Mount" type="Node3D" parent="Helm"]
transform = Transform3D(1, 0, 0, 0, 0.9848078, -0.17364818, 0, 0.17364818, 0.9848078, 0, 1.05, 0)
''')
nodes.append('''[node name="Wheel" type="MeshInstance3D" parent="Helm/Mount"]
material_override = ExtResource("14_mat_wood")
script = ExtResource("8_wheel")
Radius = 0.45
HandleLength = 0.16
''')
nodes.append('''[node name="Grip" type="AnimatableBody3D" parent="Helm/Mount/Wheel"]
collision_layer = 2
collision_mask = 0
sync_to_physics = false
''')
nodes.append('''[node name="Shape" type="CollisionShape3D" parent="Helm/Mount/Wheel/Grip"]
transform = Transform3D(1, 0, 0, 0, 0, 1, 0, -1, 0, 0, 0, 0)
shape = SubResource("Cylinder_grip")
''')
nodes.append('''[node name="Wake" type="Node" parent="."]
script = ExtResource("9_wake")
DropSpacing = 4.0
FullSpeed = 7.0
''')

ext = '''[ext_resource type="Script" path="res://scripts/boat/SailController.cs" id="1_ctrl"]
[ext_resource type="Script" path="res://scripts/boat/Buoyancy.cs" id="2_buoy"]
[ext_resource type="Script" path="res://scripts/boat/Rudder.cs" id="3_rudder"]
[ext_resource type="Script" path="res://scripts/boat/Sail.cs" id="4_sail"]
[ext_resource type="Script" path="res://scripts/boat/Spar.cs" id="5_spar"]
[ext_resource type="Script" path="res://scripts/boat/Halyard.cs" id="6_halyard"]
[ext_resource type="Script" path="res://scripts/boat/Helm.cs" id="7_helm"]
[ext_resource type="Script" path="res://scripts/boat/Wheel.cs" id="8_wheel"]
[ext_resource type="Script" path="res://scripts/boat/BoatWake.cs" id="9_wake"]
[ext_resource type="PackedScene" path="res://models/boat_toon.glb" id="10_model"]
[ext_resource type="Script" path="res://scripts/style/ToonPaint.cs" id="11_paint"]
[ext_resource type="Material" path="res://materials/toon_paint.tres" id="12_mat_paint"]
[ext_resource type="Material" path="res://materials/toon_deck.tres" id="13_mat_deck"]
[ext_resource type="Material" path="res://materials/toon_wood.tres" id="14_mat_wood"]
'''

steps = 14 + len(sub) + 1
text = "[gd_scene load_steps=%d format=3]\n\n%s\n%s\n%s" % (steps, ext, "\n".join(sub), "\n".join(nodes))
open(out_path, "w", newline="\n").write(text)
print("wrote %s: %d walls, %d hull points, hull %.1f x %.1f m, deck %.2f, quarterdeck %.2f, helm z %.2f" % (
    out_path, len(spec["walls"]), len(hull_points), HULL_LENGTH, BEAM, MAIN, QD, -HELM_Y))
