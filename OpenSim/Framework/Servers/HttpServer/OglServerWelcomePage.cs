// SPDX-License-Identifier: MPL-2.0
// Browser-only informational welcome document. Never intercept XML-RPC, LLSD,
// legacy protocol traffic, or the simulator's HTTP capabilities.
using System.Text;

namespace OpenSim.Framework.Servers.HttpServer
{
    internal static class OglServerWelcomePage
    {
        internal static readonly byte[] Html = Encoding.UTF8.GetBytes("""
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="dark">
<meta name="description" content="OpenGenesisLINK – Grid- und Weltserver für virtuelle Welten.">
<title>OpenGenesisLINK | Virtual World Server</title>
<style>
:root{font-family:system-ui,-apple-system,"Segoe UI",sans-serif;color:#eaf4ff;background:#07111e}
*{box-sizing:border-box}body{margin:0;min-height:100vh;background:radial-gradient(ellipse at 12% 8%,#123653,transparent 47%),linear-gradient(150deg,#07111e,#0d1c30)}
.wrap{max-width:1130px;margin:auto;padding:0 25px}
header{border-bottom:1px solid #25425b;display:flex;align-items:center;justify-content:space-between;gap:20px;min-height:87px}
.brand{display:flex;align-items:center;gap:13px;font-size:21px;font-weight:790;letter-spacing:-.03em}
.logo{background:linear-gradient(135deg,#207caa,#193a73);color:#fff;border:1px solid #3b9ece;border-radius:14px;width:50px;height:50px;display:grid;place-items:center;font-size:20px;box-shadow:0 0 30px #177ec13d}
.pill{border:1px solid #385872;padding:10px 15px;border-radius:99px;font-size:12px;color:#accce3}
.pill:before{content:'';display:inline-block;background:#4ce4a7;width:8px;height:8px;margin-right:10px;border-radius:50%}
main{padding:80px 0 90px}
.overline{letter-spacing:.16em;color:#69d1f4;font-size:12px;font-weight:760;text-transform:uppercase}
h1{font-weight:780;letter-spacing:-.055em;font-size:clamp(38px,6vw,65px);line-height:1.13;margin:20px 0}
h1 span{color:#73d5f2}p{color:#aec5da;font-size:17px;line-height:1.8;max-width:690px}
.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:17px;margin-top:40px}
article{border:1px solid #304d64;border-radius:15px;background:#112339;padding:23px}
article strong{display:block;font-size:18px;color:#e6f7ff;margin-bottom:12px}article p{font-size:14px;line-height:1.6;margin:0}
a{color:#8bdfff}a:focus-visible{outline:2px solid #8bdfff;outline-offset:4px;border-radius:3px}
.cta{display:inline-block;color:#061727;background:#74d5f1;border-radius:10px;padding:13px 21px;font-weight:760;text-decoration:none;margin-top:24px}
footer{border-top:1px solid #22394f;padding:25px 0;color:#8eabc4;font-size:12px}
@media(max-width:790px){header{flex-wrap:wrap;padding:20px 0}.grid{grid-template-columns:1fr}main{padding:48px 0 55px}}
</style>
</head>
<body><div class="wrap"><header><div class="brand"><span class="logo" aria-hidden="true">OGL</span><span>OpenGenesisLINK</span></div><span class="pill">Virtual World Infrastructure</span></header>
<main><div class="overline">OpenGenesisLINK · Server Endpoint</div>
<h1>Virtuelle Welten.<br><span>Offene Verbindungen.</span></h1>
<p>Dieser Endpunkt wird von einem OpenGenesisLINK-Grid- oder Regionsserver betrieben. Er stellt die technischen Schnittstellen für Viewer, Regionen und verbundene Welten bereit.</p>
<a class="cta" href="https://opengenesislink.de/">OpenGenesisLINK entdecken</a>
<div class="grid">
<article><strong>World &amp; Grid</strong><p>OpenSim-kompatible Grid-, Hypergrid- und Regionsdienste. Die Verfügbarkeit einzelner Funktionen ist serverabhängig.</p></article>
<article><strong>Viewer-Verbindungen</strong><p>Unterstützung für kompatible Viewer und offene Protokolle. Diese Startseite ist kein Viewer-Login.</p></article>
<article><strong>Verwaltung</strong><p>Administrationsdienste und API-Zugriffe sind separat authentifiziert; auf dieser öffentlichen Informationsseite werden keine Betriebsdaten offengelegt.</p></article>
</div></main><footer>OpenGenesisLINK · Open virtual world infrastructure · <a href="https://opengenesislink.de/">Projektwebseite</a></footer></div></body>
</html>
""");
    }
}
