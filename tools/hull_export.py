import argparse
import math
import os

LENGTH = 17.0
BEAM = 6.2
DRAFT = 2.1
FREEBOARD = 2.3
DECK_HEIGHT = 1.35
BOW_SHEER_RISE = 1.6
STERN_SHEER_RISE = 1.2
SHEER_POWER = 2.4
ROCKER = 1.75
ROCKER_POWER = 2.6
BOW_SHARPNESS = 2.2
STERN_SHARPNESS = 3.0
BILGE_FULLNESS = 0.42
THICKNESS = 0.16
STRAKE_PROUD = 0.15
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

    return hull, deck


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
    parser.add_argument("--transom-rake", type=float, default=0.6)
    parser.add_argument("--out", default=None)
    args = parser.parse_args()

    out = args.out or os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "models", "hull.obj"
    )
    hull, deck = build(args.transom_width, args.transom_rake)
    write_obj(out, [("hull", hull), ("deck", deck)])
    print("wrote", out)


if __name__ == "__main__":
    main()
