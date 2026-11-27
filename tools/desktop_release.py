"""Build portable desktop assets, publish drafts, and announce verified stable releases."""

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import stat
import string
import subprocess
import sys
import tarfile
import tempfile
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile

REPO = "needforrewrite/NFM-World"
PLATFORMS = {"win-x64": "Windows x64", "linux-x64": "Linux x64",
             "osx-x64": "macOS Intel", "osx-arm64": "macOS Apple Silicon"}
CORE = {"win-x64", "linux-x64"}
MANIFEST = "nfmworld-install.json"
MAX_ARCHIVE = 2 << 30
MUTABLE = ("data/cfg", "data/music/custom", "mods")
LEGACY = re.compile(r"release-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{8}")


def stable_version(tag):
    if not isinstance(tag, str) or not re.fullmatch(r"v(?:0|[1-9][0-9]{0,4})(?:\.(?:0|[1-9][0-9]{0,4})){2}", tag):
        return None
    version = tuple(map(int, tag[1:].split(".")))
    return version if max(version) <= 65534 else None


def checked_sha(sha):
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError("Expected a full commit SHA.")
    return sha


def asset_name(rid):
    return f"nfm-world-{rid}" + (".zip" if rid == "win-x64" else ".tar.gz")


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(1 << 20), b""):
            result.update(chunk)
    return result.hexdigest()


def invoke(*args, capture=True, check=True):
    return subprocess.run(args, stdout=subprocess.PIPE if capture else None, encoding="utf-8" if capture else None, check=check)


def publish_project(project, rid, output, *properties):
    invoke("dotnet", "publish", f"{project}/{project}.csproj", "-c", "Release", "-r", rid, "-o", str(output),
           "-p:GeneratePackageOnBuild=false", *[f"-p:{p}" for p in properties], "--", "/maxcpucount:1", capture=False)


def ordinary_files(root, stable):
    names = set()
    files = {}

    def visit(directory):
        for path in sorted(directory.iterdir()):
            relative = path.relative_to(root).as_posix()
            key = unicodedata.normalize("NFC", relative).upper()
            if key in names:
                raise ValueError(f"Duplicate package path: {relative}")
            names.add(key)
            info = path.lstat()
            if getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
                raise ValueError(f"Linked package entry: {relative}")
            if stable:
                if any(relative.lower() == prefix or relative.lower().startswith(prefix + "/") for prefix in MUTABLE):
                    continue
                part = path.name
                stem = part.split(".")[0].rstrip().upper()
                if (relative.lower() == MANIFEST or relative.lower().split("/")[0] == ".nfmw-update"
                        or part.endswith((".", " ")) or len(relative) > 1024
                        or any(unicodedata.category(c) == "Cc" or c in '\\:<>"|?*' for c in part)
                        or stem in {"CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$"}
                        or re.fullmatch(r"(?:COM|LPT)[0-9¹²³]", stem)):
                    raise ValueError(f"Unsafe package path: {relative}")
            if stat.S_ISDIR(info.st_mode):
                visit(path)
            elif stat.S_ISREG(info.st_mode):
                files[relative] = path
            else:
                raise ValueError(f"Unsupported package entry: {relative}")

    visit(root)
    return files


def package(root, rid, output, tag):
    files = ordinary_files(root, tag is not None)
    windows = rid == "win-x64"
    ext = ".exe" if windows else ""
    apphosts = ["NFMWorld" + ext, "NFMWorld.CrashReporter" + ext]
    if tag:
        apphosts.append("NFMWorld.Updater" + ext)
    for name in [*apphosts, "data/images/logo.svg"]:
        if name not in files or files[name].stat().st_size == 0:
            raise ValueError(f"Missing package marker: {name}")
    if tag:
        manifest = root / MANIFEST
        payload = json.dumps({"format": 1, "product": "NFMWorld", "tag": tag, "rid": rid,
                              "files": {name: digest(path) for name, path in files.items()}}, ensure_ascii=False).encode("utf-8")
        if len(payload) > 4 << 20 or len(files) + 1 > 50_000 or sum(p.stat().st_size for p in files.values()) + len(payload) > 4 << 30:
            raise ValueError("Package exceeds updater extraction limits.")
        with manifest.open("xb") as file:
            file.write(payload)
        files[MANIFEST] = manifest
    if windows:
        with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for name, path in files.items():
                archive.write(path, name)
    else:
        def ordinary_mode(member):
            member.mode &= 0o777
            return member

        with tarfile.open(output, "w:gz", format=tarfile.PAX_FORMAT, dereference=True, compresslevel=6) as archive:
            for name, path in files.items():
                archive.add(path, name, recursive=False, filter=ordinary_mode)
        with tarfile.open(output, "r:gz") as archive:
            modes = {member.name: member.mode for member in archive if member.name in apphosts}
        if any(not modes.get(name, 0) & stat.S_IXUSR for name in apphosts):
            raise ValueError("An apphost in the TAR is not executable.")
    if not 0 < output.stat().st_size <= MAX_ARCHIVE:
        raise ValueError("Package exceeds GitHub asset size limits.")


def build(args):
    sha = checked_sha(args.sha)
    tag = args.stable_tag
    if tag is not None and (stable_version(tag) is None or tag != args.ref):
        raise ValueError("Expected a stable version tag matching the build ref.")
    host_os = {"Windows": "win", "Darwin": "osx", "Linux": "linux"}.get(platform.system())
    host_arch = {"AMD64": "x64", "X86_64": "x64", "ARM64": "arm64", "AARCH64": "arm64"}.get(platform.machine().upper())
    if f"{host_os}-{host_arch}" != args.rid:
        raise ValueError("Build on a host matching the target RID; native dependencies follow the SDK host.")
    info = re.sub(r"[^A-Za-z0-9._/-]", "_", args.ref)
    date = dt.datetime.now(dt.timezone.utc).strftime("%Y.%m.%d")
    args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="nfmw-build-", dir=args.output.resolve()) as temp:
        staging = Path(temp)
        root = staging / "game"
        publish_project("NFMWorld", args.rid, root,
                        f"Version={tag[1:] if tag else '1.0.0'}", f"InformationalVersion=NFM-World {info}-{date}-{sha[:8]}",
                        "SelfContained=true", "PublishReadyToRun=true", "PublishSingleFile=true", "DebugType=embedded",
                        "IncludeNativeLibrariesForSelfExtract=false", "IncludeAllContentForSelfExtract=false",
                        "ErrorOnDuplicatePublishOutputFiles=false", *([f"NfmwReleaseTag={tag}"] if tag else []))
        publish_project("NFMWorld.Library", args.rid, root / "native")
        aot = ("SelfContained=true", "PublishAot=true", "DebugType=none", "DebugSymbols=false", "StripSymbols=true")
        publish_project("NFMWorld.CrashReporter", args.rid, root, *aot)
        if tag:
            helper = "NFMWorld.Updater" + (".exe" if args.rid == "win-x64" else "")
            publish_project("NFMWorld.Updater", args.rid, staging / "helper", *aot)
            shutil.copy2(staging / "helper" / helper, root / helper)
        output = staging / asset_name(args.rid)
        package(root, args.rid, output, tag)
        output.replace(args.output / output.name)


class GitHub:
    def __init__(self, repo):
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repo):
            raise ValueError("Invalid repository name.")
        self.repo = repo

    def releases(self):
        pages = invoke("gh", "api", "--paginate", "--slurp", f"repos/{self.repo}/releases?per_page=100").stdout
        return [release for page in json.loads(pages) for release in page]

    def latest(self):
        return json.loads(invoke("gh", "api", f"repos/{self.repo}/releases/latest").stdout)

    def release(self, command, *args):
        invoke("gh", "release", command, "--repo", self.repo, *args, capture=False)

    def ensure_tag(self, channel, tag, sha, *, create=False):
        ref = f"refs/tags/{tag}"
        refs = invoke("git", "ls-remote", "--tags", "origin", ref, ref + "^{}").stdout
        found = {name: value for value, name in (line.split() for line in refs.splitlines())}
        if ref not in found and channel == "dev" and create:
            # Push the exact built commit, not a possibly stale local tag.
            invoke("git", "push", "origin", f"{sha}:{ref}", capture=False)
        elif found.get(ref + "^{}", found.get(ref)) != sha:
            raise ValueError(f"Remote tag {tag} does not point to the built commit.")


def exact_release(releases, tag):
    matches = [release for release in releases if release["tag_name"] == tag]
    if len(matches) > 1:
        raise ValueError(f"Multiple releases refer to {tag}.")
    return matches[0] if matches else None


def matching_assets(release, repo, channel, files=None):
    expected = {asset_name(rid) for rid in PLATFORMS}
    required = expected if channel == "stable" else {asset_name(rid) for rid in CORE}
    assets = release["assets"]
    by_name = {asset["name"]: asset for asset in assets}
    if len(assets) != len(by_name) or not required <= by_name.keys() <= expected or (files is not None and set(by_name) != set(files)):
        raise ValueError("Release is missing expected assets or has unexpected assets.")
    tag = release["tag_name"]
    for name, asset in by_name.items():
        if (asset["state"] != "uploaded" or not 0 < asset["size"] <= MAX_ARCHIVE
                or not re.fullmatch(r"sha256:[0-9a-fA-F]{64}", asset.get("digest") or "")):
            raise ValueError(f"Release asset could not be verified: {name}")
        if not release["draft"] and asset["browser_download_url"] != f"https://github.com/{repo}/releases/download/{tag}/{name}":
            raise ValueError(f"Unexpected release download URL: {name}")
        if files is not None:
            size, sha = files[name]
            if asset["size"] != size or asset["digest"].lower() != "sha256:" + sha:
                raise ValueError(f"Uploaded bytes do not match the build: {name}")
    return by_name


def git_commit(tag):
    result = invoke("git", "rev-parse", "--verify", "--quiet", f"refs/tags/{tag}^{{commit}}", check=False)
    if result.returncode not in (0, 1):
        raise RuntimeError("Could not resolve release tag.")
    return checked_sha(result.stdout.strip()) if result.returncode == 0 else None


def is_ancestor(base, head):
    status = invoke("git", "merge-base", "--is-ancestor", base, head, check=False).returncode
    if status not in (0, 1):
        raise RuntimeError("Could not verify release ancestry.")
    return status == 0


def commit_notes(releases, tag, sha):
    published = [release for release in releases if not release["draft"]]
    stable = [r["tag_name"] for r in published if not r["prerelease"] and stable_version(r["tag_name"]) is not None]
    previous = None
    if stable:
        prior = max(stable, key=stable_version)
        if stable_version(prior) >= stable_version(tag):
            raise ValueError(f"Stable tag must be newer than {prior}.")
        previous = git_commit(prior)
        if previous is None or not is_ancestor(previous, sha):
            raise ValueError(f"Previous stable release {prior} must be an ancestor of {tag}.")
    else:
        legacy = sorted((r for r in published if LEGACY.fullmatch(r["tag_name"])),
                        key=lambda r: r.get("published_at") or "", reverse=True)
        for release in legacy:
            candidate = git_commit(release["tag_name"])
            if candidate and candidate != sha and is_ancestor(candidate, sha):
                previous = candidate
                break
    subjects = invoke("git", "log", "--format=%s", f"{previous}..{sha}" if previous else sha, "--").stdout
    lines = []
    for subject in subjects.split("\n"):
        subject = "".join(c for c in subject if not unicodedata.category(c).startswith("C")).strip()
        if subject:
            lines.append("- " + "".join("\\" + c if c in string.punctuation else c for c in subject))
    return "\n".join(lines or ["No commits since the previous release."]) + "\n"


def publish(args, github, state):
    channel, tag, sha = args.channel, args.tag, checked_sha(args.sha)
    if channel == "stable":
        if stable_version(tag) is None or args.ref != tag or git_commit(tag) != sha:
            raise ValueError("Stable publishing requires the explicit tag at the built commit.")
    elif not re.fullmatch(rf"release-[0-9]+-{sha[:8]}", tag):
        raise ValueError("Invalid development snapshot tag.")
    files = {asset_name(rid): args.assets / asset_name(rid) for rid in PLATFORMS}
    available = {name: path for name, path in files.items() if path.is_file()}
    if ((channel == "stable" and files.keys() != available.keys())
            or not {asset_name(rid) for rid in CORE} <= available.keys()):
        raise ValueError("Required package artifacts are missing.")
    releases = github.releases()
    existing = exact_release(releases, tag)
    if existing is not None and not existing["draft"]:
        github.ensure_tag(channel, tag, sha)
        if existing["prerelease"] != (channel == "dev"):
            raise ValueError("Published release is in the wrong channel.")
        matching_assets(existing, github.repo, channel)
        print(f"{tag} is already published; no changes or announcement.")
        return False
    sizes = {name: path.stat().st_size for name, path in available.items()}
    if not all(0 < size <= MAX_ARCHIVE for size in sizes.values()):
        raise ValueError("Invalid package artifact size.")
    hashes = {name: (sizes[name], digest(path)) for name, path in available.items()}
    notes = commit_notes(releases, tag, sha) if channel == "stable" else f"Development snapshot of {sha}.\n"
    state.mkdir(parents=True, exist_ok=True)
    notes_path = state / "commits.txt"
    notes_path.write_text(notes, encoding="utf-8")
    title = f"NFM World {tag}" if channel == "stable" else f"Build {args.ref} - {sha[:8]}"
    github.ensure_tag(channel, tag, sha, create=existing is None)
    if existing is None:
        github.release("create", tag, "--verify-tag", "--draft", f"--target={sha}", f"--title={title}",
                       "--notes-file", str(notes_path), *(["--prerelease", "--latest=false"] if channel == "dev" else []))
    elif existing["target_commitish"] != sha:
        raise ValueError("Existing draft was not created for the built commit.")
    github.release("upload", tag, *[str(path.resolve()) for path in available.values()], "--clobber")
    uploaded = exact_release(github.releases(), tag)
    if uploaded is None or not uploaded["draft"]:
        raise ValueError("Expected an unpublished draft after upload.")
    matching_assets(uploaded, github.repo, channel, hashes)
    github.ensure_tag(channel, tag, sha)
    github.release("edit", tag, "--verify-tag", "--draft=false", f"--title={title}", "--notes-file", str(notes_path),
                   *(["--prerelease", "--latest=false"] if channel == "dev" else ["--prerelease=false", "--latest"]))
    if channel == "dev":
        return False
    latest = github.latest()
    if latest["tag_name"] != tag or latest["draft"] or latest["prerelease"]:
        raise ValueError("Published stable release is not GitHub's latest release.")
    matching_assets(latest, github.repo, channel, hashes)
    (state / "release.json").write_text(json.dumps(latest), encoding="utf-8")
    return True


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def discord_payload(release, notes, repo):
    tag = release["tag_name"]
    if stable_version(tag) is None or release["draft"] or release["prerelease"]:
        raise ValueError("Only a published stable release can be announced.")
    assets = matching_assets(release, repo, "stable")
    lines = [f"[NFM World {tag}](https://github.com/{repo}/releases/tag/{tag})"]
    lines.extend(f"[{label}]({assets[asset_name(rid)]['browser_download_url']})" for rid, label in PLATFORMS.items())
    header = "\n".join(lines) + "\n\n"
    content = header + notes.rstrip()
    attachment = len(content.encode("utf-16-le")) > 4000
    body = json.dumps({"content": header + "Commit messages are attached." if attachment else content,
                       "allowed_mentions": {"parse": []}, **({"attachments": [{"id": 0, "filename": "commits.txt"}]} if attachment else {})}).encode()
    if not attachment:
        return "application/json", body
    boundary = uuid.uuid4().hex
    return (f"multipart/form-data; boundary={boundary}",
            f'--{boundary}\r\nContent-Disposition: form-data; name="payload_json"\r\nContent-Type: application/json\r\n\r\n'.encode()
            + body + f'\r\n--{boundary}\r\nContent-Disposition: form-data; name="files[0]"; filename="commits.txt"\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n'.encode()
            + notes.encode("utf-8") + f"\r\n--{boundary}--\r\n".encode())


def announce(release, notes, webhook, repo=REPO, opener=None):
    if not webhook:
        print("::warning::Discord release webhook is not configured.")
        return
    try:
        url = urllib.parse.urlsplit(webhook)
        if (url.scheme != "https" or url.netloc.lower() != "discord.com" or url.query or url.fragment
                or not re.fullmatch(r"/api(?:/v[0-9]+)?/webhooks/[0-9]+/[A-Za-z0-9_.-]+", url.path)):
            raise ValueError()
        content_type, body = discord_payload(release, notes, repo)
        request = urllib.request.Request(urllib.parse.urlunsplit(url._replace(query="wait=true")), data=body,
                                         headers={"Content-Type": content_type, "User-Agent": "NFMWorld-Releases/1"}, method="POST")
        with (opener or urllib.request.build_opener(NoRedirect())).open(request, timeout=15) as response:
            if not 200 <= response.status < 300:
                print(f"::warning::Discord announcement failed (HTTP {response.status}).")
    except urllib.error.HTTPError as error:
        print(f"::warning::Discord announcement failed (HTTP {error.code}).")
    except TimeoutError:
        print("::warning::Discord announcement timed out.")
    except Exception:
        # Do not print exceptions: they can contain the credential-bearing webhook URL.
        print("::warning::Discord announcement failed (configuration or transport error).")


def prepare():
    sha = checked_sha(os.environ["BUILD_SHA"])
    ref = os.environ["BUILD_REF_NAME"]
    ref_type = os.environ["BUILD_REF_TYPE"]
    if ref_type == "tag" and stable_version(ref) is not None:
        channel, tag = "stable", ref
    elif ref_type == "branch" and re.fullmatch(r"[0-9]+", os.environ["GITHUB_RUN_ID"]):
        channel, tag = "dev", f"release-{os.environ['GITHUB_RUN_ID']}-{sha[:8]}"
    else:
        raise ValueError("Expected a branch or explicit vMAJOR.MINOR.PATCH tag.")
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        print(f"channel={channel}\ntag={tag}", file=output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("prepare")
    builder = commands.add_parser("build")
    builder.add_argument("--rid", choices=PLATFORMS, required=True)
    builder.add_argument("--output", type=Path, required=True)
    builder.add_argument("--stable-tag")
    publisher = commands.add_parser("publish")
    publisher.add_argument("--assets", type=Path, required=True)
    publisher.add_argument("--channel", choices=("dev", "stable"), required=True)
    for command in (builder, publisher):
        command.add_argument("--ref", required=True)
        command.add_argument("--sha", required=True)
    publisher.add_argument("--tag", required=True)
    announcer = commands.add_parser("announce")
    announcer.add_argument("--release-json", type=Path, required=True)
    announcer.add_argument("--notes", type=Path, required=True)
    args = parser.parse_args()
    repo = os.environ.get("GITHUB_REPOSITORY", REPO)
    try:
        if args.command == "prepare":
            prepare()
        elif args.command == "build":
            build(args)
        elif args.command == "publish":
            if publish(args, GitHub(repo), Path(os.environ["RUNNER_TEMP"]) / "nfmw-release"):
                with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
                    print("published=true", file=output)
        else:
            try:
                announce(json.loads(args.release_json.read_text(encoding="utf-8")), args.notes.read_text(encoding="utf-8"),
                         os.environ.get("DISCORD_RELEASE_WEBHOOK_URL"), repo)
            except (OSError, ValueError):
                print("::warning::Discord announcement failed (missing or invalid release metadata).")
    except (ValueError, RuntimeError, OSError, subprocess.CalledProcessError) as error:
        print(f"{args.command}: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
