"""
FORGEBORN workshop models, generated procedurally (no downloaded assets, so no licence to track).

Run (headless):
  "C:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" -b -P tools/blender/forge_models.py -- <out_dir> [--preview]

Writes anvil.fbx, furnace.fbx, quench_tank.fbx, grinder.fbx, handle.fbx into <out_dir>.
With --preview it also renders <out_dir>/previews/<name>.png (Workbench, for a quick look only).

Units are metres, real-world size. Z is up in Blender. Each model's origin sits on the floor, centred.
The handle is the exception: its origin is the blade joint, the blade runs along +X and the handle along -X,
matching the Domain's blade axis (x along the length).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector


def args():
    argv = sys.argv
    rest = argv[argv.index("--") + 1:] if "--" in argv else []
    out = next((a for a in rest if not a.startswith("--")), os.getcwd())
    return os.path.abspath(out), "--preview" in rest


MATS = {}


def mat(name, color, rough=0.5, metal=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    m.diffuse_color = (color[0], color[1], color[2], 1.0)
    MATS[name] = m
    return m


def steel():  return mat("Steel", (0.42, 0.43, 0.45), 0.45, 1.0)
def iron():   return mat("DarkIron", (0.16, 0.16, 0.17), 0.7, 0.9)
def brick():  return mat("Brick", (0.45, 0.20, 0.14), 0.9, 0.0)
def dark():   return mat("FurnaceInside", (0.03, 0.02, 0.02), 1.0, 0.0)
def wood():   return mat("Wood", (0.30, 0.18, 0.09), 0.8, 0.0)
def stone():  return mat("Stone", (0.55, 0.52, 0.46), 0.95, 0.0)
def leather(): return mat("Leather", (0.22, 0.12, 0.07), 0.85, 0.0)
def water():  return mat("Water", (0.10, 0.25, 0.32), 0.1, 0.0)
def ember():  return mat("Ember", (1.0, 0.35, 0.05), 0.6, 0.0)


class Builder:
    """Collects boxes, frustums, cylinders and spheres into one mesh, with one material per call."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.mat_index = {}

    def _assign(self, verts, m):
        if m.name not in self.mat_index:
            self.mat_index[m.name] = len(self.mats)
            self.mats.append(m)
        i = self.mat_index[m.name]
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = i

    def box(self, size, center, m):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        bmesh.ops.scale(self.bm, vec=size, verts=verts)
        bmesh.ops.translate(self.bm, vec=center, verts=verts)
        self._assign(verts, m)

    def frustum(self, bottom_xy, top_xy, height, center_xy, z0, m):
        """Box that narrows from bottom_xy at z0 to top_xy at z0+height."""
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        for v in verts:
            top = v.co.z > 0
            sx, sy = (top_xy if top else bottom_xy)
            v.co = Vector((v.co.x * sx + center_xy[0], v.co.y * sy + center_xy[1], z0 + (height if top else 0.0)))
        self._assign(verts, m)

    def cylinder(self, r1, r2, depth, center, axis, m, segments=24):
        """Cone/cylinder along the given axis ('X','Y','Z'), centred at 'center'."""
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                                    radius1=r1, radius2=r2, depth=depth)
        verts = res["verts"]
        if axis == "X":
            bmesh.ops.rotate(self.bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=verts)
        elif axis == "Y":
            bmesh.ops.rotate(self.bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=verts)
        bmesh.ops.translate(self.bm, vec=center, verts=verts)
        self._assign(verts, m)

    def sphere(self, radius, center, m, segments=16):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segments, v_segments=segments // 2, radius=radius)
        verts = res["verts"]
        bmesh.ops.translate(self.bm, vec=center, verts=verts)
        self._assign(verts, m)

    def quad(self, size_xy, center, m):
        sx, sy = size_xy
        x, y, z = center
        v = [self.bm.verts.new((x - sx / 2, y - sy / 2, z)), self.bm.verts.new((x + sx / 2, y - sy / 2, z)),
             self.bm.verts.new((x + sx / 2, y + sy / 2, z)), self.bm.verts.new((x - sx / 2, y + sy / 2, z))]
        self.bm.faces.new(v)
        self._assign(v, m)

    def finish(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(ob)
        return ob


# ---------------------------------------------------------------- models

def make_anvil():
    b = Builder("Anvil")
    b.box((0.38, 0.28, 0.10), (0, 0, 0.05), iron())                       # base
    b.frustum((0.24, 0.20), (0.16, 0.14), 0.22, (0, 0), 0.10, iron())      # waist
    b.box((0.52, 0.17, 0.06), (0, 0, 0.35), steel())                       # hardened face plate
    b.box((0.12, 0.15, 0.06), (-0.30, 0, 0.35), iron())                    # heel
    b.cylinder(0.065, 0.014, 0.32, (0.41, 0, 0.317), "X", iron(), 20)      # horn (top flush with the face)
    return b.finish()


def make_furnace():
    b = Builder("Furnace")
    w, d, h = 0.9, 0.8, 0.9
    ox, oz0, oz1 = 0.2, 0.25, 0.55                                         # mouth half-width, bottom, top
    cav_y1 = 0.10                                                          # cavity depth (front face is y = -d/2)
    b.box((w / 2 - ox, d, h), (-(w / 2 + ox) / 2, 0, h / 2), brick())      # left cheek
    b.box((w / 2 - ox, d, h), ((w / 2 + ox) / 2, 0, h / 2), brick())       # right cheek
    b.box((2 * ox, d, oz0), (0, 0, oz0 / 2), brick())                      # hearth block
    b.box((2 * ox, d, h - oz1), (0, 0, (oz1 + h) / 2), brick())            # lintel and top
    b.box((2 * ox, d / 2 - cav_y1, oz1 - oz0), (0, (d / 2 + cav_y1) / 2, (oz0 + oz1) / 2), dark())   # back wall
    b.quad((2 * ox - 0.02, d / 2 + cav_y1 - 0.02), (0, (-d / 2 + cav_y1) / 2, oz0 + 0.002), ember())  # ember bed
    b.box((0.30, 0.30, 0.60), (0, 0.18, h + 0.30), brick())                # chimney
    return b.finish()


def make_quench_tank():
    b = Builder("QuenchTank")
    L, W, H, t = 0.80, 0.40, 0.40, 0.035
    b.box((L, W, t), (0, 0, t / 2), wood())                                # bottom
    b.box((L, t, H), (0, -(W - t) / 2, H / 2), wood())                     # front
    b.box((L, t, H), (0, (W - t) / 2, H / 2), wood())                      # back
    b.box((t, W - 2 * t, H), (-(L - t) / 2, 0, H / 2), wood())             # left end
    b.box((t, W - 2 * t, H), ((L - t) / 2, 0, H / 2), wood())              # right end
    for x in (-0.28, 0.28):                                                # iron bands
        b.box((0.03, W + 0.01, 0.02), (x, 0, 0.10), iron())
        b.box((0.03, W + 0.01, 0.02), (x, 0, 0.30), iron())
    b.quad((L - 2 * t, W - 2 * t), (0, 0, 0.32), water())                  # liquid surface (swap the material for oil)
    return b.finish()


def make_grinder():
    b = Builder("Grinder")
    b.box((0.44, 0.26, 0.03), (0, 0, 0.015), wood())                       # base plate
    for y in (-0.10, 0.10):
        b.box((0.05, 0.04, 0.30), (0, y, 0.18), wood())                    # uprights
    b.cylinder(0.15, 0.15, 0.05, (0, 0, 0.22), "Y", stone(), 32)           # grinding wheel (axis along Y)
    b.cylinder(0.012, 0.012, 0.30, (0, 0, 0.22), "Y", steel(), 12)         # axle
    b.box((0.10, 0.02, 0.02), (0.05, 0.16, 0.22), iron())                  # crank arm
    b.cylinder(0.012, 0.012, 0.06, (0.10, 0.19, 0.22), "Y", wood(), 10)    # crank grip
    return b.finish()


def make_handle():
    b = Builder("Handle")
    b.box((0.022, 0.18, 0.03), (-0.011, 0, 0), iron())                     # crossguard, flush with the blade joint at x = 0
    b.cylinder(0.017, 0.015, 0.20, (-0.122, 0, 0), "X", leather(), 14)     # grip, x from -0.222 to -0.022
    b.sphere(0.026, (-0.248, 0, 0), iron(), 16)                            # pommel
    return b.finish()


MODELS = [("anvil", make_anvil), ("furnace", make_furnace), ("quench_tank", make_quench_tank),
          ("grinder", make_grinder), ("handle", make_handle)]


# ---------------------------------------------------------------- export / preview

def stats(ob):
    me = ob.data
    me.calc_loop_triangles()
    pts = [v.co for v in me.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return len(me.vertices), len(me.loop_triangles), lo, hi, hi - lo


def export_fbx(ob, path):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, global_scale=1.0,
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="COPY", embed_textures=False)


def render_preview(ob, path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 720, 540
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.filepath = path
    pts = [ob.matrix_world @ v.co for v in ob.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    radius = (hi - lo).length / 2
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = radius * 2.1
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    direction = Vector((1.0, -1.2, 0.8)).normalized()
    cam.location = center + direction * (radius * 4)
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    bpy.data.cameras.remove(cam_data)


def main():
    out_dir, preview = args()
    os.makedirs(out_dir, exist_ok=True)
    if preview:
        os.makedirs(os.path.join(out_dir, "previews"), exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name, fn in MODELS:
        ob = fn()
        ob.name = name
        verts, tris, lo, hi, size = stats(ob)
        print("MODEL %s verts=%d tris=%d size=%.3f x %.3f x %.3f m  min=(%.3f,%.3f,%.3f)" %
              (name, verts, tris, size.x, size.y, size.z, lo.x, lo.y, lo.z))
        export_fbx(ob, os.path.join(out_dir, name + ".fbx"))
        if preview:
            try:
                render_preview(ob, os.path.join(out_dir, "previews", name + ".png"))
            except Exception as e:  # preview is optional, never fail the export
                print("PREVIEW FAILED %s: %s" % (name, e))
        bpy.data.objects.remove(ob)
    print("DONE %s" % out_dir)


main()
