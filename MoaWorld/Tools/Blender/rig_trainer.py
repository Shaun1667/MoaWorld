# Rigs and animates a Varco3D trainer (player character) in Blender, including fingers, and exports an FBX.
# Run headless:
#   blender --background --factory-startup --python rig_trainer.py -- <obj path> <out fbx> <hair: yes|no> [preview dir]
# hair=yes adds a two-bone chain for a ponytail behind the head.
#
# The model must stand in a T-pose with open hands (fingers along the arm, palms down). Bones are fitted to the
# measured mesh. Body weights come from automatic weights on a closed copy (see rig_moa.py); hands are weighted by
# distance to the hand and finger bones, because the fingers are too close together for automatic weights.
# Actions: Idle, Walk, Run, JumpUp, Fall, Land, Throw, Summon, Hit. In Blender the model faces -Y, up is +Z and
# the character's left is +X.

import math
import os
import sys

import bpy
from mathutils import Quaternion, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import rig_moa as common  # noqa: E402  shared import, proxy weights, keying and export
from rig_moa import X, Y, Z, centroid, percentile, select_only  # noqa: E402

# The source meshes are 30k triangles; decimating them bridges neighboring fingers with long triangles that
# tear when the fingers move apart, so they are kept as they are.
TARGET_TRIANGLES = 1000000
FINGERS = ("Index", "Middle", "Ring", "Pinky")
SIDES = (("L", 1), ("R", -1))
# Relative finger widths (index .. pinky) used to cut a fused finger slab into four strips.
FUSED_FINGER_WIDTHS = (1.0, 1.05, 1.0, 0.85)


# ---------- measuring ----------

class Body:
    """Joint positions measured from the T-pose mesh."""

    def __init__(self, verts, edges):
        self.verts = verts
        self.neighbors = [[] for _ in verts]
        for a, b in edges:
            self.neighbors[a].append(b)
            self.neighbors[b].append(a)
        self.min = Vector((min(v.x for v in verts), min(v.y for v in verts), min(v.z for v in verts)))
        self.max = Vector((max(v.x for v in verts), max(v.y for v in verts), max(v.z for v in verts)))
        self.H = self.max.z - self.min.z
        self.floor = self.min.z
        self.half_w = max(abs(v.x) for v in verts)
        self.cx = (self.min.x + self.max.x) / 2

        self._arms()
        self._torso()
        self._legs()
        self.hands = {side: self._hand(sign) for side, sign in SIDES}

    def z(self, f):
        return self.floor + f * self.H

    def components(self, indices):
        """Connected pieces of the mesh made of the given vertices."""
        inside, seen, pieces = set(indices), set(), []
        for start in indices:
            if start in seen:
                continue
            piece, stack = [], [start]
            seen.add(start)
            while stack:
                i = stack.pop()
                piece.append(i)
                for n in self.neighbors[i]:
                    if n in inside and n not in seen:
                        seen.add(n)
                        stack.append(n)
            pieces.append(piece)
        return pieces

    def grow_together(self, pieces, allowed, stop_x):
        """Breadth-first growth of several named pieces through `allowed` vertices past stop_x."""
        inside = {i for i in allowed if abs(self.verts[i].x - self.cx) > stop_x}
        owner = {}
        frontier = []
        for name, piece in pieces.items():
            for i in piece:
                owner[i] = name
                frontier.append(i)
        while frontier:
            nxt = []
            for i in frontier:
                for n in self.neighbors[i]:
                    if n in inside and n not in owner:
                        owner[n] = owner[i]
                        nxt.append(n)
            frontier = nxt
        return owner

    def _arms(self):
        # Arms are the widest part of a T-pose; the arm line is the median height of the mid-arm.
        mid_arm = [v for v in self.verts if 0.45 * self.half_w < abs(v.x - self.cx) < 0.7 * self.half_w]
        self.arm_z = percentile([v.z for v in mid_arm], 0.5)
        self.arm_y = percentile([v.y for v in mid_arm], 0.5)

    def _torso(self):
        chest = [v for v in self.verts if self.arm_z - 0.12 * self.H < v.z < self.arm_z - 0.05 * self.H]
        self.torso_hw = percentile([abs(v.x - self.cx) for v in chest], 0.95)
        self.body_y = percentile([v.y for v in chest], 0.5)
        self.shoulder_x = self.torso_hw * 0.85

    def _legs(self):
        # Crotch: highest height where nothing crosses the middle line (the gap between the legs).
        self.crotch_z = self.z(0.3)
        for i in range(60):
            z = self.z(0.15 + i * 0.006)
            band = [v for v in self.verts if abs(v.z - z) < 0.004 * self.H]
            if band and not any(abs(v.x - self.cx) < 0.012 * self.H for v in band):
                self.crotch_z = z
        self.hip_z = self.crotch_z + 0.05 * self.H
        self.legs = {}
        for side, sign in SIDES:
            shin = [v for v in self.verts if self.z(0.15) < v.z < self.z(0.3) and (v.x - self.cx) * sign > 0]
            foot = [v for v in self.verts if v.z < self.z(0.04) and (v.x - self.cx) * sign > 0]
            leg = centroid(shin)
            toe_y = percentile([v.y for v in foot], 0.03)
            heel_y = percentile([v.y for v in foot], 0.97)
            self.legs[side] = (leg.x, leg.y, toe_y, heel_y)

    def _hand(self, sign):
        arm_idx = [i for i, v in enumerate(self.verts) if (v.x - self.cx) * sign > self.torso_hw and abs(v.z - self.arm_z) < 0.08 * self.H]
        arm = [self.verts[i] for i in arm_idx]
        tip_x = max(abs(v.x - self.cx) for v in arm)

        # Wrist: thinnest point (front-to-back) near the end of the arm; the palm is wider again.
        def depth(x0, x1):
            s = [v.y for v in arm if x0 <= abs(v.x - self.cx) < x1]
            return (max(s) - min(s)) if len(s) > 5 else 1e9

        # Searched where a hand of 10-17% of the height would end, so it never lands up on the forearm.
        step = 0.01 * self.half_w
        wrist_x = tip_x - 0.13 * self.H
        best = 1e9
        x = tip_x - 0.17 * self.H
        while x < tip_x - 0.10 * self.H:
            d = depth(x, x + step)
            if d < best:
                best, wrist_x = d, x
            x += step
        hand_idx = [i for i in arm_idx if abs(self.verts[i].x - self.cx) > wrist_x]
        hand = [self.verts[i] for i in hand_idx]
        hand_len = tip_x - wrist_x

        # Knuckles: where the hand splits into separate fingers along Y.
        def clusters(x0, x1):
            ys = sorted(v.y for v in hand if x0 <= abs(v.x - self.cx) < x1)
            if not ys:
                return 0
            gaps = sum(1 for a, b in zip(ys, ys[1:]) if b - a > 0.003 * self.H)
            return gaps + 1

        knuckle_x = wrist_x + 0.45 * hand_len
        x = wrist_x + 0.25 * hand_len
        while x < tip_x - 0.2 * hand_len:
            if clusters(x, x + step) >= 4:
                knuckle_x = x
                break
            x += step

        # Fingers: past the knuckles each finger is its own connected piece of mesh. The four pieces that reach
        # farthest are the fingers (front -Y to back: index .. pinky); the thumb is shorter and sits in front.
        cut = knuckle_x + 0.2 * (tip_x - knuckle_x)
        pieces = self.components([i for i in hand_idx if abs(self.verts[i].x - self.cx) > cut])
        pieces = [p for p in pieces if len(p) >= 10]
        pieces.sort(key=lambda p: max(abs(self.verts[i].x - self.cx) for i in p), reverse=True)
        finger_pieces = sorted(pieces[:4], key=lambda p: centroid([self.verts[i] for i in p]).y)
        fingers, owner = {}, {}
        fused = None if len(finger_pieces) == 4 else self._split_fused(hand_idx, wrist_x, knuckle_x, tip_x, hand_len)
        if fused is not None:
            fingers, owner, root, thumb_tip = fused
            print(f"[rig] hand {sign:+d}: fused fingers, separate thumb, owned verts={len(owner)}")
            return {"wrist_x": wrist_x, "knuckle_x": knuckle_x, "tip_x": tip_x, "fingers": fingers,
                    "thumb_root": root, "thumb_tip": thumb_tip, "hand_len": hand_len, "owner": owner}
        if len(finger_pieces) == 4:
            for name, piece in zip(FINGERS, finger_pieces):
                pts = [self.verts[i] for i in piece]
                c = centroid(pts)
                fingers[name] = (c.y, c.z, max(abs(v.x - self.cx) for v in pts))
            # Grow all pieces back toward the knuckles at the same time; each vertex goes to the finger that
            # reaches it first, so the webbing between fingers is split instead of taken by one finger.
            owner = self.grow_together({n: p for n, p in zip(FINGERS, finger_pieces)}, hand_idx, knuckle_x)
        else:
            # Fingers touch in the mesh: fall back to splitting a slice of the finger section by Y.
            band = sorted([v for v in hand if knuckle_x + 0.25 * (tip_x - knuckle_x) <= abs(v.x - self.cx) <= knuckle_x + 0.55 * (tip_x - knuckle_x)], key=lambda v: v.y)

            def describe(group):
                c = centroid(group)
                spacing = 0.5 * max(0.004 * self.H, (max(v.y for v in group) - min(v.y for v in group)))
                reach = [v for v in hand if abs(v.y - c.y) < spacing and abs(v.x - self.cx) > knuckle_x]
                return (c.y, c.z, max(abs(v.x - self.cx) for v in reach) if reach else tip_x)

            # The thumb tip can reach into the slice: split in five, and if the front group is clearly shorter
            # than the others it is the thumb and is dropped.
            five = [describe(g) for g in split_1d(band, 5)]
            others = sorted(d[2] for d in five[1:])
            if five[0][2] < others[len(others) // 2] - 0.15 * (tip_x - knuckle_x):
                chosen = five[1:]
            else:
                chosen = [describe(g) for g in split_1d(band, 4)]
            for name, d in zip(FINGERS, chosen):
                fingers[name] = d
            # Fingers lie side by side along the arm, so the hand is cut into strips along Y halfway between
            # finger centers; each vertex past the knuckles belongs to the finger of its strip.
            ys = [fingers[n][0] for n in FINGERS]
            cuts = [(a + b) / 2 for a, b in zip(ys, ys[1:])]
            for i in hand_idx:
                v = self.verts[i]
                if abs(v.x - self.cx) < knuckle_x - 0.05 * hand_len:
                    continue
                strip = sum(1 for cut_y in cuts if v.y > cut_y)
                owner[i] = FINGERS[strip]
        print(f"[rig] hand {sign:+d}: finger pieces={len(finger_pieces)} of {len(pieces)} owned verts={len(owner)}")
        index_y = fingers["Index"][0]
        thumb = [v for v in hand if v.y < index_y - 0.006 * self.H and abs(v.x - self.cx) < knuckle_x + 0.35 * (tip_x - knuckle_x)]
        root = Vector((self.cx + sign * (wrist_x + 0.12 * hand_len), self.arm_y - 0.004 * self.H, self.arm_z - 0.004 * self.H))
        thumb_tip = max(thumb, key=lambda v: (v - root).length) if thumb else root + Vector((sign * 0.4 * hand_len, -0.3 * hand_len, -0.1 * hand_len))
        for i in hand_idx:
            v = self.verts[i]
            if v.y < index_y - 0.006 * self.H and abs(v.x - self.cx) < knuckle_x + 0.35 * (tip_x - knuckle_x) and abs(v.x - self.cx) > wrist_x + 0.05 * hand_len:
                owner[i] = "Thumb"
        return {"wrist_x": wrist_x, "knuckle_x": knuckle_x, "tip_x": tip_x, "fingers": fingers,
                "thumb_root": root, "thumb_tip": thumb_tip, "hand_len": hand_len, "owner": owner}

    def _split_fused(self, hand_idx, wrist_x, knuckle_x, tip_x, hand_len):
        """For hands whose four fingers are one fused slab of mesh (only shallow grooves between them) and whose
        thumb is a separate tube. Returns (fingers, owner, thumb_root, thumb_tip) or None if the hand is not
        shaped like that."""
        span = tip_x - knuckle_x

        def reach(piece):
            return max(abs(self.verts[i].x - self.cx) for i in piece)

        # Cut the finger section at increasing distances until it falls apart into exactly two pieces:
        # the shorter one is the thumb, the other is the finger slab.
        thumb_piece = slab_piece = None
        for f in (0.25, 0.3, 0.35, 0.4, 0.45, 0.5, 0.55, 0.6):
            cut = knuckle_x + f * span
            pieces = [p for p in self.components([i for i in hand_idx if abs(self.verts[i].x - self.cx) > cut]) if len(p) >= 10]
            if len(pieces) == 2:
                thumb_piece, slab_piece = sorted(pieces, key=reach)
                break
        if thumb_piece is None or reach(thumb_piece) > knuckle_x + 0.9 * span:
            return None

        # Grow both back toward the wrist together, so the thumb takes its side of the palm.
        grown = self.grow_together({"Thumb": thumb_piece, "Slab": slab_piece}, hand_idx, wrist_x + 0.05 * hand_len)
        slab = [i for i, n in grown.items() if n == "Slab"]
        mid = [self.verts[i] for i in slab if knuckle_x + 0.3 * span <= abs(self.verts[i].x - self.cx) <= knuckle_x + 0.7 * span]
        thumb = [self.verts[i] for i, n in grown.items() if n == "Thumb"]
        if len(mid) < 20 or len(thumb) < 10:
            return None

        # Cut the slab across its width (front -Y = index side) in proportion to typical finger widths.
        y0 = percentile([v.y for v in mid], 0.02)
        y1 = percentile([v.y for v in mid], 0.98)
        total = sum(FUSED_FINGER_WIDTHS)
        edges, acc = [y0], 0.0
        for w in FUSED_FINGER_WIDTHS:
            acc += w
            edges.append(y0 + (y1 - y0) * acc / total)

        def strip(y):
            return FINGERS[sum(1 for e in edges[1:-1] if y > e)]

        owner = {}
        for i, n in grown.items():
            if n == "Thumb":
                owner[i] = "Thumb"
            elif abs(self.verts[i].x - self.cx) >= knuckle_x - 0.05 * hand_len:
                owner[i] = strip(self.verts[i].y)

        fingers = {}
        for k, name in enumerate(FINGERS):
            band = [v for v in mid if edges[k] <= v.y <= edges[k + 1]]
            members = [self.verts[i] for i in slab if owner.get(i) == name]
            if not band or not members:
                return None
            c = centroid(band)
            fingers[name] = (c.y, c.z, max(abs(v.x - self.cx) for v in members))

        # Thumb bone: from the end that joins the palm to the farthest point of the thumb.
        by_reach = sorted(thumb, key=lambda v: abs(v.x - self.cx))
        root = centroid(by_reach[:max(5, len(thumb) // 6)])
        thumb_tip = max(thumb, key=lambda v: (v - root).length)
        return fingers, owner, root, thumb_tip


def split_1d(items, k):
    """Splits items (sorted by y) into k groups at the k-1 largest gaps."""
    if len(items) < k:
        return [items] * k
    gaps = sorted(range(1, len(items)), key=lambda i: items[i].y - items[i - 1].y, reverse=True)[:k - 1]
    cuts = sorted(gaps)
    groups, start = [], 0
    for c in cuts:
        groups.append(items[start:c])
        start = c
    groups.append(items[start:])
    return groups


# ---------- armature ----------

def build_rig(b, with_hair):
    rb = common.RigBuilder.__new__(common.RigBuilder)
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    rb.rig = bpy.context.object
    rb.rig.name = "Armature"
    rb.arm = rb.rig.data
    rb.arm.edit_bones.remove(rb.arm.edit_bones[0])
    cx, by, H = b.cx, b.body_y, b.H
    V = Vector

    rb.bone("Root", V((cx, by, b.floor)), V((cx, by - 0.1 * H, b.floor)), deform=False)
    chest_z = b.arm_z - 0.03 * H
    neck_z = b.arm_z + 0.045 * H
    rb.bone("Hips", V((cx, by, b.hip_z)), V((cx, by, b.hip_z + 0.08 * H)), "Root")
    rb.bone("Spine", V((cx, by, b.hip_z + 0.08 * H)), V((cx, by, (b.hip_z + chest_z) / 2 + 0.05 * H)), "Hips")
    rb.bone("Chest", V((cx, by, (b.hip_z + chest_z) / 2 + 0.05 * H)), V((cx, by, chest_z)), "Spine")
    rb.bone("Neck", V((cx, by, chest_z)), V((cx, by, neck_z)), "Chest")
    rb.bone("Head", V((cx, by, neck_z)), V((cx, by, neck_z + 0.6 * (b.max.z - neck_z))), "Neck")

    for side, sign in SIDES:
        h = b.hands[side]
        sx = cx + sign * b.shoulder_x
        wx = cx + sign * h["wrist_x"]
        ex = (sx + wx) / 2
        rb.bone(f"Shoulder.{side}", V((cx + sign * 0.2 * b.shoulder_x, by, b.arm_z)), V((sx, by, b.arm_z)), "Chest")
        rb.bone(f"UpperArm.{side}", V((sx, b.arm_y, b.arm_z)), V((ex, b.arm_y + 0.004 * H, b.arm_z)), f"Shoulder.{side}")
        rb.bone(f"LowerArm.{side}", V((ex, b.arm_y + 0.004 * H, b.arm_z)), V((wx, b.arm_y, b.arm_z)), f"UpperArm.{side}")
        kx = cx + sign * h["knuckle_x"]
        rb.bone(f"Hand.{side}", V((wx, b.arm_y, b.arm_z)), V((kx, b.arm_y, b.arm_z)), f"LowerArm.{side}")
        for name, (fy, fz, ftip) in h["fingers"].items():
            root = V((kx, fy, fz))
            tip = V((cx + sign * ftip * 0.985, fy, fz))
            pts = [root, root.lerp(tip, 0.45), root.lerp(tip, 0.75), tip]
            rb.chain([f"{name}1.{side}", f"{name}2.{side}", f"{name}3.{side}"], pts, f"Hand.{side}")
        troot, ttip = h["thumb_root"], h["thumb_tip"]
        tpts = [troot, troot.lerp(ttip, 0.4), troot.lerp(ttip, 0.72), troot.lerp(ttip, 0.97)]
        rb.chain([f"Thumb1.{side}", f"Thumb2.{side}", f"Thumb3.{side}"], tpts, f"Hand.{side}")

        lx, ly, toe_y, heel_y = b.legs[side]
        ankle_z = b.z(0.055)
        knee_z = (b.hip_z + ankle_z) / 2
        ankle_y = heel_y + 0.25 * (toe_y - heel_y)
        ball_y = heel_y + 0.72 * (toe_y - heel_y)
        rb.bone(f"Thigh.{side}", V((lx, by, b.hip_z)), V((lx, ly - 0.006 * H, knee_z)), "Hips")
        rb.bone(f"Shin.{side}", V((lx, ly - 0.006 * H, knee_z)), V((lx, ankle_y, ankle_z)), f"Thigh.{side}")
        rb.bone(f"Foot.{side}", V((lx, ankle_y, ankle_z)), V((lx, ball_y, b.z(0.015))), f"Shin.{side}")
        rb.bone(f"Toes.{side}", V((lx, ball_y, b.z(0.015))), V((lx, toe_y, b.z(0.012))), f"Foot.{side}")

    # Ponytail or other long hair hanging behind the head (tall enough to be more than the back of a cap).
    head_back = [v for v in b.verts if v.z > neck_z - 0.12 * H and v.y > by + 0.06 * H and abs(v.x - cx) < 0.12 * H]
    tall = head_back and max(v.z for v in head_back) - min(v.z for v in head_back) > 0.1 * H
    if with_hair and len(head_back) > 40 and tall:
        top = max(head_back, key=lambda v: v.z)
        end = max(head_back, key=lambda v: v.y - v.z * 0.3)
        attach = V((cx, (by + top.y) / 2, top.z - 0.02 * H))
        rb.chain(["Hair1", "Hair2"], [attach, attach.lerp(end, 0.5), end], "Head")

    bpy.ops.object.mode_set(mode="OBJECT")
    return rb.rig


# ---------- weights ----------

def weight_hands(body, rig, b):
    """Hands and fingers get weights by distance to their bones (closest two, smoothly mixed),
    faded in from the forearm so the wrist bends smoothly."""
    bones = {bn.name: bn for bn in rig.data.bones}
    for side, sign in SIDES:
        h = b.hands[side]
        names = [f"Hand.{side}", f"LowerArm.{side}"] + [n for n in bones if n.endswith("." + side) and
                                                       any(n.startswith(f) for f in FINGERS + ("Thumb",))]
        groups = {n: body.vertex_groups.get(n) or body.vertex_groups.new(name=n) for n in names}
        fade_start = h["wrist_x"] - 0.03 * b.H
        fade_end = h["wrist_x"] + 0.01 * b.H
        for v in body.data.vertices:
            reach = (v.co.x - b.cx) * sign
            if reach < fade_start:
                continue
            t = min(1.0, (reach - fade_start) / (fade_end - fade_start))
            # Vertices of a finger (by mesh connectivity) only follow that finger's bones and the hand.
            finger = h["owner"].get(v.index)
            candidates = [n for n in names if n.startswith(finger) or n.startswith("Hand.")] if finger else names
            dists = []
            for n in candidates:
                bone = bones[n]
                seg = bone.tail_local - bone.head_local
                u = max(0.0, min(1.0, (v.co - bone.head_local).dot(seg) / max(seg.length_squared, 1e-9)))
                dists.append(((v.co - (bone.head_local + seg * u)).length, n))
            dists.sort()
            d0, n0 = dists[0]
            # The second bone must be on the same finger (or the hand/forearm); mixing two fingers tears the
            # skin between them when one curls and the other stays straight.
            chain = n0.rstrip("123.LR")
            d1, n1 = next(((d, n) for d, n in dists[1:] if n.rstrip("123.LR") == chain
                           or n.startswith("Hand.") or n.startswith("LowerArm.")), dists[1])
            w0 = 1.0 / (d0 ** 4 + 1e-6)
            w1 = 1.0 / (d1 ** 4 + 1e-6)
            local = {n0: w0 / (w0 + w1), n1: w1 / (w0 + w1)}
            # Mix with the automatic weights the vertex already has (only near the wrist).
            for g in list(v.groups):
                name = body.vertex_groups[g.group].name
                g.weight *= (1.0 - t)
            for n, w in local.items():
                groups[n].add([v.index], w * t, "ADD")
    select_only(body)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)


# ---------- animation ----------

def set_world(k, bone, f, q_world):
    """Keys a bone turn given as a rotation in world axes (as seen in the rest pose)."""
    pb = k.rig.pose.bones[bone]
    rest = pb.bone.matrix_local.to_quaternion()
    pb.rotation_quaternion = rest.inverted() @ q_world @ rest
    pb.keyframe_insert("rotation_quaternion", frame=f)


def rest_dir(k, bone):
    b = k.rig.data.bones[bone]
    return (b.tail_local - b.head_local).normalized()


def arm_dir(sign, out, fwd):
    """Arm direction from angles measured from hanging straight down: `out` raises it sideways (90 = T-pose),
    `fwd` swings it forward (90 = pointing ahead, 180 = straight up, negative = behind). Negative `out` crosses
    in front of the body."""
    o, w = math.radians(out), math.radians(fwd)
    return Vector((sign * math.sin(o) * math.cos(w), -math.sin(w), -math.cos(o) * math.cos(w))).normalized()


def arm_pose(k, f, side, out=8.0, fwd=6.0, elbow=12.0):
    """Points the upper arm along arm_dir and bends the elbow by `elbow` degrees toward the front
    (or upward when the arm already points forward)."""
    sign = 1 if side == "L" else -1
    upper = arm_dir(sign, out, fwd)
    q_upper = rest_dir(k, f"UpperArm.{side}").rotation_difference(upper)
    set_world(k, f"UpperArm.{side}", f, q_upper)

    hint = Vector((0, 0, 1)) if abs(upper.y) > 0.7 else Vector((0, -1, 0))
    axis = upper.cross(hint)
    if axis.length < 1e-4:
        axis = Vector((sign, 0, 0)).cross(upper)
    forearm = Quaternion(axis.normalized(), math.radians(elbow)) @ upper
    q_fore = rest_dir(k, f"LowerArm.{side}").rotation_difference(q_upper.inverted() @ forearm)
    set_world(k, f"LowerArm.{side}", f, q_fore)


def hand_pose(k, f, side, curl=20.0, index=None, thumb=None):
    """Finger curl in degrees per joint (index can differ, e.g. pointing)."""
    sign = 1 if side == "L" else -1
    for name in FINGERS:
        c = index if (name == "Index" and index is not None) else curl * (1.0 + 0.15 * FINGERS.index(name))
        for j in (1, 2, 3):
            k.rot(f"{name}{j}.{side}", f, (Y, sign * c * (1.0 if j == 1 else 1.1)))
    t = curl * 0.7 if thumb is None else thumb
    for j in (1, 2, 3):
        k.rot(f"Thumb{j}.{side}", f, (Z, sign * t * 0.6), (Y, sign * t * 0.5))


def legs(k, f, side, thigh=0.0, knee=0.0, foot=0.0, spread=0.0):
    sign = 1 if side == "L" else -1
    k.rot(f"Thigh.{side}", f, (X, -thigh), (Y, sign * spread))
    k.rot(f"Shin.{side}", f, (X, knee))
    k.rot(f"Foot.{side}", f, (X, -foot))


def hair(k, f, sway):
    k.rot("Hair1", f, (X, -6 * sway), (Z, 4 * sway))
    k.rot("Hair2", f, (X, -10 * sway))


def cycle(length, keys=8):
    return [(1 + round(i * length / keys), i / keys * 2 * math.pi) for i in range(keys + 1)]


def animate(rig, height):
    k = common.Keyer(rig, height)

    k.start("Idle")
    for f, p in cycle(90, 4):
        breathe = (1 - math.cos(p)) / 2
        k.rot("Spine", f, (X, -1.5 * breathe))
        k.rot("Chest", f, (X, -1.5 * breathe))
        k.rot("Head", f, (X, 2 * math.sin(p)), (Z, 3 * math.sin(p * 0.5)))
        for side in ("L", "R"):
            arm_pose(k, f, side, out=9, fwd=5 + 2 * breathe, elbow=14)
            hand_pose(k, f, side, curl=18)
            legs(k, f, side)
        k.loc("Hips", f, (0.004 * math.sin(p), 0, -0.004 * breathe))
        hair(k, f, math.sin(p))

    k.start("Walk")
    for f, p in cycle(32):
        s, c = math.sin(p), math.cos(p)
        legs(k, f, "L", thigh=25 * s, knee=8 + 30 * max(0.0, -s), foot=5 * s)
        legs(k, f, "R", thigh=-25 * s, knee=8 + 30 * max(0.0, s), foot=-5 * s)
        arm_pose(k, f, "L", out=9, fwd=-20 * s, elbow=16)
        arm_pose(k, f, "R", out=9, fwd=20 * s, elbow=16)
        hand_pose(k, f, "L", curl=22)
        hand_pose(k, f, "R", curl=22)
        k.rot("Spine", f, (Z, 4 * s), (X, 3))
        k.rot("Head", f, (Z, -3 * s))
        k.loc("Hips", f, (0, 0, 0.012 * abs(c)))
        hair(k, f, c)

    k.start("Run")
    for f, p in cycle(20):
        s, c = math.sin(p), math.cos(p)
        legs(k, f, "L", thigh=40 * s, knee=15 + 70 * max(0.0, -s), foot=10 * s)
        legs(k, f, "R", thigh=-40 * s, knee=15 + 70 * max(0.0, s), foot=-10 * s)
        arm_pose(k, f, "L", out=12, fwd=10 - 40 * s, elbow=80)
        arm_pose(k, f, "R", out=12, fwd=10 + 40 * s, elbow=80)
        hand_pose(k, f, "L", curl=45)
        hand_pose(k, f, "R", curl=45)
        k.rot("Spine", f, (Z, 7 * s), (X, 10))
        k.rot("Head", f, (X, -6), (Z, -5 * s))
        k.loc("Hips", f, (0, 0, 0.025 * abs(c) - 0.01))
        hair(k, f, 1.5 * c)

    k.start("JumpUp")
    for f, crouch, rise in ((1, 0, 0), (4, 1, 0), (8, 0, 1), (12, 0, 0.6)):
        for side in ("L", "R"):
            legs(k, f, side, thigh=35 * crouch + 25 * rise * 0.6, knee=60 * crouch + 50 * rise * 0.6, foot=-15 * crouch)
            arm_pose(k, f, side, out=15, fwd=-35 * crouch + 120 * rise, elbow=20 + 25 * rise)
            hand_pose(k, f, side, curl=25)
        k.rot("Spine", f, (X, 15 * crouch - 5 * rise))
        k.loc("Hips", f, (0, 0, -0.06 * crouch))
        hair(k, f, -2 * rise + crouch)

    k.start("Fall")
    for f, p in cycle(20, 4):
        s = math.sin(p)
        for side, sg in (("L", 1), ("R", -1)):
            legs(k, f, side, thigh=15 + 5 * s * sg, knee=35 + 5 * s)
            arm_pose(k, f, side, out=50 + 6 * s, fwd=25, elbow=30)
            hand_pose(k, f, side, curl=15)
        k.rot("Spine", f, (X, -3))
        hair(k, f, 2.5)

    k.start("Land")
    for f, a in ((1, 0.6), (4, 1.0), (10, 0.0)):
        for side in ("L", "R"):
            legs(k, f, side, thigh=35 * a, knee=65 * a, foot=-20 * a)
            arm_pose(k, f, side, out=12 + 15 * a, fwd=6 + 25 * a, elbow=15 + 20 * a)
            hand_pose(k, f, side, curl=22)
        k.rot("Spine", f, (X, 12 * a))
        k.loc("Hips", f, (0, 0, -0.07 * a))
        hair(k, f, -1.5 * a)

    k.start("Throw")
    # Quick wind-up, release around frame 7 (0.2 s) so the arm matches the ball leaving, then follow-through.
    keys = ((1, 0.0, 0.0), (5, 1.0, 0.0), (7, 0.2, 1.0), (11, 0.0, 0.6), (20, 0.0, 0.0))
    for f, wind, release in keys:
        k.rot("Spine", f, (Z, -25 * wind + 20 * release), (X, -6 * wind + 12 * release))
        k.rot("Chest", f, (Z, -10 * wind + 10 * release))
        # Right arm: raised behind the head on the wind-up, whips over the shoulder on release.
        arm_pose(k, f, "R", out=10 + 20 * wind + 5 * release, fwd=6 - 146 * wind + 94 * release,
                 elbow=14 + 90 * wind + 6 * release)
        hand_pose(k, f, "R", curl=45 * wind + 15 * (1 - release), thumb=40 * wind)
        # Left arm points ahead for aim.
        arm_pose(k, f, "L", out=10, fwd=6 + 74 * wind + 20 * release, elbow=14)
        hand_pose(k, f, "L", curl=10)
        legs(k, f, "L", thigh=20 * wind + 10 * release, knee=10 * wind)
        legs(k, f, "R", thigh=-10 * wind, knee=15 * release)
        hair(k, f, 1.5 * release - wind)

    k.start("Summon")
    # Points forward with the index finger to send the moa out.
    for f, a in ((1, 0.0), (6, 1.0), (16, 1.0), (24, 0.0)):
        arm_pose(k, f, "R", out=9 - 4 * a, fwd=5 + 80 * a, elbow=14 - 11 * a)
        # Other fingers only half close: the source mesh joins neighboring fingers, so a big difference between
        # the straight index and its neighbor stretches the skin between them.
        hand_pose(k, f, "R", curl=18 + 22 * a, index=18 - 18 * a, thumb=12 + 25 * a)
        arm_pose(k, f, "L", out=9, fwd=5, elbow=14 + 20 * a)
        hand_pose(k, f, "L", curl=18 + 20 * a)
        k.rot("Spine", f, (Z, 10 * a), (X, 4 * a))
        k.rot("Head", f, (X, 4 * a))
        legs(k, f, "L", thigh=8 * a)
        legs(k, f, "R")

    k.start("Hit")
    for f, a in ((1, 0.0), (3, 1.0), (8, 0.4), (12, 0.0)):
        k.rot("Spine", f, (X, -12 * a))
        k.rot("Head", f, (X, -12 * a))
        for side in ("L", "R"):
            arm_pose(k, f, side, out=9 + 15 * a, fwd=5 - 15 * a, elbow=14 + 25 * a)
            hand_pose(k, f, side, curl=18 + 25 * a)
            legs(k, f, side, knee=10 * a)
        k.loc("Hips", f, (0, 0.03 * a, 0))
        hair(k, f, 1.5 * a)


# ---------- previews ----------

SHOTS = [
    ("rest", None, 1, (0.4, -1, 0.2), 1.1),
    ("idle", "Idle", 30, (0.4, -1, 0.2), 1.1),
    ("walk", "Walk", 9, (1, -0.4, 0.15), 1.1),
    ("run", "Run", 6, (1, -0.4, 0.15), 1.1),
    ("jump", "JumpUp", 8, (1, -0.6, 0.2), 1.2),
    ("fall", "Fall", 6, (1, -0.6, 0.2), 1.2),
    ("throw_wind", "Throw", 5, (1, 0.7, 0.3), 1.2),
    ("throw_release", "Throw", 7, (1, -0.8, 0.25), 1.2),
    ("summon", "Summon", 12, (0.9, -1, 0.2), 1.2),
    ("hand_idle", "Idle", 30, (0.2, -1, 0.6), 0.22),
    ("hand_point", "Summon", 12, (0.3, -1, 0.4), 0.22),
]


def render_previews(out_dir, rig, b):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 320
    scene.render.resolution_y = 320
    scene.world = bpy.data.worlds.new("World")
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    center = (b.min + b.max) / 2
    for name, action, frame, direction, scale in SHOTS:
        rig.animation_data.action = bpy.data.actions[action] if action else None
        if action is None:
            for pb in rig.pose.bones:
                pb.rotation_quaternion = (1, 0, 0, 0)
                pb.location = (0, 0, 0)
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        target = center
        if name.startswith("hand"):
            target = rig.matrix_world @ rig.pose.bones["Hand.R"].tail
        cam_data.ortho_scale = b.H * scale
        d = Vector(direction).normalized()
        cam.location = target + d * b.H * 3
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, f"{name}.png")
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    obj_path, fbx_path, with_hair = argv[0], argv[1], argv[2] == "yes"
    preview_dir = argv[3] if len(argv) > 3 else None

    common.TARGET_TRIANGLES = TARGET_TRIANGLES
    common.reset_scene()
    body = common.import_model(obj_path)
    tris = common.decimate(body)
    b = Body([body.matrix_world @ v.co for v in body.data.vertices], [tuple(e.vertices) for e in body.data.edges])
    rig = build_rig(b, with_hair)
    weighted = common.bind(body, rig, b.H)
    weight_hands(body, rig, b)
    animate(rig, b.H)
    hands = {s: {n: round(v[2], 2) for n, v in h["fingers"].items()} for s, h in b.hands.items()}
    print(f"[rig] trainer triangles={tris} H={b.H:.2f} arm_z={b.arm_z:.2f} crotch={b.crotch_z:.2f} "
          f"wrist={b.hands['L']['wrist_x']:.2f} knuckle={b.hands['L']['knuckle_x']:.2f} bones={len(rig.data.bones)} "
          f"weighted={weighted}/{len(body.data.vertices)} fingers={hands}")
    if preview_dir:
        render_previews(preview_dir, rig, b)
    common.export_fbx(fbx_path, body, rig)
    print(f"[rig] exported {fbx_path}")


if __name__ == "__main__":
    main()
