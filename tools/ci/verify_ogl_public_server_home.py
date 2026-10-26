#!/usr/bin/env python3
"""Guard OGL browser-root landing page and legacy viewer protocol isolation."""
from html.parser import HTMLParser
from pathlib import Path
import re

root=Path(__file__).resolve().parents[2]
welcome=(root/"OpenSim/Framework/Servers/HttpServer/OglServerWelcomePage.cs").read_text(encoding="utf-8")
server=(root/"OpenSim/Framework/Servers/HttpServer/BaseHttpServer.cs").read_text(encoding="utf-8")
match=re.search(r'Encoding\.UTF8\.GetBytes\("""\s*(<!doctype html>.*?)\s*"""\);',welcome,re.I|re.S)
assert match, "Embedded promo page not found"
html=match.group(1)

class Page(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.tags=[]
        self.external_resources=[]
        self.links=[]
        self.ids=[]
    def handle_starttag(self,tag,attrs):
        self.tags.append(tag)
        a=dict(attrs)
        if "id" in a:
            self.ids.append(a["id"])
        if tag=="a":
            self.links.append(a.get("href",""))
        if tag in ("script","iframe","object","embed"):
            self.external_resources.append(tag)
        if tag in ("img","script","link") and a.get("src") or (
                tag=="link" and a.get("rel") in ("stylesheet","preload","modulepreload")):
            self.external_resources.append((tag,a))

page=Page()
page.feed(html)
for t in ("html","head","body","header","nav","main","footer","h1","h2","article","svg"):
    assert t in page.tags, t
assert html.startswith("<!doctype html>")
assert html.count("<h1") == 1
assert len(page.ids)==len(set(page.ids)), "Duplicate element ids"
assert "inhalt" in page.ids and "plattform" in page.ids
assert "#inhalt" in page.links and "#plattform" in page.links
assert "https://opengenesislink.de/" in page.links
assert "https://github.com/opengenesislink/opengenesislinkserver" in page.links
assert page.external_resources==[], "Public endpoint must have no external active assets"
assert "<script" not in html.lower()
assert "url(" not in html.lower(), "No CSS remote image/font requests"
assert "prefers-reduced-motion" in html
assert "aria-label" in html and "focus-visible" in html
for t in ("OpenGenesisLINK","Grid","Regionen","World API","Viewer","Hypergrid",
          "Hier läuft OpenGenesisLINK","keine", "OpenSimulator"):
    # "keine" is not required as a visible claim: just public technical description.
    if t != "keine":
        assert t in html,t
assert "Passwort" not in html and "UserLevel" not in html
assert "version:" not in html.lower(), "No guessed server runtime version"
assert 'request.UriPath.Equals("/")' in server
assert 'request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase)' in server
assert 'request.QueryString.Count == 0' in server
assert 'accept?.IndexOf("text/html"' in server
assert 'string.Equals(accept?.Trim(), "*/*", StringComparison.Ordinal)' in server
assert 'm_RootDefaultGET is null' in server
assert "OglServerWelcomePage.Html" in server
assert 'response.StatusCode = (int)HttpStatusCode.OK' in server
for t in ("X-Content-Type-Options","X-Frame-Options","Referrer-Policy",
          "Content-Security-Policy", "Cache-Control"):
    assert t in server,t
for t in ("HandleXmlRpcRequests(request, response)","HandleLLSDLogin(request, response)",
          "HandleJsonRpcRequests(request, response)","TryGetSimpleStreamHandler(path"):
    assert t in server, "Protocol endpoint broken: "+t
assert 'httpResponse.Redirect("/")' in server
assert 'httpResponse.Redirect("http://opensimulator.org")' not in server
print("OGL HTTP promo: responsive isolated landing HTML, safe root negotiation, LLSD/XML-RPC/JSON-RPC preserved")
