"""Failure-oriented tests for the catalog and verification runner (standard library only)."""
import copy
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
from verify_catalog import build_index, check_index, load_json, serialize, validate_schema
from verify import aggregate, run_steps, run_step
from verification_profiles import steps_for

ROOT = TOOLS.parent


class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="sunmao-catalog-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        catalog = load_json(ROOT / "agent/capabilities.json")
        paths = {"agent/capabilities.json", "agent/capabilities.schema.json", "Directory.Build.props", "Sunmao.slnx"}
        for item in catalog["capabilities"]:
            paths.update(item["source_paths"])
            paths.update([item["contract_path"], item["recipe"]["source"], item["recipe"]["readme"],
                          item["test"]["project"], item["test"]["source"],
                          f"src/{item['package']}/{item['package']}.csproj"])
        for relative in paths:
            destination = self.root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, destination)
        self.catalog = catalog

    def write(self):
        (self.root / "agent/capabilities.json").write_text(json.dumps(self.catalog), encoding="utf-8")

    def test_deterministic_index_changes_when_source_changes(self):
        original = serialize(build_index(self.root))
        self.assertEqual(original, serialize(build_index(self.root)))
        (self.root / "agent/index.json").write_text(original, encoding="utf-8")
        check_index(self.root)
        source = self.root / self.catalog["capabilities"][0]["source_paths"][0]
        source.write_text(source.read_text(encoding="utf-8") + "\n// Changed contract\n", encoding="utf-8")
        self.assertNotEqual(original, serialize(build_index(self.root)))
        with self.assertRaisesRegex(ValueError, "Stale"):
            check_index(self.root)

    def test_duplicate_ids_rejected(self):
        self.catalog["capabilities"].append(copy.deepcopy(self.catalog["capabilities"][0]))
        self.write()
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            build_index(self.root)

    def test_bad_metadata_rejected(self):
        original = copy.deepcopy(self.catalog)
        for key, value in (("api_version", "99.0.0"), ("verification_profile", "arbitrary-command"),
                           ("target_frameworks", ["net99.0"]), ("source_paths", ["missing.cs"]),
                           ("contract_path", "../outside.md")):
            with self.subTest(key=key):
                self.catalog = copy.deepcopy(original)
                self.catalog["capabilities"][0][key] = value
                self.write()
                with self.assertRaises(ValueError):
                    build_index(self.root)

    def test_recipe_must_be_compiled_and_test_class_must_exist(self):
        item = self.catalog["capabilities"][0]
        path = self.root / item["test"]["project"]
        path.write_text("<Project><PropertyGroup><TargetFrameworks>$(SunmaoTargetFrameworks)</TargetFrameworks>"
                        "</PropertyGroup></Project>", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "not compiled"):
            build_index(self.root)
        shutil.copyfile(ROOT / item["test"]["project"], path)
        item["test"]["class"] = "MissingTest"
        self.write()
        with self.assertRaisesRegex(ValueError, "test class"):
            build_index(self.root)

    def test_unknown_fields_and_schema_keywords_rejected(self):
        self.catalog["command"] = "anything"
        self.write()
        with self.assertRaises(ValueError):
            build_index(self.root)
        with self.assertRaisesRegex(ValueError, "unsupported schema"):
            validate_schema({}, {"unexpectedKeyword": True})

    def test_actual_build_target_drift_rejected(self):
        props = self.root / "Directory.Build.props"
        props.write_text(props.read_text(encoding="utf-8").replace("net8.0;net10.0", "net8.0"),
                         encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "framework drift"):
            build_index(self.root)

    def test_test_project_removed_from_solution_is_rejected(self):
        (self.root / "Sunmao.slnx").write_text("<Solution />", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "not executed"):
            build_index(self.root)

    def test_duplicate_json_keys_rejected(self):
        path = self.root / "duplicate.json"
        path.write_text('{"id": 1, "id": 2}', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Duplicate JSON"):
            load_json(path)


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="sunmao-runner-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def run_commands(self, commands, timeout=5):
        return run_steps(commands, self.root, self.root, timeout)

    def test_failure_blocks_following_steps_and_keeps_logs(self):
        results = self.run_commands([
            ("failure", [sys.executable, "-c", "print('diagnostic'); raise SystemExit(7)"]),
            ("blocked", [sys.executable, "-c", "raise SystemExit(0)"])])
        self.assertEqual("failed", aggregate(results))
        self.assertEqual(7, results[0]["exit_code"])
        self.assertEqual("blocked", results[1]["status"])
        self.assertIn("diagnostic", Path(results[0]["log"]).read_text())

    def test_timeout_terminates_owned_child(self):
        results = self.run_commands([("timeout", [sys.executable, "-c",
            "import threading; threading.Event().wait()"])], timeout=0.3)
        self.assertEqual("timed_out", results[0]["status"])
        self.assertIsNotNone(results[0]["exit_code"])
        self.assertEqual("failed", aggregate(results))

    @unittest.skipUnless(os.name == "nt", "Windows process-tree ownership check")
    def test_timeout_also_terminates_descendant(self):
        import ctypes
        command = (
            "import subprocess,sys,threading; from pathlib import Path; "
            "child=subprocess.Popen([sys.executable,'-c','import threading; threading.Event().wait()']); "
            "Path('child.pid').write_text(str(child.pid)); threading.Event().wait()"
        )
        results = self.run_commands([("tree-timeout", [sys.executable, "-c", command])], timeout=2)
        self.assertEqual("timed_out", results[0]["status"])
        pid = int((self.root / "child.pid").read_text())
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x00100000, False, pid)
        if handle:
            try:
                self.assertEqual(0, kernel.WaitForSingleObject(handle, 5000))
            finally:
                kernel.CloseHandle(handle)

    def test_cancellation_is_not_a_pass_and_requests_tree_cleanup(self):
        with patch("verify.subprocess.Popen") as factory, patch("verify.terminate_tree") as terminate:
            factory.return_value.wait.side_effect = KeyboardInterrupt()
            factory.return_value.returncode = -1
            result = run_step("cancel", ["injected"], self.root, self.root, 1)
            self.assertEqual("cancelled", result["status"])
            terminate.assert_called_once_with(factory.return_value)
            self.assertEqual("failed", aggregate([result]))

    def test_missing_executable_is_an_error(self):
        results = self.run_commands([("missing", [str(self.root / "does-not-exist")])])
        self.assertEqual("error", results[0]["status"])
        self.assertEqual("failed", aggregate(results))

    def test_pass_and_aggregate_cannot_accept_empty_or_blocked_runs(self):
        results = self.run_commands([("pass", [sys.executable, "-c", "print('ok')"])])
        self.assertEqual("passed", aggregate(results))
        self.assertEqual("failed", aggregate([]))
        self.assertEqual("failed", aggregate([{"status": "blocked"}]))
        with self.assertRaises(ValueError):
            steps_for("user-command")


if __name__ == "__main__":
    unittest.main()
