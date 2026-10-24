"""Read-only checks for repository Markdown encoding and local link targets.

Supports inline links/images, reference definitions, and quoted HTML href/src.
External URLs, fragments, code examples, and full CommonMark parsing are out of scope.
"""

from __future__ import annotations

import argparse
import re
import subprocess
from pathlib import Path, PurePosixPath
from urllib.parse import unquote, urlsplit


LINK = re.compile(r"!?\[[^\]\n]*\]\(\s*(<[^>\n]+>|[^\s)]+)")
REFERENCE = re.compile(r"^ {0,3}\[[^\]\n]+\]:\s*(<[^>\n]+>|\S+)", re.MULTILINE)
HTML_LINK = re.compile(r"\b(?:href|src)\s*=\s*([\"'])(.*?)\1", re.IGNORECASE)
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})(.*)$")
INLINE_CODE = re.compile(r"(`+).*?\1", re.DOTALL)


def prose_only(text: str) -> str:
    """Mask code and comments without changing line numbers."""
    def blank(match: re.Match) -> str:
        return re.sub(r"[^\n]", " ", match.group(0))

    text = re.sub(r"<!--.*?-->", blank, text, flags=re.DOTALL)
    lines = []
    fence = ""
    for line in text.splitlines(keepends=True):
        marker = FENCE.match(line)
        if fence:
            if marker and marker[1][0] == fence[0] and len(marker[1]) >= len(fence) and not marker[2].strip():
                fence = ""
            lines.append(re.sub(r"[^\n]", " ", line))
        elif marker:
            fence = marker[1]
            lines.append(re.sub(r"[^\n]", " ", line))
        elif line.startswith(("    ", "\t")):
            lines.append(re.sub(r"[^\n]", " ", line))
        else:
            lines.append(line)
    return INLINE_CODE.sub(blank, "".join(lines))


def markdown_files(root: Path) -> list[Path]:
    result = subprocess.run(
        ["git", "-C", str(root), "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        check=True, capture_output=True,
    )
    names = result.stdout.decode("utf-8").split("\0")
    # Deleted tracked files are absent from the working tree; inbound links still fail.
    return sorted({root / name for name in names if name.lower().endswith(".md") and (root / name).is_file()})


def target_error(root: Path, source: Path, destination: str) -> str | None:
    destination = destination.strip("<>")
    if "\\" in destination:
        return "use forward slashes in link paths"
    try:
        url = urlsplit(destination)
    except ValueError:
        return "invalid link URL"
    if url.scheme or url.netloc or not url.path:
        return None
    if url.path.startswith("/"):
        return "use a relative repository link"

    current = source.parent
    for part in PurePosixPath(unquote(url.path)).parts:
        if part == "..":
            current = current.parent
            if not current.is_relative_to(root):
                return "link escapes repository"
            continue
        if not current.is_dir():
            return "parent directory is missing"
        names = {entry.name for entry in current.iterdir()}
        if part not in names:
            match = next((name for name in names if name.casefold() == part.casefold()), None)
            return f"path case mismatch: use {match}" if match else "target is missing"
        current /= part
    if not current.exists():
        return "target is missing"
    if not current.resolve().is_relative_to(root):
        return "link resolves outside repository"
    return None


def check_document(root: Path, path: Path) -> tuple[list[str], int]:
    errors = []
    label = path.relative_to(root).as_posix()
    data = path.read_bytes()
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError:
        return [f"{label}: expected UTF-8"], 0
    if data.startswith(b"\xef\xbb\xbf"):
        errors.append(f"{label}: remove UTF-8 BOM")
    if b"\r" in data:
        errors.append(f"{label}: expected LF line endings")
    if data and not data.endswith(b"\n"):
        errors.append(f"{label}: missing final newline")
    prose = prose_only(text)
    links = [(match.start(), match[1]) for pattern in (LINK, REFERENCE) for match in pattern.finditer(prose)]
    links.extend((match.start(), match[2]) for match in HTML_LINK.finditer(prose))
    for position, destination in links:
        problem = target_error(root, path, destination)
        if problem:
            line = prose.count("\n", 0, position) + 1
            errors.append(f"{label}:{line}: {problem}: {destination}")
    return errors, len(links)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1], help="Git repository root")
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        files = markdown_files(root)
        if not files:
            print("No Git-visible Markdown files found.")
            return 1
        errors = []
        links = 0
        for path in files:
            problems, count = check_document(root, path)
            errors.extend(problems)
            links += count
    except (OSError, UnicodeError, subprocess.CalledProcessError) as error:
        print(f"Documentation check failed: {error}")
        return 1
    for error in errors:
        print(error)
    print(f"Checked {len(files)} Markdown files and {links} link destinations; {len(errors)} error(s).")
    return int(bool(errors))


if __name__ == "__main__":
    raise SystemExit(main())
