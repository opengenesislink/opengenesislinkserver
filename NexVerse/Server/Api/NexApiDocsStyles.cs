// SPDX-License-Identifier: MPL-2.0
// Styles are kept separate from the HTML markup/JS so future changes can
// be reviewed and tested independently without changing protected routes.
namespace NexVerse.Server.Api
{
    internal static class NexApiDocsStyles
    {
        internal static readonly string Content = """
:root{color-scheme:dark;--bg:#071019;--panel:#0f1a26;--panel2:#142335;--line:#24384d;--text:#e9f1f8;--muted:#8fa5b8;--accent:#66c7ff;--accent2:#8be0c2;--good:#62d49b;--warn:#ffd166;--danger:#ff7d7d;--shadow:0 18px 55px rgba(0,0,0,.28)}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:radial-gradient(circle at 85% 0,#112b3d 0,transparent 34%),var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}
button,input,textarea{font:inherit}button{cursor:pointer}
.shell{min-height:100vh;display:grid;grid-template-columns:250px 1fr}
nav{position:sticky;top:0;height:100vh;padding:24px 18px;border-right:1px solid var(--line);background:rgba(8,15,24,.94);backdrop-filter:blur(12px)}
.brand{padding:0 8px 24px}.brand strong{display:block;font-size:19px;letter-spacing:.2px}.brand span{color:var(--muted);font-size:12px}
.navbtn{width:100%;text-align:left;margin:4px 0;padding:10px 12px;border:1px solid transparent;border-radius:9px;color:var(--muted);background:transparent}
.navbtn:hover,.navbtn.active{color:var(--text);background:var(--panel2);border-color:var(--line)}
.navmeta{position:absolute;left:26px;right:26px;bottom:24px;color:var(--muted);font-size:12px}
main{min-width:0;padding:34px}.page{display:none;max-width:1280px;margin:0 auto}.page.active{display:block}
.hero{display:flex;justify-content:space-between;gap:24px;align-items:flex-end;margin-bottom:24px}.eyebrow{color:var(--accent);font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.12em}.hero h1{font-size:32px;line-height:1.15;margin:7px 0 8px}.hero p{margin:0;color:var(--muted);max-width:760px}
.pill{display:inline-flex;align-items:center;gap:7px;border:1px solid var(--line);border-radius:999px;padding:6px 10px;background:var(--panel);font-size:12px;color:var(--muted)}.dot{width:8px;height:8px;border-radius:50%;background:var(--warn)}.dot.good{background:var(--good)}.dot.bad{background:var(--danger)}
.cards{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px;margin:20px 0 26px}.card,.section{border:1px solid var(--line);background:linear-gradient(180deg,rgba(20,35,53,.95),rgba(13,24,35,.95));border-radius:12px;box-shadow:var(--shadow)}
.card{padding:16px}.card .label{color:var(--muted);font-size:12px}.card .value{font-size:21px;font-weight:700;margin-top:6px;word-break:break-word}.card .sub{color:var(--muted);font-size:12px;margin-top:4px}
.section{padding:20px;margin-bottom:18px}.section h2{margin:0 0 5px;font-size:19px}.sectionlead{color:var(--muted);margin:0 0 16px}
.changegrid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.change{border:1px solid var(--line);background:#0b1621;border-radius:10px;padding:14px}.changehead{display:flex;justify-content:space-between;gap:12px;align-items:start}.change h3{font-size:15px;margin:0 0 7px}.change p{color:var(--muted);margin:0}.meta{font-size:12px;color:var(--muted);margin-top:10px}.tag{display:inline-block;border-radius:999px;padding:3px 7px;margin:2px 4px 2px 0;background:#1b3044;color:#cfe7f8;font-size:11px}.tag.good{background:#153727;color:#aaf0ca}
.filters{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:14px}.filter{border:1px solid var(--line);background:#0b1621;color:var(--muted);border-radius:999px;padding:6px 10px}.filter.active{border-color:var(--accent);color:var(--text)}
.version{display:grid;grid-template-columns:150px 1fr;gap:16px;padding:14px 0;border-bottom:1px solid var(--line)}.version:last-child{border-bottom:0}.version strong{display:block}.version ul{margin:6px 0 0;padding-left:18px;color:var(--muted)}
.roadmap-summary{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:18px}.roadmap-list{display:grid;gap:12px}.milestone{border:1px solid var(--line);background:#0b1621;border-radius:11px;padding:16px}.milestone.current{border-color:var(--accent);box-shadow:0 0 0 1px rgba(102,199,255,.12)}.milestonehead{display:flex;justify-content:space-between;gap:16px;align-items:flex-start}.milestone h3{margin:0 0 4px;font-size:16px}.milestone p{margin:8px 0;color:var(--muted)}.milestone .evidence{margin:8px 0 0;padding-left:18px;color:var(--muted)}.status{display:inline-block;border:1px solid var(--line);border-radius:999px;padding:3px 8px;font-size:11px;text-transform:uppercase;letter-spacing:.06em}.status-released{color:#bdf4dc;border-color:#2d7053;background:#113326}.status-active{color:#b9ecff;border-color:#2c6f91;background:#102c3e}.status-advanced{color:#bdf4dc;border-color:#2d7053;background:#113326}.status-started,.status-foundation{color:#ffe6a6;border-color:#7c6131;background:#342914}.status-planned{color:#aebdca;background:#111b25}.progressline{margin-top:12px}.progressmeta{display:flex;justify-content:space-between;gap:12px;color:var(--muted);font-size:12px;margin-bottom:5px}.progressbar{height:7px;background:#07111b;border-radius:999px;overflow:hidden;border:1px solid var(--line)}.progressbar span{display:block;height:100%;background:linear-gradient(90deg,var(--accent),var(--accent2))}
.explorer{display:grid;grid-template-columns:minmax(290px,32%) 1fr;gap:16px}.endpointlist,.detail{min-width:0}.search{width:100%;background:#08131e;color:var(--text);border:1px solid var(--line);border-radius:9px;padding:10px;margin-bottom:10px}
.endpoint{padding:10px;margin:7px 0;border:1px solid var(--line);border-radius:9px;background:#0b1621;cursor:pointer}.endpoint:hover,.endpoint.active{border-color:var(--accent);background:var(--panel2)}
.method{display:inline-block;min-width:58px;font-weight:800;color:var(--accent)}.path{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;font-size:12px}.small{font-size:12px;color:var(--muted)}
input,textarea,select{width:100%;background:#08131e;color:var(--text);border:1px solid var(--line);border-radius:8px;padding:9px}textarea{min-height:150px;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;resize:vertical}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:12px}.label2{display:block;color:var(--muted);margin:8px 0 5px}pre{white-space:pre-wrap;word-break:break-word;background:#06101a;border:1px solid var(--line);padding:12px;border-radius:8px;max-height:350px;overflow:auto}
.action{background:#15324a;color:var(--text);border:1px solid #315a78;border-radius:8px;padding:8px 12px}.action:hover{border-color:var(--accent)}.row{display:flex;gap:10px;align-items:center}.row>*{flex:1}.hidden{display:none}.status-good{color:var(--good)}.status-bad{color:var(--danger)}
.empty{color:var(--muted);padding:18px;border:1px dashed var(--line);border-radius:9px;text-align:center}
.portalgrid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.portalcard{border:1px solid var(--line);background:#0b1621;border-radius:11px;padding:18px}.portalcard h3{margin:0 0 7px;font-size:17px}.portalcard p{margin:0 0 12px;color:var(--muted)}.portalactions{display:flex;gap:8px;flex-wrap:wrap}.actionlink{display:inline-block;text-decoration:none;flex:0 0 auto}.portalmeta{margin-top:12px;color:var(--muted);font-size:12px}
.tablewrap{overflow:auto;border:1px solid var(--line);border-radius:9px}.datatable{width:100%;border-collapse:collapse;min-width:720px}.datatable th,.datatable td{padding:10px 12px;text-align:left;border-bottom:1px solid var(--line);white-space:nowrap}.datatable th{font-size:12px;color:var(--muted);background:#0a1520}.datatable tr:last-child td{border-bottom:0}.datatable td:first-child{font-weight:600}.authnote{margin-top:10px;color:var(--muted);font-size:12px}
.planner-controls{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px}.planner-actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:12px}.planner-actions .action{flex:0 0 auto}.gridlegend{display:flex;gap:8px;flex-wrap:wrap;margin:10px 0}.legenditem{display:inline-flex;gap:6px;align-items:center;color:var(--muted);font-size:12px}.legendswatch{width:13px;height:13px;border-radius:3px;border:1px solid var(--line)}.gridboardwrap{overflow:auto;max-height:680px;border:1px solid var(--line);border-radius:10px;background:#06101a;padding:12px}.gridboard{--cell:30px;display:grid;gap:2px;width:max-content;min-width:100%}.gridcell{width:var(--cell);height:var(--cell);min-width:var(--cell);padding:0;border:1px solid #22384b;border-radius:3px;background:#0c1a26;color:transparent;position:relative}.gridcell:hover{outline:2px solid var(--accent);z-index:2}.gridcell.free{background:#123326}.gridcell.occupied{background:#1d4c6a}.gridcell.reserved{background:#5a461c}.gridcell.conflict{background:#6a2323}.gridcell.selected{outline:2px solid #fff;z-index:3}.gridcell.preview-ok{box-shadow:inset 0 0 0 2px var(--good)}.gridcell.preview-bad{box-shadow:inset 0 0 0 2px var(--danger)}.gridcell.dimmed{opacity:.22}.gridcell[data-region-name]:after{content:'';position:absolute;inset:35%;border-radius:50%;background:rgba(255,255,255,.72)}.gridinfo{display:grid;grid-template-columns:1fr 1fr;gap:12px}.gridinfo pre{margin:0;min-height:150px}.gridcoord{font-family:ui-monospace,SFMono-Regular,Menlo,monospace}.planner-note{padding:10px 12px;border:1px solid var(--line);border-radius:8px;background:#0a1520;color:var(--muted);font-size:12px}.noderow{cursor:pointer}.noderow:hover{background:var(--panel2)}.node-state-online{color:var(--good)}.node-state-stale{color:var(--warn)}.node-state-offline{color:var(--danger)}
.ogl-perf-chart{width:100%;height:148px;display:block;background:var(--panel2);border:1px solid var(--line);border-radius:8px}
.ogl-perf-stat{padding:12px;background:var(--panel2);border:1px solid var(--line);border-radius:9px;min-width:0}
.ogl-perf-stat strong{display:block;font-size:21px;padding:6px 0}
.ogl-perf-cpus{display:grid;grid-template-columns:repeat(auto-fill,minmax(114px,1fr));gap:8px;max-height:250px;overflow:auto;margin:12px 0}
.ogl-perf-core{border:1px solid var(--line);border-radius:7px;padding:7px;background:var(--panel2);font-size:12px}
.ogl-perf-core .meter{height:7px;margin-top:6px;background:var(--line);border-radius:5px;overflow:hidden}
.ogl-perf-core .fill{height:7px;background:var(--good);border-radius:5px}
.ogl-perf-core .fill.hot{background:var(--danger)}
.ogl-perf-plots{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}
.ogl-perf-plots>div{min-width:0}
@media(max-width:820px){.ogl-perf-plots{grid-template-columns:1fr}}
.node-update-status{display:block;font-size:11px;margin-top:6px;white-space:normal;max-width:230px;color:var(--muted)}.node-update-status.good{color:var(--good)}.node-update-status.bad{color:var(--danger)}.node-update-status.warn{color:var(--warn)}
@media(max-width:1100px){.cards,.roadmap-summary{grid-template-columns:repeat(2,1fr)}.changegrid{grid-template-columns:1fr}.explorer{grid-template-columns:1fr}.endpointlist{max-height:420px;overflow:auto}.planner-controls{grid-template-columns:repeat(2,1fr)}}
@media(max-width:760px){.portalgrid{grid-template-columns:1fr}.shell{display:block}nav{position:static;height:auto;border-right:0;border-bottom:1px solid var(--line)}.navmeta{display:none}.navbtn{display:inline-block;width:auto}.brand{padding-bottom:12px}main{padding:22px 14px}.hero{display:block}.hero .pill{margin-top:14px}.cards,.roadmap-summary{grid-template-columns:1fr}.grid2,.version,.gridinfo{grid-template-columns:1fr}.planner-controls{grid-template-columns:1fr 1fr}.milestonehead{display:block}.milestonehead .status{margin-top:8px}}

/* OpenGenesisLINK OGL Control Center */
:root{--bg:#060b16;--panel:#121f35;--panel2:#182c47;--line:rgba(128,187,243,.24);--text:#eff8ff;--muted:#9bb0cd;--accent:#75e7fa;--accent2:#ad92fb;--good:#55e6b7;--warn:#ffcb7f;--danger:#ff8199;--shadow:0 26px 90px rgba(0,0,0,.34);color-scheme:dark}
body{background:#060b16;color:var(--text);font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif;position:relative}
body:before{content:"";position:fixed;inset:-28%;z-index:-2;pointer-events:none;background:radial-gradient(ellipse 35% 29% at 23% 21%,rgba(44,178,232,.17),transparent 92%),radial-gradient(ellipse 30% 34% at 82% 55%,rgba(107,70,222,.17),transparent 91%),radial-gradient(ellipse 33% 25% at 48% 92%,rgba(17,178,169,.07),transparent 92%);animation:ogl-aurora 23s ease-in-out infinite alternate}
body:after{content:"";position:fixed;inset:0;z-index:-1;pointer-events:none;opacity:.26;background-image:linear-gradient(rgba(124,157,204,.12) 1px,transparent 1px),linear-gradient(90deg,rgba(124,157,204,.12) 1px,transparent 1px);background-size:66px 66px;mask-image:linear-gradient(110deg,transparent 2%,black 48%,transparent 94%)}
@keyframes ogl-aurora{to{transform:translate3d(3.4%,-2.8%,0) rotate(5deg)}}
.shell{grid-template-columns:270px 1fr}
nav{padding:29px 18px;background:linear-gradient(177deg,rgba(15,26,47,.97),rgba(6,13,27,.99));border-right:1px solid rgba(113,175,236,.22);box-shadow:18px 0 70px rgba(0,0,0,.14);z-index:3}
nav:before{content:"";position:absolute;top:0;left:0;right:0;height:2px;background:linear-gradient(90deg,transparent,rgba(113,236,255,.9),rgba(177,133,255,.8),transparent);box-shadow:0 0 25px #59bdfa54}
.brand{padding:0 8px 27px;margin-bottom:17px;position:relative;border-bottom:1px solid rgba(123,174,228,.16);display:grid;grid-template-columns:45px minmax(0,1fr);column-gap:11px;align-items:center}
.brand .ogl-logo{grid-row:span 2;height:45px;width:45px;border-radius:13px;border:1px solid #69dafa88;box-shadow:0 0 30px #4de7fe23,inset 0 0 20px #4da4e02d;background:linear-gradient(133deg,#15334a,#312049);color:#c7f8ff;font-size:17px;font-weight:900;letter-spacing:-1px;display:grid;place-items:center}
.brand strong{font-size:16px;letter-spacing:.005em;line-height:1.3;align-self:end;white-space:nowrap}
.brand span{font-size:9px;font-weight:750;letter-spacing:.13em;color:#89abc9;align-self:start;margin-top:3px}
.navbtn{border-radius:11px;padding:12px 13px;display:flex;gap:12px;align-items:center;font-size:12px;letter-spacing:.01em;border:1px solid transparent;margin:5px 0;transition:.23s;background:transparent;color:#98aecb}
.navbtn:before{content:"⌁";display:inline-grid;place-items:center;flex:0 0 23px;height:24px;border-radius:7px;background:rgba(100,140,204,.08);color:#88b3df;font-size:18px}
.navbtn[data-page=overview]:before{content:"◈"}.navbtn[data-page=statistics]:before{content:"▥"}.navbtn[data-page=grid]:before{content:"⊞"}.navbtn[data-page=nodes]:before{content:"⬡"}.navbtn[data-page=changes]:before{content:"✦"}.navbtn[data-page=versions]:before{content:"⌘"}.navbtn[data-page=roadmap]:before{content:"↗"}.navbtn[data-page=chatgpt]:before{content:"◇"}.navbtn[data-page=adminlogin]:before{content:"♙"}.navbtn[data-page=secrets]:before{content:"⚿"}.navbtn[data-page=explorer]:before{content:"◎"}
.navbtn:hover{transform:translateX(3px);border-color:rgba(96,194,255,.23);background:rgba(42,73,120,.17);color:#e5f7ff}
.navbtn.active{border:1px solid rgba(92,218,254,.37);color:#dff7ff;background:linear-gradient(100deg,rgba(30,110,158,.3),rgba(105,71,167,.13));box-shadow:inset 3px 0 #57deff,0 0 21px rgba(88,199,247,.05)}
.navbtn.active:before{background:rgba(61,181,224,.2);color:#86f1ff}
.navmeta{font-size:10px;line-height:1.6;color:#8ba3c3;left:25px;right:20px;border-top:1px solid rgba(117,165,220,.17);padding-top:15px}
main{padding:36px clamp(24px,3.6vw,69px)}.page{max-width:1550px}
.hero{position:relative;overflow:hidden;border-radius:19px;border:1px solid rgba(99,192,248,.22);padding:28px 31px;margin-bottom:27px;min-height:155px;align-items:center;background:linear-gradient(114deg,rgba(17,52,82,.89),rgba(28,31,66,.93) 53%,rgba(13,28,48,.91));box-shadow:0 20px 65px rgba(0,0,0,.2),inset 0 1px rgba(176,234,255,.1)}
.hero:before{content:"";position:absolute;right:-65px;top:-210px;width:470px;height:470px;border:1px solid rgba(96,214,255,.21);border-radius:50%;box-shadow:0 0 0 45px rgba(71,158,232,.035),0 0 0 103px rgba(71,158,232,.029),inset 0 0 55px rgba(72,157,255,.065);pointer-events:none}
.hero:after{content:"";position:absolute;left:0;right:0;top:0;height:2px;background:linear-gradient(90deg,rgba(80,242,255,.85),rgba(160,119,255,.5),transparent)}
.eyebrow{font-size:11px;letter-spacing:.18em;color:#83e7ff}
.hero h1{font-weight:780;letter-spacing:-.047em;font-size:clamp(26px,3vw,42px);margin:9px 0}
.hero h1 em{font-style:normal;background:linear-gradient(95deg,#86eeff,#d6b6ff);color:transparent;background-clip:text}
.hero p{color:#b0c1d7;line-height:1.7;font-size:13px}
.pill{background:rgba(14,27,48,.68);border:1px solid rgba(130,189,244,.25);padding:9px 13px;box-shadow:inset 0 1px rgba(255,255,255,.03);backdrop-filter:blur(12px)}
.dot.good{box-shadow:0 0 11px #44e7ac80}.dot.bad{box-shadow:0 0 11px #ff6a9380}
.cards{gap:16px}
.card,.section,.change{position:relative;border:1px solid rgba(113,165,226,.2);border-radius:15px;background:linear-gradient(147deg,rgba(24,40,64,.91),rgba(13,23,42,.95));box-shadow:0 14px 45px rgba(0,0,0,.18),inset 0 1px rgba(197,236,255,.028);backdrop-filter:blur(13px)}
.card:before,.section:before{content:"";position:absolute;inset:-1px;border-radius:inherit;padding:1px;background:linear-gradient(145deg,rgba(101,214,252,.28),transparent 33%,transparent 75%,rgba(151,104,249,.2));mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);mask-composite:exclude;pointer-events:none}
.card{padding:20px;transition:transform .24s,border-color .24s}.card:hover{transform:translateY(-3px);border-color:rgba(98,209,254,.46);box-shadow:0 17px 54px rgba(0,0,0,.29),0 0 30px rgba(99,165,250,.07)}
.card .label{font-size:11px;letter-spacing:.11em;text-transform:uppercase;color:#97aecb}.card .value{font-size:23px;letter-spacing:-.03em;margin:12px 0 4px}.card .sub{color:#9aabc3}
.section{padding:24px 26px;margin-bottom:20px}.section h2{font-size:20px;letter-spacing:-.025em;margin-bottom:8px}.sectionlead{font-size:13px;line-height:1.7;color:#97adc8}
.change{background:rgba(11,23,43,.76);border-radius:13px;transition:border-color .2s,transform .2s}.change:hover{transform:translateY(-2px);border-color:rgba(90,220,247,.35)}
input,select,textarea{background:rgba(6,16,33,.89)!important;color:#e9f7ff!important;border:1px solid rgba(123,168,223,.27)!important;border-radius:10px!important;box-shadow:inset 0 1px 4px rgba(0,0,0,.12)}
input:focus,select:focus,textarea:focus{outline:2px solid rgba(99,217,255,.43)!important;outline-offset:1px}
button.action,.actionlink,.filter{background:linear-gradient(107deg,rgba(33,81,125,.77),rgba(53,41,112,.65));border:1px solid rgba(105,177,238,.35)!important;color:#e5f7ff!important;border-radius:10px;transition:.2s}
button.action:hover,.actionlink:hover,.filter:hover{border-color:rgba(114,224,252,.68)!important;box-shadow:0 0 23px rgba(62,156,236,.13);filter:brightness(1.15)}
a{color:#83dcff}a:hover{color:#bcb0ff}
::-webkit-scrollbar{height:9px;width:9px}::-webkit-scrollbar-thumb{background:#314761;border-radius:7px}::-webkit-scrollbar-track{background:#0b1729}
@media(max-width:1100px){.shell{grid-template-columns:233px 1fr}nav{padding:20px 12px}.brand{padding:0 6px 20px}.brand strong{font-size:14px}.brand span{font-size:8px}main{padding:26px 23px}}
@media(max-width:760px){.shell{display:block}nav{position:relative;height:auto;display:flex;flex-wrap:wrap;align-items:center;gap:4px;padding:15px;overflow:visible}.brand{width:100%;border:0;margin:0;padding:0 6px 12px}.navbtn{width:auto;margin:1px;font-size:11px;padding:8px 9px;gap:5px}.navbtn:before{height:21px;width:19px;font-size:14px}.navmeta{display:none}main{padding:22px 15px}.hero{padding:23px 18px;display:block}.hero .pill{margin-top:16px}.section{padding:19px 15px}}
@media(prefers-reduced-motion:reduce){body:before{animation:none}.navbtn,.card,.change{transition:none}}


/* OGL CONTROL CENTER 2026 | Visible redesign, no external dependencies */
:root { --ogl-ice:#8af4ff; --ogl-violet:#bfa1ff; }
body { background:#050915; }
.shell { grid-template-columns:282px minmax(0,1fr); }
nav { box-shadow:24px 0 86px rgba(0,0,0,.25);background:linear-gradient(155deg,rgba(7,15,32,.99),rgba(20,20,46,.98));border-right:1px solid rgba(115,220,255,.27); }
nav .brand { background:linear-gradient(130deg,rgba(17,59,86,.56),rgba(75,39,103,.3));border-radius:18px;padding:13px;margin:0 0 20px;border:1px solid rgba(113,206,250,.2);box-shadow:inset 0 1px rgba(182,237,255,.15),0 18px 30px rgba(0,0,0,.15)}
.navsection-label { margin:19px 14px 7px; font-size:10px; letter-spacing:.21em;color:rgba(145,190,224,.72);font-weight:800; }
nav .navbtn { margin:5px 2px; border-radius:12px; padding:12px; }
nav .navbtn.active {background:linear-gradient(95deg,rgba(25,118,165,.45),rgba(89,65,184,.22));box-shadow:inset 3px 0 #8cf5ff,0 0 30px rgba(55,185,234,.12);}
main { padding:31px clamp(24px,4vw,74px);background:radial-gradient(ellipse at 85% -15%,rgba(74,39,180,.12),transparent 60%); }
main:before {content:"";position:fixed;inset:0;pointer-events:none;opacity:.17;z-index:-1;background:repeating-linear-gradient(0deg,transparent,transparent 63px,rgba(122,185,255,.11) 64px),repeating-linear-gradient(90deg,transparent,transparent 63px,rgba(122,185,255,.11) 64px);}
.hero { min-height:220px; padding:42px clamp(25px,4vw,59px);overflow:hidden;border:1px solid rgba(136,235,255,.38);border-radius:22px;background:linear-gradient(115deg,rgba(17,57,89,.97),rgba(35,34,83,.96) 58%,rgba(7,25,45,.96));box-shadow:0 32px 82px rgba(0,0,0,.3),0 0 42px rgba(95,154,245,.07),inset 0 1px rgba(255,255,255,.1); }
.hero:before{width:580px;height:580px;right:-95px;top:-310px;animation:ogl-orbit 25s linear infinite;border:1px solid rgba(131,237,255,.4);box-shadow:0 0 0 51px rgba(79,160,225,.05),0 0 0 117px rgba(99,89,228,.06),0 0 0 179px rgba(76,167,230,.025);}
.hero:after {height:3px;background:linear-gradient(90deg,rgba(93,238,255,.95),rgba(184,115,255,.95),transparent 86%);box-shadow:0 0 32px rgba(77,187,255,.5);}
@keyframes ogl-orbit {to{transform:rotate(360deg)}}
.hero h1 {font-size:clamp(33px,3.5vw,57px);letter-spacing:-.052em;max-width:800px;line-height:1.08;}
.hero p {max-width:630px;font-size:14px;color:#b9cce4;}
.hero .eyebrow {letter-spacing:.25em;font-size:11px;text-shadow:0 0 20px rgba(64,220,255,.65);}
.hero .pill {border:1px solid rgba(148,227,250,.35);padding:11px 18px;background:rgba(11,28,54,.72);backdrop-filter:blur(12px);}
.ogl-live-strip{display:flex;align-items:center;gap:11px;min-height:47px;margin:19px 0 27px;padding:9px 18px;border-radius:12px;background:linear-gradient(95deg,rgba(16,56,70,.8),rgba(28,31,70,.8));border:1px solid rgba(88,216,233,.19);font-size:11px;letter-spacing:.08em;color:#a6c7da;}
.ogl-live-strip strong{color:#a8ffe1;letter-spacing:.15em;}
.ogl-live-strip .live-version{margin-left:auto;letter-spacing:0;color:#c4d9f2;}
.livepulse{height:9px;width:9px;border-radius:50%;background:#5de7b8;box-shadow:0 0 0 5px rgba(41,202,143,.12),0 0 14px #56e1b2;animation:ogl-pulse 2.6s ease-in-out infinite;}
@keyframes ogl-pulse{50%{box-shadow:0 0 0 9px rgba(41,202,143,.04),0 0 20px #56e1b2;}}
.ogl-section-head{display:flex;justify-content:space-between;align-items:baseline;gap:12px;margin:27px 2px 13px;font-size:18px;font-weight:800;letter-spacing:-.02em;}
.ogl-section-head small{font-size:10px;color:#83abc9;letter-spacing:.19em;font-weight:700}
.card {min-height:141px;border-radius:18px;padding:26px 22px;background:linear-gradient(140deg,rgba(24,43,72,.92),rgba(13,25,45,.93));border:1px solid rgba(135,207,255,.24);box-shadow:0 18px 45px rgba(0,0,0,.18);}
.card .value{font-size:clamp(22px,2vw,32px);color:#ecfbff;letter-spacing:-.05em;white-space:normal;overflow-wrap:anywhere;}
.card .label{color:#a1c1dc;letter-spacing:.16em;font-size:10px;}
.card:hover{transform:translateY(-5px);border-color:#78dffd;}
.section{border-radius:18px;background:linear-gradient(144deg,rgba(22,38,65,.93),rgba(11,22,41,.97));border-color:rgba(122,198,252,.25);padding:27px;}
.section h2{font-size:22px;}
.filter {padding:10px 14px;}
@media(max-width:850px){.shell{grid-template-columns:1fr;}nav{position:relative;height:auto;display:flex;flex-wrap:wrap;align-items:center;padding:15px;gap:5px;}nav .brand{width:100%;}.navsection-label{width:100%;margin:12px 8px 2px}.navbtn{width:auto;}.navmeta{display:none}main{padding:18px}.ogl-live-strip{flex-wrap:wrap}.ogl-live-strip .live-version{margin-left:0;width:100%}.hero{padding:27px 20px}}
@media(prefers-reduced-motion:reduce){.hero:before,.livepulse{animation:none!important}}


.navbtn[data-page=diagnostics]:before{content:"⚙";}
.ogl-diagnostic-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;margin-top:23px;}
.ogl-diagnostic-result{padding:18px;border:1px solid rgba(121,177,235,.22);background:rgba(9,22,42,.67);border-radius:13px;min-height:102px;}
.ogl-diagnostic-result strong{font-size:14px;display:block;margin-bottom:11px;color:#ecf8ff;}
.ogl-diagnostic-result small{color:#9bb6d0;font-size:12px;line-height:1.5;display:block;overflow-wrap:anywhere;}
.ogl-diagnostic-result.good{border-color:rgba(82,210,164,.32);box-shadow:inset 3px 0 rgba(82,230,187,.65);}
.ogl-diagnostic-result.bad{border-color:rgba(255,135,158,.32);box-shadow:inset 3px 0 rgba(255,135,158,.7);}
.ogl-diagnostic-result.optional{border-color:rgba(253,203,113,.3);box-shadow:inset 3px 0 rgba(253,203,113,.65);}
.ogl-triage-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:13px;margin-top:18px;}
.ogl-triage-tile{border:1px solid rgba(127,185,235,.2);background:rgba(8,20,38,.7);border-radius:13px;padding:17px 18px;}
.ogl-triage-tile strong{color:#aff1ff;}
.ogl-triage-tile p{font-size:12px;color:#a4b8d4;line-height:1.6;}
.ogl-triage-tile code{overflow-wrap:anywhere;}


/* OGL COMMAND CENTER 2.0 — overlays existing validated controls and preserves IDs */
:root{--bg:#030914;--panel:#0a172b;--panel2:#10243b;--line:rgba(75,157,218,.25);--text:#edf8ff;--muted:#97b5d2;--accent:#3ddcff;--accent2:#568cff;--good:#55eebd;--shadow:0 12px 46px rgba(0,0,0,.36)}
body{background:radial-gradient(ellipse at 78% 0%,rgba(11,70,143,.22),transparent 36%),repeating-linear-gradient(90deg,transparent 0 77px,rgba(34,87,148,.024) 78px 79px),#030913}
.shell{grid-template-columns:284px minmax(0,1fr)}
nav{width:284px;display:flex;flex-direction:column;overflow:hidden;border-right:1px solid rgba(74,168,241,.23);background:linear-gradient(180deg,rgba(5,17,34,.99),rgba(5,12,25,.99));padding:0 12px}
.ogl-brand{flex:0 0 auto;display:flex;gap:10px;align-items:center;padding:23px 10px 15px;margin:0;border-bottom:1px solid rgba(75,157,218,.23)}
.ogl-brand .ogl-logo img{width:56px;height:56px;object-fit:contain;filter:drop-shadow(0 0 12px rgba(56,209,255,.2))}
.ogl-brand .ogl-logo{flex:0 0 61px;width:61px;height:61px;border-radius:12px;display:grid;place-items:center;font-size:14px;box-shadow:0 0 16px rgba(0,220,255,.18);border:1px solid rgba(60,211,252,.3)}
.ogl-brand .brand-copy{min-width:0}.ogl-brand strong{font-size:15px;letter-spacing:-.25px}.ogl-brand span{font-size:9px;letter-spacing:.11em}
.nav-scroll{overflow-y:auto;overscroll-behavior:contain;flex:1;padding:12px 3px 24px;scrollbar-width:thin;scrollbar-color:#24466b transparent}
.navgroup{border-bottom:1px solid rgba(74,140,209,.10);margin-bottom:5px;padding-bottom:7px}
.navgroup summary{cursor:pointer;list-style:none;padding:12px 12px 7px;font-size:10px;font-weight:800;letter-spacing:.125em;text-transform:uppercase;color:#81b2d9;display:flex;justify-content:space-between;align-items:center}
.navgroup summary:after{content:'+';color:#57cfff;font-size:17px;line-height:1}.navgroup[open] summary:after{content:'−'}
.navgroup summary::-webkit-details-marker{display:none}
.navbtn{display:block;width:100%;margin:1px 0;padding:9px 13px;font-size:12px;border-radius:8px;min-height:35px}
.navmeta{position:static;flex:0 0 auto;padding:12px 10px 15px;line-height:1.4;max-height:88px;overflow:hidden;border-top:1px solid rgba(72,167,240,.21)}
main{padding:28px clamp(17px,3vw,52px)}
.page{max-width:1600px}
.hero{background:linear-gradient(110deg,rgba(11,33,68,.94),rgba(6,25,54,.94) 65%,rgba(7,17,39,.93));min-height:146px}
.hero h1{letter-spacing:-.035em;font-size:clamp(26px,2.6vw,37px)}
.section,.card,.ogl-release-card,.ogl-status-card{background:linear-gradient(145deg,rgba(13,28,53,.92),rgba(6,17,34,.97));border:1px solid rgba(73,155,224,.21);box-shadow:0 12px 45px rgba(0,0,0,.21)}
.ogl-overline{font-size:10px;letter-spacing:.17em;color:#50d9ff;font-weight:800}
.ogl-command-bar{display:flex;justify-content:space-between;align-items:center;gap:15px;margin:0 0 17px}
.ogl-command-bar h2{font-size:24px;margin:3px 0}.ogl-command-bar p{color:var(--muted);margin:0}
.ogl-quick-actions{display:flex;flex-wrap:wrap;gap:8px}
.ogl-status-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin:0 0 19px}
.ogl-status-card{padding:19px 20px;border-radius:13px;display:flex;flex-direction:column;gap:5px;position:relative;overflow:hidden}
.ogl-status-card:before{content:'';position:absolute;left:0;right:0;top:0;height:2px;background:linear-gradient(90deg,#2ee0ff,transparent);opacity:.7}
.ogl-status-card small,.ogl-release-type{font-size:10px;letter-spacing:.11em;font-weight:750;color:#83b9e0}
.ogl-status-card strong{font-size:clamp(16px,1.5vw,23px);color:#e6f9ff;overflow-wrap:anywhere}
.ogl-status-card span{font-size:11px;color:var(--muted)}
.ogl-panel-split{display:grid;grid-template-columns:1fr 1fr;gap:15px}
.ogl-section-flex{display:flex;justify-content:space-between;gap:15px;align-items:start}
.ogl-shortcuts{display:grid;grid-template-columns:1fr 1fr;gap:9px}.ogl-shortcuts button{padding:13px;border:1px solid rgba(79,173,240,.21);background:#081a31;color:#c7edff;text-align:left;border-radius:9px;cursor:pointer}.ogl-shortcuts button:hover{border-color:#42cfff}
.ogl-release-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin-bottom:18px}
.ogl-release-card{border-radius:14px;padding:23px;min-height:195px;display:flex;flex-direction:column;gap:12px}
.ogl-release-card strong{font-size:25px;overflow-wrap:anywhere}
.ogl-release-card p{margin:0;color:var(--muted);flex:1;overflow-wrap:anywhere}
.ogl-release-card a{color:#75ddff;font-size:12px}
.ogl-mini-status{border:1px solid rgba(81,149,213,.2);padding:12px;margin:10px 0;border-radius:9px;background:rgba(7,20,42,.6);color:#a5c9e4;overflow-wrap:anywhere}
.ogl-workspace-tools{display:flex;gap:15px;align-items:center;margin:15px 0}.ogl-workspace-tools input{flex:1;min-width:0}.ogl-workspace-tools span{color:var(--muted);white-space:nowrap}
.ogl-operation-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}
.ogl-opcard{display:flex;flex-direction:column;gap:9px;align-items:start;border:1px solid rgba(72,148,219,.25);background:rgba(4,17,37,.66);border-radius:10px;padding:15px;color:var(--text);text-align:left;cursor:pointer;overflow-wrap:anywhere}
.ogl-opcard:hover,.ogl-opcard:focus-visible{border-color:#33c8ff;background:rgba(13,53,90,.5)}
.ogl-opcard small{font-size:11px;color:var(--muted)}.ogl-opcard code{font-size:11px;white-space:normal}
.ogl-opbadge{font-size:10px;font-weight:850;padding:3px 7px;border-radius:5px;background:#1b4262;color:#b9f4ff}
.ogl-opbadge.write{background:#4b351d;color:#ffce80}
.ogl-status-ok{color:#52e3ac}.ogl-status-error{color:#ff9797}
#page-repository .section{min-height:125px}
@media(max-width:1180px){.ogl-status-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.ogl-release-grid{grid-template-columns:1fr 1fr}}
@media(max-width:850px){.shell{grid-template-columns:1fr}nav{position:relative;top:auto;width:100%;height:auto;max-height:440px}.nav-scroll{max-height:285px}.ogl-status-grid,.ogl-release-grid,.ogl-panel-split,.ogl-operation-grid{grid-template-columns:1fr}.ogl-command-bar{align-items:start;flex-direction:column}.navmeta{display:none}}

/* Operator Control Center 3.0 – restrained, legible aerospace design */
:root{--bg:#060d19;--panel:#111d2c;--panel2:#17283d;--line:#28435a;--text:#edf4fc;--muted:#9cb3c8;--accent:#52c9eb;--accent2:#89aaff;--good:#56d9a9;--shadow:0 8px 28px rgba(0,0,0,.24)}
body{background:linear-gradient(148deg,#0a1628 0%,#050a14 56%,#0a1020 100%);font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif}
.shell{grid-template-columns:276px minmax(0,1fr)}
nav{width:276px;background:#091423;border-right:1px solid #243b53}
.ogl-brand{border-bottom:1px solid #253c52}
.navgroup summary{color:#9db4ca;letter-spacing:.08em}
.navbtn{font-size:13px}.navbtn.active{box-shadow:inset 3px 0 #4fcbed;color:#ebfaff;background:#162f43}
main{max-width:1900px;width:100%;margin:0 auto}
.hero{border-radius:14px;background:linear-gradient(108deg,#152c43,#101d31 64%,#10182b);box-shadow:none;border:1px solid #2b4962;min-height:145px}
.hero:before{opacity:.25}
.ogl-status-card,.ogl-release-card,.section,.card{background:#111f31;border:1px solid #2c4358;box-shadow:none;border-radius:12px}
.ogl-status-card:before{background:#4fcbed;opacity:.55}
.ogl-command-bar h2{font-size:26px}.ogl-opcard{background:#0d1929;border:1px solid #2d455c}
.ogl-opcard:hover{background:#172d43}
.ogl-log-list{display:grid;gap:9px}
.ogl-day{background:#0b192a;border:1px solid #2c455c;border-radius:10px;overflow:hidden}
.ogl-day summary{cursor:pointer;padding:17px 18px;display:flex;align-items:center;gap:10px;justify-content:space-between;font-weight:700}
.ogl-day summary:hover{background:#122b42}
.ogl-log-body{padding:14px;border-top:1px solid #29435e}
.ogl-logs-pre{font:12px/1.65 ui-monospace,SFMono-Regular,Consolas,monospace;max-height:560px;overflow:auto;white-space:pre-wrap;overflow-wrap:anywhere;background:#06111e;color:#bdd6e6;padding:16px;border-radius:9px;border:1px solid #20384d}
.ogl-audit-line{padding:12px;border-bottom:1px solid #294052;display:grid;grid-template-columns:178px 160px 1fr;gap:12px;color:#cad9e9;overflow-wrap:anywhere}
.ogl-action-surface{border:1px solid #3c6e86;border-radius:12px;background:#10273a;padding:19px;margin:16px 0}
.ogl-action-surface h3{margin:0 0 8px;font-size:17px}
.ogl-action-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:11px;margin:14px 0}
.ogl-action-field{display:flex;flex-direction:column;gap:6px;min-width:0}
.ogl-action-field label{color:#a9c7df;font-size:12px}
.ogl-action-surface textarea{min-height:145px;width:100%;font-family:ui-monospace,monospace}
.ogl-action-surface pre{font-size:12px;max-height:420px;overflow:auto;white-space:pre-wrap;overflow-wrap:anywhere;background:#061321;border-radius:8px;padding:14px}
.ogl-action-buttonrow{display:flex;gap:9px;flex-wrap:wrap;margin-top:11px}
.ogl-inline-admin{display:flex;flex-wrap:wrap;gap:10px;margin:16px 0}
.ogl-inline-admin input{flex:1;min-width:140px}
.ogl-user-results{display:grid;gap:8px;margin:10px 0}
.ogl-user-row{display:flex;flex-wrap:wrap;gap:12px;align-items:center;justify-content:space-between;background:#0b1b2d;border:1px solid #31475b;border-radius:9px;padding:11px}
.ogl-user-row small{color:#a1bacf}
@media(max-width:900px){.ogl-audit-line{grid-template-columns:1fr}.ogl-action-grid{grid-template-columns:1fr}}

/* OGL Control Center 5.0 — coherent dark operations workspace.
   Single authoritative skin: no light theme or runtime CDN. */
:root{
 --bg:#0a101a;--panel:#121e2d;--panel2:#1a2b40;--line:#293e53;
 --text:#e9f1f8;--muted:#9bb0c4;--accent:#48b9ec;--accent2:#679ae9;
 --good:#3ed2a4;--warn:#f1b65d;--danger:#f17e8c;
 --shadow:0 12px 34px rgba(0,0,0,.22);color-scheme:dark;
}
html,body{height:100%;overflow:hidden}
body{background:var(--bg);color:var(--text);font-family:Inter,system-ui,-apple-system,"Segoe UI",sans-serif}
body:before{display:none}
.shell{display:grid;grid-template-columns:282px minmax(0,1fr);grid-template-rows:66px minmax(0,1fr);height:100dvh;overflow:hidden;background:var(--bg)}
nav{
 grid-column:1;grid-row:1/3;display:flex;flex-direction:column;
 position:relative;top:auto;width:282px;height:100%;min-height:0;overflow:hidden;
 background:linear-gradient(180deg,#0d1b2a 0%,#0b1725 58%,#0a1521 100%);
 border-right:1px solid #263b4f;box-shadow:8px 0 34px #02060d38;padding:0 13px;
}
nav:before{height:0;box-shadow:none;background:none}
.ogl-brand{
 display:flex;align-items:center;gap:12px;min-height:86px;padding:15px 12px;
 margin:0 -13px 0;border-bottom:1px solid #24394a;background:#0e1d2d;
}
.ogl-brand .ogl-logo{display:grid;place-items:center;flex:0 0 52px;width:52px;height:52px;
 background:#132b42;border:1px solid #355a75;border-radius:12px;box-shadow:none}
.ogl-brand .ogl-logo img{width:50px;height:50px;object-fit:contain;filter:none}
.ogl-brand strong{font-size:15px;color:#f1f7fd;letter-spacing:-.02em;font-weight:750}
.ogl-brand span{display:block;color:#90aeca;font-size:9px;letter-spacing:.105em}
.navsection-label{padding:21px 13px 9px;font-size:10px;font-weight:750;letter-spacing:.17em;color:#7796b2}
.nav-scroll{flex:1;min-height:0;overflow-y:auto;overscroll-behavior:contain;padding:0 1px 22px;scrollbar-width:thin;scrollbar-color:#34506c transparent}
.nav-home{padding:0 4px 11px;margin-bottom:6px;border-bottom:1px solid #25394d}
.navgroup{padding:4px 4px 10px;margin:0 0 6px;border-bottom:1px solid #243648}
.navgroup summary{
 display:flex;align-items:center;justify-content:space-between;gap:8px;list-style:none;
 cursor:pointer;min-height:39px;padding:9px 10px;color:#8fa9bf;
 font-size:10px;font-weight:800;letter-spacing:.10em;text-transform:uppercase;
 border-radius:8px;user-select:none;transition:color .16s,background .16s;
}
.navgroup summary:hover{background:#16283b;color:#d5e6f4}
.navgroup summary::-webkit-details-marker{display:none}
.navgroup summary:after{content:"";width:7px;height:7px;flex:0 0 7px;
 border-right:1.7px solid #86a6c1;border-bottom:1.7px solid #86a6c1;
 transform:rotate(45deg);transition:transform .16s}
.navgroup[open] summary:after{transform:rotate(225deg)}
.navbtn{
 display:flex;align-items:center;gap:12px;width:100%;min-height:42px;
 padding:10px 12px;margin:2px 0;color:#abc0d4;background:transparent;
 border:1px solid transparent;border-radius:9px;text-align:left;
 font-size:12.5px;font-weight:540;line-height:1.2;letter-spacing:0;
 cursor:pointer;transition:background .15s,border-color .15s,color .15s;
}
.navbtn:before{display:none!important;content:none!important}
.navbtn .ogl-nav-icon{width:18px;height:18px;flex:0 0 18px;stroke-width:1.8;color:#7e9eb7}
.navbtn:hover{background:#182e44;border-color:#284762;color:#f2f9ff;transform:none}
.navbtn:hover .ogl-nav-icon{color:#6bcdf6}
.navbtn.active{background:#173b56;border:1px solid #286284;box-shadow:inset 3px 0 #5ad2f7;color:#f3faff;font-weight:720}
.navbtn.active .ogl-nav-icon{color:#69d5ff}
.navbtn:focus-visible,.navgroup summary:focus-visible{outline:2px solid #59c9f3;outline-offset:2px}
.navmeta{position:static;flex:0 0 auto;max-height:72px;overflow:hidden;margin:0 -13px;padding:12px 18px;
 border-top:1px solid #273b50;background:#0b1826;color:#7896ae;font-size:10px;line-height:1.55}
.ogl-admin-header{
 grid-column:2;grid-row:1;min-width:0;display:flex;align-items:center;
 justify-content:space-between;gap:15px;padding:0 clamp(18px,2.5vw,38px);
 background:#0f1c2b;border-bottom:1px solid #293e53;box-shadow:0 4px 20px #02081225;
 color:#e9f2fb;z-index:2;
}
.ogl-header-left,.ogl-header-right{display:flex;align-items:center;gap:12px;white-space:nowrap}
.ogl-header-left strong{font-size:13px;font-weight:750;letter-spacing:.01em}
.ogl-header-sub{color:#819ab2;font-size:12px}
.ogl-header-right{font-size:11px;color:#95aec3}
.ogl-header-dot{width:8px;height:8px;border-radius:50%;background:#3dd4a2;box-shadow:0 0 0 4px #31cda31b}
.ogl-header-sep{color:#48677e}
.ogl-admin-header button{cursor:pointer;border:1px solid #35536b;background:#172b40;color:#c7e4f4;border-radius:8px;padding:8px 13px}
.ogl-admin-header button:hover{background:#203b55;border-color:#5295c0}
#oglSideToggle{font-size:19px;line-height:1;border:1px solid transparent;background:transparent;padding:8px 10px;color:#a9c7d9}
#oglSideToggle:hover{background:#1c354d}
main{grid-column:2;grid-row:2;width:100%;height:100%;min-width:0;max-width:none;margin:0;
 overflow-y:auto;overflow-x:hidden;padding:28px clamp(18px,2.8vw,43px) 55px;
 background:radial-gradient(ellipse at 80% -17%,#15324b44,transparent 44%),#0b1420;color:var(--text);
 scrollbar-color:#385573 #101f30}
.page{width:100%;max-width:1640px;margin:0 auto;color:var(--text)}
.hero{position:relative;overflow:hidden;min-height:122px;padding:24px 28px;
 background:linear-gradient(111deg,#172d43,#112236 65%,#101f31);
 border:1px solid #2a4d68;border-radius:13px;box-shadow:0 8px 24px #030a1229;color:var(--text)}
.hero:before{display:none}
.hero:after{height:2px;background:linear-gradient(90deg,#4bbceb,transparent 65%);box-shadow:none}
.hero h1{font-size:clamp(24px,2.3vw,31px);margin:5px 0;color:#f1f7ff;letter-spacing:-.035em}
.hero h1 em,#page-overview .hero h1 em{font-style:normal;color:#63cbf4;background:none}
.hero p{font-size:12px;color:#a7bdd0;line-height:1.55}
.hero .eyebrow,.ogl-overline{font-size:10px;font-weight:780;letter-spacing:.13em;color:#6ecbf1}
.hero .pill,.pill{background:#142f42;border:1px solid #35617a;color:#b8e0f3;border-radius:24px;padding:8px 13px;box-shadow:none}
#page-overview .hero{min-height:106px;padding:20px 26px}
#page-overview .hero h1{font-size:clamp(23px,2.2vw,30px)}
#page-overview .hero p{font-size:12px;margin:0}
.ogl-command-bar{margin:24px 0 15px;align-items:center}
.ogl-command-bar h2{font-size:23px;color:#eaf4fc;margin:2px 0}
.ogl-command-bar p,.sectionlead,.authnote{color:#92a9be}
.section,.card,.ogl-status-card,.ogl-release-card{
 background:#142235;border:1px solid #294057;
 border-radius:12px;color:#e9f2f9;box-shadow:0 8px 23px #0309111c;
}
.section{margin-top:17px;padding:23px 25px}
.section h2,.section h3,.ogl-section-flex h2{color:#e6f1f9;font-size:18px}
.section .sectionlead{font-size:12px;line-height:1.6}
.ogl-status-grid{gap:13px;margin-bottom:18px}
.ogl-status-card{min-height:121px;gap:7px;padding:19px 21px}
.ogl-status-card:before{height:2px;background:linear-gradient(90deg,#43bde9,#4a8bbd 60%,transparent);opacity:1}
.ogl-status-card small,.ogl-release-type{font-size:10px;font-weight:770;letter-spacing:.12em;color:#8caac2}
.ogl-status-card strong{font-size:clamp(22px,1.8vw,29px);font-weight:760;line-height:1.2;color:#f0f8ff;overflow-wrap:anywhere}
.ogl-status-card span,.ogl-release-card p{font-size:11px;color:#92adc2}
.ogl-panel-split{gap:16px}
.ogl-section-flex{align-items:start}
.ogl-release-card{min-height:170px}
.ogl-release-card strong{color:#eef6ff}
.ogl-release-card a{color:#74d4fa}
.ogl-mini-status{background:#101e2e;border:1px solid #2c4660;border-radius:9px;color:#adc5d9}
.ogl-shortcuts button,.ogl-opcard{background:#182a3e;border:1px solid #35526b;color:#d0e5f4;border-radius:9px}
.ogl-shortcuts button{position:relative;padding:13px 35px 13px 14px;font-weight:610}
.ogl-shortcuts button:after{content:"›";position:absolute;right:14px;color:#7ed3f5;font-size:22px;top:50%;transform:translateY(-53%)}
.ogl-shortcuts button:hover,.ogl-opcard:hover{background:#203a54;border-color:#538bb0;color:#fff}
.ogl-opcard code{color:#97dafa}.ogl-opcard small{color:#9bb1c6}
.ogl-opbadge{background:#204663;color:#a7e5fc}.ogl-opbadge.write{background:#503b20;color:#ffd69c}
.ogl-section-head{color:#e3eef7}
.card{min-height:135px;padding:23px}
.card .value{color:#f2f9ff}
.card .label{color:#93b4cd}
.card:hover{transform:none;border-color:#466d8c}
.ogl-live-strip{background:#122a38;border:1px solid #28516b;color:#d3e6f3}
.ogl-live-strip .live-version{color:#a8c9db}
.action,button.action,.filter{background:#1b5075!important;color:#effaff!important;border:1px solid #327fac!important;border-radius:8px!important;box-shadow:none;font-weight:660}
.action:hover,button.action:hover,.filter:hover{background:#24658d!important;border-color:#61bde9!important}
.action:disabled{opacity:.55}
.filter.active{border-color:#71d4fb!important;color:#fff!important}
input,textarea,select,.search{background:#0d1c2d!important;color:#eaf4ff!important;border:1px solid #3b566e!important;border-radius:8px!important;min-height:38px}
input::placeholder,textarea::placeholder{color:#8da5ba!important}
input:focus-visible,textarea:focus-visible,select:focus-visible,button:focus-visible,a:focus-visible{outline:2px solid #67c9f2;outline-offset:2px}
.ogl-action-surface{background:#102235;border:1px solid #31536d;border-radius:11px}
.ogl-action-surface h3{color:#e1f1fc}
.ogl-action-field label,.section .authnote,.ogl-action-surface p{color:#a3bdd2}
.ogl-user-row{background:#102335;border-color:#2d4b62}
.ogl-console-output,.ogl-logs-pre,pre{background:#0b1726;color:#d3e3ef;border:1px solid #2c4359;border-radius:9px}
.ogl-command-help{background:#112235;border:1px solid #304960;border-radius:9px}
.ogl-command-help summary{color:#91d8f5}
.ogl-help-list{color:#bad3e7}
.ogl-help-list>div{border-color:#2a4054}
.ogl-datatable{width:100%;border-collapse:collapse;font-size:13px;color:#d7e8f5}
.ogl-datatable th{background:#1c3044;color:#9ebbd2;text-align:left;font-size:11px;letter-spacing:.06em;text-transform:uppercase}
.ogl-datatable th,.ogl-datatable td{padding:13px 14px;border-bottom:1px solid #29445b}
.ogl-datatable tbody tr:hover{background:#20364b}
.ogl-table-scroll{overflow-x:auto}
.datatable th{background:#1a3044;color:#aac6d9}
.datatable td{border-color:#2b4156}
.ogl-kpi-row{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:11px;margin:16px 0}
.ogl-kpi-row>div{display:flex;flex-direction:column;gap:8px;border:1px solid #32506b;border-radius:10px;background:#182d42;padding:16px}
.ogl-kpi-row strong{font-size:27px;color:#d7f1ff}
.ogl-kpi-row small{color:#a0b8cb}
.ogl-day{background:#132437;border:1px solid #304d65;border-radius:10px;box-shadow:none}
.ogl-day summary{color:#e6f2fb}
.ogl-day summary:hover{background:#20364b}
.ogl-log-body{border-top:1px solid #2a465b;background:#101d2c}
.ogl-logs-pre{display:block;padding:0;font:12px/1.55 ui-monospace,"SFMono-Regular",Consolas,monospace;white-space:normal;max-height:620px;overflow:auto}
.ogl-log-line{display:grid;grid-template-columns:65px minmax(0,1fr);gap:12px;padding:8px 12px;border-bottom:1px solid #24384d;overflow-wrap:anywhere}
.ogl-log-line:nth-child(even){background:#15263a}
.ogl-log-line .ogl-level-label{font-weight:770;color:#a7bbc8;font-size:10px;align-self:start;padding:3px 2px}
.ogl-log-line>span:last-child{white-space:pre-wrap;overflow-wrap:anywhere}
.ogl-log-line.level-error{background:#462733;color:#ffe0e6}
.ogl-log-line.level-error .ogl-level-label{color:#ff9ba9}
.ogl-log-line.level-warn{background:#413524;color:#ffe8c3}
.ogl-log-line.level-warn .ogl-level-label{color:#ffd18a}
.ogl-log-line.level-info .ogl-level-label{color:#83cef3}
.ogl-log-line.level-debug .ogl-level-label{color:#9aa9b7}
.ogl-audit-line{border-color:#2c4358;color:#cddfeb}
#oglSideToggle:focus-visible{outline:2px solid #67c9f2}
body.ogl-sidebar-collapsed .shell{grid-template-columns:0 minmax(0,1fr)}
body.ogl-sidebar-collapsed nav{visibility:hidden;width:0;overflow:hidden}
body.ogl-sidebar-collapsed .ogl-admin-header,body.ogl-sidebar-collapsed main{grid-column:2}
@media(max-width:1170px){
 .ogl-status-grid,.ogl-release-grid{grid-template-columns:repeat(2,minmax(0,1fr))}
 .ogl-panel-split{grid-template-columns:1fr}
}
@media(max-width:900px){
 .shell{grid-template-columns:minmax(0,1fr);grid-template-rows:60px minmax(0,1fr)}
 nav{position:fixed;z-index:12;top:60px;bottom:0;width:min(282px,90vw);height:calc(100dvh - 60px);box-shadow:12px 0 30px #02070dac}
 .ogl-admin-header{grid-column:1;grid-row:1;padding:0 14px}
 main{grid-column:1;grid-row:2;padding:19px 14px 45px}
 body.ogl-sidebar-collapsed .shell{grid-template-columns:minmax(0,1fr)}
 body.ogl-sidebar-collapsed nav{visibility:hidden;pointer-events:none}
 body.ogl-sidebar-collapsed .ogl-admin-header,body.ogl-sidebar-collapsed main{grid-column:1}
 body:not(.ogl-sidebar-collapsed) main{pointer-events:none}
 .ogl-kpi-row{grid-template-columns:1fr}
 .ogl-header-right{display:none}
}
@media(max-width:600px){
 .ogl-status-grid,.ogl-release-grid,.ogl-operation-grid,.ogl-panel-split{grid-template-columns:1fr}
 .section{padding:17px}.hero{padding:20px}.ogl-log-line{grid-template-columns:48px minmax(0,1fr)}
 .ogl-header-sub{display:none}
}
@media(prefers-reduced-motion:reduce){.navbtn,.navgroup summary{transition:none}}

""";
    }
}
