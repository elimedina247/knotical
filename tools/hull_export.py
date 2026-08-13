import argparse
import math
import os

LENGTH = 50.0
BEAM = 18.0
DRAFT = 7.0
FREEBOARD = 3.5
DECK_HEIGHT = 2.7
BOW_SHEER_RISE = 2.6
STERN_SHEER_RISE = 1.9
SHEER_POWER = 2.4
ROCKER = 5.6
ROCKER_POWER = 2.6
BOW_SHARPNESS = 2.2
STERN_SHARPNESS = 3.0
BILGE_FULLNESS = 0.42
THICKNESS = 0.32
STRAKE_PROUD = 0.28
STRAKE_COUNT = 24
STATIONS = 40
EVEN_PLANK_WIDTH = True


class Hull:
    def __init__(self, transom_width, transom_rake):
        self.transom_width = transom_width
        self.transom_rake = transom_rake

    def plan_factor(self, t):
        m = abs(t * 2.0 - 1.0)
        sharpness = BOW_SHARPNESS if t > 0.5 else STERN_SHARPNESS
        f = max(0.0, 1.0 - m ** sharpness) ** 0.65
        if t < 0.5:
            w = self.transom_width * m * m
            f = f + (1.0 - f) * w
        return f

    def transom_z(self, t, v):
        if t >= 0.5:
            return 0.0
        m = 1.0 - t * 2.0
        return self.transom_rake * v * m * m * m

    @staticmethod
    def section_factor(v):
        return min(max(v, 0.0), 1.0) ** BILGE_FULLNESS

    @staticmethod
    def keel_y(t):
        m = abs(t * 2.0 - 1.0)
        return -DRAFT + ROCKER * m ** ROCKER_POWER

    @staticmethod
    def sheer_y(t):
        m = abs(t * 2.0 - 1.0)
        rise = BOW_SHEER_RISE if t > 0.5 else STERN_SHEER_RISE
        return FREEBOARD + rise * m ** SHEER_POWER

    @staticmethod
    def z_at(t):
        return LENGTH * 0.5 + (-LENGTH * 0.5 - LENGTH * 0.5) * t

    def v_at_height(self, t, y):
        keel = self.keel_y(t)
        sheer = self.sheer_y(t)
        if sheer - keel < 0.001:
            return 0.0
        return min(max((y - keel) / (sheer - keel), 0.0), 1.0)

    def shell(self, t, v, offset, side):
        y = self.keel_y(t) + (self.sheer_y(t) - self.keel_y(t)) * v
        half = max(0.02, BEAM * 0.5 * self.plan_factor(t) * self.section_factor(v) + offset)
        return (half * side, y, self.z_at(t) + self.transom_z(t, v))

    def ring(self, v, offset):
        count = max(3, STATIONS)
        points = [None] * (count * 2)
        for j in range(count):
            t = j / (count - 1)
            s = self.shell(t, v, offset, 1.0)
            points[j] = s
            points[count * 2 - 1 - j] = (-s[0], s[1], s[2])
        return points

    def ring_at_height(self, y, offset):
        count = max(3, STATIONS)
        points = [None] * (count * 2)
        for j in range(count):
            t = j / (count - 1)
            s = self.shell(t, self.v_at_height(t, y), offset, 1.0)
            points[j] = (s[0], y, s[2])
            points[count * 2 - 1 - j] = (-s[0], y, s[2])
        return points

    def strake_seams(self, strakes):
        seams = [0.0] * (strakes + 1)
        seams[strakes] = 1.0

        if not EVEN_PLANK_WIDTH:
            for i in range(1, strakes):
                seams[i] = i / strakes
            return seams

        samples = 256
        girth = [0.0] * (samples + 1)
        keel = self.keel_y(0.5)
        sheer = self.sheer_y(0.5)
        widest = BEAM * 0.5 * self.plan_factor(0.5)
        previous_width = 0.0
        previous_y = keel

        for i in range(1, samples + 1):
            v = i / samples
            width = widest * self.section_factor(v)
            y = keel + (sheer - keel) * v
            dw = width - previous_width
            dy = y - previous_y
            girth[i] = girth[i - 1] + math.sqrt(dw * dw + dy * dy)
            previous_width = width
            previous_y = y

        if girth[samples] < 0.0001:
            for i in range(1, strakes):
                seams[i] = i / strakes
            return seams

        cursor = 0
        for s in range(1, strakes):
            target = girth[samples] * s / strakes
            while cursor < samples - 1 and girth[cursor + 1] < target:
                cursor += 1
            span = girth[cursor + 1] - girth[cursor]
            fraction = (target - girth[cursor]) / span if span > 0.0001 else 0.0
            seams[s] = (cursor + fraction) / samples

        return seams


class Surface:
    def __init__(self):
        self.tris = []

    def tri(self, a, b, c):
        self.tris.append((a, b, c))

    def band(self, lower, upper):
        n = len(lower)
        for k in range(n):
            nxt = (k + 1) % n
            self.tri(lower[k], lower[nxt], upper[nxt])
            self.tri(lower[k], upper[nxt], upper[k])

    def band_inward(self, lower, upper):
        n = len(lower)
        for k in range(n):
            nxt = (k + 1) % n
            self.tri(lower[k], upper[nxt], lower[nxt])
            self.tri(lower[k], upper[k], upper[nxt])

    def cap(self, ring, face_up):
        half = len(ring) // 2
        for j in range(half - 1):
            s0 = ring[j]
            s1 = ring[j + 1]
            p0 = ring[len(ring) - 1 - j]
            p1 = ring[len(ring) - 2 - j]
            if face_up:
                self.tri(s0, p1, p0)
                self.tri(s0, s1, p1)
            else:
                self.tri(s0, p0, p1)
                self.tri(s0, p1, s1)


class Welder:
    def __init__(self):
        self.vertex_index = {}
        self.vertices = []
        self.normal_index = {}
        self.normals = []

    def vertex(self, p):
        key = (round(p[0], 5), round(p[1], 5), round(p[2], 5))
        i = self.vertex_index.get(key)
        if i is None:
            i = len(self.vertices)
            self.vertex_index[key] = i
            self.vertices.append(key)
        return i

    def normal(self, n):
        key = (round(n[0], 5), round(n[1], 5), round(n[2], 5))
        i = self.normal_index.get(key)
        if i is None:
            i = len(self.normals)
            self.normal_index[key] = i
            self.normals.append(key)
        return i

    def add(self, surface):
        faces = []
        for a, b, c in surface.tris:
            ia, ib, ic = self.vertex(a), self.vertex(b), self.vertex(c)
            if ia == ib or ib == ic or ia == ic:
                continue
            va, vb, vc = self.vertices[ia], self.vertices[ib], self.vertices[ic]
            ux, uy, uz = vb[0] - va[0], vb[1] - va[1], vb[2] - va[2]
            vx, vy, vz = vc[0] - va[0], vc[1] - va[1], vc[2] - va[2]
            nx = uy * vz - uz * vy
            ny = uz * vx - ux * vz
            nz = ux * vy - uy * vx
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            if length < 1e-12:
                continue
            n = self.normal((nx / length, ny / length, nz / length))
            faces.append((ia, ib, ic, n))
        return faces



SC_WALL = THICKNESS
SC_BULWARK = 0.8
SC_RAIL_H = 0.7
SC_RAIL_BAR = 0.1
SC_DOOR_HALF = 0.5
SC_DOOR_HEIGHT = 1.5
SC_POST = 0.09
SC_POST_GAP = 1.7
SC_STAIR_HALF = 0.6
SC_STAIR_RISE = 0.3
SC_STAIR_RUN = 0.36
SC_SAMPLES = 13

SC_TIERS = [
    dict(t_fwd=0.18, floor=DECK_HEIGHT, ceil=5.6, deck=5.8, stair_x=5.8),
    dict(t_fwd=0.09, floor=5.8, ceil=8.8, deck=9.0, stair_x=4.0),
]


def quad(surface, a, b, c, d):
    surface.tri(a, b, c)
    surface.tri(a, c, d)


def box(surface, lo, hi):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    p0, p1, p2, p3 = (x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)
    p4, p5, p6, p7 = (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)
    quad(surface, p4, p7, p6, p5)
    quad(surface, p0, p1, p2, p3)
    quad(surface, p1, p5, p6, p2)
    quad(surface, p0, p3, p7, p4)
    quad(surface, p0, p4, p5, p1)
    quad(surface, p3, p2, p6, p7)


def castle_stations(hull_shape, t_fwd):
    out = []
    for i in range(SC_SAMPLES):
        t = t_fwd * i / (SC_SAMPLES - 1)
        s = hull_shape.shell(t, 1.0, 0.0, 1.0)
        out.append((s[0], hull_shape.sheer_y(t), s[2]))
    return out


def inner_half(hw):
    return max(hw * 0.35, hw - SC_WALL)


def tier_walls(surface, stations, bases, top_y, closed=0):
    n = len(stations)
    for k in range(n - 1):
        ya, yb = min(bases[k], top_y), min(bases[k + 1], top_y)
        if ya >= top_y - 1e-4 and yb >= top_y - 1e-4:
            continue
        for side in (1.0, -1.0):
            xa, _, za = stations[k]
            xb, _, zb = stations[k + 1]
            oa, ob = xa * side, xb * side
            ia, ib = inner_half(xa) * side, inner_half(xb) * side
            o = [(oa, ya, za), (ob, yb, zb), (ob, top_y, zb), (oa, top_y, za)]
            m = [(ia, ya, za), (ib, yb, zb), (ib, top_y, zb), (ia, top_y, za)]
            if side > 0:
                quad(surface, o[0], o[1], o[2], o[3])
                quad(surface, m[3], m[2], m[1], m[0])
                quad(surface, o[3], o[2], m[2], m[3])
            else:
                quad(surface, o[3], o[2], o[1], o[0])
                quad(surface, m[0], m[1], m[2], m[3])
                quad(surface, m[3], m[2], o[2], o[3])

    ci = 0 if closed == 0 else n - 1
    oi = n - 1 if closed == 0 else 0

    xa, _, za = stations[ci]
    if xa > 0.1 and bases[ci] < top_y:
        lo, hi = (za - SC_WALL, za) if closed == 0 else (za, za + SC_WALL)
        box(surface, (-xa, bases[ci], lo), (xa, top_y, hi))

    xe, _, ze = stations[oi]
    if xe > SC_WALL and bases[oi] < top_y:
        zl, zh = (ze, ze + SC_WALL) if closed == 0 else (ze - SC_WALL, ze)
        box(surface, (inner_half(xe), bases[oi], zl), (xe, top_y, zh))
        box(surface, (-xe, bases[oi], zl), (-inner_half(xe), top_y, zh))


def tier_floor(surface, stations, ceil_y, deck_y):
    for k in range(len(stations) - 1):
        wa = inner_half(stations[k][0])
        wb = inner_half(stations[k + 1][0])
        za, zb = stations[k][2], stations[k + 1][2]
        quad(surface, (-wb, deck_y, zb), (-wa, deck_y, za), (wa, deck_y, za), (wb, deck_y, zb))
        quad(surface, (-wa, ceil_y, za), (-wb, ceil_y, zb), (wb, ceil_y, zb), (wa, ceil_y, za))


def castle_parts(hull_shape):
    tiers = []
    base_top = None
    for spec in SC_TIERS:
        stations = castle_stations(hull_shape, spec["t_fwd"])
        bases = [s[1] for s in stations] if base_top is None else [base_top] * len(stations)
        top = spec["deck"] + SC_BULWARK
        w = stations[-1][0] - SC_WALL
        z = stations[-1][2]
        floor_y, ceil_y, deck_y = spec["floor"], spec["ceil"], spec["deck"]
        door_top = floor_y + SC_DOOR_HEIGHT

        bulkhead = [
            ((-w, floor_y, z), (-SC_DOOR_HALF, ceil_y, z + SC_WALL)),
            ((SC_DOOR_HALF, floor_y, z), (w, ceil_y, z + SC_WALL)),
            ((-SC_DOOR_HALF, door_top, z), (SC_DOOR_HALF, ceil_y, z + SC_WALL)),
            ((-w, ceil_y, z), (w, deck_y, z + SC_WALL)),
        ]

        sx = spec["stair_x"]
        runs = [(-w, -(sx + SC_STAIR_HALF)), (-(sx - SC_STAIR_HALF), sx - SC_STAIR_HALF),
                (sx + SC_STAIR_HALF, w)]
        rail_y = deck_y + SC_RAIL_H
        rails = [((a, rail_y - SC_RAIL_BAR, z), (b, rail_y, z + SC_RAIL_BAR)) for a, b in runs]

        posts = []
        for a, b in runs:
            count = max(2, int((b - a) / SC_POST_GAP) + 1)
            for i in range(count):
                cx = a + (b - a - SC_POST) * (i / (count - 1)) + SC_POST * 0.5
                posts.append(((cx - SC_POST * 0.5, deck_y, z),
                              (cx + SC_POST * 0.5, rail_y - SC_RAIL_BAR, z + SC_POST)))

        steps = []
        count = max(1, int(round((deck_y - floor_y) / SC_STAIR_RISE)))
        rise = (deck_y - floor_y) / count
        for side in (-sx, sx):
            for i in range(count):
                z1 = z - (count - 1 - i) * SC_STAIR_RUN
                steps.append(((side - SC_STAIR_HALF, floor_y, z1 - SC_STAIR_RUN),
                              (side + SC_STAIR_HALF, floor_y + (i + 1) * rise, z1)))

        tiers.append(dict(stations=stations, bases=bases, top=top, deck=deck_y, ceil=ceil_y,
                          floor=floor_y, rail_y=rail_y, width=w, z=z, bulkhead=bulkhead,
                          rails=rails, posts=posts, steps=steps, stair_x=sx,
                          stair_steps=count, stair_run=count * SC_STAIR_RUN))
        base_top = top
    return tiers


def add_sterncastle(hull_shape, hull, deck, stairs):
    for tier in castle_parts(hull_shape):
        tier_walls(hull, tier["stations"], tier["bases"], tier["top"])
        tier_floor(deck, tier["stations"], tier["ceil"], tier["deck"])
        for lo, hi in tier["bulkhead"]:
            box(hull, lo, hi)
        for lo, hi in tier["rails"] + tier["posts"]:
            box(hull, lo, hi)
        for lo, hi in tier["steps"]:
            box(stairs, lo, hi)


MAST_SIDES = 12
MASTS = [
    dict(x=0.0, z=0.0, base=DECK_HEIGHT, top=34.7, r_base=0.55, r_top=0.28),
    dict(x=0.0, z=24.5, base=SC_TIERS[1]["deck"], top=25.0, r_base=0.35, r_top=0.18),
]


def mast_rings(spec, sides=MAST_SIDES):
    lower = []
    upper = []
    for i in range(sides):
        a = math.tau * i / sides
        c, s = math.cos(a), math.sin(a)
        lower.append((spec["x"] + spec["r_base"] * c, spec["base"], spec["z"] + spec["r_base"] * s))
        upper.append((spec["x"] + spec["r_top"] * c, spec["top"], spec["z"] + spec["r_top"] * s))
    return lower, upper


def add_masts(surface):
    for spec in MASTS:
        lower, upper = mast_rings(spec)
        n = len(lower)
        for i in range(n):
            j = (i + 1) % n
            quad(surface, lower[i], upper[i], upper[j], lower[j])
        for i in range(1, n - 1):
            surface.tri(upper[0], upper[i + 1], upper[i])
            surface.tri(lower[0], lower[i], lower[i + 1])



FC_T_AFT = 0.84
FC_CEIL = 5.1
FC_DECK = 5.3
FC_TOP = 6.1
FC_STAIR_X = 5.0


def fore_stations(hull_shape):
    out = []
    for i in range(SC_SAMPLES):
        t = FC_T_AFT + (1.0 - FC_T_AFT) * i / (SC_SAMPLES - 1)
        p = hull_shape.shell(t, 1.0, 0.0, 1.0)
        out.append((p[0], hull_shape.sheer_y(t), p[2]))
    return out


def forecastle_parts(hull_shape):
    stations = fore_stations(hull_shape)
    bases = [p[1] for p in stations]
    w = stations[0][0] - SC_WALL
    z = stations[0][2]
    floor_y = DECK_HEIGHT
    door_top = floor_y + SC_DOOR_HEIGHT

    bulkhead = [
        ((-w, floor_y, z - SC_WALL), (-SC_DOOR_HALF, FC_CEIL, z)),
        ((SC_DOOR_HALF, floor_y, z - SC_WALL), (w, FC_CEIL, z)),
        ((-SC_DOOR_HALF, door_top, z - SC_WALL), (SC_DOOR_HALF, FC_CEIL, z)),
        ((-w, FC_CEIL, z - SC_WALL), (w, FC_DECK, z)),
    ]

    runs = [(-w, -(FC_STAIR_X + SC_STAIR_HALF)),
            (-(FC_STAIR_X - SC_STAIR_HALF), FC_STAIR_X - SC_STAIR_HALF),
            (FC_STAIR_X + SC_STAIR_HALF, w)]
    rail_y = FC_DECK + SC_RAIL_H
    rails = [((a, rail_y - SC_RAIL_BAR, z - SC_RAIL_BAR), (b, rail_y, z)) for a, b in runs]

    posts = []
    for a, b in runs:
        count = max(2, int((b - a) / SC_POST_GAP) + 1)
        for i in range(count):
            cx = a + (b - a - SC_POST) * (i / (count - 1)) + SC_POST * 0.5
            posts.append(((cx - SC_POST * 0.5, FC_DECK, z - SC_POST),
                          (cx + SC_POST * 0.5, rail_y - SC_RAIL_BAR, z)))

    count = max(1, int(round((FC_DECK - floor_y) / SC_STAIR_RISE)))
    rise = (FC_DECK - floor_y) / count
    steps = []
    for side in (-FC_STAIR_X, FC_STAIR_X):
        for i in range(count):
            z1 = z + (count - 1 - i) * SC_STAIR_RUN
            steps.append(((side - SC_STAIR_HALF, floor_y, z1),
                          (side + SC_STAIR_HALF, floor_y + (i + 1) * rise, z1 + SC_STAIR_RUN)))

    return dict(stations=stations, bases=bases, top=FC_TOP, deck=FC_DECK, ceil=FC_CEIL,
                floor=floor_y, rail_y=rail_y, width=w, z=z, bulkhead=bulkhead,
                rails=rails, posts=posts, steps=steps, stair_x=FC_STAIR_X,
                stair_steps=count, stair_run=count * SC_STAIR_RUN)


def add_forecastle(hull_shape, hull, deck, stairs):
    f = forecastle_parts(hull_shape)
    tier_walls(hull, f["stations"], f["bases"], f["top"], closed=1)
    tier_floor(deck, f["stations"], f["ceil"], f["deck"])
    for lo, hi in f["bulkhead"]:
        box(hull, lo, hi)
    for lo, hi in f["rails"] + f["posts"]:
        box(hull, lo, hi)
    for lo, hi in f["steps"]:
        box(stairs, lo, hi)


def build(transom_width, transom_rake):
    hull_shape = Hull(transom_width, transom_rake)
    strakes = max(1, STRAKE_COUNT)
    seams = hull_shape.strake_seams(strakes)

    hull = Surface()
    previous = hull_shape.ring(0.0, STRAKE_PROUD)
    hull.cap(previous, False)

    for i in range(strakes):
        top = seams[i + 1]
        plank_top = hull_shape.ring(top, 0.0)
        hull.band(previous, plank_top)
        if i < strakes - 1:
            next_bottom = hull_shape.ring(top, STRAKE_PROUD)
            hull.band(plank_top, next_bottom)
            previous = next_bottom
        else:
            previous = plank_top

    sheer_inner = hull_shape.ring(1.0, -THICKNESS)
    hull.band(previous, sheer_inner)

    deck_edge = hull_shape.ring_at_height(DECK_HEIGHT, -THICKNESS)
    hull.band_inward(deck_edge, sheer_inner)

    deck = Surface()
    deck.cap(deck_edge, True)

    stairs = Surface()
    spars = Surface()

    add_sterncastle(hull_shape, hull, deck, stairs)
    add_forecastle(hull_shape, hull, deck, stairs)
    add_masts(spars)

    return hull, deck, stairs, spars


def write_obj(path, surfaces):
    welder = Welder()
    groups = [(material, welder.add(surface)) for material, surface in surfaces]

    lines = ["mtllib hull.mtl", "o Hull"]
    for v in welder.vertices:
        lines.append("v %.5f %.5f %.5f" % v)
    for n in welder.normals:
        lines.append("vn %.5f %.5f %.5f" % n)
    for material, faces in groups:
        lines.append("usemtl " + material)
        for a, b, c, n in faces:
            lines.append(
                "f %d//%d %d//%d %d//%d" % (a + 1, n + 1, b + 1, n + 1, c + 1, n + 1)
            )

    with open(path, "w", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--transom-width", type=float, default=0.55)
    parser.add_argument("--transom-rake", type=float, default=1.75)
    parser.add_argument("--out", default=None)
    args = parser.parse_args()

    out = args.out or os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "models", "hull.obj"
    )
    hull, deck, stairs, spars = build(args.transom_width, args.transom_rake)
    write_obj(out, [("hull", hull), ("deck", deck), ("stair", stairs), ("spar", spars)])
    print("wrote", out)


if __name__ == "__main__":
    main()
