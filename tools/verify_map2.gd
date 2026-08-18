extends SceneTree

func _initialize() -> void:
	var packed: PackedScene = load("res://map2.tscn")
	var map: Node = packed.instantiate()
	root.add_child(map)
	var terrain: Terrain3D = map.get_node("Terrain3D")
	await process_frame
	await process_frame
	print("regions loaded: %d" % terrain.data.get_region_count())
	print("region size: %d  vertex spacing: %.1f" % [terrain.region_size, terrain.vertex_spacing])
	var samples := [
		["lagoon centre", 0, 0],
		["entrance channel", 0, 210],
		["NE peak", 1300, -1300],
		["NE deep water", 1300, -800],
		["NW bank clear", -1500, -1100],
		["SW beach shelf", -1300, 850],
		["SE chain shelf", 1450, 1150],
		["map corner SW", -2000, 2000],
	]
	for s in samples:
		var h: float = terrain.data.get_height(Vector3(s[1], 0.0, s[2]))
		print("%-18s h=%7.1f  depth=%6.1f" % [s[0], h, 30.0 - h])
	quit()
