# Rigs and animates one Varco3D moa model in Blender, then exports an FBX for Unity.
# Run headless:
#   blender --background --factory-startup --python rig_moa.py -- <obj path> <out fbx> <body type> [preview dir]
# Body types:
#   biped     stands on two legs, arms out in a T-pose (rabbit, bear, ...)
#   upright   stands on two legs, arms at the sides (marten)
#   quadruped four legs (tiger, deer, boar)
#   hopper    four legs, hops instead of walking (frog)
#   bird      two legs, wings, tail (hawk, owl, magpie, crane)
#   insect    flying body with wings (bee, butterfly)
#   snake     coiled body with a raised neck (rat snake)
#
# The model is decimated, an armature is fitted to the measured mesh, the mesh is bound with automatic weights,
# and four actions are keyed: Idle, Walk, Attack, Hit. In Blender the model faces -Y, up is +Z and the
# character's left is +X.

import math
import os
import sys

import bpy
import bmesh
from mathutils import Quaternion, Vector

FPS = 30
TARGET_TRIANGLES = 15000
# Automatic (heat) weights fail on tiny meshes, so the work happens at 10x and the FBX is scaled back down.
WORK_SCALE = 10.0

X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 3:
        raise SystemExit("usage: -- <obj> <out fbx> <body type> [preview dir]")
    return argv[0], argv[1], argv[2], (argv[3] if len(argv) > 3 else None)


# ---------- scene ----------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS


def import_model(path):
    bpy.ops.wm.obj_import(filepath=path)
    meshes = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    select_only(meshes[0], *meshes[1:])
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = "Body"
    obj.scale = (WORK_SCALE, WORK_SCALE, WORK_SCALE)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def decimate(obj):
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if tris > TARGET_TRIANGLES:
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = TARGET_TRIANGLES / tris
        select_only(obj)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Welds seams so the surface is as connected as possible.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005 * WORK_SCALE)
    bm.to_mesh(obj.data)
    bm.free()
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def select_only(active, *others):
    bpy.ops.object.select_all(action="DESELECT")
    for o in (active,) + tuple(others):
        o.select_set(True)
    bpy.context.view_layer.objects.active = active


# ---------- measuring ----------

def percentile(values, p):
    values = sorted(values)
    if not values:
        return 0.0
    return values[min(len(values) - 1, max(0, int(len(values) * p)))]


def centroid(verts):
    total = Vector()
    for v in verts:
        total += v
    return total / max(1, len(verts))


class Shape:
    def __init__(self, obj):
        self.verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
        self.min = Vector((min(v.x for v in self.verts), min(v.y for v in self.verts), min(v.z for v in self.verts)))
        self.max = Vector((max(v.x for v in self.verts), max(v.y for v in self.verts), max(v.z for v in self.verts)))
        self.size = self.max - self.min
        self.center = (self.min + self.max) / 2
        self.floor = self.min.z
        self.H = self.size.z

    def z(self, fraction):
        return self.floor + fraction * self.H

    def half_width(self, verts):
        return percentile([abs(v.x - self.center.x) for v in verts], 0.92) if verts else 0.0

    def band(self, z0, z1, verts=None):
        return [v for v in (verts or self.verts) if z0 <= v.z <= z1]

    def widest_z(self, z0, z1, steps=40, verts=None):
        best_z, best_w = z0, -1.0
        for i in range(steps):
            a = z0 + (z1 - z0) * i / steps
            b = a + (z1 - z0) / steps
            w = self.half_width(self.band(a, b, verts))
            if w > best_w:
                best_z, best_w = (a + b) / 2, w
        return best_z, best_w

    def torso_range(self, verts, steps=30, keep=0.6):
        """Lowest and highest z where the body is at least `keep` of its widest (legs and necks are thinner)."""
        z0, z1 = min(v.z for v in verts), max(v.z for v in verts)
        widths = []
        for i in range(steps):
            a = z0 + (z1 - z0) * i / steps
            b = a + (z1 - z0) / steps
            widths.append(((a + b) / 2, self.half_width(self.band(a, b, verts))))
        widest = max(w for _, w in widths)
        rows = [z for z, w in widths if w >= keep * widest]
        return min(rows), max(rows), widest

    def feet(self, height_fraction=0.08):
        return [v for v in self.verts if v.z < self.z(height_fraction)]


# ---------- armature ----------

class RigBuilder:
    def __init__(self, shape):
        self.shape = shape
        bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
        self.rig = bpy.context.object
        self.rig.name = "Armature"
        self.arm = self.rig.data
        self.arm.edit_bones.remove(self.arm.edit_bones[0])
        c = shape.center
        self.bone("Root", Vector((c.x, c.y, shape.floor)), Vector((c.x, c.y - 0.15 * shape.H, shape.floor)), deform=False)

    def bone(self, name, head, tail, parent=None, deform=True):
        b = self.arm.edit_bones.new(name)
        b.head = head
        b.tail = tail
        b.use_deform = deform
        if parent is not None:
            b.parent = self.arm.edit_bones[parent]
        return b

    def chain(self, names, points, parent):
        for i, name in enumerate(names):
            self.bone(name, points[i], points[i + 1], parent)
            parent = name

    def finish(self):
        bpy.ops.object.mode_set(mode="OBJECT")
        return self.rig


def lerp(a, b, t):
    return a + (b - a) * t


def tail_points(shape, verts, base, count=2):
    """Points from base along the tail geometry, ordered by distance from the base."""
    far = sorted(verts, key=lambda v: (v - base).length)
    points = [base]
    for i in range(1, count + 1):
        lo = int(len(far) * (i - 0.5) / count)
        hi = int(len(far) * min(1.0, (i + 0.3) / count))
        points.append(centroid(far[lo:max(lo + 1, hi)]))
    return points


def build_biped(shape, arms=True):
    c, H, floor = shape.center, shape.H, shape.floor
    if arms:
        arm_z, reach = shape.widest_z(shape.z(0.3), shape.z(0.75))
    else:
        arm_z, reach = shape.z(0.58), 0.0
    torso = max(shape.half_width(shape.band(shape.z(0.2), shape.z(0.35))), 0.08 * shape.size.x)
    hip_z = floor + 0.32 * (arm_z - floor) + 0.05 * H
    neck_z = arm_z + 0.06 * H
    # The body column. Without T-pose arms a big tail can make up most of the mesh, so the column is placed
    # under the head instead of at the bounds center.
    body_y = c.y if arms else centroid([v for v in shape.verts if v.z > shape.z(0.88)]).y

    b = RigBuilder(shape)
    b.bone("Hips", Vector((c.x, body_y, hip_z)), Vector((c.x, body_y, hip_z + 0.12 * H)), "Root")
    b.bone("Spine", Vector((c.x, body_y, hip_z + 0.12 * H)), Vector((c.x, body_y, arm_z - 0.02 * H)), "Hips")
    b.bone("Neck", Vector((c.x, body_y, arm_z - 0.02 * H)), Vector((c.x, body_y, neck_z)), "Spine")
    # Stops short of the top so it stays inside the head (not between ears or horns).
    b.bone("Head", Vector((c.x, body_y, neck_z)), Vector((c.x, body_y, neck_z + 0.55 * (shape.max.z - neck_z))), "Neck")

    if arms:
        shoulder_x = min(torso * 0.9, reach * 0.5)
        for side, sign in (("L", 1), ("R", -1)):
            sx = c.x + sign * shoulder_x
            hand_x = c.x + sign * reach * 0.97
            elbow_x = (sx + hand_x) / 2
            b.bone(f"UpperArm.{side}", Vector((sx, c.y, arm_z)), Vector((elbow_x, c.y, arm_z)), "Spine")
            b.bone(f"LowerArm.{side}", Vector((elbow_x, c.y, arm_z)), Vector((hand_x, c.y, arm_z)), f"UpperArm.{side}")

    feet = [v for v in shape.feet(0.06) if abs(v.y - body_y) < 0.22 * shape.size.y]
    for side, sign in (("L", 1), ("R", -1)):
        side_feet = [v for v in feet if (v.x - c.x) * sign > 0]
        foot = centroid(side_feet) if side_feet else Vector((c.x + sign * torso * 0.5, body_y, floor))
        lx = c.x + sign * min(abs(foot.x - c.x), torso * 0.7)
        knee_z = floor + (hip_z - floor) * 0.5
        b.bone(f"Thigh.{side}", Vector((lx, body_y, hip_z)), Vector((lx, lerp(body_y, foot.y, 0.5) - 0.01 * H, knee_z)), "Hips")
        b.bone(f"Shin.{side}", Vector((lx, lerp(body_y, foot.y, 0.5) - 0.01 * H, knee_z)), Vector((lx, foot.y, floor + 0.04 * H)), f"Thigh.{side}")

    tail_verts = [v for v in shape.verts if v.y > body_y + 0.25 * shape.size.y and v.z < arm_z]
    if len(tail_verts) > 30:
        base = Vector((c.x, body_y + 0.1 * shape.size.y, centroid(tail_verts).z if arms else hip_z))
        b.chain(["Tail", "Tail2"], tail_points(shape, tail_verts, base), "Hips")
    return b.finish()


def build_quadruped(shape):
    c, H, L = shape.center, shape.H, shape.size.y
    feet = shape.feet(0.1)
    mid_y = percentile([v.y for v in feet], 0.5)
    front_feet = [v for v in feet if v.y < mid_y]
    back_feet = [v for v in feet if v.y >= mid_y]
    chest_y = centroid(front_feet).y
    hip_y = centroid(back_feet).y

    body = [v for v in shape.verts if chest_y <= v.y <= hip_y and v.z > shape.z(0.15)]
    tz0, tz1, half_w = shape.torso_range(body)
    spine_z = (tz0 + tz1) / 2

    # Head: geometry in front of the chest and above the back line.
    head_verts = [v for v in shape.verts if v.y < chest_y + 0.05 * L and v.z > spine_z]
    head = centroid(head_verts) if head_verts else Vector((c.x, shape.min.y + 0.1 * L, tz1))
    head_base = Vector((c.x, lerp(chest_y, head.y, 0.5), lerp(tz1, head.z, 0.5)))
    snout = Vector((c.x, head.y - 0.6 * abs(head.y - shape.min.y), head.z))

    b = RigBuilder(shape)
    b.bone("Hips", Vector((c.x, hip_y, spine_z)), Vector((c.x, lerp(hip_y, chest_y, 0.45), spine_z)), "Root")
    b.bone("Spine", Vector((c.x, lerp(hip_y, chest_y, 0.45), spine_z)), Vector((c.x, chest_y, spine_z)), "Hips")
    b.bone("Neck", Vector((c.x, chest_y, spine_z)), head_base, "Spine")
    b.bone("Head", head_base, lerp(head, snout, 0.5), "Neck")

    for name, group, parent, knee_dir in (("F", front_feet, "Spine", -1), ("B", back_feet, "Hips", 1)):
        for side, sign in (("L", 1), ("R", -1)):
            side_feet = [v for v in group if (v.x - c.x) * sign > 0]
            foot = centroid(side_feet) if side_feet else Vector((c.x + sign * half_w * 0.6, chest_y if name == "F" else hip_y, shape.floor))
            top = Vector((lerp(c.x, foot.x, 0.85), foot.y, lerp(tz0, tz1, 0.25)))
            knee = lerp(top, Vector((foot.x, foot.y, shape.floor)), 0.5) + Vector((0, knee_dir * 0.04 * L, 0))
            b.bone(f"Leg{name}.{side}", top, knee, parent)
            b.bone(f"Shin{name}.{side}", knee, Vector((foot.x, foot.y, shape.z(0.03))), f"Leg{name}.{side}")

    tail_verts = [v for v in shape.verts if v.y > hip_y + 0.08 * L and v.z > tz0]
    if len(tail_verts) > 30:
        base = Vector((c.x, hip_y + 0.04 * L, lerp(spine_z, tz1, 0.5)))
        b.chain(["Tail", "Tail2"], tail_points(shape, tail_verts, base), "Hips")
    return b.finish()


def build_bird(shape):
    c, H = shape.center, shape.H
    feet = shape.feet(0.05)
    upper = [v for v in shape.verts if v.z > shape.z(0.12)]
    tz0, tz1, half_w = shape.torso_range(upper)
    torso_verts = shape.band(tz0, tz1)
    body_y = percentile([v.y for v in torso_verts], 0.5)
    back_y = percentile([v.y for v in torso_verts], 0.85)
    front_y = percentile([v.y for v in torso_verts], 0.1)
    depth = back_y - front_y

    b = RigBuilder(shape)
    hips = Vector((c.x, lerp(body_y, back_y, 0.4), lerp(tz0, tz1, 0.25)))
    chest = Vector((c.x, lerp(body_y, front_y, 0.3), lerp(tz0, tz1, 0.8)))
    b.bone("Hips", hips, lerp(hips, chest, 0.5), "Root")
    b.bone("Spine", lerp(hips, chest, 0.5), chest, "Hips")

    head_verts = [v for v in shape.verts if v.z > shape.z(0.85)]
    head = centroid(head_verts)
    if head.z - chest.z > 0.12 * H:
        neck_top = lerp(chest, head, 0.6)
        b.bone("Neck", chest, neck_top, "Spine")
        b.bone("Head", neck_top, head + Vector((0, -0.25 * depth, 0)), "Neck")
    else:
        b.bone("Head", chest, head + Vector((0, -0.2 * depth, 0)), "Spine")

    for side, sign in (("L", 1), ("R", -1)):
        side_feet = [v for v in feet if (v.x - c.x) * sign > 0]
        foot = centroid(side_feet) if side_feet else Vector((c.x + sign * half_w * 0.4, body_y, shape.floor))
        top = Vector((lerp(c.x, foot.x, 0.8), foot.y, tz0 + 0.1 * (tz1 - tz0)))
        knee = lerp(top, Vector((foot.x, foot.y, shape.floor)), 0.5) + Vector((0, 0.03 * depth, 0))
        b.bone(f"Thigh.{side}", top, knee, "Hips")
        b.bone(f"Shin.{side}", knee, Vector((foot.x, foot.y, shape.z(0.02))), f"Thigh.{side}")

        root = Vector((c.x + sign * half_w * 0.6, lerp(body_y, front_y, 0.2), lerp(tz0, tz1, 0.75)))
        # Tip stays over the body; reaching back to the tail would drag long tail feathers along with the wing.
        tip = Vector((c.x + sign * half_w * 0.85, lerp(body_y, back_y, 0.55), lerp(tz0, tz1, 0.35)))
        b.bone(f"Wing.{side}", root, tip, "Spine")

    tail_verts = [v for v in shape.verts if v.y > back_y and v.z > shape.z(0.08)]
    if len(tail_verts) > 30:
        base = Vector((c.x, lerp(body_y, back_y, 0.7), lerp(tz0, tz1, 0.35)))
        b.chain(["Tail"], tail_points(shape, tail_verts, base, count=1), "Hips")
    return b.finish()


def build_insect(shape):
    c, H, W = shape.center, shape.H, shape.size.x
    upright = shape.size.z > shape.size.y * 1.3  # butterfly stands up; bee lies along Y
    central = [v for v in shape.verts if abs(v.x - c.x) < 0.12 * W]
    wings = [v for v in shape.verts if abs(v.x - c.x) > 0.3 * W * 0.5]

    b = RigBuilder(shape)
    if upright:
        body_y = centroid(central).y
        bottom = Vector((c.x, body_y, shape.z(0.1)))
        top = Vector((c.x, body_y, shape.z(0.7)))
        b.bone("Body", bottom, top, "Root")
        b.bone("Head", top, Vector((c.x, body_y, shape.z(0.9))), "Body")
    else:
        bz = centroid(central).z
        back = Vector((c.x, shape.max.y - 0.2 * shape.size.y, bz))
        front = Vector((c.x, shape.min.y + 0.3 * shape.size.y, bz))
        b.bone("Body", back, front, "Root")
        b.bone("Head", front, Vector((c.x, shape.min.y + 0.08 * shape.size.y, bz)), "Body")

    for side, sign in (("L", 1), ("R", -1)):
        side_wing = [v for v in wings if (v.x - c.x) * sign > 0 and (upright or v.z > shape.z(0.55))]
        tip = centroid(side_wing) if side_wing else Vector((c.x + sign * 0.4 * W, c.y, shape.z(0.8)))
        root = Vector((c.x + sign * 0.06 * W, tip.y, tip.z if upright else lerp(shape.z(0.6), tip.z, 0.5)))
        b.bone(f"Wing.{side}", root, tip, "Body")
    return b.finish(), upright


def build_snake(shape):
    c, H = shape.center, shape.H
    head_verts = [v for v in shape.verts if v.z > shape.z(0.82)]
    head = centroid(head_verts)
    points = []
    previous = Vector((head.x, head.y, shape.z(0.25)))
    for i in range(4):
        z = lerp(shape.z(0.25), head.z, i / 4)
        near = [v for v in shape.verts if abs(v.z - z) < 0.04 * H and (Vector((v.x, v.y)) - Vector((previous.x, previous.y))).length < 0.18 * max(shape.size.x, shape.size.y)]
        p = centroid(near) if near else Vector((previous.x, previous.y, z))
        p.z = z
        points.append(p)
        previous = p
    points.append(head + Vector((0, -0.05 * shape.size.y, 0)))

    b = RigBuilder(shape)
    b.bone("Coil", Vector((c.x, c.y, shape.z(0.05))), points[0], "Root")
    b.chain(["Neck1", "Neck2", "Neck3", "Head"], points, "Coil")
    return b.finish()


# ---------- binding ----------

def weighted_count(obj):
    return sum(1 for v in obj.data.vertices if any(g.weight > 0.0 for g in v.groups))


def fill_unweighted(body, rig):
    """Vertices the transfer missed (small detached bits) follow their nearest bone instead of staying put."""
    bones = [b for b in rig.data.bones if b.use_deform]
    groups = {b.name: body.vertex_groups.get(b.name) or body.vertex_groups.new(name=b.name) for b in bones}
    filled = 0
    for v in body.data.vertices:
        if any(g.weight > 0.0 for g in v.groups):
            continue
        best, best_d = None, float("inf")
        for b in bones:
            head, tail = b.head_local, b.tail_local
            seg = tail - head
            t = max(0.0, min(1.0, (v.co - head).dot(seg) / max(seg.length_squared, 1e-9)))
            d = (v.co - (head + seg * t)).length
            if d < best_d:
                best, best_d = b, d
        groups[best.name].add([v.index], 1.0, "REPLACE")
        filled += 1
    if filled:
        print(f"[rig] filled {filled} unweighted vertices from nearest bones")


def make_proxy(body, voxel_size):
    proxy = body.copy()
    proxy.data = body.data.copy()
    proxy.name = "WeightProxy"
    bpy.context.collection.objects.link(proxy)
    remesh = proxy.modifiers.new("Remesh", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = voxel_size
    select_only(proxy)
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    return proxy


def bind(body, rig, height):
    # Varco meshes are not watertight, which makes heat weighting fail. Weights are solved on a voxel-remeshed
    # (closed) copy and then transferred to the real mesh by nearest surface. A fine voxel keeps thin parts
    # (tail feathers, antennae); when heat weighting still fails on it, a coarser copy is tried.
    for voxel in (0.006, 0.012, 0.02):
        proxy = make_proxy(body, height * voxel)
        select_only(rig, proxy)
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        solved = weighted_count(proxy)
        print(f"[rig] proxy voxel={voxel} weights {solved}/{len(proxy.data.vertices)} vertices")
        if solved >= len(proxy.data.vertices) * 0.9:
            break
        bpy.data.objects.remove(proxy, do_unlink=True)
        proxy = None
    if proxy is None:
        proxy = make_proxy(body, height * 0.012)
        select_only(rig, proxy)
        bpy.ops.object.parent_set(type="ARMATURE_ENVELOPE")

    select_only(rig, body)
    bpy.ops.object.parent_set(type="ARMATURE_NAME")  # armature modifier + empty groups per bone
    select_only(proxy, body)
    bpy.ops.object.data_transfer(
        data_type="VGROUP_WEIGHTS",
        use_create=True,
        vert_mapping="POLYINTERP_NEAREST",
        layers_select_src="ALL",
        layers_select_dst="NAME",
        mix_mode="REPLACE",
    )
    select_only(body)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.data.objects.remove(proxy, do_unlink=True)

    fill_unweighted(body, rig)
    weighted = weighted_count(body)
    if weighted < len(body.data.vertices) * 0.9:
        print(f"[rig] transferred weights cover only {weighted}/{len(body.data.vertices)}, falling back to envelopes")
        for g in list(body.vertex_groups):
            body.vertex_groups.remove(g)
        select_only(rig, body)
        bpy.ops.object.parent_set(type="ARMATURE_ENVELOPE")
    return weighted


# ---------- animation ----------

def world_rotation(pose_bone, axis, degrees):
    """Pose-basis quaternion that turns the bone around a world axis, as seen in the rest pose."""
    rest = pose_bone.bone.matrix_local.to_quaternion()
    q = Quaternion(Vector(axis).normalized(), math.radians(degrees))
    return rest.inverted() @ q @ rest


class Keyer:
    """Keys bone turns around world axes. Missing bones are skipped, so one routine fits several shapes.

    Turn directions (model faces -Y): around X, a bone pointing down swings back for +, forward for -;
    a bone pointing forward or up tips forward/down for +. Around Z, + turns toward the character's left.
    """

    def __init__(self, rig, height):
        self.rig = rig
        self.h = height
        for pb in rig.pose.bones:
            pb.rotation_mode = "QUATERNION"

    def start(self, name):
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        self.rig.animation_data_create()
        self.rig.animation_data.action = action
        for pb in self.rig.pose.bones:
            pb.rotation_quaternion = Quaternion()
            pb.location = Vector()

    def rot(self, bone, frame, *turns):
        pb = self.rig.pose.bones.get(bone)
        if pb is None:
            return
        q = Quaternion()
        for axis, degrees in turns:
            q = world_rotation(pb, axis, degrees) @ q
        pb.rotation_quaternion = q
        pb.keyframe_insert("rotation_quaternion", frame=frame)

    def loc(self, bone, frame, offset):
        """Offset in world axes, as a fraction of the model height."""
        pb = self.rig.pose.bones.get(bone)
        if pb is None:
            return
        rest = pb.bone.matrix_local.to_quaternion()
        pb.location = rest.inverted() @ (Vector(offset) * self.h)
        pb.keyframe_insert("location", frame=frame)


def cycle(length, keys=4):
    """(frame, phase radians) pairs for one looping cycle, including the closing frame."""
    return [(1 + round(i * length / keys), i / keys * 2 * math.pi) for i in range(keys + 1)]


# biped / upright

ARMS_DOWN = 65  # T-pose arms are lowered to a relaxed pose in every action


def biped_arms(k, f, swing_l=0.0, swing_r=0.0, extra_r=()):
    # Left arm points +X: lowering it is a turn around +Y (right arm: -Y). Elbows bend toward the front.
    k.rot("UpperArm.L", f, (Y, ARMS_DOWN), (X, -swing_l))
    k.rot("UpperArm.R", f, (Y, -ARMS_DOWN), (X, -swing_r), *extra_r)
    k.rot("LowerArm.L", f, (Z, -15))
    k.rot("LowerArm.R", f, (Z, 15))


def tail_sway(k, f, s, amount):
    k.rot("Tail", f, (Z, amount * s))
    k.rot("Tail2", f, (Z, amount * 1.3 * s))


def animate_biped(k):
    k.start("Idle")
    for f, p in cycle(60):
        breathe = (1 - math.cos(p)) / 2
        k.rot("Spine", f, (X, -3 * breathe))
        k.rot("Head", f, (X, 4 * math.sin(p)), (Z, 4 * math.sin(p)))
        biped_arms(k, f, 4 * breathe, 4 * breathe)
        k.rot("Thigh.L", f)
        k.rot("Thigh.R", f)
        tail_sway(k, f, math.sin(p), 15)
        k.loc("Hips", f, (0, 0, -0.01 * breathe))

    k.start("Walk")
    for f, p in cycle(24):
        s = math.sin(p)
        k.rot("Thigh.L", f, (X, -30 * s))
        k.rot("Thigh.R", f, (X, 30 * s))
        k.rot("Shin.L", f, (X, 25 * max(0.0, -s)))
        k.rot("Shin.R", f, (X, 25 * max(0.0, s)))
        biped_arms(k, f, -25 * s, 25 * s)
        k.rot("Spine", f, (Z, 5 * s), (X, -4))
        k.rot("Head", f, (Z, -3 * s))
        tail_sway(k, f, s, 12)
        k.loc("Hips", f, (0, 0, 0.035 * abs(math.cos(p))))

    k.start("Attack")
    for f, a in ((1, 0.0), (6, -1.0), (10, 1.0), (16, 0.6), (21, 0.0)):
        k.rot("Spine", f, (Z, -20 * a), (X, -12 * max(a, 0) + 6 * max(-a, 0)))
        k.rot("Head", f, (Z, 10 * a))
        # Right arm raises back on the wind-up and swipes across the front.
        biped_arms(k, f, -10 * a, 0, extra_r=((Y, 90 * max(-a, 0)), (Z, 60 * max(a, 0)), (X, -40 * max(a, 0))))
        k.rot("Thigh.L", f, (X, -15 * max(a, 0)))
        k.rot("Thigh.R", f, (X, 10 * max(a, 0)))
        tail_sway(k, f, a, 20)
        k.loc("Hips", f, (0, -0.05 * max(a, 0), 0))

    k.start("Hit")
    for f, a in ((1, 0.0), (4, 1.0), (9, 0.4), (13, 0.0)):
        k.rot("Spine", f, (X, 15 * a))
        k.rot("Head", f, (X, 12 * a))
        biped_arms(k, f, -15 * a, -15 * a)
        tail_sway(k, f, a, 15)
        k.loc("Hips", f, (0, 0.04 * a, 0))


def animate_upright(k):
    # Same as the biped but the attack is a lunging bite (no arms to swipe with).
    animate_biped(k)
    bpy.data.actions.remove(bpy.data.actions["Attack"])
    k.start("Attack")
    for f, a in ((1, 0.0), (6, -1.0), (10, 1.0), (16, 0.5), (21, 0.0)):
        k.rot("Spine", f, (X, -20 * max(a, 0) + 10 * max(-a, 0)))
        k.rot("Neck", f, (X, -15 * max(a, 0) + 8 * max(-a, 0)))
        k.rot("Head", f, (X, 10 * max(a, 0) - 10 * max(-a, 0)))
        k.rot("Thigh.L", f, (X, -15 * max(a, 0)))
        k.rot("Thigh.R", f, (X, 10 * max(a, 0)))
        tail_sway(k, f, a, 25)
        k.loc("Hips", f, (0, -0.06 * max(a, 0), -0.02 * max(a, 0)))


# quadruped / hopper

def quad_legs(k, f, fl, fr, bl, br, bend=25.0):
    """Leg swings in degrees (+ forward). Shins bend when a leg swings back (lifting the foot)."""
    for name, swing in (("LegF.L", fl), ("LegF.R", fr), ("LegB.L", bl), ("LegB.R", br)):
        k.rot(name, f, (X, -swing))
        shin = name.replace("Leg", "Shin")
        lift = max(0.0, swing) / 30.0 * bend
        k.rot(shin, f, (X, lift if "F" in name else -lift))


def animate_quadruped(k, hopper=False):
    k.start("Idle")
    for f, p in cycle(60):
        breathe = (1 - math.cos(p)) / 2
        k.rot("Spine", f, (X, 2 * breathe))
        k.rot("Neck", f, (X, -3 * breathe))
        k.rot("Head", f, (Z, 10 * math.sin(p)), (X, 3 * math.sin(2 * p)))
        quad_legs(k, f, 0, 0, 0, 0)
        tail_sway(k, f, math.sin(p), 18)
        k.loc("Hips", f, (0, 0, -0.008 * breathe))

    k.start("Walk")
    if hopper:
        # Both back legs push off together, the body arcs up and lands on the front legs.
        for f, push, air in ((1, 0, 0), (5, 1, 0.3), (9, 0.6, 1), (13, -0.6, 0.4), (17, 0, 0)):
            quad_legs(k, f, 20 * air - 10 * push, 20 * air - 10 * push, -35 * push, -35 * push)
            k.rot("Spine", f, (X, -10 * push + 6 * air))
            k.rot("Head", f, (X, -8 * push))
            k.loc("Hips", f, (0, 0, 0.25 * air))
    else:
        # Diagonal pairs move together (front-left with back-right).
        for f, p in cycle(24):
            s = math.sin(p)
            quad_legs(k, f, 25 * s, -25 * s, -25 * s, 25 * s)
            k.rot("Spine", f, (Z, 3 * s))
            k.rot("Neck", f, (X, 3 * math.cos(2 * p)))
            k.rot("Head", f, (Z, -4 * s))
            tail_sway(k, f, s, 15)
            k.loc("Hips", f, (0, 0, 0.025 * abs(math.cos(p))))

    k.start("Attack")
    # Crouch back, then lunge forward with the head down (bite or headbutt).
    for f, a in ((1, 0.0), (6, -1.0), (10, 1.0), (16, 0.5), (21, 0.0)):
        quad_legs(k, f, 30 * max(a, 0) - 10 * max(-a, 0), 30 * max(a, 0) - 10 * max(-a, 0), -20 * max(a, 0), -20 * max(a, 0))
        k.rot("Spine", f, (X, 8 * max(a, 0) - 6 * max(-a, 0)))
        k.rot("Neck", f, (X, 15 * max(a, 0) - 15 * max(-a, 0)))
        k.rot("Head", f, (X, 15 * max(a, 0) - 10 * max(-a, 0)))
        tail_sway(k, f, a, 20)
        k.loc("Hips", f, (0, -0.12 * max(a, 0) + 0.05 * max(-a, 0), -0.04 * max(-a, 0)))

    k.start("Hit")
    for f, a in ((1, 0.0), (4, 1.0), (9, 0.4), (13, 0.0)):
        quad_legs(k, f, -10 * a, -10 * a, 5 * a, 5 * a)
        k.rot("Neck", f, (X, -15 * a))
        k.rot("Head", f, (X, -10 * a))
        tail_sway(k, f, a, 15)
        k.loc("Hips", f, (0, 0.06 * a, 0))


# bird

def bird_wings(k, f, spread=0.0, flap=0.0):
    # Wings hang along the sides; spreading turns the left wing out toward +X (around -Y).
    k.rot("Wing.L", f, (Y, -(10 * spread + 70 * spread * flap)))
    k.rot("Wing.R", f, (Y, 10 * spread + 70 * spread * flap))


def animate_bird(k):
    k.start("Idle")
    for f, p in cycle(60):
        breathe = (1 - math.cos(p)) / 2
        k.rot("Spine", f, (X, -2 * breathe))
        k.rot("Neck", f, (Z, 12 * math.sin(p)))
        k.rot("Head", f, (Z, 15 * math.sin(p + 0.5)), (Y, 8 * math.sin(2 * p)))
        bird_wings(k, f, 0.2 * breathe)
        k.rot("Tail", f, (X, -6 * math.sin(2 * p)))
        k.loc("Hips", f, (0, 0, -0.008 * breathe))

    k.start("Walk")
    for f, p in cycle(20):
        s = math.sin(p)
        k.rot("Thigh.L", f, (X, -30 * s))
        k.rot("Thigh.R", f, (X, 30 * s))
        k.rot("Shin.L", f, (X, -20 * max(0.0, s)))
        k.rot("Shin.R", f, (X, -20 * max(0.0, -s)))
        k.rot("Hips", f, (Y, 6 * s))
        k.rot("Neck", f, (X, 10 * math.cos(2 * p)))
        bird_wings(k, f, 0.15)
        k.rot("Tail", f, (Z, 8 * s))
        k.loc("Hips", f, (0, 0, 0.02 * abs(math.cos(p))))

    k.start("Attack")
    # Wings spread and beat while the head strikes forward (peck).
    for f, spread, flap, peck in ((1, 0, 0, 0), (5, 1, 1, -0.5), (8, 1, 0, 0.3), (11, 1, 1, 1), (15, 1, 0, 0.6), (21, 0, 0, 0)):
        bird_wings(k, f, spread, flap)
        k.rot("Spine", f, (X, 12 * max(peck, 0) - 8 * max(-peck, 0)))
        k.rot("Neck", f, (X, 25 * max(peck, 0) - 15 * max(-peck, 0)))
        k.rot("Head", f, (X, 15 * max(peck, 0)))
        k.rot("Tail", f, (X, -15 * spread))
        k.loc("Hips", f, (0, -0.05 * max(peck, 0), 0.03 * spread))

    k.start("Hit")
    for f, a in ((1, 0.0), (4, 1.0), (9, 0.4), (13, 0.0)):
        k.rot("Spine", f, (X, -12 * a))
        k.rot("Neck", f, (X, -15 * a))
        bird_wings(k, f, 0.6 * a, 0.5)
        k.rot("Tail", f, (X, 10 * a))
        k.loc("Hips", f, (0, 0.04 * a, 0))


# insect

def insect_wings(k, f, flap, upright):
    if upright:
        # Butterfly: wings fold back toward each other (left wing turns toward +Y around +Z).
        k.rot("Wing.L", f, (Z, 60 * flap))
        k.rot("Wing.R", f, (Z, -60 * flap))
    else:
        # Bee: wings beat up and down (left wing up = around -Y).
        k.rot("Wing.L", f, (Y, -35 * flap))
        k.rot("Wing.R", f, (Y, 35 * flap))


def animate_insect(k, upright):
    beats = 3 if upright else 8  # wing beats per second-ish cycle; butterflies beat slowly

    def flapping(length, body_turns, lift):
        k_frames = beats * 2
        for i in range(k_frames + 1):
            f = 1 + round(i * length / k_frames)
            flap = 1.0 if i % 2 else 0.0
            insect_wings(k, f, flap, upright)
        for f, p in cycle(length):
            k.rot("Body", f, *body_turns(p))
            k.loc("Body", f, (0, 0, lift * math.sin(p)))

    k.start("Idle")
    flapping(30, lambda p: ((X, 3 * math.sin(p)),), 0.03)

    k.start("Walk")
    flapping(30, lambda p: ((X, -12 if upright else 12), (Z, 4 * math.sin(p))), 0.04)

    k.start("Attack")
    for f, a in ((1, 0.0), (6, -1.0), (10, 1.0), (16, 0.4), (21, 0.0)):
        k.rot("Body", f, (X, (20 if not upright else -20) * max(a, 0) - 10 * max(-a, 0)))
        k.loc("Body", f, (0, -0.25 * max(a, 0) + 0.08 * max(-a, 0), 0.08 * max(-a, 0) - 0.1 * max(a, 0)))
        k.rot("Head", f, (X, 10 * max(a, 0)))
    for i in range(9):
        insect_wings(k, 1 + i * 20 // 8, 1.0 if i % 2 else 0.0, upright)

    k.start("Hit")
    for f, a in ((1, 0.0), (4, 1.0), (9, 0.4), (13, 0.0)):
        k.rot("Body", f, (X, -15 * a), (Z, 10 * a))
        k.loc("Body", f, (0, 0.1 * a, -0.05 * a))
        insect_wings(k, f, 0.3 * a, upright)


# snake

def animate_snake(k):
    necks = ("Neck1", "Neck2", "Neck3", "Head")

    def sway(f, p, amount, lean=0.0):
        for i, name in enumerate(necks):
            k.rot(name, f, (Z, amount * math.sin(p - i * 0.9)), (X, lean))

    k.start("Idle")
    for f, p in cycle(60):
        sway(f, p, 6)
        k.rot("Coil", f, (Z, 2 * math.sin(p)))

    k.start("Walk")
    for f, p in cycle(24):
        sway(f, p, 14)
        k.rot("Coil", f, (Z, 5 * math.sin(p)))
        k.loc("Coil", f, (0.02 * math.sin(p), 0, 0))

    k.start("Attack")
    # Draws back into an S, then strikes forward.
    for f, a in ((1, 0.0), (7, -1.0), (10, 1.0), (16, 0.5), (21, 0.0)):
        for i, name in enumerate(necks):
            k.rot(name, f, (X, (18 if i < 2 else 10) * max(a, 0) - (14 if i % 2 == 0 else -6) * max(-a, 0)))
        k.rot("Coil", f, (X, 5 * max(a, 0)))

    k.start("Hit")
    for f, a in ((1, 0.0), (4, 1.0), (9, 0.4), (13, 0.0)):
        for name in necks:
            k.rot(name, f, (X, -8 * a), (Z, 6 * a))


def animate(rig, body_type, height, upright_insect):
    k = Keyer(rig, height)
    if body_type == "biped":
        animate_biped(k)
    elif body_type == "upright":
        animate_upright(k)
    elif body_type in ("quadruped", "hopper"):
        animate_quadruped(k, hopper=body_type == "hopper")
    elif body_type == "bird":
        animate_bird(k)
    elif body_type == "insect":
        animate_insect(k, upright_insect)
    elif body_type == "snake":
        animate_snake(k)


# ---------- output ----------

def export_fbx(path, body, rig):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    select_only(rig, body)
    rig.animation_data.action = None
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        global_scale=1.0 / WORK_SCALE,
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        path_mode="COPY",
        embed_textures=False,
    )


SHOTS = [
    ("rest", None, 1, (0.5, -1, 0.25)),
    ("idle", "Idle", 16, (0.5, -1, 0.25)),
    ("walk_a", "Walk", 7, (1, -0.3, 0.15)),
    ("walk_b", "Walk", 13, (1, -0.3, 0.15)),
    ("attack_wind", "Attack", 6, (0.8, -1, 0.2)),
    ("attack_hit", "Attack", 10, (0.8, -1, 0.2)),
    ("hit", "Hit", 4, (1, -0.3, 0.15)),
]


def render_previews(out_dir, rig, shape):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 320
    scene.render.resolution_y = 320
    scene.world = bpy.data.worlds.new("World")

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    size = max(shape.size)
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = size * 1.6
    target = shape.center

    for name, action, frame, direction in SHOTS:
        rig.animation_data.action = bpy.data.actions[action] if action else None
        if action is None:
            for pb in rig.pose.bones:
                pb.rotation_quaternion = Quaternion()
                pb.location = Vector()
        scene.frame_set(frame)
        d = Vector(direction).normalized()
        cam.location = target + d * size * 3
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, f"{name}.png")
        bpy.ops.render.render(write_still=True)


BUILDERS = {
    "biped": lambda s: (build_biped(s, arms=True), False),
    "upright": lambda s: (build_biped(s, arms=False), False),
    "quadruped": lambda s: (build_quadruped(s), False),
    "hopper": lambda s: (build_quadruped(s), False),
    "bird": lambda s: (build_bird(s), False),
    "insect": build_insect,
    "snake": lambda s: (build_snake(s), False),
}


def main():
    obj_path, fbx_path, body_type, preview_dir = parse_args()
    if body_type not in BUILDERS:
        raise SystemExit(f"unknown body type: {body_type}")
    reset_scene()
    body = import_model(obj_path)
    tris = decimate(body)
    shape = Shape(body)
    rig, upright_insect = BUILDERS[body_type](shape)
    weighted = bind(body, rig, shape.H)
    animate(rig, body_type, shape.H, upright_insect)
    print(f"[rig] {body_type} triangles={tris} size={tuple(round(v, 2) for v in shape.size)} weighted={weighted}/{len(body.data.vertices)} bones={[b.name for b in rig.data.bones]}")
    if preview_dir:
        render_previews(preview_dir, rig, shape)
    export_fbx(fbx_path, body, rig)
    print(f"[rig] exported {fbx_path}")


main()
