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
 

@export_group("References")
@export var anchor: StaticBody3D 
@export var link_container: Node3D



var links: Array[RigidBody3D] = []
var joints: Array[Generic6DOFJoint3D] = []

func _ready() -> void:
	if not Engine.is_editor_hint():
		_generate_chain()
		
		
func _generate_chain() -> void:
	_clear_chain()
	
	for child in link_container.get_children():
		child.queue_free()
	
	
	for i in range(link_count):
		var link = _create_link(i)
		link_container.add_child(link)
		links.append(link)
		link.position = Vector3(0, -(i + 1) * link_length, 0)
		
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
	
	
	var mesh_instance = MeshInstance3D.new()
	var cylinder = CylinderMesh.new()
	cylinder.height = link_length
	cylinder.top_radius = link_radius
	cylinder.bottom_radius = link_radius
	mesh_instance.mesh = cylinder
	link.add_child(mesh_instance)
	
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
