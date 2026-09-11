extends SceneTree

const PX := 4096
const SPACING := 4.0
const ORIGIN := -8192.0
const SEA := 30.0

const DOWNWIND := Vector2(0.81915, 0.57358)
const UPWIND := Vector2(-0.81915, -0.57358)

const GYM := Vector2(-1200.0, -600.0)
const GYM_EDGE := 640.0
const CLIFF_DIR := Vector2(0.70711, -0.70711)
const TRENCH_C := Vector2(-3200.0, -2200.0)
const COAST_C := Vector2(1800.0, 700.0)
const COAST_LEN := 1600.0
const COAST_WID := 240.0

var base_noise := FastNoiseLite.new()
var canyon_noise := FastNoiseLite.new()
var crevice_noise := FastNoiseLite.new()
var detail_noise := FastNoiseLite.new()
var ridge_noise := FastNoiseLite.new()
var edge_noise := FastNoiseLite.new()

var cove_m: Vector2
var cove_axis: Vector2
var cove_len := 560.0
var islands: Array = []
var pads: Array = []

func _initialize() -> void:
	_setup()
	var preview := false
	for arg in OS.get_cmdline_user_args():
		if arg == "--preview":
			preview = true
	if preview:
		_preview()
		_samples(null)
		quit()
		return
	_bake()

func _setup() -> void:
	base_noise.seed = 71
	base_noise.frequency = 0.0009
	base_noise.fractal_octaves = 3
	canyon_noise.seed = 72
	canyon_noise.frequency = 0.0035
	canyon_noise.fractal_type = FastNoiseLite.FRACTAL_RIDGED
	crevice_noise.seed = 73
	crevice_noise.frequency = 0.011
	crevice_noise.fractal_type = FastNoiseLite.FRACTAL_RIDGED
	detail_noise.seed = 74
	detail_noise.frequency = 0.02
	ridge_noise.seed = 75
	ridge_noise.frequency = 0.006
	ridge_noise.fractal_type = FastNoiseLite.FRACTAL_RIDGED
	edge_noise.seed = 76
	edge_noise.frequency = 0.0016

	cove_m = GYM + UPWIND * 860.0
	cove_axis = DOWNWIND

	islands = [
		{ pos = Vector2(-4800.0, -4300.0), edge = 420.0, peak = 20.0 },
		{ pos = Vector2(4300.0, -2800.0), edge = 380.0, peak = 16.0 },
		{ pos = Vector2(-3800.0, 3400.0), edge = 450.0, peak = 22.0 },
		{ pos = Vector2(4700.0, 4300.0), edge = 400.0, peak = 18.0 },
	]

	pads = [GYM + UPWIND * 310.0 + Vector2(0.57358, -0.81915) * 100.0]
	pads.append(COAST_C + DOWNWIND * 1590.0)
	for isl in islands:
		pads.append(isl.pos + DOWNWIND * (isl.edge - 10.0))

func _shelf(d: float, h0: float, h1: float, sw: float, dw: float) -> float:
	if d < sw:
		return lerpf(h0, h1, clampf(d / sw, 0.0, 1.0))
	var t: float = clampf((d - sw) / dw, 0.0, 1.0)
	return lerpf(h1, -25.0, t * t * (3.0 - 2.0 * t))

func _height(x: float, z: float) -> float:
	var p := Vector2(x, z)
	var h := 9.0 + 11.0 * base_noise.get_noise_2d(x, z)
	var c := canyon_noise.get_noise_2d(x, z)
	if c > 0.45:
		h -= (c - 0.45) / 0.55 * 46.0
	var cr := crevice_noise.get_noise_2d(x, z)
	if cr > 0.62:
		h -= (cr - 0.62) / 0.38 * 13.0
	h += detail_noise.get_noise_2d(x, z) * 2.5

	var tv := _ellipse_t(p, TRENCH_C, UPWIND, 1700.0, 800.0)
	if tv < 1.0:
		h = lerpf(-45.0 + ridge_noise.get_noise_2d(x, z) * 3.0, h, smoothstep(0.5, 1.0, tv))

	h = max(h, _gym(x, z))
	h = max(h, _coast(x, z))
	for isl in islands:
		h = max(h, _island(p, x, z, isl))
	h = max(h, _banks(p, x, z))

	for pad in pads:
		var dp: float = p.distance_to(pad)
		if dp < 70.0:
			var pull := (1.0 - smoothstep(24.0, 70.0, dp)) * smoothstep(27.8, 29.2, h)
			h = lerpf(h, 30.9, pull)
	return h

func _ellipse_t(p: Vector2, centre: Vector2, axis: Vector2, half_len: float, half_wid: float) -> float:
	var q := p - centre
	var a := q.dot(axis)
	var b := q.dot(Vector2(-axis.y, axis.x))
	return sqrt(pow(a / half_len, 2.0) + pow(b / half_wid, 2.0))

func _gym(x: float, z: float) -> float:
	var p := Vector2(x, z)
	var d := p.distance_to(GYM)
	if d > 1600.0:
		return -1e6
	var rn := ridge_noise.get_noise_2d(x, z)
	var edge := GYM_EDGE + 50.0 * edge_noise.get_noise_2d(x * 0.7, z * 0.7)
	var in_cliff := d > 1.0 and (p - GYM).normalized().dot(CLIFF_DIR) > 0.82
	var h: float
	if d < edge:
		var u := 1.0 - d / edge
		var lift := smoothstep(0.0, 12.0 / edge, u) if in_cliff else pow(u, 0.8)
		h = 30.0 + 26.0 * lift + rn * 2.0
	elif in_cliff:
		h = _shelf(d - edge, 5.0, 2.0, 60.0, 260.0) + rn
	else:
		h = _shelf(d - edge, 26.0, 12.0, 380.0, 260.0) + rn
	if d >= edge:
		h -= 60.0 * smoothstep(1350.0, 1600.0, d)

	var t := clampf((p - cove_m).dot(cove_axis) / cove_len, 0.0, 1.0)
	var seg := cove_m + cove_axis * (cove_len * t)
	var dc := p.distance_to(seg)
	var cove_r := lerpf(150.0, 100.0, t)
	var wall_in := cove_r * lerpf(0.55, 0.85, t)
	var wall_out := cove_r * lerpf(1.6, 1.15, t)
	if dc < wall_out:
		var floor_h := 26.5 + detail_noise.get_noise_2d(x, z) * 0.4
		h = lerpf(floor_h, h, smoothstep(wall_in, wall_out, dc))
	return h

func _coast(x: float, z: float) -> float:
	var p := Vector2(x, z)
	var e := _ellipse_t(p, COAST_C, DOWNWIND, COAST_LEN, COAST_WID)
	if e > 4.0:
		return -1e6
	var rn := ridge_noise.get_noise_2d(x, z)
	if e < 1.0:
		return 30.0 + 26.0 * pow(maxf(1.0 - e * e, 0.0), 0.8) + rn * 2.5
	return _shelf((e - 1.0) * COAST_WID, 26.0, 12.0, 300.0, 240.0) + rn - 60.0 * smoothstep(3.3, 4.0, e)

func _island(p: Vector2, x: float, z: float, isl: Dictionary) -> float:
	var d: float = p.distance_to(isl.pos)
	var edge: float = isl.edge
	if d > edge + 620.0:
		return -1e6
	var rn := ridge_noise.get_noise_2d(x, z)
	if d < edge:
		return 30.0 + isl.peak * pow(1.0 - d / edge, 0.8) + rn * 1.5
	return _shelf(d - edge, 26.0, 12.0, 300.0, 220.0) + rn - 60.0 * smoothstep(edge + 530.0, edge + 620.0, d)

func _banks(p: Vector2, x: float, z: float) -> float:
	var best := -1e6
	var banks := [
		{ c = Vector2(300.0, 1500.0), axis = DOWNWIND, hl = 600.0, hw = 300.0 },
		{ c = Vector2(2600.0, -1200.0), axis = Vector2(1.0, 0.0), hl = 400.0, hw = 400.0 },
		{ c = Vector2(-2000.0, 2600.0), axis = Vector2(0.0, 1.0), hl = 500.0, hw = 450.0 },
	]
	for bk in banks:
		var t: float = _ellipse_t(p, bk.c, bk.axis, bk.hl, bk.hw)
		if t < 1.6:
			best = maxf(best, 27.0 - 20.0 * smoothstep(0.55, 1.3, t) - 80.0 * smoothstep(1.3, 1.6, t) + detail_noise.get_noise_2d(x, z) * 0.6)
	return best

func _bake() -> void:
	var t0 := Time.get_ticks_msec()
	DirAccess.make_dir_recursive_absolute("res://playground_data")
	var dir := DirAccess.open("res://playground_data")
	for f in dir.get_files():
		dir.remove(f)
	var packed: PackedScene = load("res://playground_map.tscn")
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
		if py % 512 == 0:
			print("row %d / %d" % [py, PX])
	var imgs: Array[Image] = []
	imgs.resize(Terrain3DRegion.TYPE_MAX)
	imgs[Terrain3DRegion.TYPE_HEIGHT] = img
	terrain.data.import_images(imgs, Vector3(ORIGIN, 0.0, ORIGIN), 0.0, 1.0)
	terrain.data.calc_height_range(true)
	terrain.data.save_directory("res://playground_data")
	print("regions: %d" % terrain.data.get_region_count())
	print("elapsed: %.1f s" % ((Time.get_ticks_msec() - t0) / 1000.0))
	_samples(terrain)
	quit()

func _preview() -> void:
	var t0 := Time.get_ticks_msec()
	var size := 1024
	var step := (PX * SPACING) / size
	var img := Image.create_empty(size, size, false, Image.FORMAT_RGB8)
	for py in size:
		var z := ORIGIN + py * step
		for px in size:
			img.set_pixel(px, py, _chart_color(_height(ORIGIN + px * step, z)))
	img.save_png("res://playground_preview.png")
	print("preview written in %.1f s" % ((Time.get_ticks_msec() - t0) / 1000.0))

func _chart_color(h: float) -> Color:
	var depth := SEA - h
	if depth > 55.0:
		return Color(0.03, 0.07, 0.25).lerp(Color(0.0, 0.02, 0.12), clampf((depth - 55.0) / 40.0, 0.0, 1.0))
	if depth > 25.0:
		return Color(0.08, 0.2, 0.5).lerp(Color(0.03, 0.07, 0.25), (depth - 25.0) / 30.0)
	if depth > 5.0:
		return Color(0.2, 0.55, 0.75).lerp(Color(0.08, 0.2, 0.5), (depth - 5.0) / 20.0)
	if depth > 0.0:
		return Color(0.45, 0.85, 0.85).lerp(Color(0.2, 0.55, 0.75), depth / 5.0)
	var up := h - SEA
	if up < 1.0:
		return Color(0.87, 0.8, 0.55)
	if up < 12.0:
		return Color(0.35, 0.6, 0.3).lerp(Color(0.5, 0.45, 0.3), (up - 1.0) / 11.0)
	return Color(0.5, 0.45, 0.3).lerp(Color(0.95, 0.95, 0.95), clampf((up - 12.0) / 20.0, 0.0, 1.0))

func _samples(terrain: Terrain3D) -> void:
	var rows := [
		["gym peak", GYM],
		["gym cove basin", GYM + UPWIND * 310.0],
		["gym cove mouth", cove_m],
		["gym pad", pads[0]],
		["gym berth", pads[0] + Vector2(-0.57358, 0.81915) * 20.0],
		["coast berth", pads[1] + DOWNWIND * 20.0],
		["isle NW berth", pads[2] + DOWNWIND * 20.0],
		["gym cliff rim", GYM + CLIFF_DIR * (GYM_EDGE - 30.0)],
		["gym cliff base", GYM + CLIFF_DIR * (GYM_EDGE + 90.0)],
		["gym beach SE", GYM + DOWNWIND * (GYM_EDGE + 150.0)],
		["trench centre", TRENCH_C],
		["coast ridge", COAST_C],
		["coast pad", pads[1]],
		["isle NW peak", islands[0].pos],
		["isle NW pad", pads[2]],
		["isle NE peak", islands[1].pos],
		["isle SW peak", islands[2].pos],
		["isle SE peak", islands[3].pos],
		["bank A", Vector2(300.0, 1500.0)],
		["bank B", Vector2(2600.0, -1200.0)],
		["bank C", Vector2(-2000.0, 2600.0)],
		["open sea E", Vector2(6000.0, 0.0)],
		["open sea S", Vector2(0.0, 6000.0)],
		["open sea NW", Vector2(-6000.0, -6000.0)],
		["corner SE", Vector2(7800.0, 7800.0)],
	]
	for row in rows:
		var p: Vector2 = row[1]
		var h: float
		if terrain != null:
			h = terrain.data.get_height(Vector3(p.x, 0.0, p.y))
		else:
			h = _height(p.x, p.y)
		print("%-16s (%6.0f,%6.0f)  h=%7.1f  depth=%6.1f" % [row[0], p.x, p.y, h, SEA - h])
