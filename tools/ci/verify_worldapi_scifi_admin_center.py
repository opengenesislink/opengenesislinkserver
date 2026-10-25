#!/usr/bin/env python3
"""Fail-closed checks for sci-fi protected World API admin center."""
from pathlib import Path
import base64, re, subprocess, shutil, tempfile

root=Path(__file__).resolve().parents[2]
page=(root/"NexVerse/Server/Api/NexApiDocsPage.cs").read_text()
login=(root/"NexVerse/Server/Api/OglApiDocsAccess.cs").read_text()
branding=(root/"NexVerse/Server/Api/NexOglBranding.cs").read_text()
assert "private static readonly string s_Html" in page
assert "private static readonly string s_LoginPage" in login
assert "NexOglBranding.DataUri" in page and "NexOglBranding.DataUri" in login
assert "OGL_LOGO_DATA_URI" in page and "OGL_LOGO_DATA_URI" in login
image=re.search(r'DataUri\s*=\s*"data:image/webp;base64,([A-Za-z0-9+/=]+)"',branding)
assert image, "Supplied logo must be stored as an inline WebP image"
data=base64.b64decode(image.group(1),validate=True)
assert data[:4]==b"RIFF" and data[8:12]==b"WEBP" and len(data)>2000
assert "frame-ancestors 'none'" in login
assert "m_Authenticator.TryAuthenticate" in login
assert "UserLevel < MinimumLevel" in login
assert 'adminIsActive()' in page
assert 'api.github.com' in page and "connect-src 'self' https://api.github.com" in page
for name in ["overview","statistics","diagnostics","repository","grid","nodes",
             "estates","console","users","groups","inventory","profiles","economy",
             "marketplace","discovery","experiences","secrets","security","explorer",
             "chatgpt","adminlogin","changes","versions","roadmap"]:
    assert 'data-page="'+name+'"' in page,name
    assert 'id="page-'+name+'"' in page,name
assert '<details class="navgroup"' in page
assert "oglRenderWorkspaces()" in page and "oglWorkspaceOpen(entry)" in page
assert "oglGithubJson(base+'/releases/latest')" in page
assert "oglGithubJson(base+'/commits/main')" in page
assert "oglGithubJson(base+'/actions/workflows/nexjast-ci.yml/runs?branch=main&per_page=1')" in page
assert "release?.tag_name||'Nicht verfügbar'" in page
assert "commit?.sha?" in page
assert "d.fail.length" in page
assert "data-workspace-search" in page
assert "secretKeySearch" in page and "secretKeyState" in page
assert "oglFilterKeys()" in page
assert "entries.filter(x=>prefixes.some(p=>x.path.startsWith(p)))" in page
assert "selectEntry(entry)" in page
assert "['secrets','nodes','grid','security','console'].includes(name)" in page
assert "keine unbeschränkte Shell" in page or "Keine unbeschränkte Shell" in page
# Extract JS from the actual HTML page (do not create a separate, unused demo file).
script=re.search(r"<script>\s*('use strict';.*?)\s*</script>",page,re.S)
assert script,"Embedded dashboard JS unavailable"
if shutil.which("node") is None: raise RuntimeError("Node is required for World API UI validation")
with tempfile.TemporaryDirectory() as td:
    f=Path(td)/"world-admin.mjs"
    f.write_text(script.group(1))
    result=subprocess.run(["node","--check",str(f)],capture_output=True,text=True)
    assert result.returncode==0,result.stderr
print("OGL World API Sci-Fi dashboard: logo, navigation, auth boundaries, GitHub and browser syntax OK")
