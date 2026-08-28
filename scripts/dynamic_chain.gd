@tool
class_name DynamicChain extends Node3D

@export_group("Chain Setup")
@export_range(2, 50) var link_count: int = 10:
	set(value):
		link_count = value
		if Engine.is_editor_hint():
			_regenerate_chain()
			
			
@export var link_length: float = 0.3:
	set(value):
		link_length = value
		if Engine.is_editor_hint():
			_regenerate_chain()
			
@export var link_radius: float = 0.05:
	set(value):
		link_radius = value
		if Engine.is_editor_hint():
			_regenerate_chain()

@export_group("Joint settings")
@export var angular_limit_degrees: float = 30.0
@export var twist_limit_degrees: float = 15.0

@export_group("Physics settings")
@export var link_mass: float = 0.5
@export var gravity_scale: float = 1.0
@export var link_damping: float = 0.5
 

@export_group("Rope Mesh")
@export var rope_radius: float = 0.045
@export_range(3, 16) var rope_sides: int = 8
@export_range(1, 8) var segments_per_link: int = 3
@export_range(0, 6) var strand_count: int = 3
@export_range(0.0, 0.4) var strand_depth: float = 0.14
@export var strand_twist: float = 9.0
@export var rope_color: Color = Color(0.72, 0.6, 0.42)

@export_group("References")
@export var anchor: StaticBody3D 
@export var link_container: Node3D



var links: Array[RigidBody3D] = []
var joints: Array[Generic6DOFJoint3D] = []

var _skin: MeshInstance3D
var _mesh: ImmediateMesh
var _material: StandardMaterial3D
var _points: PackedVector3Array = PackedVector3Array()

func _ready() -> void:
	_build_skin()
	if not Engine.is_editor_hint():
		_generate_chain()


func _process(_delta: float) -> void:
	_draw_rope()
		
		
func _generate_chain() -> void:
	_clear_chain()
	
	for child in link_container.get_children():
		child.queue_free()
	
	
	for i in range(link_count):
		var link = _create_link(i)
		link_container.add_child(link)
		links.append(link)
		link.position = Vector3(0, -(i + 1) * link_length, 0)

	for i in range(link_count):
		if anchor != null:
			links[i].add_collision_exception_with(anchor)
		if i >= 1:
			links[i].add_collision_exception_with(links[i - 1])
		if i >= 2:
			links[i].add_collision_exception_with(links[i - 2])
		
	await get_tree().process_frame
	
	for i in range(link_count):
		var body_a = anchor if i == 0 else links[i - 1]
		var body_b = links[i]
		
		var joint = _create_joint(body_a, body_b)
		body_b.add_child(joint)
		joints.append(joint)
		
	
	
func _create_link(index: int) -> RigidBody3D:
	var link = RigidBody3D.new()
	link.name = "Link" + str(index)
	
	link.mass = link_mass
	link.gravity_scale = gravity_scale
	link.linear_damp = link_damping
	link.angular_damp = link_damping
	
	
	link.set_collision_layer_value(1, false)
	link.set_collision_layer_value(5, true)
	link.set_collision_mask_value(1, true)
	link.set_collision_mask_value(2, true)
	link.set_collision_mask_value(3, true)
	link.set_collision_mask_value(4, true)
	link.set_collision_mask_value(5, true)
	
	var collision_shape = CollisionShape3D.new()
	var shape = CylinderShape3D.new()
	shape.height = link_length
	shape.radius = link_radius
	collision_shape.shape = shape
	link.add_child(collision_shape)
	
	return link
	
	
func _create_joint(body_a: Node3D, body_b: RigidBody3D) -> Generic6DOFJoint3D:
	var joint = Generic6DOFJoint3D.new()
	joint.name = "Joint_to_" + body_a.name
	joint.position = Vector3(0, link_length * 0.5, 0)
	
	joint.set_flag_x(Generic6DOFJoint3D.FLAG_ENABLE_LINEAR_LIMIT, true)
	joint.set_param_x(Generic6DOFJoint3D.PARAM_LINEAR_LOWER_LIMIT, 0)
	joint.set_param_x(Generic6DOFJoint3D.PARAM_LINEAR_UPPER_LIMIT, 0)
	
	joint.set_flag_y(Generic6DOFJoint3D.FLAG_ENABLE_LINEAR_LIMIT, true)
	joint.set_param_y(Generic6DOFJoint3D.PARAM_LINEAR_LOWER_LIMIT, 0)
	joint.set_param_y(Generic6DOFJoint3D.PARAM_LINEAR_UPPER_LIMIT, 0)
	
	joint.set_flag_z(Generic6DOFJoint3D.FLAG_ENABLE_LINEAR_LIMIT, true)
	joint.set_param_z(Generic6DOFJoint3D.PARAM_LINEAR_LOWER_LIMIT, 0)
	joint.set_param_z(Generic6DOFJoint3D.PARAM_LINEAR_UPPER_LIMIT, 0)
	
	
	var angular_limit_rad = deg_to_rad(angular_limit_degrees)
	var twist_limit_rad = deg_to_rad(twist_limit_degrees)
	
	joint.set_flag_x(Generic6DOFJoint3D.FLAG_ENABLE_ANGULAR_LIMIT, true)
	joint.set_param_x(Generic6DOFJoint3D.PARAM_ANGULAR_LOWER_LIMIT, -angular_limit_rad)
	joint.set_param_x(Generic6DOFJoint3D.PARAM_ANGULAR_UPPER_LIMIT, angular_limit_rad)
	
	joint.set_flag_y(Generic6DOFJoint3D.FLAG_ENABLE_ANGULAR_LIMIT, true)
	joint.set_param_y(Generic6DOFJoint3D.PARAM_ANGULAR_LOWER_LIMIT, -twist_limit_rad)
	joint.set_param_y(Generic6DOFJoint3D.PARAM_ANGULAR_UPPER_LIMIT, twist_limit_rad)
	
	joint.set_flag_z(Generic6DOFJoint3D.FLAG_ENABLE_ANGULAR_LIMIT, true)
	joint.set_param_z(Generic6DOFJoint3D.PARAM_ANGULAR_LOWER_LIMIT, -angular_limit_rad)
	joint.set_param_z(Generic6DOFJoint3D.PARAM_ANGULAR_UPPER_LIMIT, angular_limit_rad)
	
	joint.ready.connect(func():
		joint.node_a = joint.get_path_to(body_a)
		joint.node_b = NodePath("..")
		
	)
	
	return joint


func _clear_chain() -> void:
		for link in links:
			if is_instance_valid(link):
				link.queue_free()
		links.clear()
		joints.clear()

		if not is_instance_valid(link_container):
			return

		for child in link_container.get_children():
			child.queue_free()
	

func _regenerate_chain() -> void:
	if not Engine.is_editor_hint():
		return
	if not is_node_ready() or anchor == null or link_container == null:
		return

	_clear_chain()

	await get_tree().process_frame

	if not is_instance_valid(link_container):
		return

	_generate_chain()


func _build_skin() -> void:
	if is_instance_valid(_skin):
		return

	_mesh = ImmediateMesh.new()

	_material = StandardMaterial3D.new()
	_material.albedo_color = rope_color
	_material.roughness = 1.0
	_material.cull_mode = BaseMaterial3D.CULL_DISABLED

	_skin = MeshInstance3D.new()
	_skin.name = "Skin"
	_skin.mesh = _mesh
	_skin.top_level = true
	add_child(_skin)
	_skin.global_transform = Transform3D.IDENTITY


func _chain_points() -> PackedVector3Array:
	var spine := PackedVector3Array()
	if links.is_empty():
		return spine

	var half := link_length * 0.5

	for link in links:
		if not is_instance_valid(link):
			return PackedVector3Array()

	spine.append(links[0].global_transform * Vector3(0, half, 0))
	for link in links:
		spine.append(link.global_transform * Vector3(0, -half, 0))

	if segments_per_link <= 1:
		return spine

	var smooth := PackedVector3Array()
	var last: int = spine.size() - 1

	for i in range(last):
		var p0: Vector3 = spine[max(i - 1, 0)]
		var p1: Vector3 = spine[i]
		var p2: Vector3 = spine[i + 1]
		var p3: Vector3 = spine[min(i + 2, last)]

		for s in range(segments_per_link):
			smooth.append(_catmull(p0, p1, p2, p3, float(s) / segments_per_link))

	smooth.append(spine[last])
	return smooth


func _catmull(p0: Vector3, p1: Vector3, p2: Vector3, p3: Vector3, t: float) -> Vector3:
	var t2 := t * t
	var t3 := t2 * t
	return 0.5 * ((2.0 * p1) + (p2 - p0) * t \
		+ (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 \
		+ (3.0 * p1 - p0 - 3.0 * p2 + p3) * t3)


func _draw_rope() -> void:
	if not is_instance_valid(_skin):
		return

	_points = _chain_points()
	var count := _points.size()

	_mesh.clear_surfaces()
	if count < 2:
		return

	_material.albedo_color = rope_color

	var normals := PackedVector3Array()
	var binormals := PackedVector3Array()
	var arcs := PackedFloat32Array()
	normals.resize(count)
	binormals.resize(count)
	arcs.resize(count)

	var normal := Vector3.RIGHT
	var arc := 0.0

	for i in range(count):
		var ahead: Vector3 = _points[min(i + 1, count - 1)] - _points[max(i - 1, 0)]
		var tangent := ahead.normalized() if ahead.length_squared() > 1e-8 else Vector3.UP

		normal -= tangent * normal.dot(tangent)
		if normal.length_squared() < 1e-6:
			normal = tangent.cross(Vector3.UP)
		if normal.length_squared() < 1e-6:
			normal = tangent.cross(Vector3.RIGHT)
		normal = normal.normalized()

		if i > 0:
			arc += _points[i].distance_to(_points[i - 1])

		normals[i] = normal
		binormals[i] = tangent.cross(normal)
		arcs[i] = arc

	var sides: int = max(3, rope_sides)

	_mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES, _material)

	for i in range(count - 1):
		for s in range(sides):
			var n: int = (s + 1) % sides
			var ta: float = TAU * s / sides
			var tb: float = TAU * n / sides

			var a: Array = _ring_point(i, ta, normals, binormals, arcs)
			var b: Array = _ring_point(i, tb, normals, binormals, arcs)
			var c: Array = _ring_point(i + 1, ta, normals, binormals, arcs)
			var d: Array = _ring_point(i + 1, tb, normals, binormals, arcs)

			_mesh.surface_set_normal(a[1])
			_mesh.surface_add_vertex(a[0])
			_mesh.surface_set_normal(c[1])
			_mesh.surface_add_vertex(c[0])
			_mesh.surface_set_normal(d[1])
			_mesh.surface_add_vertex(d[0])

			_mesh.surface_set_normal(a[1])
			_mesh.surface_add_vertex(a[0])
			_mesh.surface_set_normal(d[1])
			_mesh.surface_add_vertex(d[0])
			_mesh.surface_set_normal(b[1])
			_mesh.surface_add_vertex(b[0])

	_mesh.surface_end()


func _ring_point(i: int, theta: float, normals: PackedVector3Array, binormals: PackedVector3Array, arcs: PackedFloat32Array) -> Array:
	var dir: Vector3 = normals[i] * cos(theta) + binormals[i] * sin(theta)
	var ripple: float = 1.0 + strand_depth * cos(strand_count * (theta + arcs[i] * strand_twist))
	return [_points[i] + dir * rope_radius * ripple, dir]
