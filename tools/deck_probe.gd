extends Node3D

var _frames := 0
var _boat: RigidBody3D

func _ready() -> void:
	var packed: PackedScene = load("res://boat_toon.tscn")
	_boat = packed.instantiate()
	_boat.position = Vector3(0, 31, 0)
	add_child(_boat)

func _physics_process(_delta: float) -> void:
	_frames += 1
	if _frames < 240:
		return
	set_physics_process(false)
	var space := get_world_3d().direct_space_state
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.21
	capsule.height = 1.19
	print("deckprobe boat y=%.2f roll=%.1f pitch=%.1f" % [_boat.global_position.y, rad_to_deg(_boat.rotation.x), rad_to_deg(_boat.rotation.z)])
	var spots := {
		"helm_stand": Vector3(0.0, 2.05, 4.85),
		"helm_side": Vector3(0.9, 2.05, 4.3),
		"quarter_mid": Vector3(0.0, 2.05, 3.2),
		"ramp_toe": Vector3(0.0, 1.25, 1.1),
		"ramp_mid": Vector3(0.0, 1.65, 2.6),
		"main_mid": Vector3(0.0, 1.25, 0.0),
		"main_side": Vector3(1.5, 1.25, 0.0),
		"gangway": Vector3(-1.85, 1.25, -0.56),
		"gangway_out": Vector3(-2.35, 1.25, -0.56),
		"bow_deck": Vector3(0.0, 1.25, -3.2),
		"cabin_side": Vector3(1.18, 1.25, -3.0),
		"cabin_side2": Vector3(-1.18, 1.25, -4.2),
		"fore_deck": Vector3(0.0, 1.25, -5.0),
		"fore_tip": Vector3(0.0, 1.25, -6.2),
	}
	for name in spots:
		var local: Vector3 = spots[name]
		var world: Vector3 = _boat.global_transform * local
		var from := world + Vector3(0, 3.0, 0)
		var ray := PhysicsRayQueryParameters3D.create(from, world + Vector3(0, -3.0, 0))
		var hit := space.intersect_ray(ray)
		var floor_local := "none"
		var shape_name := "-"
		if hit:
			floor_local = "%.2f" % (_boat.to_local(hit.position).y)
			var owner_id: int = hit.collider.shape_find_owner(hit.shape) if hit.collider.has_method("shape_find_owner") else -1
			shape_name = str(hit.collider.shape_owner_get_owner(owner_id).name) if owner_id >= 0 else str(hit.collider.name)
		var stand := PhysicsShapeQueryParameters3D.new()
		stand.shape = capsule
		stand.transform = Transform3D(_boat.global_basis, _boat.global_transform * (local + Vector3(0, 0.62, 0)))
		var overlaps := space.intersect_shape(stand, 8)
		var names := []
		for o in overlaps:
			var oid: int = o.collider.shape_find_owner(o.shape) if o.collider.has_method("shape_find_owner") else -1
			names.append(str(o.collider.shape_owner_get_owner(oid).name) if oid >= 0 else str(o.collider.name))
		print("deckprobe %-12s floor=%s via %s  capsule overlaps: %s" % [name, floor_local, shape_name, ", ".join(names) if names.size() > 0 else "clear"])
	get_tree().quit()
