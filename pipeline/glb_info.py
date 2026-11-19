"""GLB'nin JSON bolumunu okuyup ozet basar (Blender'siz, kredisiz): dugumler, ucgenler, dokular, iskelet eklemleri, klipler.
Kosu: python -I pipeline/glb_info.py <dosya.glb> [...]
"""
import json
import struct
import sys


def load(path):
    b = open(path, "rb").read()
    n = struct.unpack("<I", b[12:16])[0]
    return json.loads(b[20:20 + n])


def main():
    for p in sys.argv[1:]:
        j = load(p)
        nodes = j["nodes"]
        print("==", p.replace("\\", "/").split("/")[-1])
        print("nodes", len(nodes), "meshes", len(j.get("meshes", [])), "skins", len(j.get("skins", [])),
              "anims", [(a["name"], max((j["accessors"][s["input"]].get("max", [0])[0] for s in a["samplers"]), default=0)) for a in j.get("animations", [])])
        for m in j.get("meshes", []):
            tris = sum(j["accessors"][pr["indices"]]["count"] // 3 for pr in m["primitives"] if "indices" in pr)
            print("mesh", m.get("name"), "tris", tris, "prims", len(m["primitives"]))
        for i, im in enumerate(j.get("images", [])):
            if "bufferView" in im:
                print("image", i, im.get("mimeType"), j["bufferViews"][im["bufferView"]]["byteLength"] // 1024, "KB")
            else:
                print("image", i, im.get("uri"))
        if j.get("skins"):
            names = [nodes[k]["name"] for k in j["skins"][0]["joints"]]
            print("joints", len(names))
            print(" | ".join(names))
        print("scene roots", [nodes[i].get("name") for i in j["scenes"][0]["nodes"]])
        for x in nodes:
            if "mesh" in x:
                print("meshnode", x.get("name"), "scale", x.get("scale"), "rot", x.get("rotation"), "pos", x.get("translation"))


if __name__ == "__main__":
    main()
