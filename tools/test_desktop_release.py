import contextlib
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch
import zipfile

import desktop_release as release


def asset(tag, path):
    return {"name": path.name, "size": path.stat().st_size, "state": "uploaded", "digest": "sha256:" + release.digest(path),
            "browser_download_url": f"https://github.com/{release.REPO}/releases/download/{tag}/{path.name}"}


def record(tag, prerelease=False, draft=False, files=(), sha=""):
    return {"tag_name": tag, "draft": draft, "prerelease": prerelease, "target_commitish": sha,
            "assets": [asset(tag, path) for path in files]}


class GitHubFixture:
    repo = release.REPO

    def __init__(self, records=()):
        self.records = {item["tag_name"]: item for item in records}
        self.latest_tag = "v1.9.0"
        self.commands = []

    def releases(self):
        return list(self.records.values())

    def latest(self):
        return self.records[self.latest_tag]

    def ensure_tag(self, channel, tag, sha, *, create=False):
        self.sha = sha

    def release(self, command, tag, *args):
        self.commands.append(command)
        if command == "create":
            self.records[tag] = record(tag, "--prerelease" in args, True, sha=self.sha)
        elif command == "upload":
            self.records[tag]["assets"] = [asset(tag, Path(path)) for path in args if path != "--clobber"]
        elif command == "edit":
            self.records[tag].update(draft="--draft=false" not in args, prerelease="--prerelease" in args)
            if "--latest" in args:
                self.latest_tag = tag


class DesktopReleaseTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.assets = self.root / "assets"
        self.assets.mkdir()
        self.state = self.root / "state"
        self.git("init", "-q")
        self.git("config", "user.name", "Updater Test")
        self.git("config", "user.email", "updater@example.invalid")
        self.commit("Old history")
        self.git("tag", "v1.9.0")
        self.commit("Fix [menu] @everyone")
        self.git("tag", "release-42-aabbccdd")
        self.commit("Improve downloads")
        self.git("tag", "v1.10.0")
        self.sha = self.git("rev-parse", "HEAD").strip()
        run = release.invoke
        commands = patch.object(release, "invoke", side_effect=lambda *args, **kwargs: (
            subprocess.run(args, cwd=self.root, capture_output=True, encoding="utf-8", check=kwargs.get("check", True))
            if args[0] == "git" else run(*args, **kwargs)))
        commands.start()
        self.addCleanup(commands.stop)

    def git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.root, encoding="utf-8")

    def commit(self, subject):
        self.git("commit", "-q", "--allow-empty", "-m", subject)

    def downloads(self, rids):
        files = [self.assets / release.asset_name(rid) for rid in rids]
        for path in files:
            path.write_bytes(path.name.encode())
        return files

    def args(self, channel):
        tag = "v1.10.0" if channel == "stable" else f"release-42-{self.sha[:8]}"
        return SimpleNamespace(channel=channel, tag=tag, sha=self.sha, ref=tag if channel == "stable" else "feature", assets=self.assets)

    def test_stable_notes_ignore_dev_tags_and_announce_exact_assets_once(self):
        files = self.downloads(release.PLATFORMS)
        github = GitHubFixture([record("v1.9.0"), record("release-42-aabbccdd", True)])
        args = self.args("stable")
        self.assertTrue(release.publish(args, github, self.state))
        result = json.loads((self.state / "release.json").read_text(encoding="utf-8"))
        notes = (self.state / "commits.txt").read_text(encoding="utf-8")
        self.assertEqual(notes, "- Improve downloads\n- Fix \\[menu\\] \\@everyone\n")
        opener = Mock()
        opener.open.return_value = contextlib.nullcontext(SimpleNamespace(status=200))
        release.announce(result, notes, "https://discord.com/api/webhooks/123/example", opener=opener)
        opener.open.assert_called_once()
        request = opener.open.call_args.args[0]
        body = json.loads(request.data)
        self.assertEqual(request.get_method(), "POST")
        self.assertTrue(request.full_url.endswith("?wait=true"))
        self.assertEqual(body["allowed_mentions"], {"parse": []})
        self.assertTrue(body["content"].endswith(notes.rstrip()))
        for path, label in zip(files, release.PLATFORMS.values()):
            self.assertIn(f"[{label}]({asset(args.tag, path)['browser_download_url']})", body["content"])
        files[0].write_bytes(b"different rebuild")
        self.assertFalse(release.publish(args, github, self.state))
        self.assertEqual(github.commands, ["create", "upload", "edit"])

    def test_dev_can_publish_without_mac_without_changing_latest(self):
        self.downloads(release.CORE)
        github = GitHubFixture([record("v1.9.0")])
        args = self.args("dev")
        self.assertFalse(release.publish(args, github, self.state))
        self.assertTrue(github.records[args.tag]["prerelease"])
        self.assertFalse(github.records[args.tag]["draft"])
        self.assertEqual(github.latest_tag, "v1.9.0")
        self.assertFalse((self.state / "release.json").exists())

    def test_missing_stable_asset_does_not_create_draft(self):
        self.downloads(release.CORE)
        github = GitHubFixture()
        with self.assertRaisesRegex(ValueError, "artifacts are missing"):
            release.publish(self.args("stable"), github, self.state)
        self.assertEqual(github.commands, [])

    def test_bad_uploaded_digest_does_not_publish_draft(self):
        self.downloads(release.PLATFORMS)
        github = GitHubFixture()
        upload = github.release

        def corrupt(command, tag, *args):
            upload(command, tag, *args)
            if command == "upload":
                github.records[tag]["assets"][0]["digest"] = "sha256:" + "0" * 64

        github.release = corrupt
        with self.assertRaisesRegex(ValueError, "Uploaded bytes"):
            release.publish(self.args("stable"), github, self.state)
        self.assertEqual(github.commands, ["create", "upload"])

    def test_long_discord_notes_attach_full_list_and_keep_download_links(self):
        files = self.downloads(release.PLATFORMS)
        notes = "- " + "😀" * 1000
        content_type, body = release.discord_payload(record("v1.10.0", files=files), notes, release.REPO)
        self.assertTrue(content_type.startswith("multipart/form-data; boundary="))
        self.assertIn(b'filename="commits.txt"', body)
        self.assertIn(notes.encode(), body)
        for name in files:
            self.assertIn(name.name.encode(), body)

    def test_long_notes_post_as_attachment_to_legacy_discord_host(self):
        files = self.downloads(release.PLATFORMS)
        notes = "- " + "😀" * 1100
        opener = Mock()
        opener.open.return_value = contextlib.nullcontext(SimpleNamespace(status=200))
        release.announce(record("v1.10.0", files=files), notes,
                         " https://discordapp.com/api/webhooks/123/example ", opener=opener)
        request = opener.open.call_args.args[0]
        self.assertEqual(request.host, "discord.com")
        self.assertIn(b'filename="commits.txt"', request.data)
        self.assertIn(notes.encode(), request.data)

    def test_canary_webhook_posts_to_discord(self):
        files = self.downloads(release.PLATFORMS)
        opener = Mock()
        opener.open.return_value = contextlib.nullcontext(SimpleNamespace(status=200))
        release.announce(record("v1.10.0", files=files), "notes",
                         "https://canary.discord.com/api/webhooks/123/example", opener=opener)
        request = opener.open.call_args.args[0]
        self.assertEqual(request.full_url, "https://discord.com/api/webhooks/123/example?wait=true")

    def test_bad_webhook_and_network_failure_report_safe_categories(self):
        files = self.downloads(release.PLATFORMS)
        data = record("v1.10.0", files=files)
        opener = Mock()
        opener.open.side_effect = release.urllib.error.URLError("secret-token")
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            release.announce(data, "notes", "https://discord.com/api/webhooks/123/example?secret-token", opener=opener)
            release.announce(data, "notes", "https://discord.com/api/webhooks/123/example", opener=opener)
        self.assertIn("invalid webhook URL", output.getvalue())
        self.assertIn("network or TLS error", output.getvalue())
        self.assertNotIn("secret-token", output.getvalue())
        opener.open.assert_called_once()

    def test_missing_or_wrong_origin_webhook_never_posts(self):
        opener = Mock()
        with contextlib.redirect_stdout(io.StringIO()):
            release.announce({}, "", None, opener=opener)
            release.announce({}, "", "https://discord.com.example.invalid/api/webhooks/123/example", opener=opener)
        opener.open.assert_not_called()

    def test_stable_package_inventories_payload_without_user_data(self):
        root = self.root / "game"
        names = ["NFMWorld.exe", "NFMWorld.CrashReporter.exe", "NFMWorld.Updater.exe", "data/images/logo.svg"]
        for name in names + ["mods/settings.txt", "data/cfg/config.cfg", "data/music/custom/user.ogg"]:
            path = root / name
            path.parent.mkdir(exist_ok=True, parents=True)
            path.write_bytes(name.encode())
        output = self.root / "game.zip"
        release.package(root, "win-x64", output, "v1.10.0")
        with zipfile.ZipFile(output) as archive:
            self.assertCountEqual(archive.namelist(), names + [release.MANIFEST])
            manifest = json.loads(archive.read(release.MANIFEST))
            self.assertEqual(manifest["files"], {name: release.digest(root / name) for name in names})

    @unittest.skipIf(os.name == "nt", "requires Unix file modes")
    def test_unix_archive_keeps_apphosts_executable(self):
        root = self.root / "game"
        (root / "data/images").mkdir(parents=True)
        (root / "data/images/logo.svg").write_bytes(b"logo")
        names = ["NFMWorld", "NFMWorld.CrashReporter", "NFMWorld.Updater"]
        for name in names:
            path = root / name
            path.write_bytes(b"apphost")
            path.chmod(0o755)
        out = self.root / "game.tar.gz"
        release.package(root, "linux-x64", out, "v1.10.0")
        with tarfile.open(out) as archive:
            self.assertEqual([archive.getmember(name).mode for name in names], [0o755] * 3)


if __name__ == "__main__":
    unittest.main()
