"""
Build Barnaby and write his FBX (for Unity) and his .blend (the source), next to the game's files.

Headless:  blender -b --python Tools/Blender/export_barnaby.py [-- <fbx path> <blend path>]
In Blender: open this file in the Scripting tab and Run Script (the scene is cleared first).
Defaults: Assets/Art/Models/Night/Barnaby.fbx and BlenderSource/Night1_Barnaby.blend in this repository.
Then in Unity: Fixit Fidget > Night > Night 1 - Barnaby: use the Blender model (the prefab only).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import fixit_blender as fb  # noqa: E402
import barnaby  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fbx_path = args[0] if len(args) > 0 else os.path.join(REPO, 'Assets', 'Art', 'Models', 'Night', 'Barnaby.fbx')
blend_path = args[1] if len(args) > 1 else os.path.join(REPO, 'BlenderSource', 'Night1_Barnaby.blend')

ob, report = barnaby.main(os.path.dirname(fbx_path))
coll = fb.collection('Night1_Barnaby')
for c in list(ob.users_collection):
    c.objects.unlink(ob)
coll.objects.link(ob)
fb.export_fbx(ob, fbx_path)
os.makedirs(os.path.dirname(blend_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
print('\n'.join(report))
print('FBX:  ' + fbx_path)
print('blend: ' + blend_path)
