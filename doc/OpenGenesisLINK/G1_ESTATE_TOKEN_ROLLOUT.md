# OpenGenesisLINK Generation 1 – sichere Estate-Kommunikation

**Status:** sicherheitsrelevante Rollout-Aenderung, PR vor Live-Deployment kontrolliert testen.

## Befund

Der historische OpenSimulator-`EstateModule`-Code enthielt einen fest einkompilierten Token für den HTTP-Handler `/estate`. Bei fehlender Konfiguration konnten Dritte mit Kenntnis dieses Tokens Estate-Kommandos an den Regionsserver senden. Der G1-Fix entfernt dieses Default-Geheimnis.

## Neuer Vertrag

- Der Handler wird nur registriert, wenn ein starkes, **auf allen beteiligten Simulatoren identisches** Geheimnis mit mindestens **32 Zeichen** vorliegt.
- Bevorzugt wird die Prozessumgebungsvariable `OGL_ESTATE_SHARED_TOKEN`. Nur wenn sie fehlt/leer ist, wird `[Estates] Token` aus der **privaten Betreiber-INI** gelesen (kein Git-Secret).
- Fehlt das Geheimnis oder ist es zu kurz, wird **nur die Remote-Estate-Kommunikation** deaktiviert: kein `/estate`-Handler und keine ausgehenden Estate-HTTP-Aufrufe. Lokale Estate-Callbacks und intra-prozessuale Estate-Updates bleiben aktiv. Der Fehler wird ohne Token-Wert protokolliert. Es gibt **keinen** Fallback auf den historischen Token.
- Eingehende Token werden mittels `CryptographicOperations.FixedTimeEquals` geprüft.
- Lokale Estate-Management-Funktionen bleiben aktiv; regionsübergreifendes Estate-Update, Estate-Broadcast und Fernausweisungen fallen ohne gültigen Shared Token aus.

## Vor dem Update – Pflicht für Stadt NexVerse

1. Auf einer vertrauenswürdigen Admin-Maschine **einmal** einen zufälligen Token erzeugen: `openssl rand -base64 32` (44 Zeichen Ausgabe).
2. Den Wert über einen sicheren Kanal auf **jedem** Region-Simulator bereitstellen, z. B. als `OGL_ESTATE_SHARED_TOKEN` in einer **nur für den Dienstaccount lesbaren** EnvironmentFile. Keinen Token in Git, Chat, Logs oder Tickets kopieren.
3. Die Dienst-Umgebung/Container-Konfiguration vor dem Neustart prüfen. ROBUST benötigt den Token für diese Region-zu-Region-Kommunikation nicht.
4. Sicherstellen, dass `/estate` ausschließlich über vertrauenswürdiges Netz (z. B. SNX-WireGuard oder internes VLAN) und möglichst TLS erreichbar ist. **Der POST-Body transportiert den Token**; HTTP auf öffentlichen Netzen ist nicht ausreichend geschützt.
5. Erst nach Backup und Bereitstellung aller Simulatoren den Code ausrollen. Gemischte Token-Versionen und ältere Simulatoren können Estate-Updates stören.

## Funktions- und Security-Tests

- Mit gültigem Token über zwei Simulatoren: Estate-Update, Covenant, Estate-Nachricht, Teleport-Home jeweils durchführen.
- Fehlender/zu kurzer Token: `/estate` darf **nicht registriert** werden und der Connector darf keine Remote-Requests versenden; lokale Estate-Funktionen müssen weiterarbeiten. Das Startprotokoll enthält einen Fehler, niemals das Geheimnis.
- Falscher Token und POST ohne Token: keine autorisierte Estate-Operation; gültiger Token funktioniert.
- Nur authentische/autorisiert veranlasste Aktionen; Remote-Netzzugriff über Firewall/Proxy begrenzen.
- Kein Abhören auf dem öffentlichen Simulator-HTTP-Port ohne separate Zugriffskontrolle, auch nach diesem Fix.
- Prüfen, dass andere Server-/Viewer-Module durch die Estate-Deaktivierung nicht ausfallen.

## Rollback und Betrieb

**Nicht** zum alten eingebauten Token zurückkehren. Bei Problemen die Verteilung des identischen Secrets, Dienstumgebung, Proxy/Firewall und `ServerURI` kontrollieren. In Wartungsszenarien lässt sich das Modul bewusst über `[Estates] EstateCommunicationsHandler = "Disabled"` deaktivieren. Ein Rollback der Binärdatei ohne sichere Netzsperre würde die bekannte Schwachstelle erneut öffnen.
