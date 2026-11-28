"""Shipped HD hero GLB I/O for in-place patches (spring_rig.py, shield_slide.py): numpy reader and skinning (the
shipped files are interleaved/quantized, which Blender's importer misreads), in-place accessor writes, and a few
mesh helpers. From the clipping review tools (scratchpad clip/glbio.py, glbpatch.py, detect.py)."""
import json, math, struct, numpy as np

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def load(path):
    b = open(path, "rb").read()
    n = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + n])
    off = 20 + n
    bl = struct.unpack("<I", b[off:off + 4])[0]
    return j, b[off + 8:off + 8 + bl]


def acc(j, binb, i):
    a = j["accessors"][i]; bv = j["bufferViews"][a["bufferView"]]
    dt = np.dtype(CT[a["componentType"]]); nc = NC[a["type"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride", dt.itemsize * nc)
    cnt = a["count"]
    raw = np.frombuffer(binb, dtype=np.uint8, count=stride * (cnt - 1) + dt.itemsize * nc, offset=start)
    arr = np.lib.stride_tricks.as_strided(raw, shape=(cnt, dt.itemsize * nc), strides=(stride, 1)).copy()
    out = arr.view(dt).reshape(cnt, nc).astype(np.float64 if a["componentType"] == 5126 or a.get("normalized") else np.int64)
    if a.get("normalized"):
        out = {5120: lambda x: np.maximum(x / 127.0, -1), 5121: lambda x: x / 255.0, 5122: lambda x: np.maximum(x / 32767.0, -1),
               5123: lambda x: x / 65535.0}[a["componentType"]](out)
    return out


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return np.array([aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
                     aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz])


def qinv(q): return np.array([-q[0], -q[1], -q[2], q[3]])


def trs(t, r, s):
    m = np.eye(4); m[:3, :3] = qmat(r) * np.asarray(s)[None, :]; m[:3, 3] = t; return m


def node_trs(n):
    if "matrix" in n:
        m = np.array(n["matrix"]).reshape(4, 4).T
        t = m[:3, 3]; s = np.linalg.norm(m[:3, :3], axis=0); R = m[:3, :3] / s
        # matrix -> quaternion
        w = np.sqrt(max(0, 1 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
        q = np.array([(R[2, 1] - R[1, 2]) / (4 * w), (R[0, 2] - R[2, 0]) / (4 * w), (R[1, 0] - R[0, 1]) / (4 * w), w]) if w > 1e-6 else np.array([1, 0, 0, 0.0])
        return t, q, s
    return (np.array(n.get("translation", [0, 0, 0]), float), np.array(n.get("rotation", [0, 0, 0, 1]), float),
            np.array(n.get("scale", [1, 1, 1]), float))


class Hero:
    def __init__(self, path):
        j, b = load(path)
        self.j = j
        mi = next(i for i, n in enumerate(j["nodes"]) if "mesh" in n and "skin" in n)
        prim = j["meshes"][j["nodes"][mi]["mesh"]]["primitives"][0]
        at = prim["attributes"]
        self.V = acc(j, b, at["POSITION"])
        self.N = acc(j, b, at["NORMAL"]) if "NORMAL" in at else None
        self.UV = acc(j, b, at["TEXCOORD_0"]) if "TEXCOORD_0" in at else None
        self.J = acc(j, b, at["JOINTS_0"]).astype(int)
        self.W = acc(j, b, at["WEIGHTS_0"])
        self.W = self.W / np.maximum(self.W.sum(1, keepdims=True), 1e-9)
        self.F = acc(j, b, prim["indices"]).reshape(-1, 3).astype(int)
        sk = j["skins"][j["nodes"][mi]["skin"]]
        self.joints = sk["joints"]
        self.IBM = acc(j, b, sk["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
        self.names = [n.get("name", f"n{i}") for i, n in enumerate(j["nodes"])]
        self.jnames = [self.names[k] for k in self.joints]
        self.parent = {}
        for i, n in enumerate(j["nodes"]):
            for c in n.get("children", []): self.parent[c] = i
        self.rest = [node_trs(n) for n in j["nodes"]]
        self.order = []
        seen = set()
        def visit(i):
            if i in seen: return
            if i in self.parent: visit(self.parent[i])
            seen.add(i); self.order.append(i)
        for i in range(len(j["nodes"])): visit(i)

    def globals_(self, local):
        G = [None] * len(local)
        for i in self.order:
            m = trs(*local[i])
            G[i] = G[self.parent[i]] @ m if i in self.parent else m
        return G

    def skin(self, local=None):
        local = local or self.rest
        G = self.globals_(local)
        M = np.stack([G[k] @ self.IBM[ji] for ji, k in enumerate(self.joints)])  # (J,4,4)
        Vh = np.c_[self.V, np.ones(len(self.V))]
        out = np.zeros((len(self.V), 3))
        for k in range(4):
            Mk = M[self.J[:, k]]
            out += self.W[:, k:k + 1] * np.einsum("nij,nj->ni", Mk[:, :3, :], Vh)
        return out, G

    def joint_global(self, G, name):
        return G[self.names.index(name)]


class Clips:
    def __init__(self, path):
        j, b = load(path)
        self.names = [n.get("name", "") for n in j["nodes"]]
        self.rest = {self.names[i]: node_trs(n) for i, n in enumerate(j["nodes"])}
        self.clips = {}
        for an in j["animations"]:
            ch = {}
            dur = 0
            for c in an["channels"]:
                s = an["samplers"][c["sampler"]]
                t = acc(j, b, s["input"])[:, 0]; v = acc(j, b, s["output"])
                nm = self.names[c["target"]["node"]]
                ch.setdefault(nm, {})[c["target"]["path"]] = (t, v, s.get("interpolation", "LINEAR"))
                dur = max(dur, t[-1])
            self.clips[an["name"].split("|")[-1]] = (ch, dur)

    @staticmethod
    def sample(t, v, interp, x, rot):
        if x <= t[0]: return v[0]
        if x >= t[-1]: return v[-1]
        i = np.searchsorted(t, x) - 1
        if interp == "STEP": return v[i]
        u = (x - t[i]) / max(t[i + 1] - t[i], 1e-9)
        a, b = v[i], v[i + 1]
        if rot:
            if np.dot(a, b) < 0: b = -b
            q = a * (1 - u) + b * u; return q / np.linalg.norm(q)
        return a * (1 - u) + b * u

    def local_pose(self, hero, clip, u):
        """Local TRS for every hero node at normalized time u of clip (RigExtras rule)."""
        ch, dur = self.clips[clip]
        x = u * dur
        out = []
        for i, nm in enumerate(hero.names):
            t1, r1, s1 = hero.rest[i]
            if nm not in ch or nm not in self.rest:
                out.append((t1, r1, s1)); continue
            t0, r0, _ = self.rest[nm]
            c = ch[nm]
            q = self.sample(*c["rotation"], x, True) if "rotation" in c else r0
            p = self.sample(*c["translation"], x, False) if "translation" in c else t0
            D = qmul(r1, qinv(r0))
            rq = qmul(D, q); rq /= np.linalg.norm(rq)
            pp = t1 + qmat(D) @ (p - t0)
            out.append((pp, rq, s1))
        return out


def write_acc(binb, j, ai, arr):
    a = j["accessors"][ai]; bv = j["bufferViews"][a["bufferView"]]
    dt = np.dtype(CT[a["componentType"]]); nc = NC[a["type"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0); stride = bv.get("byteStride", dt.itemsize * nc)
    if a.get("normalized"): raise ValueError("normalized accessor")
    for i in range(a["count"]):
        o = start + i * stride
        binb[o:o + dt.itemsize * nc] = np.asarray(arr[i], dtype=dt).tobytes()
    if a["type"] == "VEC3" and "min" in a:
        a["min"] = [float(x) for x in np.asarray(arr).min(0)]; a["max"] = [float(x) for x in np.asarray(arr).max(0)]


def save(path, j, binb):
    js = json.dumps(j, separators=(",", ":")).encode()
    js += b" " * ((4 - len(js) % 4) % 4)
    bb = bytes(binb) + b"\0" * ((4 - len(binb) % 4) % 4)
    total = 12 + 8 + len(js) + 8 + len(bb)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack("<II", len(bb), 0x004E4942)); f.write(bb)


def weld(V, tol=2e-4):
    k = np.round(V / tol).astype(np.int64)
    _, inv = np.unique(k, axis=0, return_inverse=True)
    return inv.ravel()


def components(F, wid):
    n = wid.max() + 1; lab = np.arange(n)
    Fw = wid[F]
    for _ in range(200):
        m = lab[Fw].min(1)
        old = lab.copy()
        for c in range(3): np.minimum.at(lab, Fw[:, c], m)
        lab = lab[lab]
        if np.array_equal(lab, old): break
    return lab[Fw[:, 0]]


def mat_rot(M):
    R = M[:3, :3] / np.linalg.norm(M[:3, :3], axis=0)
    w = math.sqrt(max(0, 1 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
    if w > 1e-4: return np.array([(R[2, 1] - R[1, 2]) / (4 * w), (R[0, 2] - R[2, 0]) / (4 * w), (R[1, 0] - R[0, 1]) / (4 * w), w])
    # fallback
    i = int(np.argmax([R[0, 0], R[1, 1], R[2, 2]]))
    if i == 0:
        s = math.sqrt(1 + R[0, 0] - R[1, 1] - R[2, 2]) * 2; return np.array([0.25 * s, (R[0, 1] + R[1, 0]) / s, (R[0, 2] + R[2, 0]) / s, (R[2, 1] - R[1, 2]) / s])
    if i == 1:
        s = math.sqrt(1 + R[1, 1] - R[0, 0] - R[2, 2]) * 2; return np.array([(R[0, 1] + R[1, 0]) / s, 0.25 * s, (R[1, 2] + R[2, 1]) / s, (R[0, 2] - R[2, 0]) / s])
    s = math.sqrt(1 + R[2, 2] - R[0, 0] - R[1, 1]) * 2; return np.array([(R[0, 2] + R[2, 0]) / s, (R[1, 2] + R[2, 1]) / s, 0.25 * s, (R[1, 0] - R[0, 1]) / s])
