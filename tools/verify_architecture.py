#!/usr/bin/env python3
"""Architecture checks for the Sunmao repository.

Run from the repository root:
    python tools/verify_architecture.py --root .
    python tools/verify_architecture.py --self-test

Rules
    A1  Every project under src/ is registered below and has a README.md.
    A2  Project and package references follow the allowed dependency direction.
    A3  WPF is used only by the WPF packages and by windows-targeted tests/samples/templates.
    A4  Forbidden concurrency primitives never appear in library, sample or template code.
    A5  PollingTaskBase subclasses are never public: pollers are private owner members.
    A6  Approved concurrency primitives match tools/architecture_concurrency_baseline.json exactly.
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

# Allowed references per library project. Adding a package means adding it here, which keeps the
# dependency direction a reviewed decision instead of an accident.
ALLOWED_DEPENDENCIES: dict[str, dict[str, frozenset[str]]] = {
    "Sunmao.Core": {"projects": frozenset(), "packages": frozenset()},
    "Sunmao.Diagnostics": {"projects": frozenset({"Sunmao.Core"}), "packages": frozenset()},
    "Sunmao.Testing": {"projects": frozenset({"Sunmao.Core"}), "packages": frozenset()},
    "Sunmao.Wpf": {"projects": frozenset({"Sunmao.Core"}), "packages": frozenset()},
    "Sunmao.Wpf.Theme": {
        "projects": frozenset({"Sunmao.Core", "Sunmao.Wpf"}),
        "packages": frozenset(),
    },
    "Sunmao.Communication": {"projects": frozenset({"Sunmao.Core"}), "packages": frozenset()},
    "Sunmao.Communication.Serial": {
        "projects": frozenset({"Sunmao.Core", "Sunmao.Communication"}),
        "packages": frozenset({"System.IO.Ports"}),
    },
    "Sunmao.Communication.Modbus": {
        "projects": frozenset({"Sunmao.Core", "Sunmao.Communication"}),
        "packages": frozenset({"NModbus"}),
    },
}
WPF_LIBRARIES = frozenset({"Sunmao.Wpf", "Sunmao.Wpf.Theme"})
CODE_ROOTS = ("src", "samples", "templates")

BASELINE_NAME = "architecture_concurrency_baseline.json"
BASELINE_PRIMITIVES: dict[str, re.Pattern[str]] = {
    "lock": re.compile(r"\block\s*\("),
    "channel.create_bounded": re.compile(r"\bChannel\s*\.\s*CreateBounded\b"),
    "task.run": re.compile(r"\bTask\s*\.\s*Run\s*\("),
    "semaphore_slim.new": re.compile(
        r"(?:\bnew\s+SemaphoreSlim\s*\(|\bSemaphoreSlim\??\s+[A-Za-z_]\w*\s*=\s*new\s*\()"
    ),
    "cancellation_token_source.new": re.compile(
        r"(?:\bnew\s+CancellationTokenSource\s*\(|\bCancellationTokenSource\??\s+[A-Za-z_]\w*\s*=\s*new\s*\()"
    ),
    "cancellation_token_source.linked": re.compile(r"\bCancellationTokenSource\s*\.\s*CreateLinkedTokenSource\s*\("),
    "timer.new": re.compile(r"\bnew\s+(?:System\.Threading\.)?Timer\s*\("),
    "dispatcher_timer.new": re.compile(r"\bnew\s+(?:System\.Windows\.Threading\.)?DispatcherTimer\s*\("),
    "async_void": re.compile(r"\basync\s+(?:[A-Za-z_][\w<>,\[\]?\.]*\s+)*void\s+[A-Za-z_]\w*\s*\("),
}
FORBIDDEN_PRIMITIVES: dict[str, re.Pattern[str]] = {
    "new Thread": re.compile(r"\bnew\s+Thread\s*\("),
    "Thread.Sleep": re.compile(r"\bThread\s*\.\s*Sleep\s*\("),
    "Task.Factory.StartNew": re.compile(r"\bTask\s*\.\s*Factory\s*\.\s*StartNew\s*\("),
    "Channel.CreateUnbounded": re.compile(r"\bChannel\s*\.\s*CreateUnbounded\b"),
    "fire-and-forget Task.Run": re.compile(r"(?m)^\s*_\s*=\s*Task\s*\.\s*Run\s*\("),
    "PeriodicTimer": re.compile(r"\bnew\s+PeriodicTimer\s*\("),
    "sync-over-async GetResult": re.compile(r"\.\s*GetAwaiter\s*\(\s*\)\s*\.\s*GetResult\s*\("),
}
# Reviewed exceptions to A4, keyed by (path, label). Keep this list short and explained.
FORBIDDEN_EXEMPTIONS: dict[tuple[str, str], str] = {
    ("src/Sunmao.Testing/SingleThreadUiTestHost.cs", "new Thread"):
        "The test host owns exactly one STA thread with a message pump; it only runs test scenarios.",
}
PUBLIC_POLLER = re.compile(
    r"\bpublic\s+(?:(?:sealed|abstract|partial)\s+)*class\s+(\w+)(?:<[^>]*>)?\s*:\s*(?:[\w.]+\.)?PollingTaskBase\b"
)
WPF_USING = re.compile(r"(?m)^\s*(?:global\s+)?using\s+(?:static\s+)?System\.Windows(?:\.|;)")


@dataclass(frozen=True)
class CheckResult:
    rule: str
    description: str
    failures: tuple[str, ...] = ()

    @property
    def passed(self) -> bool:
        return not self.failures


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def display(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return str(path)


def iter_projects(root: Path, folder: str) -> Iterable[Path]:
    base = root / folder
    if base.is_dir():
        for path in sorted(base.rglob("*.csproj")):
            if not {"bin", "obj"} & set(part.lower() for part in path.parts):
                yield path


def iter_code(root: Path) -> Iterable[Path]:
    for folder in CODE_ROOTS:
        base = root / folder
        if not base.is_dir():
            continue
        for path in sorted(base.rglob("*.cs")):
            if not {"bin", "obj"} & set(part.lower() for part in path.parts):
                yield path


def project_references(project: Path) -> tuple[set[str], set[str], bool]:
    """Returns (project reference names, package ids, uses WPF)."""
    tree = ET.parse(project)
    projects: set[str] = set()
    packages: set[str] = set()
    uses_wpf = False
    for element in tree.getroot().iter():
        name = local_name(element.tag)
        include = element.attrib.get("Include", "")
        if name == "ProjectReference" and include:
            projects.add(Path(include.replace("\\", "/")).stem)
        elif name == "PackageReference" and include:
            packages.add(include)
        elif name == "UseWPF" and (element.text or "").strip().lower() == "true":
            uses_wpf = True
    return projects, packages, uses_wpf


# ---- C# masking: blank comments and literals so patterns only match code -------------------

def _skip_char_literal(text: str, start: int) -> int:
    index = start + 1
    while index < len(text):
        if text[index] == "\\":
            index += 2
        elif text[index] == "'":
            return index + 1
        else:
            index += 1
    return len(text)


def _string_end(text: str, index: int) -> int | None:
    """Returns the end offset of a string literal starting at index, or None if none starts."""
    cursor = index
    verbatim = False
    while cursor < len(text) and text[cursor] in "$@":
        verbatim |= text[cursor] == "@"
        cursor += 1
    if cursor >= len(text) or text[cursor] != '"':
        return None
    quotes = 0
    while cursor + quotes < len(text) and text[cursor + quotes] == '"':
        quotes += 1
    if quotes >= 3:
        closing = text.find('"' * quotes, cursor + quotes)
        return len(text) if closing < 0 else closing + quotes
    position = cursor + 1
    while position < len(text):
        if verbatim and text.startswith('""', position):
            position += 2
        elif not verbatim and text[position] == "\\":
            position += 2
        elif text[position] == '"':
            return position + 1
        else:
            position += 1
    return len(text)


def mask_csharp(text: str) -> str:
    characters = list(text)

    def blank(start: int, end: int) -> None:
        for position in range(start, min(end, len(characters))):
            if characters[position] not in "\r\n":
                characters[position] = " "

    index = 0
    while index < len(text):
        if text.startswith("//", index):
            newline = text.find("\n", index)
            end = len(text) if newline < 0 else newline
        elif text.startswith("/*", index):
            closing = text.find("*/", index + 2)
            end = len(text) if closing < 0 else closing + 2
        elif text[index] == "'":
            end = _skip_char_literal(text, index)
        else:
            string_end = _string_end(text, index)
            if string_end is None:
                index += 1
                continue
            end = string_end
        blank(index, end)
        index = end
    return "".join(characters)


def line_of(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


# ---- Rules ------------------------------------------------------------------------------------

def check_a1(root: Path) -> CheckResult:
    failures = []
    for project in iter_projects(root, "src"):
        name = project.stem
        if name not in ALLOWED_DEPENDENCIES:
            failures.append(f"{display(root, project)}: not registered in ALLOWED_DEPENDENCIES")
        if not (project.parent / "README.md").is_file():
            failures.append(f"{display(root, project)}: missing README.md next to the project")
    return CheckResult("A1", "Library projects are registered and documented", tuple(failures))


def check_a2(root: Path) -> CheckResult:
    failures = []
    for project in iter_projects(root, "src"):
        allowed = ALLOWED_DEPENDENCIES.get(project.stem)
        if allowed is None:
            continue
        projects, packages, _ = project_references(project)
        for reference in sorted(projects - allowed["projects"]):
            failures.append(f"{project.stem} must not reference project {reference}")
        for package in sorted(packages - allowed["packages"]):
            failures.append(f"{project.stem} must not reference package {package}")
    return CheckResult("A2", "Library dependencies follow the allowed direction", tuple(failures))


def check_a3(root: Path) -> CheckResult:
    failures = []
    for folder in ("src", "tests", "samples", "templates"):
        for project in iter_projects(root, folder):
            _, _, uses_wpf = project_references(project)
            if not uses_wpf:
                continue
            if folder == "src" and project.stem not in WPF_LIBRARIES:
                failures.append(f"{display(root, project)}: only {sorted(WPF_LIBRARIES)} may use WPF")
            text = read_text(project)
            if "-windows" not in text and "SunmaoWindowsTargetFrameworks" not in text:
                failures.append(f"{display(root, project)}: uses WPF without a windows target framework")
    for source in iter_code(root):
        parts = source.relative_to(root).parts
        if parts[0] == "src" and len(parts) > 1 and parts[1] not in WPF_LIBRARIES:
            text = read_text(source)
            for match in WPF_USING.finditer(text):
                failures.append(f"{display(root, source)}:{line_of(text, match.start())}: WPF namespace outside WPF packages")
    return CheckResult("A3", "WPF stays inside the WPF packages", tuple(failures))


def check_a4(root: Path) -> CheckResult:
    failures = []
    for source in iter_code(root):
        text = mask_csharp(read_text(source))
        for label, pattern in FORBIDDEN_PRIMITIVES.items():
            if (display(root, source), label) in FORBIDDEN_EXEMPTIONS:
                continue
            for match in pattern.finditer(text):
                failures.append(f"{display(root, source)}:{line_of(text, match.start())}: forbidden {label}")
    return CheckResult("A4", "No forbidden concurrency primitives", tuple(failures))


def check_a5(root: Path) -> CheckResult:
    failures = []
    for source in iter_code(root):
        text = mask_csharp(read_text(source))
        for match in PUBLIC_POLLER.finditer(text):
            failures.append(
                f"{display(root, source)}:{line_of(text, match.start())}: public poller {match.group(1)}; "
                "hold a private PollingTaskBase member instead"
            )
    return CheckResult("A5", "PollingTaskBase subclasses are not public", tuple(failures))


def scan_baseline_primitives(root: Path) -> dict[tuple[str, str], int]:
    counts: dict[tuple[str, str], int] = {}
    for source in iter_code(root):
        text = mask_csharp(read_text(source))
        relative = display(root, source)
        for primitive, pattern in BASELINE_PRIMITIVES.items():
            count = len(pattern.findall(text))
            if count:
                counts[(relative, primitive)] = count
    return counts


def check_a6(root: Path) -> CheckResult:
    description = "Concurrency primitives match the reviewed baseline"
    baseline_file = root / "tools" / BASELINE_NAME
    if not baseline_file.is_file():
        return CheckResult("A6", description, (f"missing {display(root, baseline_file)}",))
    try:
        document = json.loads(read_text(baseline_file))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        return CheckResult("A6", description, (f"cannot read baseline: {error}",))

    failures: list[str] = []
    expected: dict[tuple[str, str], int] = {}
    for entry in document.get("entries", []):
        key = (entry.get("path", ""), entry.get("primitive", ""))
        if key[1] not in BASELINE_PRIMITIVES:
            failures.append(f"baseline entry {key}: unknown primitive")
        if not str(entry.get("owner", "")).strip():
            failures.append(f"baseline entry {key}: owner explanation is required")
        if key in expected:
            failures.append(f"baseline entry {key}: duplicated")
        expected[key] = int(entry.get("count", -1))

    actual = scan_baseline_primitives(root)
    for key in sorted(set(expected) | set(actual)):
        want, have = expected.get(key, 0), actual.get(key, 0)
        if want != have:
            failures.append(f"{key[0]} {key[1]}: baseline {want}, source {have}")
    return CheckResult("A6", description, tuple(failures))


CHECKS = (check_a1, check_a2, check_a3, check_a4, check_a5, check_a6)


def run_checks(root: Path) -> list[CheckResult]:
    return [check(root) for check in CHECKS]


def print_results(results: Iterable[CheckResult]) -> int:
    results = list(results)
    failed = 0
    for result in results:
        status = "PASS" if result.passed else "FAIL"
        print(f"[{status}] {result.rule} {result.description}")
        for failure in result.failures:
            print(f"    - {failure}")
        failed += 0 if result.passed else 1
    print(f"{failed} checks failed out of {len(results)}.")
    return 1 if failed else 0


# ---- Self-test: every rule must fire on a seeded violation ---------------------------------

def _write(root: Path, relative: str, content: str) -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def _library(root: Path, name: str, body: str = "", readme: bool = True) -> None:
    _write(root, f"src/{name}/{name}.csproj", f'<Project Sdk="Microsoft.NET.Sdk">{body}</Project>')
    if readme:
        _write(root, f"src/{name}/README.md", f"# {name}")


def _baseline(root: Path, entries: list[dict]) -> None:
    _write(root, f"tools/{BASELINE_NAME}", json.dumps({"version": 1, "entries": entries}))


def run_self_test() -> int:
    cases: list[tuple[str, str, callable]] = []

    def clean(root: Path) -> None:
        _library(root, "Sunmao.Core")
        _baseline(root, [])

    def a1(root: Path) -> None:
        clean(root)
        _library(root, "Sunmao.Unregistered")
        _library(root, "Sunmao.Diagnostics", readme=False)

    def a2(root: Path) -> None:
        clean(root)
        _library(
            root,
            "Sunmao.Diagnostics",
            '<ItemGroup><ProjectReference Include="..\\Sunmao.Wpf\\Sunmao.Wpf.csproj" />'
            '<PackageReference Include="Newtonsoft.Json" /></ItemGroup>',
        )

    def a3(root: Path) -> None:
        clean(root)
        _library(root, "Sunmao.Communication", "<PropertyGroup><UseWPF>true</UseWPF></PropertyGroup>")
        _write(root, "src/Sunmao.Core/Bad.cs", "using System.Windows;\nclass Bad {}\n")

    def a4(root: Path) -> None:
        clean(root)
        _write(
            root,
            "src/Sunmao.Core/Bad.cs",
            "class Bad { void M() { Thread.Sleep(1); _ = 1; }\n"
            "void N() {\n    _ = Task.Run(() => {});\n var t = new PeriodicTimer(x); }\n"
            "// Thread.Sleep(1) in a comment is fine\n string s = \"new Thread(\"; }\n",
        )
        # The fire-and-forget line is also a counted Task.Run; baseline it so only A4 fails.
        _baseline(root, [{"path": "src/Sunmao.Core/Bad.cs", "primitive": "task.run", "count": 1, "owner": "x"}])

    def a5(root: Path) -> None:
        clean(root)
        _write(root, "src/Sunmao.Core/Bad.cs", "public sealed class BadPoller : PollingTaskBase { }\n")

    def a6(root: Path) -> None:
        clean(root)
        _write(root, "src/Sunmao.Core/Gate.cs", "class Gate { object o = new(); void M() { lock (o) { } } }\n")
        _baseline(root, [{"path": "src/Sunmao.Core/Gate.cs", "primitive": "lock", "count": 2, "owner": "x"}])

    cases = [("clean", "", clean), ("A1", "A1", a1), ("A2", "A2", a2), ("A3", "A3", a3),
             ("A4", "A4", a4), ("A5", "A5", a5), ("A6", "A6", a6)]
    errors: list[str] = []
    for label, expected_rule, arrange in cases:
        root = Path(tempfile.mkdtemp(prefix="sunmao-arch-"))
        try:
            arrange(root)
            results = {result.rule: result for result in run_checks(root)}
            failing = sorted(rule for rule, result in results.items() if not result.passed)
            wanted = [expected_rule] if expected_rule else []
            if failing != wanted:
                details = "; ".join(f for r in failing for f in results[r].failures)
                errors.append(f"{label}: expected failing {wanted}, got {failing} ({details})")
            if label == "A4" and len(results["A4"].failures) != 3:
                errors.append(f"A4: expected 3 findings (masking), got {list(results['A4'].failures)}")
        finally:
            shutil.rmtree(root, ignore_errors=True)
    if errors:
        for error in errors:
            print(f"self-test failure: {error}")
        return 1
    print("A1-A6 self-test passed.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return run_self_test()
    return print_results(run_checks(args.root.resolve()))


if __name__ == "__main__":
    sys.exit(main())
