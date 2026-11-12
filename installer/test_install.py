#!/usr/bin/env python3
"""Fast offline unit tests; no root, apt, database or network required."""
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).parent))
import install as ogl

REPO = Path(__file__).resolve().parents[1]


class InstallerTests(unittest.TestCase):
    def test_ini_replace_keeps_other_sections_and_comments(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "test.ini"
            p.write_text("; comment\n[Example]\n Key=old\n Key = duplicate\n"
                         "[Other]\n x=keep\n")
            ogl.put_ini(p, "Example", "Key", '"new"')
            self.assertEqual(p.read_text().count('Key = "new"'), 1)
            self.assertIn("x=keep", p.read_text())
            ogl.remove_ini(p, "Other", "x")
            self.assertNotIn("x=keep", p.read_text())

    def test_packaged_runtime_structure(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            name = "OpenGenesisLINK-Generation1"
            archive = root / "test.zip"
            data = {
                "product": "OpenGenesisLINK Generation 1", "channel": "generation1-stable",
                "stable_release": True, "version": "0.9.4.0", "runtime": ".NET 10",
                "platform": "linux-x64", "source_commit": "a" * 40,
            }
            runtime = json.dumps({"runtimeOptions": {"tfm": "net10.0"}})
            with zipfile.ZipFile(archive, "w") as z:
                z.writestr(f"{name}/BUILD-MANIFEST.json", json.dumps(data))
                for p in ogl.REQUIRED:
                    contents = runtime if p.endswith("runtimeconfig.json") else "example"
                    z.writestr(f"{name}/bin/{p}", contents)
            pkg = ogl.unpack_verified(archive, root)
            self.assertTrue((pkg / "bin" / "OpenSimDefaults.ini").is_file())

    def test_reject_zip_traversal(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            archive = root / "bad.zip"
            with zipfile.ZipFile(archive, "w") as z:
                z.writestr("../evil.sh", "bad")
            with self.assertRaises(RuntimeError):
                ogl.unpack_verified(archive, root)

    def test_runtime_config_examples(self):
        with tempfile.TemporaryDirectory() as td:
            d = Path(td)
            b = d / "simulator" / "bin"
            (b / "config-include").mkdir(parents=True)
            (b / "Regions").mkdir()
            shutil.copy2(REPO / "bin/OpenSim.ini.example", b / "OpenSim.ini.example")
            shutil.copy2(REPO / "bin/config-include/GridCommon.ini.example",
                         b / "config-include/GridCommon.ini.example")
            cfg = {
                "robust": True, "simulator": True, "local_db": True,
                "dbhost": "127.0.0.1", "dbname": "opengenesislink",
                "dbuser": "ogl", "public_host": "mygrid.example.org",
                "public_port": 8002, "private_url": "http://127.0.0.1:8003",
                "public_url": "http://mygrid.example.org:8002",
                "region_host": "region.example.org", "region_name": "Welcome",
                "region_port": 9000, "estate_name": "Test Estate",
                "estate_owner": "Admin Resident",
            }
            ogl.configure_simulator(b, cfg)
            ini = (b / "OpenSim.ini").read_text()
            common = (b / "config-include/GridCommon.ini").read_text()
            self.assertIn('Include-Architecture = "config-include/GridHypergrid.ini"', ini)
            self.assertIn('http_listener_port = 9000', ini)
            self.assertIn('DefaultEstateName = "Test Estate"', ini)
            self.assertIn('DefaultEstateOwnerName = "Admin Resident"', ini)
            self.assertIn('Password=' + '$' + '{Environment|NEXVERSE_DB_PASSWORD}', common)
            self.assertIn('GatekeeperURI = "http://mygrid.example.org:8002"', common)
            self.assertNotIn('Include-Storage = "config-include/storage/SQLiteStandalone.ini"', common)
            self.assertTrue((b / "Regions/Regions.ini").is_file())

    def test_robust_uses_generic_template(self):
        with tempfile.TemporaryDirectory() as td:
            d = Path(td)
            shutil.copy2(REPO / "bin/Robust.HG.ini.example", d / "Robust.HG.ini.example")
            cfg = {
                "public_host": "grid.example.org", "public_port": 8002,
                "private_url": "http://127.0.0.1:8003", "local_db": True,
                "dbhost": "127.0.0.1", "dbname": "opengenesislink",
                "dbuser": "ogl", "grid": "Test Grid", "nick": "TestGrid",
            }
            ogl.configure_robust(d, cfg)
            ini = (d / "Robust.HG.ini").read_text()
            self.assertIn('gridname = "Test Grid"', ini)
            self.assertIn("Database=opengenesislink", ini)
            self.assertIn("Password=" + "$" + "{Environment|NEXVERSE_DB_PASSWORD}", ini)


if __name__ == "__main__":
    unittest.main()
