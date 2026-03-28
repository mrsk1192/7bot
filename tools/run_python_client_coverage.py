from __future__ import annotations

import json
import subprocess
import sys
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ARTIFACTS = ROOT / "artifacts" / "coverage"
JSON_REPORT = ARTIFACTS / "coverage.json"
XML_REPORT = ARTIFACTS / "coverage.xml"
TEXT_REPORT = ARTIFACTS / "summary.txt"
MODULE_REPORT = ARTIFACTS / "module_summary.md"


def run(cmd: list[str]) -> None:
    subprocess.run(cmd, cwd=str(ROOT), check=True)


def build_module_summary() -> None:
    payload = json.loads(JSON_REPORT.read_text(encoding="utf-8"))
    files = payload.get("files", {})
    grouped: dict[str, dict[str, float]] = defaultdict(lambda: {"covered": 0.0, "num": 0.0, "missing": 0.0, "branches": 0.0, "partial": 0.0})
    for path, data in files.items():
        rel = Path(path).as_posix()
        parts = rel.split("/")
        module = "other"
        if "gui" in parts:
            module = "GUI"
        elif "runtime" in parts or "decision" in parts or "priority" in parts or "commanding" in parts or "bases" in parts or "build" in parts:
            module = "Agent"
        elif "client.py" in rel or "exceptions.py" in rel or "models.py" in rel:
            module = "Bridge client"
        elif "search" in parts or "exploration" in parts or "movement" in parts:
            module = "Search / movement / exploration"
        elif "configuration" in rel or "navigation_config.py" in rel or "session_store.py" in rel:
            module = "Config"
        summary = data.get("summary", {})
        grouped[module]["covered"] += float(summary.get("covered_lines", 0))
        grouped[module]["num"] += float(summary.get("num_statements", 0))
        grouped[module]["missing"] += float(summary.get("missing_lines", 0))
        grouped[module]["branches"] += float(summary.get("num_branches", 0))
        grouped[module]["partial"] += float(summary.get("num_partial_branches", 0))

    lines = [
        "# Python Client Coverage Summary",
        "",
        "| Module | Statements | Covered | Missing | Statement % | Branches | Partial Branches |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for module, values in sorted(grouped.items()):
        statements = values["num"]
        covered = values["covered"]
        percent = 0.0 if statements == 0 else (covered / statements) * 100.0
        lines.append(
            f"| {module} | {int(statements)} | {int(covered)} | {int(values['missing'])} | {percent:.2f}% | {int(values['branches'])} | {int(values['partial'])} |"
        )
    MODULE_REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    run([sys.executable, "-m", "coverage", "erase"])
    run(
        [
            sys.executable,
            "-m",
            "coverage",
            "run",
            "--branch",
            "-m",
            "pytest",
            "python_client/tests",
            "-q",
        ]
    )
    for script in [
        "python_client/examples/phase45_exploration_smoke_test.py",
        "python_client/examples/phase5_search_smoke_test.py",
        "python_client/examples/gui_only_function_test.py",
        "python_client/examples/agent_control_runtime_smoke_test.py",
    ]:
        run([sys.executable, "-m", "coverage", "run", "--branch", "-a", script])
    with TEXT_REPORT.open("w", encoding="utf-8") as handle:
        subprocess.run(
            [sys.executable, "-m", "coverage", "report", "-m"],
            cwd=str(ROOT),
            check=True,
            stdout=handle,
            stderr=subprocess.STDOUT,
        )
    run([sys.executable, "-m", "coverage", "xml", "-o", str(XML_REPORT)])
    run([sys.executable, "-m", "coverage", "json", "-o", str(JSON_REPORT)])
    run([sys.executable, "-m", "coverage", "html", "-d", str(ARTIFACTS / "html")])
    build_module_summary()
    print(f"Coverage artifacts written to: {ARTIFACTS}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
