// SPDX-License-Identifier: MPL-2.0
// Self-contained public landing page for browser GET / on every configured
// ROBUST/Simulator HTTP listener. No authentication, runtime state, external
// scripts, trackers or external image/font dependencies are required.
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
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="dark">
<meta name="theme-color" content="#08121d">
<meta name="description" content="OpenGenesisLINK – moderne, offene Serverplattform für virtuelle 3D-Welten, Regionen, Viewer und Hypergrid.">
<meta property="og:type" content="website">
<meta property="og:title" content="OpenGenesisLINK – Die Infrastruktur hinter virtuellen Welten">
<meta property="og:description" content="Die OpenSimulator-basierte Plattform für vernetzte 3D-Welten, Grid-Dienste, Regionen und erweiterbare APIs.">
<title>OpenGenesisLINK – Virtual World Server Infrastructure</title>
<style>
:root{
 color-scheme:dark;
 --bg:#07111b;--bg2:#0b1928;--panel:#102235;--panel2:#132a40;
 --text:#edf7ff;--muted:#9eb6c9;--line:#28445a;
 --aqua:#6cddfa;--blue:#559bff;--green:#56d9ba;
 font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif
}
*,*::before,*::after{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;min-width:320px;background:var(--bg);color:var(--text);overflow-x:hidden}
body::before{content:"";pointer-events:none;position:absolute;inset:0 0 auto;height:790px;z-index:-1;
 background:radial-gradient(ellipse at 70% 24%,#164b70a6 0%,#0c274344 39%,transparent 71%),
 radial-gradient(ellipse at 4% 8%,#24518455,transparent 48%)}
body::after{content:"";pointer-events:none;position:absolute;inset:0 0 auto;height:680px;opacity:.23;z-index:-1;
 background-image:linear-gradient(#4b83a21d 1px,transparent 1px),
 linear-gradient(90deg,#4b83a21d 1px,transparent 1px);background-size:54px 54px;
 mask-image:linear-gradient(to bottom,#000,transparent 96%)}
a{color:inherit;text-decoration:none}
a:focus-visible{outline:2px solid var(--aqua);outline-offset:5px;border-radius:5px}
.container{width:min(1200px,calc(100% - 64px));margin-inline:auto}
.skip{position:absolute;left:16px;top:-80px;z-index:10;padding:11px 15px;border-radius:8px;background:var(--aqua);color:#06121a}
.skip:focus{top:12px}
header{height:88px;border-bottom:1px solid #33526a66;background:#081522c7;backdrop-filter:blur(12px)}
header .container{display:flex;align-items:center;justify-content:space-between;height:100%;gap:26px}
.brand{display:flex;align-items:center;gap:13px;min-width:0}
.brandmark{height:48px;width:48px;display:grid;place-items:center;flex:none;border-radius:13px;
 background:linear-gradient(145deg,#1c587b,#0b2b48);border:1px solid #4188b2;box-shadow:0 8px 33px #1488ca33}
.brandmark svg{width:30px;height:30px}
.wordmark{display:flex;flex-direction:column;gap:4px}
.wordmark strong{font-size:19px;letter-spacing:-.04em;line-height:1;font-weight:830;white-space:nowrap}
.wordmark small{font-size:9px;letter-spacing:.17em;font-weight:750;color:#80a8c5}
nav{display:flex;align-items:center;gap:27px;white-space:nowrap}
nav a{color:#b7cde0;font-size:12px;font-weight:680;transition:color .15s}
nav a:hover{color:#fff}
nav a:last-child{padding:11px 17px;border:1px solid #406987;border-radius:9px;background:#18364f;color:#e6f6ff}
nav a:last-child:hover{border-color:#76c9f2;background:#214762}
.hero{position:relative;display:grid;grid-template-columns:1.02fr .98fr;align-items:center;min-height:588px;gap:48px;padding-block:82px 70px}
.eyebrow{display:inline-flex;align-items:center;gap:9px;max-width:100%;border:1px solid #326184a8;
 border-radius:100px;padding:9px 13px;background:#12304785;color:#9edffc;font-size:10px;font-weight:790;letter-spacing:.1em}
.eyebrow .dot{width:6px;height:6px;flex:none;border-radius:50%;background:var(--green);box-shadow:0 0 0 4px #56d9ba20}
h1{font-size:clamp(43px,5.6vw,73px);letter-spacing:-.069em;line-height:1.07;max-width:665px;margin:25px 0 26px;font-weight:830}
h1 .accent{background:linear-gradient(110deg,#81e7ff 4%,#7dbaff 85%);-webkit-background-clip:text;background-clip:text;color:transparent}
.lead{font-size:16px;line-height:1.8;color:#afc7d9;margin:0;max-width:545px}
.actions{display:flex;align-items:center;flex-wrap:wrap;gap:12px;margin-top:35px}
.btn{display:inline-flex;align-items:center;justify-content:center;gap:10px;border-radius:10px;padding:14px 20px;
 font-size:13px;font-weight:790;transition:transform .15s,background .15s,border .15s}
.btn:hover{transform:translateY(-2px)}
.btn.primary{background:linear-gradient(115deg,#73e0f9,#78bbff);color:#071724;box-shadow:0 10px 33px #2ba9e32c}
.btn.primary:hover{background:linear-gradient(115deg,#9af0ff,#a4d2ff)}
.btn.secondary{color:#d9ebf9;border:1px solid #3b607d;background:#0d2538b8}
.btn.secondary:hover{border-color:#70c8ef;background:#173950}
.btn svg{width:16px;height:16px;flex:none}
.hero-notes{display:flex;gap:22px;flex-wrap:wrap;margin-top:34px;color:#80a5c0;font-size:11px}
.hero-notes span{display:inline-flex;align-items:center;gap:8px}
.hero-notes span::before{content:"";height:5px;width:5px;background:#67bcd9;border-radius:50%}
.network{position:relative;height:456px;border-radius:26px;isolation:isolate;
 background:radial-gradient(circle at 50% 45%,#23639150,transparent 53%),
 linear-gradient(147deg,#10283b99,#0a1a2baf);
 border:1px solid #345b7491;box-shadow:inset 0 0 65px #020c175c,0 28px 90px #0007;
 overflow:hidden}
.network::before{content:"";position:absolute;inset:0;opacity:.35;
 background-image:radial-gradient(circle,#73b8e3 1px,transparent 1px);background-size:22px 22px;
 mask-image:radial-gradient(ellipse,#000 6%,transparent 73%)}
.network::after{content:"";position:absolute;width:360px;height:360px;border-radius:50%;top:48px;left:calc(50% - 180px);
 border:1px dashed #4da9e64d;box-shadow:0 0 0 50px #1a6da809,0 0 0 101px #1a6da805;pointer-events:none}
.network .code{position:absolute;top:23px;left:26px;color:#7cbfdf;font-size:10px;letter-spacing:.13em;font-weight:780}
.network .code::before{content:"◈";color:#5bd6ee;margin-right:8px}
.network .version{position:absolute;bottom:21px;left:24px;color:#81a7bd;font-size:10px;letter-spacing:.06em}
.network .corner{position:absolute;bottom:21px;right:24px;color:#6bcdeb;font-size:11px}
.beams{position:absolute;inset:0;width:100%;height:100%;opacity:.85}
.hub{position:absolute;z-index:2;left:50%;top:49%;transform:translate(-50%,-50%);
 width:170px;height:170px;border-radius:27px;display:flex;flex-direction:column;justify-content:center;align-items:center;
 border:1px solid #70c7f199;background:linear-gradient(145deg,#163952,#0b2035);
 box-shadow:0 0 80px #259ddb38,0 16px 45px #0006}
.hub svg{width:68px;height:68px;filter:drop-shadow(0 0 14px #39aadd80)}
.hub strong{font-size:19px;letter-spacing:-.055em;margin-top:11px}
.hub small{font-size:9px;letter-spacing:.2em;font-weight:750;color:#7ec3e8;margin-top:5px}
.node{position:absolute;z-index:3;display:flex;align-items:center;gap:10px;
 padding:12px 14px;background:#10273be8;border:1px solid #38617dcc;
 border-radius:11px;box-shadow:0 8px 25px #0005;color:#d7edff;font-size:12px;font-weight:740}
.node .symbol{height:29px;width:29px;display:grid;place-items:center;border:1px solid #346887;
 background:#1b455c;border-radius:8px;color:#92e5ff;font-size:16px}
.node span:last-child{display:flex;flex-direction:column;gap:2px}
.node small{color:#83adc4;font-size:9px;letter-spacing:.06em;font-weight:600}
.node.grid-node{left:32px;top:88px}
.node.region-node{right:23px;top:86px}
.node.api-node{left:28px;bottom:86px}
.node.view-node{right:23px;bottom:88px}
.promo-strip{border-top:1px solid #264258;border-bottom:1px solid #264258;background:#0e1e2eb0}
.promo-strip .container{min-height:102px;display:flex;align-items:center;justify-content:space-between;gap:25px}
.promo-strip strong{font-size:11px;color:#799fb8;font-weight:730;letter-spacing:.1em;text-transform:uppercase}
.promo-tags{display:flex;gap:27px;flex-wrap:wrap;color:#cae0ee;font-size:12px;font-weight:760}
.promo-tags span{display:flex;align-items:center;gap:8px}
.promo-tags span::before{content:"+";color:#67d5ee;font-size:17px;font-weight:500}
.section{padding-block:83px}
.sectionhead{display:flex;align-items:end;justify-content:space-between;gap:25px;margin-bottom:30px}
.kicker{color:#74caed;font-size:10px;font-weight:790;letter-spacing:.2em;text-transform:uppercase;margin:0 0 13px}
h2{font-size:clamp(27px,3.6vw,42px);line-height:1.18;letter-spacing:-.045em;margin:0;font-weight:810}
.sectionhead>p{max-width:370px;color:#a7c0d3;font-size:13px;line-height:1.8;margin:0}
.cards{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:17px}
.card{position:relative;overflow:hidden;padding:29px 26px 30px;border:1px solid #2a4b64;
 background:linear-gradient(150deg,#132c41,#0e1e30);border-radius:16px;min-height:257px}
.card::after{content:"";position:absolute;right:-60px;top:-70px;width:150px;height:150px;border-radius:50%;
 background:#2b8cce13;filter:blur(10px)}
.card .card-icon{width:45px;height:45px;border:1px solid #376684;border-radius:12px;
 background:#20405a;display:grid;place-items:center;margin-bottom:26px;color:#81ddfd}
.card svg{width:23px;height:23px}
.card h3{font-size:18px;letter-spacing:-.025em;margin:0 0 10px}
.card p{margin:0;color:#9fbacb;font-size:13px;line-height:1.78}
.bottom-panel{position:relative;display:flex;justify-content:space-between;align-items:center;gap:30px;
 margin:0 0 90px;padding:37px 43px;border:1px solid #3b6783;border-radius:18px;
 background:linear-gradient(110deg,#173a55,#112a41 65%,#0e2336);overflow:hidden}
.bottom-panel::after{content:"";position:absolute;right:-65px;bottom:-130px;width:300px;height:300px;border:1px solid #65b7ea33;border-radius:50%;pointer-events:none}
.bottom-panel h2{font-size:28px}
.bottom-panel p{max-width:620px;font-size:13px;line-height:1.7;color:#a7c3d6;margin:12px 0 0}
.bottom-panel .btn{flex:none;z-index:1}
footer{border-top:1px solid #274156;background:#081522}
footer .container{min-height:108px;display:flex;align-items:center;justify-content:space-between;gap:24px}
footer .footmark{color:#e5f6ff;font-size:15px;font-weight:820;letter-spacing:-.03em}
footer small{display:block;color:#789bb4;font-size:11px;margin-top:7px;line-height:1.5}
footer .footlinks{display:flex;gap:24px;color:#95b2c8;font-size:12px}
footer .footlinks a:hover{color:#fff}
@media(max-width:1050px){
 nav{gap:16px}.hero{gap:22px}.network{height:420px}
 .node.grid-node{left:9px}.node.region-node{right:9px}
 .node.api-node{left:9px}.node.view-node{right:9px}
 .hub{width:145px;height:150px}.hub svg{width:56px;height:56px}
 .promo-tags{gap:16px}
}
@media(max-width:850px){
 .container{width:min(720px,calc(100% - 42px))}
 nav a:first-child{display:none}
 .hero{grid-template-columns:1fr;padding:57px 0 57px;gap:46px}
 h1{font-size:clamp(44px,8vw,64px);max-width:650px}
 .network{max-width:600px;width:100%;height:425px;justify-self:center}
 .promo-strip .container{padding:22px 0;align-items:start;flex-direction:column;gap:13px}
 .cards{grid-template-columns:1fr}.card{min-height:0}
 .section{padding-block:62px}
 .bottom-panel{margin-bottom:66px;padding:30px;align-items:start;flex-direction:column}
}
@media(max-width:530px){
 .container{width:calc(100% - 32px)}
 header{height:76px}.brandmark{width:40px;height:40px}.brandmark svg{width:26px;height:26px}
 .wordmark strong{font-size:16px}.wordmark small{font-size:7px}
 nav{gap:0}nav a:last-child{padding:9px 11px;font-size:10px}
 .hero{padding:44px 0}
 h1{font-size:clamp(38px,11vw,54px)}
 .lead{font-size:14px}
 .hero-notes{gap:11px 17px}
 .network{height:330px;border-radius:17px}
 .network::after{width:260px;height:260px;top:35px;left:calc(50% - 130px)}
 .network .code{top:12px;left:13px;font-size:8px}
 .hub{width:122px;height:121px;border-radius:20px}.hub svg{height:44px;width:44px}.hub strong{font-size:16px;margin-top:6px}.hub small{font-size:7px}
 .node{gap:5px;padding:8px;border-radius:8px;font-size:9px}
 .node .symbol{height:21px;width:21px;font-size:11px}
 .node small{font-size:7px}
 .node.grid-node{left:5px;top:60px}.node.region-node{right:5px;top:60px}
 .node.api-node{left:5px;bottom:55px}.node.view-node{right:5px;bottom:55px}
 .network .version,.network .corner{font-size:8px;bottom:12px}.network .version{left:11px}.network .corner{right:11px}
 .sectionhead{align-items:start;flex-direction:column;gap:12px}
 footer .container{padding-block:25px;align-items:start;flex-direction:column}
}
@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}.btn{transition:none}.btn:hover{transform:none}}
</style>
</head>
<body>
<a class="skip" href="#inhalt">Zum Inhalt springen</a>
<header><div class="container">
<a class="brand" href="https://opengenesislink.de/" aria-label="OpenGenesisLINK – Projektwebseite">
 <span class="brandmark"><svg viewBox="0 0 40 40" fill="none" aria-hidden="true"><path d="M6 13 20 5l14 8v14l-14 8-14-8V13Z" stroke="#99edff" stroke-width="2.1"/><path d="m6 13 14 8 14-8M20 21v14M10 25l10-6 10 6" stroke="#64c4fb" stroke-width="2"/></svg></span>
 <span class="wordmark"><strong>OpenGenesisLINK</strong><small>VIRTUAL WORLD INFRASTRUCTURE</small></span>
</a>
<nav aria-label="Hauptnavigation">
 <a href="#plattform">Plattform</a>
 <a href="https://opengenesislink.de/">Projektwebseite</a>
 <a href="https://github.com/opengenesislink/opengenesislinkserver" target="_blank" rel="noopener noreferrer">GitHub ↗</a>
</nav>
</div></header>
<main id="inhalt">
<section class="container hero" aria-labelledby="headline">
 <div>
  <div class="eyebrow"><span class="dot" aria-hidden="true"></span>OPEN GENESIS LINK · SERVER ENDPOINT</div>
  <h1 id="headline">Die Infrastruktur hinter <span class="accent">virtuellen Welten.</span></h1>
  <p class="lead">OpenGenesisLINK verbindet die bewährten Grundlagen von OpenSimulator mit einer eigenständig weiterentwickelten Serverplattform: für vernetzte Regionen, kompatible Viewer und moderne Grid-Dienste.</p>
  <div class="actions">
   <a class="btn primary" href="https://opengenesislink.de/">Plattform entdecken <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M5 12h14m-6-6 6 6-6 6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg></a>
   <a class="btn secondary" href="https://github.com/opengenesislink/opengenesislinkserver" target="_blank" rel="noopener noreferrer">Quellcode auf GitHub <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M8 16 16 8m-7 0h7v7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg></a>
  </div>
  <div class="hero-notes"><span>OpenSimulator-basierte Plattform</span><span>Grid &amp; Hypergrid</span><span>Erweiterbare Dienste</span></div>
 </div>
 <div class="network" role="img" aria-label="Schematische Verbindung zwischen OpenGenesisLINK, Grid, Regionen, World API und Viewer">
  <span class="code">CONNECTED WORLDS / OGL CORE</span>
  <svg class="beams" viewBox="0 0 520 456" preserveAspectRatio="none" fill="none" aria-hidden="true">
   <path d="M132 116 259 221 391 116M120 354 259 221 394 353" stroke="#439fdb" stroke-width="1.7" stroke-dasharray="5 6" opacity=".74"/>
   <circle cx="132" cy="116" r="4" fill="#70dfff"/><circle cx="391" cy="116" r="4" fill="#70dfff"/>
   <circle cx="120" cy="354" r="4" fill="#70dfff"/><circle cx="394" cy="353" r="4" fill="#70dfff"/>
   <circle cx="259" cy="221" r="133" stroke="#63b7e5" stroke-opacity=".13"/><circle cx="259" cy="221" r="91" stroke="#63b7e5" stroke-opacity=".17"/>
  </svg>
  <div class="hub">
   <svg viewBox="0 0 70 70" fill="none" aria-hidden="true"><path d="M10 22 35 8l25 14v26L35 62 10 48V22Z" stroke="#9aefff" stroke-width="2.8"/><path d="m10 22 25 14 25-14M35 36v26M19 43l16-10 16 10" stroke="#64caff" stroke-width="2.6"/></svg>
   <strong>OGL Core</strong><small>SERVER PLATFORM</small>
  </div>
  <div class="node grid-node"><span class="symbol">◇</span><span>Grid<small>WORLD SERVICES</small></span></div>
  <div class="node region-node"><span class="symbol">▦</span><span>Regionen<small>SIMULATORS</small></span></div>
  <div class="node api-node"><span class="symbol">{ }</span><span>World API<small>INTEGRATIONS</small></span></div>
  <div class="node view-node"><span class="symbol">◎</span><span>Viewer<small>OPEN PROTOCOLS</small></span></div>
  <span class="version">ARCHITECTURE / DISTRIBUTED 3D WORLDS</span><span class="corner">◈ OGL</span>
 </div>
</section>
<div class="promo-strip"><div class="container"><strong>Eine Plattform. Viele Möglichkeiten.</strong><div class="promo-tags"><span>Virtuelle Welten</span><span>Hypergrid</span><span>Region-Hosting</span><span>World API</span></div></div></div>
<section class="container section" id="plattform" aria-labelledby="capabilities">
 <div class="sectionhead"><div><p class="kicker">Built for virtual worlds</p><h2 id="capabilities">Mehr als ein einfacher Simulator.</h2></div><p>Eine offene Serverbasis, die Regionen, Infrastruktur und Erweiterungen miteinander verbindet.</p></div>
 <div class="cards">
  <article class="card"><div class="card-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m3 7 9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10"/></svg></div><h3>Grid &amp; Regionen</h3><p>Grid-Dienste, Simulationsregionen und Hypergrid-Verbindungen auf einer OpenSimulator-kompatiblen Grundlage.</p></article>
  <article class="card"><div class="card-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="4" width="18" height="16" rx="3"/><path d="m7 10 3 3-3 3M13 16h5"/></svg></div><h3>API &amp; Verwaltung</h3><p>Erweiterbare World-API-Schnittstellen und administrative Werkzeuge für den kontrollierten Betrieb virtueller Welten.</p></article>
  <article class="card"><div class="card-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c5 5 5 13 0 18M12 3c-5 5-5 13 0 18"/></svg></div><h3>Offen &amp; vernetzt</h3><p>Unterstützung kompatibler Viewer und offener Protokolle mit Raum für eigene Module, Dienste und Integrationen.</p></article>
 </div>
</section>
<section class="container bottom-panel" aria-labelledby="endpoint-head">
 <div><p class="kicker">OpenGenesisLINK Server Endpoint</p><h2 id="endpoint-head">Hier läuft OpenGenesisLINK.</h2><p>Du befindest dich auf dem öffentlichen HTTP-Einstiegspunkt eines Grid- oder Regionsservers. Viewer-Login, API-Dienste und Administration verwenden ihre vorgesehenen Schnittstellen. Diese Seite ist ausschließlich eine öffentliche Produktinformation.</p></div>
 <a class="btn secondary" href="https://opengenesislink.de/">Mehr über das Projekt <span aria-hidden="true">↗</span></a>
</section>
</main>
<footer><div class="container"><div><div class="footmark">OpenGenesisLINK</div><small>Virtual world server infrastructure · Aus OpenSimulator weiterentwickelt.</small></div><div class="footlinks"><a href="https://opengenesislink.de/">Website</a><a href="https://github.com/opengenesislink/opengenesislinkserver" target="_blank" rel="noopener noreferrer">GitHub</a></div></div></footer>
</body>
</html>
""");
    }
}
