extends Node3D

var _scene_path := "res://playground.tscn"
var _out := "user://shots/shot"
var _shots: Array = []
var _wait := 20
var _time_of_day := 0.5
var _half := true
var _sail := 0.0
var _world: Node

func _ready() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--scene="):
			_scene_path = arg.substr(8)
		elif arg.begins_with("--out="):
			_out = arg.substr(6)
		elif arg.begins_with("--shots="):
			_parse_shots(arg.substr(8))
		elif arg.begins_with("--set="):
			_shots = _preset(arg.substr(6))
		elif arg.begins_with("--wait="):
			_wait = int(arg.substr(7))
		elif arg.begins_with("--tod="):
			_time_of_day = float(arg.substr(6))
		elif arg == "--full":
			_half = false
		elif arg.begins_with("--sail="):
			_sail = float(arg.substr(7))
	if _shots.is_empty():
		_shots = _preset("before")
	_run()

func _parse_shots(spec: String) -> void:
	for item in spec.split(";", false):
		var parts := item.split("@")
		if parts.size() < 3:
			continue
		_shots.append({ name = parts[0], eye = parts[1], at = parts[2] })

func _preset(name: String) -> Array:
	match name:
		"before", "dock":
			return [
				{ name = "dock_boat", eye = "-1393,33,-866", at = "-1392,31.5,-846" },
				{ name = "dock_low", eye = "-1386,31.6,-850", at = "-1391,31.6,-843" },
				{ name = "deck_aft", eye = "boat:0,3.4,5.8", at = "boat:0,1.6,-7" },
				{ name = "deck_helm", eye = "boat:0.7,3.25,5.3", at = "boat:0,2.6,3" },
				{ name = "deck_bow", eye = "boat:0,2.6,-1", at = "boat:0,2.2,-9" },
				{ name = "boat_quarter", eye = "boat:-15,6,15", at = "boat:0,1.5,0" },
				{ name = "boat_side", eye = "boat:19,3.5,0", at = "boat:0,1.5,0" },
				{ name = "boat_bow", eye = "boat:12,4,-14", at = "boat:0,1.5,0" },
			]
		"sea":
			return [
				{ name = "sea_low", eye = "500,31.5,-2000", at = "1500,31,-2600" },
				{ name = "sea_deck", eye = "500,33,-2000", at = "900,31,-2300" },
				{ name = "sea_high", eye = "500,60,-2000", at = "1000,31,-2400" },
				{ name = "trench_low", eye = "-3200,32,-2200", at = "-3900,31,-2700" },
			]
		"world":
			return [
				{ name = "island_far", eye = "-100,40,500", at = "-1200,50,-600" },
				{ name = "island_near", eye = "-700,36,-100", at = "-1200,60,-600" },
				{ name = "cove", eye = "-1700,45,-1350", at = "-1450,31,-950" },
				{ name = "coast", eye = "2300,40,1500", at = "1800,45,700" },
			]
	return []

func _vec(spec: String) -> Vector3:
	var local := spec.begins_with("boat:")
	var body := spec.substr(5) if local else spec
	var n := body.split(",")
	var v := Vector3(float(n[0]), float(n[1]), float(n[2]))
	if local:
		var boat := _find_boat()
		if boat != null:
			return boat.global_transform * v
	return v

func _find_boat() -> Node3D:
	if _world == null:
		return null
	for child in _world.get_children():
		if child is RigidBody3D and (child.name.begins_with("Boat") or child.name.begins_with("boat")):
			return child
	return null

func _run() -> void:
	var packed: PackedScene = load(_scene_path)
	_world = packed.instantiate()
	add_child(_world)
	for child in _world.get_children():
		if child is CanvasLayer:
			child.visible = false
	var noon = load("res://scripts/debug/DebugNoon.cs").new()
	noon.TimeOfDay = _time_of_day
	add_child(noon)
	var cam := Camera3D.new()
	cam.far = 12000.0
	cam.near = 0.1
	cam.fov = 70.0
	add_child(cam)
	cam.make_current()
	DirAccess.make_dir_recursive_absolute(_out.get_base_dir())
	for i in _wait:
		await get_tree().process_frame
	var boat := _find_boat()
	if boat != null:
		print("boat origin %s" % boat.global_position)
		if _sail > 0.0:
			var sail := boat.get_node_or_null("BoomPivot/Sail")
			if sail != null:
				sail.set("Deployment", _sail)
				sail.set("TargetDeployment", _sail)
				print("sail set to %s -> %s" % [_sail, sail.get("Deployment")])
		for path in ["Mast", "BoomPivot", "BoomPivot/Boom", "BoomPivot/Sail", "Helm", "Rudder", "Model"]:
			var n := boat.get_node_or_null(path)
			if n != null:
				print("  %s at %s" % [path, n.global_position])
	for shot in _shots:
		var eye := _vec(shot.eye)
		var at := _vec(shot.at)
		cam.global_position = eye
		cam.look_at(at, Vector3.UP)
		for i in _wait:
			await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var img := get_viewport().get_texture().get_image()
		if _half:
			img.resize(img.get_width() / 2, img.get_height() / 2, Image.INTERPOLATE_LANCZOS)
		var path := "%s_%s.png" % [_out, shot.name]
		img.save_png(path)
		print("shot %s -> %s (eye %s)" % [shot.name, path, eye])
	print("shots done")
	get_tree().quit()
