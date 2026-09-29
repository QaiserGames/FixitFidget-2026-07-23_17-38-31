"""
Read meshes out of a Unity text (YAML) asset: positions and triangles, by mesh name. For measuring things that
live only in Unity, such as a house shell (layout v2 of Grace's house was measured this way).

    import unity_mesh
    meshes = unity_mesh.load('Assets/Playtests/AcesCafeLayout/Street doors.asset',
                             {'1 - Saffron bay-window house - Street palette (doorway)'})
    points, triangles = meshes['1 - Saffron bay-window house - Street palette (doorway)']

Positions come back in the mesh's own space (before its object's transform). Only float32 positions are read.
"""
import re
import struct

# bytes per component, by Unity's VertexAttributeFormat
FORMAT_SIZE = {0: 4, 1: 2, 2: 1, 3: 1, 4: 2, 5: 2, 6: 1, 7: 1, 8: 2, 9: 2, 10: 4, 11: 4}


def _field(block, name):
    m = re.search(r'\n\s*' + re.escape(name) + r': ?(.*)\n', block)
    return m.group(1).strip() if m else None


def parse(block):
    """(positions, triangles) from one serialized Mesh."""
    vcount = int(_field(block, 'm_VertexCount'))
    ch_txt = block.split('m_Channels:')[1].split('m_DataSize:')[0]
    chans = [tuple(map(int, c)) for c in
             re.findall(r'stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)', ch_txt)]
    data = bytes.fromhex(block.split('_typelessdata: ')[1].split('\n')[0].strip())
    # each stream's stride, and where it starts (streams follow one another)
    strides = {}
    for stream, offset, fmt, dim in chans:
        if dim & 0xF:
            strides[stream] = max(strides.get(stream, 0), offset + FORMAT_SIZE[fmt] * (dim & 0xF))
    start, at = {}, 0
    for stream in sorted(strides):
        start[stream] = at
        at += strides[stream] * vcount
    stream, offset, fmt, dim = chans[0]
    if fmt != 0 or (dim & 0xF) != 3:
        raise ValueError('positions are not float3: %r' % (chans[0],))
    points = [struct.unpack_from('<3f', data, start[stream] + i * strides[stream] + offset) for i in range(vcount)]
    wide = int(_field(block, 'm_IndexFormat') or 0) == 1
    ib = bytes.fromhex(block.split('m_IndexBuffer: ')[1].split('\n')[0].strip())
    size = 4 if wide else 2
    index = struct.unpack('<%d%s' % (len(ib) // size, 'I' if wide else 'H'), ib)
    triangles = []
    for first_byte, count, topology, base in re.findall(
            r'firstByte: (\d+)\s+indexCount: (\d+)\s+topology: (\d+)\s+baseVertex: (\d+)', block):
        first_byte, count, topology, base = int(first_byte), int(count), int(topology), int(base)
        if topology != 0:                 # triangles only
            continue
        first = first_byte // size
        for k in range(first, first + count, 3):
            triangles.append((index[k] + base, index[k + 1] + base, index[k + 2] + base))
    return points, triangles


def load(path, wanted):
    """{name: (positions, triangles)} for the meshes in `wanted` found in the asset at `path`."""
    text = open(path, encoding='utf-8').read()
    out = {}
    for block in re.split(r'\n--- !u!', text):
        m = re.search(r'\n  m_Name: (.*)\n', block)
        if m and m.group(1).strip() in wanted and 'm_VertexData' in block:
            out[m.group(1).strip()] = parse(block)
    return out
