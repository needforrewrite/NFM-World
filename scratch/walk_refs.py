"""Walks ProjectReference edges from a root csproj and reports the closure.

Used to prove the exe's build graph no longer reaches FNA/FNA.Math (Milestone 7).
"""

import os
import re
import sys
import xml.etree.ElementTree as ET

root = os.path.abspath(sys.argv[1])
seen, order = set(), []


def walk(proj):
    proj = os.path.normpath(proj)
    if proj in seen or not os.path.isfile(proj):
        return
    seen.add(proj)
    order.append(proj)
    try:
        tree = ET.parse(proj)
    except ET.ParseError as e:
        print("PARSE FAIL", proj, e)
        return
    d = os.path.dirname(proj)
    for node in tree.iter():
        if node.tag.split("}")[-1] != "ProjectReference":
            continue
        inc = node.get("Include")
        if not inc:
            continue
        for part in inc.split(";"):
            part = part.strip()
            if part:
                walk(os.path.join(d, part.replace("\\", os.sep)))


walk(root)
print(f"closure: {len(order)} projects")

fna = [p for p in order if re.search(r"(^|[\\/])FNA[\\/]", p) or re.search(r"(^|[\\/])FNA[^\\/]*\.csproj$", p, re.I)]
print("FNA projects in closure:", *[os.path.relpath(p) for p in fna] or ["(none)"], sep="\n  ")

# Report the non-FNA projects' names so the list is auditable rather than just a count.
for p in sorted(order):
    rel = os.path.relpath(p)
    print("  ", rel, "  <-- FNA" if p in fna else "")
