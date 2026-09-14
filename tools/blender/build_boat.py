import bpy
import json
import math
import os
import sys

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
repo_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
models_dir = os.path.join(repo_root, "Assets", "Knotical", "Art", "Models")
out_fbx = os.path.abspath(args[0] if args else os.path.join(models_dir, "boat_placeholder.fbx"))
out_json = os.path.abspath(args[1] if len(args) > 1 else os.path.join(models_dir, "boat_placeholder.json"))
MAST_Y = 1.2
MAST_HEIGHT = 9.5

STERN_Y = -6.2
BOW_Y = 6.8
HALF_BEAM = 2.2
DRAFT = 1.5
ROCKER = 1.15
ROCKER_POW = 2.6
FREEBOARD = 1.35
BOW_RISE = 1.0
STERN_RISE = 0.9
SHEER_POW = 2.4
BOW_SHARP = 2.2
STERN_SHARP = 3.0
TRANSOM = 0.55
TRANSOM_RAKE = 0.6
STEM_RAKE = 1.4
STEM_POW = 3.0
BILGE = 0.42
STEM_MIN = 0.06
MAIN_DECK = 1.25
QUARTER_DECK = 2.05
QD_T = 0.30
BULWARK = 0.55
WALL = 0.15
CAP_LIP = 0.07
CAP_DROP = 0.12
GANGWAY_T = 0.52
GANGWAY_HALF = 0.75
STRIPE_LO = -0.18
STRIPE_HI = 0.14
STATIONS = 28
SIDE_SAMPLES = 7
CAMBER = 0.06

COL = {
    "red": "#D0452C",
    "cream": "#F4E6C1",
    "navy": "#25324D",
    "wood": "#5B3A22",
    "wood_light": "#B98A55",
    "teal": "#2FA7A0",
    "yellow": "#F6C744",
    "plank": "#C9A468",
    "black": "#1A1414",
}


def hex_to_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def plan_factor(t):
    m = abs(t * 2.0 - 1.0)
    sharp = BOW_SHARP if t > 0.5 else STERN_SHARP
    f = max(0.0, 1.0 - m ** sharp) ** 0.65
    if t < 0.5:
        f = f + (1.0 - f) * (TRANSOM * m * m)
    return f


def half_width(t):
    return max(HALF_BEAM * plan_factor(t), STEM_MIN)


def keel_z(t):
    m = abs(t * 2.0 - 1.0)
    return -DRAFT + ROCKER * m ** ROCKER_POW


def sheer_z(t):
    m = abs(t * 2.0 - 1.0)
    rise = BOW_RISE if t > 0.5 else STERN_RISE
    return FREEBOARD + rise * m ** SHEER_POW


def rake_y(t, v):
    if t < 0.5:
        m = 1.0 - t * 2.0
        return -TRANSOM_RAKE * v * m * m * m
    n = t * 2.0 - 1.0
    return STEM_RAKE * v * n ** STEM_POW


def station_y(t):
    return STERN_Y + t * (BOW_Y - STERN_Y)


def deck_z(t):
    return QUARTER_DECK if t < QD_T else MAIN_DECK


def rail_top(t):
    return max(sheer_z(t) + BULWARK, deck_z(t) + 0.62)


def in_gangway(t):
    return abs(station_y(t) - station_y(GANGWAY_T)) < GANGWAY_HALF


def shell_point(t, u):
    v = abs(u)
    w = half_width(t)
    x = math.copysign(w * v ** BILGE, u) if v > 0 else 0.0
    k = keel_z(t)
    s = sheer_z(t)
    return (x, station_y(t) + rake_y(t, v), k + (s - k) * v)


def side_samples(t):
    k = keel_z(t)
    s = sheer_z(t)
    v_lo = (STRIPE_LO - k) / (s - k)
    v_hi = (STRIPE_HI - k) / (s - k)
    return [0.0, v_lo * 0.35, v_lo * 0.7, v_lo, v_hi, v_hi + (1.0 - v_hi) * 0.5, 1.0]


class Part:
    def __init__(self):
        self.verts = []
        self.faces = []
        self.colors = []
        self.smooth = []
        self.material = []

    def add_vert(self, p):
        self.verts.append(tuple(p))
        return len(self.verts) - 1

    def add_face(self, idx, color, smooth=False, material=0):
        idx = [i for j, i in enumerate(idx) if i not in idx[:j]]
        if len(idx) < 3:
            return
        self.faces.append(tuple(idx))
        self.colors.append(color)
        self.smooth.append(smooth)
        self.material.append(material)

    def box(self, centre, size, color, material=0):
        cx, cy, cz = centre
        sx, sy, sz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
        c = [self.add_vert((cx + dx * sx, cy + dy * sy, cz + dz * sz))
             for dz in (-1, 1) for dy in (-1, 1) for dx in (-1, 1)]
        for f in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
            self.add_face([c[i] for i in f], color, False, material)

    def cylinder(self, a, b, r0, r1, color, sides=10, material=0):
        ax, ay, az = a
        bx, by, bz = b
        d = (bx - ax, by - ay, bz - az)
        length = math.sqrt(sum(c * c for c in d))
        d = tuple(c / length for c in d)
        ref = (0.0, 0.0, 1.0) if abs(d[2]) < 0.9 else (1.0, 0.0, 0.0)
        s = (d[1] * ref[2] - d[2] * ref[1], d[2] * ref[0] - d[0] * ref[2], d[0] * ref[1] - d[1] * ref[0])
        sl = math.sqrt(sum(c * c for c in s))
        s = tuple(c / sl for c in s)
        u = (d[1] * s[2] - d[2] * s[1], d[2] * s[0] - d[0] * s[2], d[0] * s[1] - d[1] * s[0])
        ring0 = []
        ring1 = []
        for i in range(sides):
            ang = math.tau * i / sides
            off = tuple(s[j] * math.cos(ang) + u[j] * math.sin(ang) for j in range(3))
            ring0.append(self.add_vert((ax + off[0] * r0, ay + off[1] * r0, az + off[2] * r0)))
            ring1.append(self.add_vert((bx + off[0] * r1, by + off[1] * r1, bz + off[2] * r1)))
        for i in range(sides):
            j = (i + 1) % sides
            self.add_face([ring0[i], ring0[j], ring1[j], ring1[i]], color, True, material)
        self.add_face(list(reversed(ring0)), color, False, material)
        self.add_face(ring1, color, False, material)


def station_list():
    ts = [i / (STATIONS - 1) for i in range(STATIONS)]
    ts.append(QD_T)
    g0 = GANGWAY_T - GANGWAY_HALF / (BOW_Y - STERN_Y)
    g1 = GANGWAY_T + GANGWAY_HALF / (BOW_Y - STERN_Y)
    ts.extend([g0 - 1e-4, g0 + 1e-4, g1 - 1e-4, g1 + 1e-4])
    ts = sorted(set(round(t, 6) for t in ts if 0.0 <= t <= 1.0))
    return ts


def build():
    paint = Part()
    deck = Part()
    ts = station_list()
    red, cream, navy = hex_to_rgb(COL["red"]), hex_to_rgb(COL["cream"]), hex_to_rgb(COL["navy"])
    wood, wood_light, teal = hex_to_rgb(COL["wood"]), hex_to_rgb(COL["wood_light"]), hex_to_rgb(COL["teal"])
    yellow = hex_to_rgb(COL["yellow"])

    shell_rings = []
    for t in ts:
        samples = side_samples(t)
        ring = []
        for v in reversed(samples[1:]):
            ring.append(paint.add_vert(shell_point(t, -v)))
        ring.append(paint.add_vert(shell_point(t, 0.0)))
        for v in samples[1:]:
            ring.append(paint.add_vert(shell_point(t, v)))
        shell_rings.append(ring)

    def band_color(z):
        if z < STRIPE_LO:
            return red
        if z < STRIPE_HI:
            return navy
        return cream

    for i in range(len(ts) - 1):
        a, b = shell_rings[i], shell_rings[i + 1]
        for j in range(len(a) - 1):
            quad = [a[j], b[j], b[j + 1], a[j + 1]]
            zc = sum(paint.verts[q][2] for q in quad) / 4.0
            paint.add_face(quad, band_color(zc), True)

    bul = []
    for t in ts:
        w = half_width(t)
        y = station_y(t)
        y_top = y + rake_y(t, 1.0)
        s = sheer_z(t)
        inner = max(w - WALL, 0.03)
        pts = {}
        for side in (-1, 1):
            pts[(side, "sheer")] = paint.add_vert((side * w, y_top, s))
            pts[(side, "outer_top")] = paint.add_vert((side * (w + CAP_LIP), y_top, rail_top(t)))
            pts[(side, "outer_cap")] = paint.add_vert((side * (w + CAP_LIP), y_top, rail_top(t) - CAP_DROP))
            pts[(side, "inner_top")] = paint.add_vert((side * inner, y_top, rail_top(t)))
            pts[(side, "inner_bottom")] = paint.add_vert((side * inner, y_top, deck_z(t)))
        bul.append(pts)

    for i in range(len(ts) - 1):
        ta, tb = ts[i], ts[i + 1]
        if in_gangway((ta + tb) * 0.5):
            continue
        a, b = bul[i], bul[i + 1]
        for side in (-1, 1):
            order = 1 if side > 0 else -1
            def quad(k0, k1):
                q = [a[(side, k0)], b[(side, k0)], b[(side, k1)], a[(side, k1)]]
                return q if order > 0 else list(reversed(q))
            paint.add_face(quad("sheer", "outer_cap"), cream, True)
            paint.add_face(quad("outer_cap", "outer_top"), wood, False)
            paint.add_face(quad("outer_top", "inner_top"), wood, False)
            paint.add_face(quad("inner_top", "inner_bottom"), wood_light, True)

    for i in range(len(ts)):
        t = ts[i]
        left_gap = i > 0 and in_gangway((ts[i - 1] + t) * 0.5)
        right_gap = i < len(ts) - 1 and in_gangway((t + ts[i + 1]) * 0.5)
        if left_gap == right_gap:
            continue
        pts = bul[i]
        for side in (-1, 1):
            cap = [pts[(side, "sheer")], pts[(side, "outer_cap")], pts[(side, "outer_top")],
                   pts[(side, "inner_top")], pts[(side, "inner_bottom")]]
            if (right_gap and side > 0) or (left_gap and side < 0):
                cap = list(reversed(cap))
            paint.add_face(cap, wood_light, False)

    stern = bul[0]
    transom = [stern[(-1, "inner_bottom")], stern[(-1, "inner_top")], stern[(-1, "outer_top")],
               stern[(-1, "outer_cap")], stern[(-1, "sheer")]]
    transom += shell_rings[0]
    transom += [stern[(1, "sheer")], stern[(1, "outer_cap")], stern[(1, "outer_top")],
                stern[(1, "inner_top")], stern[(1, "inner_bottom")]]
    transom_c = paint.add_vert(tuple(sum(paint.verts[v][k] for v in transom) / len(transom) for k in range(3)))
    for i in range(len(transom)):
        paint.add_face([transom_c, transom[i], transom[(i + 1) % len(transom)]], cream, False)

    bow = bul[-1]
    paint.add_face([bow[(1, "inner_bottom")], bow[(1, "inner_top")], bow[(1, "outer_top")], bow[(1, "outer_cap")], bow[(1, "sheer")]], cream, False)
    paint.add_face([bow[(-1, "sheer")], bow[(-1, "outer_cap")], bow[(-1, "outer_top")], bow[(-1, "inner_top")], bow[(-1, "inner_bottom")]], cream, False)
    paint.add_face([bow[(1, "sheer")], bow[(-1, "sheer")], bow[(-1, "inner_bottom")], bow[(1, "inner_bottom")]], cream, False)

    deck_rows = []
    for i, t in enumerate(ts):
        w = half_width(t)
        inner = max(w - WALL, 0.03)
        y = station_y(t) + rake_y(t, 1.0)
        z = deck_z(t)
        deck_rows.append((t, [deck.add_vert((-inner, y, z)), deck.add_vert((0.0, y, z + CAMBER)), deck.add_vert((inner, y, z))]))
    for i in range(len(ts) - 1):
        ta, ra = deck_rows[i]
        tb, rb = deck_rows[i + 1]
        if deck_z(ta) != deck_z(tb):
            za = deck_z(ta)
            zb = deck_z(tb)
            hi_row, lo_row = (ra, rb) if za > zb else (rb, ra)
            y_edge = deck.verts[hi_row[1]][1]
            lo_edge = [deck.add_vert((deck.verts[v][0], y_edge, deck.verts[v][2])) for v in lo_row]
            for j in range(2):
                face = [hi_row[j], hi_row[j + 1], lo_edge[j + 1], lo_edge[j]]
                if za > zb:
                    face = list(reversed(face))
                deck.add_face(face, wood_light, False)
            for j in range(2):
                deck.add_face([lo_edge[j], lo_edge[j + 1], lo_row[j + 1], lo_row[j]] if za > zb else [ra[j], ra[j + 1], lo_edge[j + 1], lo_edge[j]], hex_to_rgb(COL["plank"]), False)
            continue
        for j in range(2):
            deck.add_face([ra[j], ra[j + 1], rb[j + 1], rb[j]], hex_to_rgb(COL["plank"]), False)

    qd_y = station_y(QD_T)
    step_w = 1.4
    steps = []
    for k in range(3):
        rise = (QUARTER_DECK - MAIN_DECK) / 3.0
        depth = 0.34
        z_top = MAIN_DECK + rise * (k + 1)
        y_c = qd_y + depth * (2 - k) + depth * 0.5
        paint.box((0.0, y_c, (MAIN_DECK + z_top) * 0.5), (step_w, depth, z_top - MAIN_DECK), wood_light)
        steps.append({"y": y_c, "z_top": z_top, "depth": depth})

    cabin_t0, cabin_t1 = 0.70, 0.82
    cabin_y0, cabin_y1 = station_y(cabin_t0), station_y(cabin_t1)
    cabin_w = max(half_width(cabin_t1) - WALL - 0.64, 0.7)
    cabin_h = 1.25
    roof_r = 0.5
    arch = []
    n_arch = 7
    for i in range(n_arch + 1):
        a = math.pi * i / n_arch
        arch.append((-math.cos(a), math.sin(a)))
    profile = [(-cabin_w, 0.0)]
    profile += [(x * cabin_w, cabin_h - roof_r + y * roof_r) for x, y in arch]
    profile += [(cabin_w, 0.0)]
    ring_a = [paint.add_vert((x, cabin_y0, MAIN_DECK + z)) for x, z in profile]
    ring_b = [paint.add_vert((x, cabin_y1, MAIN_DECK + z)) for x, z in profile]
    for i in range(len(profile) - 1):
        z0 = profile[i][1]
        z1 = profile[i + 1][1]
        color = red if min(z0, z1) >= cabin_h - roof_r - 1e-6 else cream
        if 0.55 <= max(z0, z1) <= 0.8:
            color = teal
        paint.add_face([ring_a[i], ring_a[i + 1], ring_b[i + 1], ring_b[i]], color, min(z0, z1) >= cabin_h - roof_r - 1e-6)
    paint.add_face(list(reversed(ring_a)), cream, False)
    paint.add_face(ring_b, cream, False)
    band_z = MAIN_DECK + 0.62
    paint.box((0.0, (cabin_y0 + cabin_y1) * 0.5, band_z), (cabin_w * 2 + 0.04, cabin_y1 - cabin_y0 + 0.04, 0.16), teal)
    paint.box((0.0, cabin_y0 - 0.02, MAIN_DECK + 0.5), (0.7, 0.04, 1.0), wood)
    paint.box((0.0, cabin_y0 - 0.03, MAIN_DECK + 0.5), (0.5, 0.03, 0.8), wood_light)

    bow_sheer = sheer_z(1.0)
    bow_tip_y = BOW_Y + rake_y(1.0, 1.0)
    paint.cylinder((0.0, bow_tip_y - 0.9, bow_sheer + 0.15), (0.0, bow_tip_y + 2.3, bow_sheer + 0.75), 0.13, 0.07, wood)
    paint.cylinder((0.0, bow_tip_y + 2.3, bow_sheer + 0.75), (0.0, bow_tip_y + 2.55, bow_sheer + 0.82), 0.16, 0.16, yellow, 8)

    for t in (0.22, 0.70):
        for side in (-1, 1):
            w = half_width(t)
            y = station_y(t) + rake_y(t, 1.0)
            z = rail_top(t)
            x = side * (w - WALL * 0.5)
            paint.box((x, y, z + 0.06), (0.1, 0.1, 0.12), wood)
            paint.box((x, y, z + 0.15), (0.14, 0.42, 0.09), wood)

    lantern_y = STERN_Y + 0.55
    paint.cylinder((0.0, lantern_y, QUARTER_DECK), (0.0, lantern_y, QUARTER_DECK + 1.5), 0.06, 0.05, wood, 8)
    paint.box((0.0, lantern_y, QUARTER_DECK + 1.68), (0.3, 0.3, 0.36), yellow)
    paint.box((0.0, lantern_y, QUARTER_DECK + 1.88), (0.36, 0.36, 0.06), wood)

    mast_top = MAIN_DECK + MAST_HEIGHT
    paint.cylinder((0.0, MAST_Y, MAIN_DECK - 0.2), (0.0, MAST_Y, mast_top), 0.17, 0.09, wood, 10)
    paint.cylinder((0.0, MAST_Y, mast_top), (0.0, MAST_Y, mast_top + 0.22), 0.14, 0.14, yellow, 8)
    yard_z = MAIN_DECK + 6.4
    paint.cylinder((-2.7, MAST_Y + 0.05, yard_z), (2.7, MAST_Y + 0.05, yard_z), 0.06, 0.06, wood, 8)
    boom_z = MAIN_DECK + 2.6
    paint.cylinder((0.0, MAST_Y - 0.1, boom_z), (0.0, MAST_Y - 5.2, boom_z + 0.25), 0.07, 0.05, wood, 8)
    paint.box((0.0, MAST_Y, MAIN_DECK + 0.12), (0.9, 0.9, 0.24), wood_light)

    helm_y = STERN_Y + 2.1
    paint.box((0.0, helm_y, QUARTER_DECK + 0.45), (0.22, 0.18, 0.9), wood)
    paint.cylinder((0.0, helm_y - 0.13, QUARTER_DECK + 1.05), (0.0, helm_y - 0.05, QUARTER_DECK + 1.05), 0.42, 0.42, wood_light, 12)
    paint.cylinder((0.0, helm_y - 0.16, QUARTER_DECK + 1.05), (0.0, helm_y - 0.02, QUARTER_DECK + 1.05), 0.09, 0.09, yellow, 8)

    fin_t0, fin_t1 = 0.16, 0.44
    fin_pts = []
    for t in (fin_t0, fin_t1):
        fin_pts.append((station_y(t), keel_z(t)))
    fa = paint.add_vert((0.0, fin_pts[0][0], fin_pts[0][1] + 0.05))
    fb = paint.add_vert((0.0, fin_pts[1][0], fin_pts[1][1] + 0.05))
    fc = paint.add_vert((0.0, fin_pts[1][0] - 0.3, fin_pts[1][1] - 0.45))
    fd = paint.add_vert((0.0, fin_pts[0][0] + 0.3, fin_pts[0][1] - 0.55))
    for side in (-1, 1):
        quad = [paint.add_vert((side * 0.07 + paint.verts[v][0], paint.verts[v][1], paint.verts[v][2])) for v in (fa, fb, fc, fd)]
        paint.add_face(list(reversed(quad)) if side > 0 else quad, red, False)
    paint.add_face([fd, fc, paint.verts.index(paint.verts[fc]), fd], red, False)

    hull_points = []
    for i in range(0, len(ts), 2):
        t = ts[i]
        for v in (0.0, 0.45, 0.8, 1.0):
            for side in (-1, 1):
                p = shell_point(t, side * v)
                if p[2] <= MAIN_DECK + 0.01 or v < 1.0:
                    hull_points.append([round(p[0], 3), round(p[1], 3), round(min(p[2], MAIN_DECK), 3)])
    seen = set()
    hull_unique = []
    for p in hull_points:
        key = tuple(p)
        if key not in seen:
            seen.add(key)
            hull_unique.append(p)

    walls = []
    seg_every = 2
    for i in range(0, len(ts) - seg_every, seg_every):
        ta, tb = ts[i], ts[min(i + seg_every, len(ts) - 1)]
        if in_gangway((ta + tb) * 0.5):
            continue
        for side in (-1, 1):
            wa, wb = half_width(ta), half_width(tb)
            ya = station_y(ta) + rake_y(ta, 1.0)
            yb = station_y(tb) + rake_y(tb, 1.0)
            xa, xb = side * (wa - WALL * 0.5), side * (wb - WALL * 0.5)
            z_bottom = min(deck_z(ta), deck_z(tb))
            z_top = min(rail_top(ta), rail_top(tb))
            length = math.hypot(xb - xa, yb - ya)
            yaw = math.atan2(xb - xa, yb - ya)
            walls.append({"x": round((xa + xb) * 0.5, 3), "y": round((ya + yb) * 0.5, 3),
                          "z0": round(z_bottom, 3), "z1": round(z_top, 3), "len": round(length, 3), "yaw": round(yaw, 4)})

    deck_outline = []
    for t, row in deck_rows:
        if deck_z(t) == MAIN_DECK:
            deck_outline.append([round(deck.verts[row[0]][0], 3), round(deck.verts[row[0]][1], 3)])
    quarter_outline = []
    for t, row in deck_rows:
        if deck_z(t) == QUARTER_DECK:
            quarter_outline.append([round(deck.verts[row[0]][0], 3), round(deck.verts[row[0]][1], 3)])

    spec = {
        "stern_y": STERN_Y, "bow_y": BOW_Y, "bow_tip_y": round(bow_tip_y, 3), "half_beam": HALF_BEAM,
        "draft": DRAFT, "main_deck": MAIN_DECK, "quarter_deck": QUARTER_DECK, "qd_y": round(qd_y, 3),
        "bulwark": BULWARK, "wall": WALL, "gangway_y": round(station_y(GANGWAY_T), 3), "gangway_half": GANGWAY_HALF,
        "mast_y": MAST_Y, "mast_top": round(mast_top, 3), "helm_y": round(helm_y, 3), "halyard": [0.55, 1.5, MAIN_DECK + 1.6],
        "length": round(BOW_Y - STERN_Y, 3), "beam": round(HALF_BEAM * 2.0, 3),
        "cabin": {"y0": round(cabin_y0, 3), "y1": round(cabin_y1, 3), "half_w": round(cabin_w, 3), "h": cabin_h},
        "steps": steps, "hull_points": hull_unique, "walls": walls,
        "deck_outline": deck_outline, "quarter_outline": quarter_outline,
        "hull_points_flat": [c for pt in hull_unique for c in pt],
        "deck_outline_flat": [c for pt in deck_outline for c in pt],
        "quarter_outline_flat": [c for pt in quarter_outline for c in pt],
        "bow_sheer": round(bow_sheer, 3), "stern_sheer": round(sheer_z(0.0), 3), "mid_sheer": round(sheer_z(0.5), 3),
        "stations": [{"t": t, "y": round(station_y(t), 3), "w": round(half_width(t), 3),
                      "keel": round(keel_z(t), 3), "sheer": round(sheer_z(t), 3), "deck": deck_z(t)} for t in ts],
    }
    return paint, deck, spec


def make_object(name, part, materials):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(part.verts, [], part.faces)
    mesh.update()
    for mat in materials:
        mesh.materials.append(mat)
    attr = mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")
    for poly in mesh.polygons:
        color = part.colors[poly.index]
        poly.use_smooth = part.smooth[poly.index]
        poly.material_index = part.material[poly.index]
        for li in poly.loop_indices:
            if hasattr(attr.data[li], "color_srgb"):
                attr.data[li].color_srgb = (color[0], color[1], color[2], 1.0)
            else:
                attr.data[li].color = (color[0] ** 2.2, color[1] ** 2.2, color[2] ** 2.2, 1.0)
    for prop in ("active_color_index", "render_color_index"):
        if hasattr(mesh.color_attributes, prop):
            setattr(mesh.color_attributes, prop, 0)
    if hasattr(mesh.color_attributes, "active_color"):
        mesh.color_attributes.active_color = attr
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def make_material(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 1.0
        attr = tree.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = "Col"
        tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    paint, deck, spec = build()
    paint_mat = make_material("Paint", (1.0, 1.0, 1.0))
    deck_mat = make_material("Deck", hex_to_rgb(COL["plank"]))
    make_object("Hull", paint, [paint_mat])
    make_object("Deck", deck, [deck_mat])

    for obj in bpy.context.scene.objects:
        obj.select_set(True)

    fbx_kwargs = dict(filepath=out_fbx, use_selection=True, global_scale=1.0, apply_unit_scale=True,
                      apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                      object_types={"MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                      bake_anim=False, add_leaf_bones=False)
    try:
        bpy.ops.export_scene.fbx(**fbx_kwargs, colors_type="LINEAR")
    except TypeError:
        bpy.ops.export_scene.fbx(**fbx_kwargs)

    with open(out_json, "w") as f:
        json.dump(spec, f, indent=1)
    print("boat: hull verts %d faces %d, deck verts %d faces %d" % (len(paint.verts), len(paint.faces), len(deck.verts), len(deck.faces)))
    print("boat: wrote %s and %s" % (out_fbx, out_json))


main()
