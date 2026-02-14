#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def fail(errors: list[str]) -> None:
    if not errors:
        print("OSS sanity check passed.")
        return

    print("OSS sanity check failed:")
    for err in errors:
        print(f"- {err}")
    sys.exit(1)


def check_readmes(errors: list[str]) -> None:
    expected_issue_url = "https://github.com/ayutaz/uStyle-Bert-VITS2/issues"
    readmes = [ROOT / "README.md", ROOT / "README_EN.md"]

    for readme in readmes:
        text = readme.read_text(encoding="utf-8")
        if "<owner>" in text:
            errors.append(f"{readme} still contains <owner> placeholder.")
        if "--repo-id" in text:
            errors.append(f"{readme} still contains deprecated --repo-id option.")
        if "convert_sbv2_for_sentis.py --repo " not in text:
            errors.append(f"{readme} does not contain updated --repo command example.")
        if expected_issue_url not in text:
            errors.append(f"{readme} does not contain canonical GitHub Issues URL.")


def check_package_metadata(errors: list[str]) -> None:
    package_path = ROOT / "Assets/uStyleBertVITS2/package.json"
    if not package_path.exists():
        errors.append("Missing Assets/uStyleBertVITS2/package.json.")
        return

    data = json.loads(package_path.read_text(encoding="utf-8"))
    expected_name = "com.ustyle.bert-vits2"
    if data.get("name") != expected_name:
        errors.append(f"package.json name must be '{expected_name}'.")

    if "samples" not in data:
        errors.append("package.json is missing samples definition.")


def check_settings_defaults(errors: list[str]) -> None:
    settings_path = ROOT / "Assets/uStyleBertVITS2/Runtime/Core/Configuration/TTSSettings.cs"
    text = settings_path.read_text(encoding="utf-8")
    expected = 'public string StyleVectorPath = "uStyleBertVITS2/Models/style_vectors.npy";'
    if expected not in text:
        errors.append("TTSSettings.StyleVectorPath default is not aligned with current distribution path.")


def check_local_markdown_links(errors: list[str]) -> None:
    md_files = [
        ROOT / "README.md",
        ROOT / "README_EN.md",
        ROOT / "CONTRIBUTING.md",
        ROOT / "SECURITY.md",
        ROOT / "THIRD_PARTY_NOTICES.md",
    ]
    link_pattern = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")

    for md_file in md_files:
        text = md_file.read_text(encoding="utf-8")
        for raw_target in link_pattern.findall(text):
            target = raw_target.strip().strip("<>").strip()
            if not target:
                continue
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue

            target = target.split("#", 1)[0].split("?", 1)[0]
            if not target:
                continue

            if target.startswith("/"):
                resolved = ROOT / target.lstrip("/")
            else:
                resolved = md_file.parent / target

            if not resolved.exists():
                errors.append(f"Broken local markdown link in {md_file}: {raw_target}")


def main() -> None:
    errors: list[str] = []
    check_readmes(errors)
    check_package_metadata(errors)
    check_settings_defaults(errors)
    check_local_markdown_links(errors)
    fail(errors)


if __name__ == "__main__":
    main()
