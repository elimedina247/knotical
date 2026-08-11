import bpy

BASE_NAME = "Seabed"
BASE_SIZE = 12000.0
BASE_SUBDIVISIONS = 250

GROUP_NAME = "Seabed Detail"

BASIN_HEIGHT = 24.0
BASIN_WAVELENGTH = 900.0
DUNE_HEIGHT = 2.5
DUNE_WAVELENGTH = 45.0
DUNE_WANDER = 12.0

PATCHES = [
    ("Patch_Canyon", -1800.0, 900.0, 1024.0, 256),
    ("Patch_Seamount", 2400.0, -1200.0, 1536.0, 384),
]


def ensure_object_mode():
    obj = bpy.context.view_layer.objects.active
    if obj is not None and obj.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')


def set_viewport_clipping():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != 'VIEW_3D':
                continue
            for space in area.spaces:
                if space.type == 'VIEW_3D':
                    space.clip_start = 1.0
                    space.clip_end = 200000.0


def add_float_input(group, name, default, minimum, maximum):
    socket = group.interface.new_socket(
        name, in_out='INPUT', socket_type='NodeSocketFloat')
    socket.default_value = default
    socket.min_value = minimum
    socket.max_value = maximum
    return socket


def build_detail_group():
    existing = bpy.data.node_groups.get(GROUP_NAME)
    if existing is not None:
        return existing

    group = bpy.data.node_groups.new(GROUP_NAME, 'GeometryNodeTree')
    group.interface.new_socket(
        "Geometry", in_out='INPUT', socket_type='NodeSocketGeometry')
    group.interface.new_socket(
        "Geometry", in_out='OUTPUT', socket_type='NodeSocketGeometry')

    add_float_input(group, "Basin Height", BASIN_HEIGHT, 0.0, 400.0)
    add_float_input(group, "Basin Wavelength", BASIN_WAVELENGTH, 20.0, 8000.0)
    add_float_input(group, "Dune Height", DUNE_HEIGHT, 0.0, 40.0)
    add_float_input(group, "Dune Wavelength", DUNE_WAVELENGTH, 2.0, 400.0)
    add_float_input(group, "Dune Wander", DUNE_WANDER, 0.0, 40.0)

    nodes = group.nodes
    links = group.links

    group_in = nodes.new('NodeGroupInput')
    group_in.location = (-900, 0)

    group_out = nodes.new('NodeGroupOutput')
    group_out.location = (700, 0)

    position = nodes.new('GeometryNodeInputPosition')
    position.location = (-900, -320)

    basin_scale = nodes.new('ShaderNodeMath')
    basin_scale.operation = 'DIVIDE'
    basin_scale.location = (-700, -160)
    basin_scale.inputs[0].default_value = 1.0

    basin_noise = nodes.new('ShaderNodeTexNoise')
    basin_noise.noise_dimensions = '3D'
    basin_noise.location = (-500, -120)
    basin_noise.inputs["Detail"].default_value = 4.0
    basin_noise.inputs["Roughness"].default_value = 0.5

    basin_center = nodes.new('ShaderNodeMath')
    basin_center.operation = 'SUBTRACT'
    basin_center.location = (-260, -100)
    basin_center.inputs[1].default_value = 0.5

    basin_amount = nodes.new('ShaderNodeMath')
    basin_amount.operation = 'MULTIPLY'
    basin_amount.location = (-80, -100)

    dune_scale = nodes.new('ShaderNodeMath')
    dune_scale.operation = 'DIVIDE'
    dune_scale.location = (-700, -520)
    dune_scale.inputs[0].default_value = 1.0

    dune_wave = nodes.new('ShaderNodeTexWave')
    dune_wave.wave_type = 'BANDS'
    dune_wave.bands_direction = 'X'
    dune_wave.wave_profile = 'SIN'
    dune_wave.location = (-500, -480)
    dune_wave.inputs["Detail"].default_value = 3.0
    dune_wave.inputs["Detail Scale"].default_value = 1.5
    dune_wave.inputs["Detail Roughness"].default_value = 0.5

    dune_center = nodes.new('ShaderNodeMath')
    dune_center.operation = 'SUBTRACT'
    dune_center.location = (-260, -460)
    dune_center.inputs[1].default_value = 0.5

    dune_amount = nodes.new('ShaderNodeMath')
    dune_amount.operation = 'MULTIPLY'
    dune_amount.location = (-80, -460)

    total = nodes.new('ShaderNodeMath')
    total.operation = 'ADD'
    total.location = (120, -260)

    combine = nodes.new('ShaderNodeCombineXYZ')
    combine.location = (300, -260)

    set_position = nodes.new('GeometryNodeSetPosition')
    set_position.location = (500, 0)

    links.new(position.outputs["Position"], basin_noise.inputs["Vector"])
    links.new(position.outputs["Position"], dune_wave.inputs["Vector"])

    links.new(group_in.outputs["Basin Wavelength"], basin_scale.inputs[1])
    links.new(basin_scale.outputs["Value"], basin_noise.inputs["Scale"])
    links.new(basin_noise.outputs["Fac"], basin_center.inputs[0])
    links.new(basin_center.outputs["Value"], basin_amount.inputs[0])
    links.new(group_in.outputs["Basin Height"], basin_amount.inputs[1])

    links.new(group_in.outputs["Dune Wavelength"], dune_scale.inputs[1])
    links.new(dune_scale.outputs["Value"], dune_wave.inputs["Scale"])
    links.new(group_in.outputs["Dune Wander"], dune_wave.inputs["Distortion"])
    links.new(dune_wave.outputs["Fac"], dune_center.inputs[0])
    links.new(dune_center.outputs["Value"], dune_amount.inputs[0])
    links.new(group_in.outputs["Dune Height"], dune_amount.inputs[1])

    links.new(basin_amount.outputs["Value"], total.inputs[0])
    links.new(dune_amount.outputs["Value"], total.inputs[1])
    links.new(total.outputs["Value"], combine.inputs["Z"])

    links.new(group_in.outputs["Geometry"], set_position.inputs["Geometry"])
    links.new(combine.outputs["Vector"], set_position.inputs["Offset"])
    links.new(set_position.outputs["Geometry"], group_out.inputs["Geometry"])

    return group


def set_modifier_input(modifier, name, value):
    for item in modifier.node_group.interface.items_tree:
        if item.item_type != 'SOCKET':
            continue
        if item.in_out == 'INPUT' and item.name == name:
            modifier[item.identifier] = value
            return


def make_grid(name, x, y, size, subdivisions, group, dune_height):
    if name in bpy.data.objects:
        print(f"{name} already exists, leaving it alone")
        return bpy.data.objects[name]

    bpy.ops.mesh.primitive_grid_add(
        x_subdivisions=subdivisions,
        y_subdivisions=subdivisions,
        size=size,
        location=(x, y, 0.0),
    )

    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    bpy.ops.object.shade_flat()

    modifier = obj.modifiers.new(GROUP_NAME, 'NODES')
    modifier.node_group = group
    set_modifier_input(modifier, "Basin Height", BASIN_HEIGHT)
    set_modifier_input(modifier, "Basin Wavelength", BASIN_WAVELENGTH)
    set_modifier_input(modifier, "Dune Height", dune_height)
    set_modifier_input(modifier, "Dune Wavelength", DUNE_WAVELENGTH)
    set_modifier_input(modifier, "Dune Wander", DUNE_WANDER)

    print(f"created {name}")
    return obj


ensure_object_mode()
set_viewport_clipping()

detail_group = build_detail_group()
make_grid(BASE_NAME, 0.0, 0.0, BASE_SIZE, BASE_SUBDIVISIONS, detail_group, 0.0)

for patch_name, patch_x, patch_y, patch_size, patch_subdivisions in PATCHES:
    make_grid(patch_name, patch_x, patch_y, patch_size,
              patch_subdivisions, detail_group, DUNE_HEIGHT)
