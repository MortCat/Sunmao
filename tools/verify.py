"""Run fixed verification profiles and emit a machine-readable, fail-closed report."""
import argparse
import datetime
import json
import os
from pathlib import Path
import platform
import signal
import subprocess
import sys
import time
import uuid

from verification_profiles import PROFILES, steps_for


def terminate_tree(process):
    """Terminate only this runner's child tree, then reap the immediate child."""
    if os.name == "nt":
        result = subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                                capture_output=True, timeout=15, check=False)
        if result.returncode and process.poll() is None:
            raise RuntimeError("Cannot terminate the owned process tree: " + result.stderr.decode(errors="replace"))
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    process.wait(timeout=15)


def run_step(name, command, root, output, timeout):
    log = output / f"{name}.log"
    started = time.monotonic()
    status, code, diagnostic = "error", None, None
    process = None
    with log.open("w", encoding="utf-8") as stream:
        try:
            process = subprocess.Popen(command, cwd=root, stdin=subprocess.DEVNULL,
                                       stdout=stream, stderr=subprocess.STDOUT,
                                       start_new_session=os.name != "nt")
            code = process.wait(timeout=timeout)
            status = "passed" if code == 0 else "failed"
        except (subprocess.TimeoutExpired, KeyboardInterrupt) as error:
            status = "cancelled" if isinstance(error, KeyboardInterrupt) else "timed_out"
            diagnostic = f"{status}; child process tree terminated"
            if process is not None:
                try:
                    terminate_tree(process)
                    code = process.returncode
                except (OSError, RuntimeError, subprocess.SubprocessError) as cleanup_error:
                    status, diagnostic = "error", str(cleanup_error)
        except OSError as error:
            diagnostic = str(error)
        if diagnostic:
            stream.write("\n" + diagnostic + "\n")
    return {"id": name, "command": command, "status": status, "exit_code": code,
            "elapsed_seconds": round(time.monotonic() - started, 4),
            "log": str(log.resolve()), "diagnostic": diagnostic}


def run_steps(steps, root, output, timeout):
    results = []
    for name, command in steps:
        if results and results[-1]["status"] != "passed":
            results.append({"id": name, "command": command, "status": "blocked", "exit_code": None,
                            "elapsed_seconds": 0, "log": None, "diagnostic": "An earlier step did not pass."})
        else:
            print(f"Running {name}", file=sys.stderr, flush=True)
            results.append(run_step(name, command, root, output, timeout))
    return results


def aggregate(results):
    return "passed" if results and all(step["status"] == "passed" for step in results) else "failed"


def git_value(root, arguments):
    try:
        result = subprocess.run(["git", *arguments], cwd=root, capture_output=True, text=True,
                                timeout=10, check=False)
        return result.stdout.strip() if result.returncode == 0 else None
    except (OSError, subprocess.SubprocessError):
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=PROFILES, default="catalog")
    parser.add_argument("--timeout", type=int, default=600)
    args = parser.parse_args()
    if not 1 <= args.timeout <= 7200:
        parser.error("timeout must be in [1, 7200] seconds")
    root = Path(__file__).resolve().parent.parent
    run_id = str(uuid.uuid4())
    output = root / "artifacts" / "verification" / run_id
    output.mkdir(parents=True)
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    # Import lazily: catalog inspection itself must not run subprocess checks.
    from verify_catalog import source_fingerprint, validate_schema
    fingerprint = source_fingerprint(root)
    revision = git_value(root, ["rev-parse", "HEAD"])
    working_tree = git_value(root, ["status", "--porcelain"])
    results = run_steps(steps_for(args.profile), root, output, args.timeout)
    if source_fingerprint(root) != fingerprint:
        results.append({"id": "source-stability", "command": ["internal:source-stability"],
                        "status": "error", "exit_code": None, "elapsed_seconds": 0, "log": None,
                        "diagnostic": "Source changed during verification; rerun on stable source."})
    report = {"schema_version": 1, "run_id": run_id, "profile": args.profile,
              "started_at": started, "finished_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "source": {"revision": revision, "working_tree": working_tree, "fingerprint": fingerprint},
              "environment": {"platform": platform.platform(), "python": sys.version,
                              "step_timeout_seconds": args.timeout},
              "status": aggregate(results), "steps": results}
    validate_schema(report, json.loads((root / "agent/verification-result.schema.json").read_text(encoding="utf-8")))
    report_path = output / "result.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": report["status"], "result": str(report_path)}))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
