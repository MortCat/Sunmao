"""Validate the catalog and deterministic retrieval index without external dependencies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

from verification_profiles import PROFILES, RECIPE_PROJECTS


def validate_schema(value, schema, location="$"):
    """Validate the documented JSON Schema subset; unknown keywords fail closed."""
    supported = {"$schema", "title", "description", "type", "properties", "required",
                 "additionalProperties", "items", "minItems", "minLength", "enum", "const", "pattern"}
    unknown = set(schema) - supported
    if unknown:
        raise ValueError(f"{location}: unsupported schema keywords {sorted(unknown)}")
    types = {"object": dict, "array": list, "string": str, "integer": int,
             "number": (int, float), "null": type(None), "boolean": bool}
    expected = schema.get("type")
    expected = [expected] if isinstance(expected, str) else expected
    if expected and not any(isinstance(value, types[k]) and
                            not (isinstance(value, bool) and k in ("integer", "number")) for k in expected):
        raise ValueError(f"{location}: expected {expected}")
    if "const" in schema and value != schema["const"]:
        raise ValueError(f"{location}: unexpected constant")
    if "enum" in schema and value not in schema["enum"]:
        raise ValueError(f"{location}: unsupported value {value}")
    if isinstance(value, dict):
        missing = set(schema.get("required", [])) - set(value)
        extra = set(value) - set(schema.get("properties", {}))
        if missing or (extra and schema.get("additionalProperties") is False):
            raise ValueError(f"{location}: missing {sorted(missing)}, extra {sorted(extra)}")
        for key, child in schema.get("properties", {}).items():
            if key in value:
                validate_schema(value[key], child, f"{location}.{key}")
    if isinstance(value, list):
        if len(value) < schema.get("minItems", 0):
            raise ValueError(f"{location}: too few entries")
        for index, child in enumerate(value):
            validate_schema(child, schema.get("items", {}), f"{location}[{index}]")
    if isinstance(value, str):
        if len(value) < schema.get("minLength", 0):
            raise ValueError(f"{location}: empty string")
        if "pattern" in schema and not re.search(schema["pattern"], value):
            raise ValueError(f"{location}: invalid format")


def resolve_file(root, relative):
    path = (root / relative).resolve()
    if Path(relative).is_absolute() or not path.is_relative_to(root.resolve()) or not path.is_file():
        raise ValueError(f"Missing or out-of-root file: {relative}")
    return path


def load_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"Duplicate JSON key: {key}")
            result[key] = value
        return result
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique)


def digest(path):
    # Text normalization makes Git CRLF checkout policy irrelevant to the index.
    return hashlib.sha256(path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").encode()).hexdigest()


def source_fingerprint(root):
    paths = [p for folder in ("src", "tests", "recipes", "tools", "agent", "templates", ".github")
             for p in (root / folder).rglob("*")
             if p.is_file() and not {"bin", "obj", "__pycache__"} & set(p.relative_to(root).parts)
             and p.suffix in (".cs", ".csproj", ".props", ".json", ".py", ".ps1", ".md", ".xaml", ".yml")
             and p != root / "agent/index.json"]
    paths.extend(p for p in (root / "Directory.Build.props", root / "Directory.Packages.props",
                            root / "Sunmao.slnx", root / "global.json", root / "AGENTS.md") if p.is_file())
    content = "\n".join(f"{p.relative_to(root).as_posix()}:{digest(p)}"
                        for p in sorted(set(paths), key=lambda item: item.relative_to(root).as_posix()))
    return hashlib.sha256(content.encode()).hexdigest()


def build_index(root):
    catalog = load_json(root / "agent/capabilities.json")
    validate_schema(catalog, load_json(root / "agent/capabilities.schema.json"))
    properties = ET.parse(root / "Directory.Build.props")
    version = properties.findtext(".//Version")
    solution_projects = {node.get("Path") for node in ET.parse(root / "Sunmao.slnx").iter("Project")}

    def frameworks(project):
        declared = ET.parse(project).findtext(".//TargetFrameworks", "")
        for property_name in ("SunmaoTargetFrameworks", "SunmaoWindowsTargetFrameworks"):
            declared = declared.replace("$(" + property_name + ")", properties.findtext(".//" + property_name, ""))
        return sorted(declared.split(";"))

    seen, recipes, entries = set(), set(), []
    for item in catalog["capabilities"]:
        capability = item["id"]
        if capability in seen or item["recipe"]["id"] in recipes:
            raise ValueError(f"Duplicate capability or recipe ID: {capability}")
        seen.add(capability)
        recipes.add(item["recipe"]["id"])
        if item["api_version"] != version:
            raise ValueError(f"{capability}: package version drift")
        if item["verification_profile"] not in PROFILES or item["verification_profile"] == "catalog":
            raise ValueError(f"{capability}: profile must execute recipe tests")
        package = resolve_file(root, f"src/{item['package']}/{item['package']}.csproj")
        if sorted(item["target_frameworks"]) != frameworks(package):
            raise ValueError(f"{capability}: target framework drift")
        recipe = item["recipe"]
        test = item["test"]
        paths = item["source_paths"] + [item["contract_path"], recipe["source"], recipe["readme"],
                                       test["source"], test["project"]]
        hashes = {relative: digest(resolve_file(root, relative)) for relative in sorted(set(paths))}
        project = resolve_file(root, test["project"])
        if test["project"] not in RECIPE_PROJECTS or test["project"] not in solution_projects:
            raise ValueError(f"{capability}: test project is not executed by the profile")
        if sorted(item["target_frameworks"]) != frameworks(project):
            raise ValueError(f"{capability}: recipe test target framework drift")
        includes = [node.get("Include", "") for node in ET.parse(project).iter("Compile")]
        if resolve_file(root, recipe["source"]) not in [
                (project.parent / include.replace("\\", "/")).resolve() for include in includes]:
            raise ValueError(f"{capability}: recipe is not compiled by its test project")
        test_text = resolve_file(root, test["source"]).read_text(encoding="utf-8")
        if not re.search(r"\bclass\s+" + re.escape(test["class"]) + r"\b", test_text):
            raise ValueError(f"{capability}: test class not found")
        entries.append({"id": capability, "summary": item["summary"], "package": item["package"],
                        "api_version": item["api_version"], "contract_path": item["contract_path"],
                        "recipe": recipe, "test": test, "verification_profile": item["verification_profile"],
                        "limitations": item["limitations"], "file_hashes": hashes})
    return {"schema_version": 1, "source_fingerprint": source_fingerprint(root),
            "entries": sorted(entries, key=lambda entry: entry["id"])}


def serialize(index):
    return json.dumps(index, indent=2, ensure_ascii=False) + "\n"


def check_index(root):
    expected = serialize(build_index(root))
    path = root / "agent/index.json"
    if not path.is_file() or path.read_text(encoding="utf-8") != expected:
        raise ValueError("Stale retrieval index. Review changes, then run --write-index.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-index", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    try:
        expected = serialize(build_index(root))
        path = root / "agent/index.json"
        if args.write_index:
            path.write_text(expected, encoding="utf-8")
            print("Regenerated agent/index.json; this is not evidence of passing tests.")
        else:
            check_index(root)
            print("Catalog and retrieval index passed.")
        return 0
    except (ValueError, OSError, ET.ParseError) as error:
        print(f"Catalog failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
