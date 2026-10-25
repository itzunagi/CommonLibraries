#!/usr/bin/env python3
"""Print a JSON array of packable csproj paths whose <Version> changed between two commits.

Usage: detect_changed_versions.py <before-sha> <after-sha>
Result is ordered so that referenced projects come before their dependents.
"""
import json
import re
import subprocess
import sys

ZERO_SHA = "0" * 40
VERSION_RE = re.compile(r"<Version>\s*([^<]*?)\s*</Version>")
PACKABLE_FALSE_RE = re.compile(r"<IsPackable>\s*false\s*</IsPackable>", re.I)
REF_RE = re.compile(r'<ProjectReference\s+Include="([^"]+)"')


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True)


def read_at(rev, path):
    r = git("show", f"{rev}:{path}")
    return r.stdout if r.returncode == 0 else None


def version_of(text):
    if text is None:
        return None
    m = VERSION_RE.search(text)
    return m.group(1) if m else None


def main():
    before, after = sys.argv[1], sys.argv[2]
    if before == ZERO_SHA or git("cat-file", "-e", f"{before}^{{commit}}").returncode != 0:
        before = None

    projects = git("ls-tree", "-r", "--name-only", after).stdout.split("\n")
    projects = [p for p in projects if p.endswith(".csproj")]

    changed, contents = [], {}
    for p in projects:
        new = read_at(after, p)
        contents[p] = new
        if PACKABLE_FALSE_RE.search(new):
            continue
        new_v = version_of(new)
        if new_v is None:
            continue
        old_v = version_of(read_at(before, p)) if before else None
        if new_v != old_v:
            changed.append(p)

    def deps(p):
        base = p.rsplit("/", 1)[0] if "/" in p else ""
        out = set()
        for ref in REF_RE.findall(contents[p]):
            parts = [] if not base else base.split("/")
            for seg in ref.replace("\\", "/").split("/"):
                if seg == "..":
                    parts.pop()
                elif seg != ".":
                    parts.append(seg)
            out.add("/".join(parts))
        return out

    ordered, seen = [], set()

    def visit(p):
        if p in seen:
            return
        seen.add(p)
        for d in sorted(deps(p)):
            if d in changed:
                visit(d)
        ordered.append(p)

    for p in sorted(changed):
        visit(p)
    print(json.dumps(ordered))


if __name__ == "__main__":
    main()
