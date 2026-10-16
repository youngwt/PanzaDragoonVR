"""The shared asset-rule cases in tests/fixtures/asset-rules/, run through tools/manifest.py.

tests/GameLogic.Tests/AssetRulesTests.cs runs the same folders through the game's AssetManifest,
so write_manifest.py --check gives the lines the game's launch report gives. Run by tools/test.sh.
"""
import contextlib
import importlib.util
import io
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CASES = ROOT / "tests" / "fixtures" / "asset-rules"
sys.path.insert(0, str(ROOT / "tools"))
import make_test_model  # noqa: E402
import manifest  # noqa: E402
import write_manifest  # noqa: E402


def load_make_cases():
    spec = importlib.util.spec_from_file_location("make_cases", CASES / "make_cases.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def case_names():
    return sorted(p.name for p in CASES.iterdir() if (p / "expected.json").is_file())


def expected(name):
    return json.loads((CASES / name / "expected.json").read_text(encoding="utf-8"))


def files_in(folder):
    folder = Path(folder)
    return {str(p.relative_to(folder)): p.read_bytes() for p in sorted(folder.rglob("*"))
            if p.is_file() and "__pycache__" not in p.parts and p.name != "make_cases.py"}


def run_command(*arguments):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        status = write_manifest.main(["write_manifest.py", *arguments])
    return status, out.getvalue(), err.getvalue()


class SharedCases(unittest.TestCase):
    def test_cases_are_found(self):
        # A wrong path would otherwise make every test below pass with no cases at all.
        self.assertGreaterEqual(len(case_names()), 30)

    def test_every_case_gives_the_expected_problems(self):
        for name in case_names():
            with self.subTest(case=name):
                wanted = expected(name)
                notices = []
                _, problems = manifest.check_report(str(CASES / name), notices)
                if wanted.get("unreadable_manifest"):
                    self.assertEqual(len(problems), 1, problems)
                    self.assertTrue(problems[0].startswith(wanted["problems"][0]), problems[0])
                else:
                    self.assertEqual(problems, wanted["problems"])
                    self.assertEqual(notices, wanted.get("pc_notices", []))

    def test_check_command_reports_a_rejected_model_in_the_games_words(self):
        for name in (n for n in case_names() if n.startswith("model-")):
            with self.subTest(case=name):
                status, out, err = run_command("--check", str(CASES / name))
                self.assertEqual(status, 1)
                self.assertEqual(out, "")
                self.assertEqual(err.splitlines(), ["write_manifest: error: " + line for line in expected(name)["problems"]])

    def test_write_mode_refuses_a_model_the_game_rejects(self):
        for name in (n for n in case_names() if n.startswith("model-")):
            with self.subTest(case=name), tempfile.TemporaryDirectory() as folder:
                shutil.copytree(CASES / name / "models", Path(folder, "models"))
                status, _, err = run_command(folder)
                self.assertEqual(status, 1)
                self.assertFalse(Path(folder, "manifest.json").exists())
                self.assertEqual(err.splitlines(), ["write_manifest: error: " + line for line in expected(name)["problems"]])

    def test_the_good_case_holds_the_test_model(self):
        self.assertEqual((CASES / "good-folder" / "models" / "e03.flyer.glb").read_bytes(), make_test_model.build())

    def test_the_cases_are_what_make_cases_writes(self):
        # A case edited by hand, or a script changed without running it, shows here.
        make_cases = load_make_cases()
        with tempfile.TemporaryDirectory() as folder:
            make_cases.write(folder)
            self.assertEqual(files_in(folder), files_in(CASES))


if __name__ == "__main__":
    unittest.main()
