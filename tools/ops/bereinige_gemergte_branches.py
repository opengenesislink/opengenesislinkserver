#!/usr/bin/env python3
"""Entfernt ausschliesslich eindeutig erledigte GitHub-Branches.

Standard: Nur Vorschau. GitHub Actions darf --ausfuehren nach dem Merge
auf main nutzen. Keine lokalen Git-Reflogs oder Release-Tags anfassen.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


LONG_LIVED = frozenset({"main", "develop/generation-1.1", "GEN1.1NET10"})


def darf_entfernt_werden(branch: dict, offene: set[str],
                        zusammengefuehrte: set[tuple[str, str]],
                        laengst_in_main: bool = False) -> tuple[bool, str]:
    name = branch.get("name", "")
    sha = branch.get("commit", {}).get("sha", "")
    if not name or not sha:
        return False, "ungueltiger Branch"
    if name in LONG_LIVED or branch.get("protected"):
        return False, "geschuetzter oder dauerhafter Branch"
    if name in offene:
        return False, "offener Pull Request"
    if (name, sha) in zusammengefuehrte:
        return True, "gemergter PR mit unveraendertem Head-SHA"
    if laengst_in_main:
        return True, "keine eigenen Commits gegenueber main"
    return False, "nicht eindeutig abgeschlossen"


def selbsttest() -> None:
    merged = {("feature/gemergt", "123"), ("feature/geaendert", "old")}
    active = {"feature/offen"}
    good = {"name": "feature/gemergt", "commit": {"sha": "123"}, "protected": False}
    assert darf_entfernt_werden(good, active, merged)[0]
    assert not darf_entfernt_werden({**good, "protected": True}, active, merged)[0]
    assert not darf_entfernt_werden({**good, "name": "main"}, active, merged)[0]
    assert not darf_entfernt_werden({**good, "name": "feature/offen"}, active, merged)[0]
    assert not darf_entfernt_werden({**good, "name": "feature/geaendert"}, active, merged)[0]
    assert darf_entfernt_werden({**good, "name": "feature/alt"}, active,
                                merged, laengst_in_main=True)[0]
    assert not darf_entfernt_werden({**good, "name": "feature/alt"}, active,
                                   merged, laengst_in_main=False)[0]
    print("Branch-Schutzregeln: PASS")


class GitHub:
    def __init__(self, repo: str, token: str) -> None:
        self.repo = repo
        self.token = token
        self.root = "https://api.github.com/repos/" + repo

    def request(self, route: str, method: str = "GET") -> object:
        request = Request(
            self.root + route, method=method,
            headers={
                "Authorization": "Bearer " + self.token,
                "Accept": "application/vnd.github+json",
                "X-GitHub-Api-Version": "2022-11-28",
                "User-Agent": "OpenGenesisLINK-G1-Branchhygiene",
            },
        )
        with urlopen(request, timeout=25) as response:
            body = response.read()
            return json.loads(body) if body else {}

    def pages(self, route: str) -> list[dict]:
        data: list[dict] = []
        for page in range(1, 40):
            separator = "&" if "?" in route else "?"
            rows = self.request(route + separator + "per_page=100&page=" + str(page))
            if not isinstance(rows, list):
                raise RuntimeError("GitHub API gab keine Liste zurueck.")
            data.extend(rows)
            if len(rows) < 100:
                return data
        raise RuntimeError("Sicherheitsgrenze: mehr als 3900 API-Eintraege.")

    def no_unique_commits(self, branch_name: str) -> bool:
        # GitHub compare: head is the candidate; ahead_by=0 means all
        # commits in the candidate are already reachable from main.
        name = quote(branch_name, safe="/")
        result = self.request("/compare/main..." + name)
        return isinstance(result, dict) and result.get("ahead_by") == 0

    def unveraendert(self, name: str, sha: str) -> bool:
        candidate = self.request("/branches/" + quote(name, safe="/"))
        return (isinstance(candidate, dict)
                and candidate.get("commit", {}).get("sha") == sha
                and not candidate.get("protected"))

    def hat_offenen_pr(self, name: str) -> bool:
        # Revalidate at deletion time; do not rely on the earlier snapshot.
        short_owner = self.repo.partition("/")[0]
        query = "?state=open&head=" + quote(short_owner + ":" + name, safe="")
        result = self.request("/pulls" + query)
        return isinstance(result, list) and len(result) != 0

    def delete(self, name: str) -> None:
        self.request("/git/refs/heads/" + quote(name, safe="/"), method="DELETE")


def run() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ausfuehren", action="store_true",
                        help="Gepruefte Remote-Branches wirklich loeschen")
    parser.add_argument("--selbsttest", action="store_true")
    args = parser.parse_args()
    if args.selbsttest:
        selbsttest()
        return 0

    repo = os.getenv("GITHUB_REPOSITORY", "")
    token = os.getenv("GITHUB_TOKEN", "")
    if "/" not in repo or not token:
        print("FEHLER: GITHUB_REPOSITORY/GITHUB_TOKEN fehlen.", file=sys.stderr)
        return 2

    api = GitHub(repo, token)
    branches = api.pages("/branches")
    open_prs = api.pages("/pulls?state=open")
    closed_prs = api.pages("/pulls?state=closed")
    offen = {
        p.get("head", {}).get("ref") for p in open_prs
        if p.get("head", {}).get("repo", {}) and
        p["head"]["repo"].get("full_name", "").lower() == repo.lower()
    }
    merged = {
        (p["head"]["ref"], p["head"]["sha"]) for p in closed_prs
        if p.get("merged_at")
        and p.get("head", {}).get("repo", {})
        and p["head"]["repo"].get("full_name", "").lower() == repo.lower()
        and p.get("head", {}).get("sha")
    }

    entfernt: list[str] = []
    kandidat: list[str] = []
    behalten: list[str] = []
    fehler: list[str] = []
    for branch in branches:
        name = branch["name"]
        eligible, reason = darf_entfernt_werden(branch, offen, merged)
        if not eligible and reason == "nicht eindeutig abgeschlossen":
            try:
                eligible, reason = darf_entfernt_werden(
                    branch, offen, merged,
                    laengst_in_main=api.no_unique_commits(name))
            except (HTTPError, URLError) as error:
                print(f"Ueberspringe {name}: Vergleich nicht moeglich ({error})")
        if not eligible:
            behalten.append(name)
            continue
        kandidat.append(name)
        if not args.ausfuehren:
            print(f"Vorschau: {name} – {reason}")
            continue
        try:
            if name in LONG_LIVED or not api.unveraendert(
                name, branch["commit"]["sha"]) or api.hat_offenen_pr(name):
                behalten.append(name)
                print(f"Ueberspringe {name}: Ref geaendert, geschuetzt oder PR offen")
                continue
            api.delete(name)
            entfernt.append(name)
            print(f"Entfernt: {name} ({reason})")
            # Remote-GitHub erlaubt nur begrenzt viele schreibende API-
            # Operationen pro Minute. Keine Loesch-Sturmwelle erzeugen.
            time.sleep(1.2)
        except (HTTPError, URLError) as error:
            fehler.append(name)
            print(f"FEHLER bei {name}: {error}", file=sys.stderr)

    summary = (
        f"## Branch-Bereinigung\n"
        f"- Betriebsart: {'Ausfuehrung' if args.ausfuehren else 'Vorschau'}\n"
        f"- Gefundene Branches: {len(branches)}\n"
        f"- Eindeutig erledigt: {len(kandidat)}\n"
        f"- Tatsaechlich entfernt: {len(entfernt)}\n"
        f"- Behalten/uebersprungen: {len(behalten)}\n"
        f"- Fehlgeschlagene Loeschungen: {len(fehler)}\n"
        f"- Geschuetzte Hauptbranches, offene PRs und Aenderungen nach "
        f"PR-Merge bleiben erhalten.\n"
    )
    print(summary)
    target = os.getenv("GITHUB_STEP_SUMMARY")
    if target:
        with open(target, "a", encoding="utf-8") as summary_file:
            summary_file.write(summary)
    return 1 if fehler else 0


if __name__ == "__main__":
    raise SystemExit(run())
