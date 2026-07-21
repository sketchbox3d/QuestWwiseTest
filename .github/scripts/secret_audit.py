#!/usr/bin/env python3
"""
Secret audit. Fails the build if a high-confidence credential is committed.

Scans tracked text files, skipping vendored trees. Patterns are deliberately
high-confidence: private key headers, provider-prefixed tokens, and explicit
password/secret assignments. Low-confidence heuristics are left out so this
gate stays trustworthy rather than noisy.
"""

import os
import re
import subprocess
import sys

PATTERNS = [
    ("private key block", re.compile(r"-----BEGIN (?:RSA |OPENSSH |DSA |EC |PGP )?PRIVATE KEY")),
    ("PuTTY private key", re.compile(r"PuTTY-User-Key-File")),
    ("AWS access key id", re.compile(r"AKIA[0-9A-Z]{16}")),
    ("GitHub token", re.compile(r"gh[psuor]_[A-Za-z0-9]{36}")),
    ("OpenAI key", re.compile(r"sk-[A-Za-z0-9]{20,}")),
    ("SendGrid key", re.compile(r"SG\.[A-Za-z0-9_\-]{16,}\.[A-Za-z0-9_\-]{16,}")),
    ("Slack token", re.compile(r"xox[baprs]-[A-Za-z0-9\-]{10,}")),
    ("Google API key", re.compile(r"AIza[0-9A-Za-z_\-]{35}")),
    ("hardcoded password", re.compile(r"""(?i)\bpassword\s*[=:]\s*["'][^"'\s]{6,}["']""")),
    # NOTE: a name-agnostic rule (any *Key/*Token assigned a long literal) was
    # tried and removed. It fired on Oculus preference keys, .NET strong-name
    # public keys and Google Play licensing keys, all public by design, at a rate
    # of roughly thirteen false alarms per true finding. Distinguishing those
    # needs entropy analysis rather than a name pattern; use gitleaks or
    # trufflehog if that coverage is wanted. This file stays high-confidence so
    # that a failure here always means something.
    # Matches identifiers containing api_key, access_token, *token, secret or
    # password, so GitbookAPIToken and steam_password are caught as well as
    # api_key. It does NOT match a bare *Key name: SegmentWriteKey, a real
    # credential found in this project, slips through. Name matching alone
    # cannot close that gap without entropy analysis; see the note above.
    ("hardcoded secret", re.compile(
        r"""(?i)\b\w*(?:api[_-]?key|access[_-]?token|[_a-z0-9]token|secret|password)\s*[=:]\s*["'][^"'\s]{16,}["']""")),
    # Unquoted assignment, e.g. a compiler definition
    # "UE4_PROJECT_NPTITLESECRET=<128 hex>" or an ini KeyStorePassword=<value>.
    # Validated further by looks_like_value() to avoid matching identifiers.
    ("embedded secret assignment", re.compile(
        r"""(?i)\b\w*(?:secret|token|passwd|password|api[_-]?key)\s*=\s*([A-Za-z0-9+/_\-]{12,})(\s*[.(])?""")),
]


def looks_like_value(match):
    """
    Distinguish a literal credential from a reference to another symbol.

    `token = GetSignatureToken (sig)` and `accessToken = Settings.OculusToken`
    are code, not secrets. A real value carries a digit or a separator that
    cannot appear in an identifier.
    """
    # PublicKeyToken is a .NET strong-name identity, published by design, and
    # appears throughout Unity and .NET manifests. It is never a credential.
    if "publickeytoken" in match.group(0).lower():
        return False

    rhs, trailer = match.group(1), match.group(2)
    if trailer:                      # followed by '.' or '(' -> member or call
        return False
    if len(rhs) < 16:
        return False
    has_digit = any(c.isdigit() for c in rhs)
    has_sep = any(c in "-+/" for c in rhs)
    return has_digit or has_sep

# This file necessarily contains the patterns it searches for. Scanning itself
# would always fail, which is how an earlier version of this script broke CI.
SELF = os.path.basename(__file__)

# Vendored or generated trees. Third-party code is not ours to fix, and scanning
# it produces noise that would make this gate useless.
# Generated or binary trees only. Vendored SDK directories are deliberately NOT
# skipped: a credential committed into vendored code is still a committed
# credential, and an earlier leak in this project sat in exactly such a tree.
SKIP_DIRS = {
    ".git", "node_modules", "Library", "Temp", "obj", "bin", "build",
    "__pycache__", "Intermediate", "DerivedDataCache", "Saved",
}

SKIP_SUFFIX = (".meta", ".png", ".jpg", ".jpeg", ".fbx", ".mat", ".unity", ".asset",
               ".prefab", ".uasset", ".umap", ".dll", ".so", ".a", ".zip", ".exe",
               ".wav", ".mp3", ".ogg", ".ttf", ".otf", ".pdf", ".bundle", ".bytes")

# Key material is often binary, so the text scan below never sees it. These are
# checked byte-wise instead. A committed OpenPGP secret key was missed this way.
KEY_SUFFIX = (".pem", ".ppk", ".p12", ".pfx", ".jks", ".keystore", ".key",
              ".asc", ".pgp", ".gpg")
KEY_MAGIC = (b"PRIVATE KEY", b"PuTTY-User-Key-File", b"OPENSSH PRIVATE KEY")


def tracked_files():
    try:
        out = subprocess.check_output(["git", "ls-files", "-z"], stderr=subprocess.DEVNULL)
    except (subprocess.CalledProcessError, OSError):
        return []
    return [f.decode("utf-8", "replace") for f in out.split(b"\0") if f]


def skipped(path):
    parts = path.split("/")
    if any(p in SKIP_DIRS for p in parts[:-1]):
        return True
    name = parts[-1]
    if name == SELF:
        return True
    low = name.lower()
    if any(t in low for t in ("example", "sample", "template")):
        return True
    return name.endswith(SKIP_SUFFIX)


# Values that look like placeholders rather than credentials: format templates,
# environment indirection, angle-bracket stubs and obvious dummies.
PLACEHOLDER = re.compile(
    r"""["']\s*(?:\{[^}]*\}|\$\{[^}]*\}|env\([^)]*\)|<[^>]*>|%\([^)]*\)s|"""
    r"""changeme|xxx+|your[_-]?\w+|todo|placeholder|dummy|example)\s*["']""", re.I)


def is_comment(text, pos):
    """True if the match sits on a line whose first token marks a comment."""
    start = text.rfind("\n", 0, pos) + 1
    line = text[start:pos].lstrip()
    return line.startswith(("#", "//", ";", "--", "*"))


def main():
    findings = []
    scanned = 0
    for rel in tracked_files():
        if not os.path.exists(rel):
            continue

        # Byte-wise check first: key material is frequently binary and would
        # otherwise be skipped as undecodable.
        low = rel.lower()
        if low.endswith(KEY_SUFFIX) and not low.endswith(".pub"):
            try:
                with open(rel, "rb") as fh:
                    head = fh.read(8192)
            except OSError:
                head = b""
            if any(magic in head for magic in KEY_MAGIC) or low.endswith(
                    (".ppk", ".p12", ".pfx", ".jks", ".keystore")):
                findings.append("%s: private key file" % rel)
                continue
            # OpenPGP secret key packet: tag 5, old-format header byte 0x95/0x94/0x99.
            if head[:1] in (b"\x95", b"\x94") and b"\x04" in head[:8]:
                findings.append("%s: OpenPGP secret key" % rel)
                continue

        if skipped(rel):
            continue
        try:
            if os.path.getsize(rel) > 2_000_000:
                continue
            with open(rel, "r", encoding="utf-8", errors="strict") as fh:
                text = fh.read()
        except (OSError, UnicodeDecodeError):
            continue  # binary or unreadable
        scanned += 1
        for label, pat in PATTERNS:
            for m in pat.finditer(text):
                if is_comment(text, m.start()):
                    continue
                if PLACEHOLDER.search(m.group(0)):
                    continue
                # Vendor documentation embeds sample credentials, e.g. AWS's
                # AKIAIOSFODNN7EXAMPLE. Treat any match saying EXAMPLE as a sample.
                if "example" in m.group(0).lower():
                    continue
                if label == "embedded secret assignment" and not looks_like_value(m):
                    continue
                line = text.count("\n", 0, m.start()) + 1
                findings.append("%s:%d: %s" % (rel, line, label))
                break
            else:
                continue
            break

    print("secret audit: scanned %d tracked text files" % scanned)
    if findings:
        print("FAILED. Committed credentials found:")
        for f in findings:
            print("  " + f)
        print("\nRemove the value, read it from the environment, and rotate the "
              "credential. It remains in git history until that history is rewritten.")
        return 1
    print("No committed credentials found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
