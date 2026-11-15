"""Run Unity smoke tests and reject missing, empty, or unsuccessful NUnit results."""

import argparse
from datetime import datetime, timezone
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=("All", "EditMode", "PlayMode"), default="All")
    parser.add_argument("--editor", default=os.environ.get("UNITY_EDITOR"))
    parser.add_argument("--test-filter")
    parser.add_argument("--timeout", type=int, default=600)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("--timeout must be positive")
    project = Path(__file__).resolve().parents[1]
    version_file = project / "ProjectSettings/ProjectVersion.txt"
    match = re.search(r"^m_EditorVersion: (\S+)$", version_file.read_text(), re.MULTILINE)
    if not match:
        parser.error("ProjectVersion.txt does not specify an Editor version")
    editor = Path(args.editor or (
        f"/Applications/Unity/Hub/Editor/{match.group(1)}/Unity.app/Contents/MacOS/Unity"
    )).expanduser()
    if not editor.is_file():
        parser.error("Unity Editor not found; use --editor or UNITY_EDITOR")
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    output = project / "TestResults" / stamp
    output.mkdir(parents=True)
    platforms = ("EditMode", "PlayMode") if args.platform == "All" else (args.platform,)
    for platform in platforms:
        results = output / f"{platform}.xml"
        log = output / f"{platform}.log"
        command = [str(editor), "-batchmode", "-nographics", "-projectPath", str(project),
                   "-buildTarget", "Android", "-runTests", "-testPlatform", platform,
                   "-testResults", str(results), "-logFile", str(log)]
        if args.test_filter:
            command += ["-testFilter", args.test_filter]
        print(f"Running {platform}; results: {results}; log: {log}", flush=True)
        try:
            completed = subprocess.run(command, timeout=args.timeout, check=False)
        except (subprocess.TimeoutExpired, OSError) as error:
            print(f"FAIL: {platform} could not finish: {error}")
            return 1
        try:
            report = ET.parse(results).getroot()
            passed = int(report.get("passed", "0"))
            failed = int(report.get("failed", "0"))
            total = int(report.get("total", "0"))
        except (OSError, ET.ParseError, ValueError) as error:
            print(f"FAIL: invalid or missing {platform} report (Editor exit {completed.returncode}): {error}")
            return 1
        print(f"{platform}: result={report.get('result')}, total={total}, "
              f"passed={passed}, failed={failed}, Editor exit={completed.returncode}")
        if completed.returncode != 0 or report.get("result") != "Passed" or passed <= 0 or failed != 0:
            for case in report.iter("test-case"):
                if case.get("result") == "Failed":
                    print(f"FAIL: {case.get('fullname')}: {case.findtext('failure/message', '')}")
            return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
