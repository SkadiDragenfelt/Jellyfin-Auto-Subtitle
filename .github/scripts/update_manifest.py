"""Regenerate manifest.json (the Jellyfin plugin repository file) from the built zips.

Usage: update_manifest.py <dist folder> <release tag>
Zip names look like: series-subtitles_<plugin version>_jellyfin-<target ABI>.zip
"""

import datetime
import hashlib
import json
import os
import pathlib
import re
import sys

ZIP_NAME = re.compile(r"series-subtitles_(\d+(?:\.\d+){3})_jellyfin-(\d+(?:\.\d+){3})\.zip")
MANIFEST = pathlib.Path("manifest.json")


def main() -> None:
    dist, tag = pathlib.Path(sys.argv[1]), sys.argv[2]
    repo = os.environ["GITHUB_REPOSITORY"]

    plugin = {
        "guid": "59ae2e45-120d-496e-a601-bc16cb28e9f2",
        "name": "Series Subtitles",
        "description": "Choose the subtitles a video starts with, per library or per series "
        "(for example anime in French, movies with no subtitles), without setting it on every episode.",
        "overview": "Subtitle language rules per library or per series",
        "owner": repo.split("/")[0],
        "category": "General",
        "versions": [],
    }

    if MANIFEST.exists():
        previous = json.loads(MANIFEST.read_text(encoding="utf-8"))
        if previous:
            plugin["versions"] = previous[0].get("versions", [])

    versions = {entry["version"]: entry for entry in plugin["versions"]}
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    zips = sorted(dist.glob("*.zip"))
    if not zips:
        sys.exit(f"No zip found in {dist}")

    for archive in zips:
        match = ZIP_NAME.fullmatch(archive.name)
        if not match:
            sys.exit(f"Unexpected zip name: {archive.name}")
        version, abi = match.groups()
        checksum = hashlib.md5(archive.read_bytes()).hexdigest()
        old = versions.get(version)
        versions[version] = {
            "version": version,
            "changelog": f"{os.environ.get('CHANGELOG', 'New release')} (build for Jellyfin {abi.rsplit('.', 2)[0]})",
            "targetAbi": abi,
            "sourceUrl": f"https://github.com/{repo}/releases/download/{tag}/{archive.name}",
            "checksum": checksum,
            "timestamp": old["timestamp"] if old and old.get("checksum") == checksum else now,
        }

    plugin["versions"] = sorted(
        versions.values(),
        key=lambda entry: tuple(int(part) for part in entry["version"].split(".")),
        reverse=True,
    )
    MANIFEST.write_text(json.dumps([plugin], indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
