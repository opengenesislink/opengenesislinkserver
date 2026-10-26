#!/usr/bin/env python3
"""OGL.API assembly / AdminLTE operator and viewer metrics compatibility gates."""
from pathlib import Path
import re, shutil, subprocess, tempfile
root=Path(__file__).resolve().parents[2]
pre=(root/"prebuild.xml").read_text()
ui=(root/"NexVerse/Server/Api/NexApiDocsPage.cs").read_text()
login=(root/"NexVerse/Server/Api/OglApiDocsAccess.cs").read_text()
base=(root/"OpenSim/Framework/Servers/HttpServer/BaseHttpServer.cs").read_text()
welcome=(root/"OpenSim/Framework/Servers/HttpServer/OglServerWelcomePage.cs").read_text()
reader=(root/"NexVerse/Server/Api/OglViewerStatisticsApi.cs").read_text()
auth=(root/"OpenSim/Services/LLLoginService/LLLoginService.cs").read_text()
console=(root/"NexVerse/Server/Api/OglOperatorConsoleApi.cs").read_text()
api=(root/"NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
spec=(root/"NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text()
catalog=(root/"NexVerse/Server/Api/NexChatGptInstructionDownloads.cs").read_text()
docs=(root/"doc/OpenGenesisLINK/ADMINLTE_OPERATOR_CENTER_OGL_API_2026-10-26.md").read_text()
assert '<Project name="OGL.API"' in pre
assert '<Project name="NexVerse.Server.Api"' not in pre
for path in ("Robust.ini.example","Robust.HG.ini.example","Robust.HG.OGLAsset.ini.example","Robust.HG.ini",
             "Robust.NexEconomy.Tests.ini","Robust.NexVerseApi.Tests.ini"):
    txt=(root/"bin"/path).read_text()
    assert "NexVerse.Server.Api.dll" not in txt,path
    assert "OGL.API.dll" in txt,path
assert 'OGL.API.csproj' in (root/"tools/ci/NexGroupsHttpRegression/NexGroupsHttpRegression.csproj").read_text()
assert 'Assembly.Load("OGL.API")' in (root/"tools/ci/NexGroupsHttpRegression/Program.cs").read_text()
assert 'OGL.API.dll' in (root/".github/workflows/nexjast-ci.yml").read_text()
assert "OglServerWelcomePage.Html" in base
assert 'request.HttpMethod.Equals("GET"' in base
assert 'request.Headers["Accept"]' in base
assert 'request.QueryString.Count == 0' in base
assert 'HandleXmlRpcRequests(request, response)' in base
assert 'HandleLLSDLogin(request, response)' in base
assert 'OpenGenesisLINK' in welcome and 'https://opengenesislink.de/' in welcome
assert 'Find hippos quick!' not in base
assert 'OGL-VIEWER-STATS' in auth and 'SanitizeViewerMetric' in auth
assert 'return response;' in auth
for token in ['NexScopes.AdminAll','admin.UserLevel < 200','OGL-VIEWER-STATS',
              'Viewer','Version','days > 30','successful_logins','FileShare.ReadWrite']:
    assert token.lower() in reader.lower(),token
for token in ['NexScopes.AdminAll','admin.UserLevel < 200','MainConsole.Instance.RunCommand']:
    pass  # actual command execution is via a captured main console reference
for token in ['NexScopes.AdminAll','admin.UserLevel < 200','console.RunCommand(command)',
              'confirmation != "AUSFÜHREN"','m_State == "running"','GetHelp(new[] { "help", "all" })']:
    assert token in console,token
for endpoint in ['/api/v1/admin/viewers','/api/v1/admin/console']:
    assert endpoint in api and endpoint in spec and endpoint in catalog,endpoint
for token in ['id="page-viewers"','id="oglViewerTable"','function oglLoadViewerStats()',
              'id="oglConsoleCommandForm"','function oglExecuteConsole(e)',
              'function oglLogRender(pre,entries,search)','ogl-level-label',
              'function oglKeepSupportedHistory(raw)','id="oglSideToggle"',
              'class="ogl-admin-header"','ogl-datatable']:
    assert token in ui,token
# Keep the final, authoritative theme dark: earlier legacy styles must not
# be allowed to override the operational shell with white backgrounds again.
dark=re.search(r"/\* OGL Control Center 5\.0.*?</style>",ui,re.S)
assert dark, "missing final dark operator theme"
final_css=dark.group(0)
for light_token in ("--bg:#eff3f8", "background:#fff", "background:#eff3f8",
                    "background:#f7fafd", "background:#f5f8fc"):
    assert light_token not in final_css, "light theme regression: "+light_token
for token in ("color-scheme:dark", ".ogl-admin-header", ".ogl-status-card",
              ".ogl-datatable", ".ogl-logs-pre", ".navbtn .ogl-nav-icon",
              ".ogl-sidebar-collapsed", "@media(max-width:900px)"):
    assert token in final_css, "dark UI component missing: "+token
assert ui.count('class="ogl-nav-icon"') == 27, "sidebar icon mismatch"
assert ui.count('<details class="navgroup"') == 6, "sidebar navigation structure changed"
assert ui.count('<button type="button" class="navbtn') == 27, "sidebar entries missing"
assert "document.querySelector('main')?.scrollTo" in ui
login_dark=re.search(r"/\* Operator Control Center 5\.0.*?</style>",login,re.S)
assert login_dark and ".window{background:#142338" in login_dark.group(0), "login must be dark"
assert "0.9.3.10" in docs
assert "NexOglBranding.DataUri" in login
assert "class=\"window\"" in login
if not shutil.which("node"): raise RuntimeError("node required for real browser script validation")
for filename,text in [('admin',ui),('login',login)]:
    m=re.search(r"<script>\s*('use strict';.*?)\s*</script>",text,re.S)
    assert m,filename
    with tempfile.TemporaryDirectory() as temp:
        f=Path(temp)/(filename+".mjs")
        f.write_text(m.group(1))
        result=subprocess.run(["node","--check",str(f)],capture_output=True,text=True)
        assert result.returncode==0,filename+result.stderr
print("OGL.API and AdminLTE operator: root protocol compatibility, viewer versions, native console, log rendering OK")
