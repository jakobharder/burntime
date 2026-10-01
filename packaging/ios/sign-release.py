"""Sign a downloaded device bundle locally; never modify the original ZIP."""
import datetime
import hashlib
import os
from pathlib import Path, PurePosixPath
import plistlib
import shutil
import subprocess
import sys
import tempfile
import zipfile


def run(*args):
    subprocess.run(args, check=True)


def sign_release(source, identity, profile_path):
    output = source.with_name("Burntime-iPad-arm64-signed.ipa")
    if output.exists():
        raise ValueError(f"Refusing to overwrite {output}")
    profile = plistlib.loads(subprocess.check_output(
        ["security", "cms", "-D", "-i", str(profile_path)]))
    if profile["ExpirationDate"] <= datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None):
        raise ValueError("Provisioning profile has expired")
    with zipfile.ZipFile(source) as archive:
        for item in archive.infolist():
            name = PurePosixPath(item.filename)
            if name.is_absolute() or ".." in name.parts:
                raise ValueError("Unsafe archive path")
            if (item.external_attr >> 16) & 0o170000 == 0o120000:
                target = PurePosixPath(archive.read(item).decode())
                if target.is_absolute() or ".." in target.parts:
                    raise ValueError("Unsafe archive symlink")
    with tempfile.TemporaryDirectory(prefix="burntime-ios-sign-") as temporary:
        root = Path(temporary)
        run("ditto", "-x", "-k", str(source), str(root))
        app = root / "Payload/Burntime.app"
        if not app.is_dir():
            raise ValueError("Archive does not contain Payload/Burntime.app")
        info = plistlib.loads((app / "Info.plist").read_bytes())
        if info.get("UIDeviceFamily") != [2]:
            raise ValueError("Expected an iPad-only build")
        bundle_id = info["CFBundleIdentifier"]
        entitlements = dict(profile["Entitlements"])
        app_id = entitlements["application-identifier"]
        prefix = profile["ApplicationIdentifierPrefix"][0] + "."
        expected = prefix + bundle_id
        if app_id != expected and not (app_id.endswith("*") and expected.startswith(app_id[:-1])):
            raise ValueError(f"Profile does not match bundle identifier {bundle_id}")
        entitlements["application-identifier"] = expected
        if "keychain-access-groups" in entitlements:
            entitlements["keychain-access-groups"] = [
                group.replace("*", bundle_id) for group in entitlements["keychain-access-groups"]]
        entitlements_path = root / "Entitlements.plist"
        entitlements_path.write_bytes(plistlib.dumps(entitlements))
        shutil.copyfile(profile_path, app / "embedded.mobileprovision")
        run("xattr", "-cr", str(app))
        # Sign nested native code before sealing the application. No --deep signing.
        nested = [p for p in app.rglob("*") if p.suffix in (".dylib", ".so", ".framework", ".appex")]
        for path in sorted(nested, key=lambda p: len(p.parts), reverse=True):
            if path.suffix == ".appex":
                raise ValueError("App extensions require their own provisioning profile")
            run("codesign", "--force", "--sign", identity, str(path))
        run("codesign", "--force", "--sign", identity, "--entitlements", str(entitlements_path), str(app))
        run("codesign", "--verify", "--deep", "--strict", str(app))
        certificate_prefix = str(root / "signing-certificate-")
        run("codesign", "--display", "--extract-certificates=" + certificate_prefix, str(app))
        certificate = Path(certificate_prefix + "0").read_bytes()
        if hashlib.sha256(certificate).digest() not in [
                hashlib.sha256(cert).digest() for cert in profile["DeveloperCertificates"]]:
            raise ValueError("Signing certificate is not included in the provisioning profile")
        # Keep symbols alongside the IPA for crash symbolication.
        staged = root / output.name
        run("ditto", "-c", "-k", "--sequesterRsrc", str(root / "Payload"), str(staged), "--keepParent")
        run("unzip", "-tq", str(staged))
        # Exclusive creation protects existing release outputs even if another signer runs.
        with output.open("xb") as target, staged.open("rb") as signed:
            shutil.copyfileobj(signed, target)
    print(f"Created {output}\nBundle ID: {bundle_id}\nProfile: {profile['Name']}")
    print("For TestFlight/App Store, use an Apple Distribution identity and App Store profile, then upload with Transporter.")


if __name__ == "__main__":
    try:
        identity = os.environ["IOS_SIGN_IDENTITY"]
        profile_path = Path(os.environ["IOS_PROVISION_PROFILE"]).expanduser().resolve()
        sign_release(Path(sys.argv[1]).resolve(), identity, profile_path)
    except (KeyError, ValueError, OSError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        print(f"Signing failed: {error}", file=sys.stderr)
        sys.exit(1)
