"""Reviewed command allowlist; catalogs contain profile names, never commands."""
import os
from pathlib import Path
import sys

PROFILES = ("catalog", "recipes", "full")
RECIPE_PROJECTS = (
    "tests/Sunmao.Recipes.Tests/Sunmao.Recipes.Tests.csproj",
    "tests/Sunmao.Recipes.Wpf.Tests/Sunmao.Recipes.Wpf.Tests.csproj",
)


def steps_for(profile):
    if profile not in PROFILES:
        raise ValueError(f"Unsupported profile: {profile}")
    steps = [
        ("tool-tests", [sys.executable, "-m", "unittest", "discover", "-s", "tools/tests", "-v"]),
        ("catalog", [sys.executable, "tools/verify_catalog.py"]),
        ("architecture-self-test", [sys.executable, "tools/verify_architecture.py", "--self-test"]),
        ("architecture", [sys.executable, "tools/verify_architecture.py", "--root", "."]),
    ]
    if profile == "recipes":
        for project in RECIPE_PROJECTS:
            name = Path(project).stem
            steps.append((f"build-{name}", ["dotnet", "build", project, "-m:1", "-p:UseSharedCompilation=false"]))
            steps.append((f"test-{name}", ["dotnet", "test", project, "--no-build", "--no-restore", "-m:1"]))
    if profile == "full":
        steps.extend([
            ("build", ["dotnet", "build", "Sunmao.slnx", "-m:1", "-p:UseSharedCompilation=false"]),
            ("test", ["dotnet", "test", "Sunmao.slnx", "--no-build", "--no-restore", "-m:1"]),
            ("template", ["powershell" if os.name == "nt" else "pwsh", "-NoProfile", "-File", "tools/verify_template.ps1"]),
        ])
    steps.append(("diff-check", ["git", "diff", "--check"]))
    return steps
