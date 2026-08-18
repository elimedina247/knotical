extends SceneTree

const PX := 2048
const SPACING := 2.0
const ORIGIN := -2048.0
const SEA := 30.0

const HS_R := 210.0
const HS_W := 65.0
const MT_C := Vector2(1300.0, -1300.0)
const MT_L := 250.0
const AR_C := Vector2(-1300.0, -1300.0)
const BE_C := Vector2(-1300.0, 1300.0)
const BE_A := 300.0
const CH_C := Vector2(1300.0, 1300.0)
const CH_AXIS := Vector2(0.70710678, -0.70710678)

var base_noise := FastNoiseLite.new()
var ridge_noise := FastNoiseLite.new()
var bank_noise := FastNoiseLite.new()
var detail_noise := FastNoiseLite.new()
var islets: Array = []
var chain: Array = []

func _initialize() -> void:
	var t0 := Time.get_ticks_msec()
	_setup()
	DirAccess.make_dir_recursive_absolute("res://map2_data")
	var dir := DirAccess.open("res://map2_data")
	for f in dir.get_files():
		dir.remove(f)
	var packed: PackedScene = load("res://map2.tscn")
	var map: Node = packed.instantiate()
	root.add_child(map)
	var terrain: Terrain3D = map.get_node("Terrain3D")
	await process_frame
	await process_frame
	var img := Image.create_empty(PX, PX, false, Image.FORMAT_RF)
	for py in PX:
		var z := ORIGIN + py * SPACING
		for px in PX:
			img.set_pixel(px, py, Color(_height(ORIGIN + px * SPACING, z), 0.0, 0.0, 1.0))
		if py % 256 == 0:
			print("row %d / %d" % [py, PX])
	var imgs: Array[Image] = []
	imgs.resize(Terrain3DRegion.TYPE_MAX)
	imgs[Terrain3DRegion.TYPE_HEIGHT] = img
	terrain.data.import_images(imgs, Vector3(ORIGIN, 0.0, ORIGIN), 0.0, 1.0)
	terrain.data.calc_height_range(true)
	terrain.data.save_directory("res://map2_data")
	print("regions: %d" % terrain.data.get_region_count())
	print("elapsed: %.1f s" % ((Time.get_ticks_msec() - t0) / 1000.0))
	_verify(terrain)
	quit()

func _setup() -> void:
	base_noise.seed = 101
	base_noise.frequency = 0.0016
	base_noise.fractal_octaves = 3
	ridge_noise.seed = 202
	ridge_noise.frequency = 0.006
	ridge_noise.fractal_type = FastNoiseLite.FRACTAL_RIDGED
	bank_noise.seed = 303
	bank_noise.frequency = 0.01
	detail_noise.seed = 404
	detail_noise.frequency = 0.02
	var rng := RandomNumberGenerator.new()
	rng.seed = 20260817
	for i in 13:
		var ang := rng.randf() * TAU
		var rad := sqrt(rng.randf()) * 500.0
		islets.append({
			pos = AR_C + Vector2.from_angle(ang) * rad,
			radius = rng.randf_range(35.0, 90.0),
			peak = rng.randf_range(4.0, 12.0),
		})
	for i in 5:
		chain.append({
			off = CH_AXIS * (-620.0 + 310.0 * i) + Vector2(-CH_AXIS.y, CH_AXIS.x) * rng.randf_range(-40.0, 40.0),
			len = rng.randf_range(70.0, 110.0),
			wid = rng.randf_range(16.0, 28.0),
			peak = rng.randf_range(75.0, 120.0),
		})

func _height(x: float, z: float) -> float:
	var h := -8.0 + 14.0 * base_noise.get_noise_2d(x, z)
	var dmt := Vector2(x, z).distance_to(MT_C)
	var basin := 1.0 - smoothstep(560.0, 1000.0, dmt)
	h = lerp(h, -75.0, basin)
	h = max(h, _horseshoe(x, z))
	h = max(h, _mountain(x, z, dmt))
	h = max(h, _archipelago(x, z))
	h = max(h, _beach(x, z))
	h = max(h, _chain(x, z))
	return h

func _shelf(d: float, h0: float, h1: float, sw: float, dw: float) -> float:
	if d < sw:
		return lerp(h0, h1, clamp(d / sw, 0.0, 1.0))
	var t: float = clamp((d - sw) / dw, 0.0, 1.0)
	return lerp(h1, -25.0, t * t * (3.0 - 2.0 * t))

func _horseshoe(x: float, z: float) -> float:
	var r := Vector2(x, z).length()
	if r > 720.0:
		return -1e6
	var rn := ridge_noise.get_noise_2d(x, z)
	if r < HS_R - HS_W:
		return 27.2 + rn * 0.6
	var gap := 0.0
	if r > 1.0:
		var a := rad_to_deg(acos(clamp(z / r, -1.0, 1.0)))
		gap = 1.0 - smoothstep(40.0, 65.0, a)
	if r < HS_R + HS_W:
		var u := (r - HS_R) / HS_W
		var bump := pow(maxf(0.5 + 0.5 * cos(u * PI), 0.0), 1.2)
		var land := 28.0 + 16.0 * bump + rn * 3.0
		var channel := 24.0 - 3.0 * bump + rn * 0.8
		return lerp(land, channel, gap)
	return _shelf(r - (HS_R + HS_W), 26.0, 14.0, 260.0, 120.0) + rn

func _mountain(x: float, z: float, r: float) -> float:
	if r > 700.0:
		return -1e6
	var rn := ridge_noise.get_noise_2d(x * 2.0, z * 2.0)
	if r < MT_L:
		var u := r / MT_L
		return 30.0 + 165.0 * pow(maxf(1.0 - u, 0.0), 0.55) + rn * 12.0 * (1.0 - u * 0.5)
	return 30.0 - (r - MT_L) * 0.9 + rn * 4.0

func _archipelago(x: float, z: float) -> float:
	var p := Vector2(x, z)
	var d := p.distance_to(AR_C)
	if d > 900.0:
		return -1e6
	var bank := 24.0 + bank_noise.get_noise_2d(x, z) * 3.5
	var edge := 1.0 - smoothstep(560.0, 760.0, d)
	var h: float = lerp(-30.0, bank, edge)
	for isl in islets:
		var q: float = p.distance_to(isl.pos) / isl.radius
		if q < 1.0:
			h = maxf(h, 30.0 + isl.peak * pow(maxf(1.0 - q * q, 0.0), 1.5))
	return h

func _beach(x: float, z: float) -> float:
	var r := Vector2(x, z).distance_to(BE_C)
	if r > 850.0:
		return -1e6
	var dn := detail_noise.get_noise_2d(x, z)
	if r < BE_A:
		var u := r / BE_A
		return 30.0 + 16.0 * pow(maxf(1.0 - u * u, 0.0), 1.4) + dn
	var d := r - BE_A
	if d < 550.0:
		return lerp(30.0, 14.0, d / 550.0) + dn * 1.5
	var t: float = clamp((d - 550.0) / 150.0, 0.0, 1.0)
	return lerp(14.0, -25.0, t * t * (3.0 - 2.0 * t)) + dn * 1.5

func _chain(x: float, z: float) -> float:
	var p := Vector2(x, z) - CH_C
	if p.length() > 950.0:
		return -1e6
	var perp := Vector2(-CH_AXIS.y, CH_AXIS.x)
	var h := -1e6
	var dmin := 1e9
	for isl in chain:
		var q: Vector2 = p - isl.off
		var a: float = q.dot(CH_AXIS)
		var b: float = q.dot(perp)
		var e := sqrt(pow(a / isl.len, 2.0) + pow(b / isl.wid, 2.0))
		dmin = minf(dmin, (e - 1.0) * isl.wid)
		if e < 1.0:
			h = maxf(h, 30.0 + isl.peak * pow(maxf(1.0 - e * e, 0.0), 0.8))
	if dmin > 0.0:
		h = maxf(h, _shelf(dmin, 26.0, 12.0, 200.0, 110.0))
	return h

func _verify(terrain: Terrain3D) -> void:
	var samples := [
		["lagoon centre", 0, 0],
		["lagoon mid", 0, 80],
		["horseshoe ridge N", 0, -210],
		["entrance channel", 0, 210],
		["horseshoe shelf N", 0, -450],
		["open sea W", -650, 0],
		["open sea mid-NE", 650, -650],
		["NE peak", 1300, -1300],
		["NE deep water", 1300, -800],
		["NW bank", -1300, -1300],
		["NW outside bank", -1300, -450],
		["SW beach peak", -1300, 1300],
		["SW beach shelf", -1300, 850],
		["SE chain centre", 1300, 1300],
		["SE chain shelf", 1450, 1150],
		["map corner SW", -2000, 2000],
	]
	for s in samples:
		var h: float = terrain.data.get_height(Vector3(s[1], 0.0, s[2]))
		print("%-20s h=%7.1f  depth=%6.1f" % [s[0], h, SEA - h])
