"""Export one of Grace's pieces again (by name) without touching the others."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import bpy, fixit_blender as fb, grace_house as gh
name, out = sys.argv[sys.argv.index('--') + 1], sys.argv[sys.argv.index('--') + 2]
fb.clear_scene()
for builder, room, what in gh.PIECES:
    prop = builder()
    if prop.name != name:
        prop.bm.free()
        continue
    ob = prop.finish()
    print(name, fb.stats(ob), fb.audit(ob))
    fb.export_fbx(ob, os.path.join(out, name + '.fbx'))
