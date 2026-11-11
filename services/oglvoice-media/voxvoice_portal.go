// SPDX-License-Identifier: MPL-2.0
// VoxVoice: deliberately small registration and presence portal for the existing
// Firestorm-compatible Pion media bridge. Does not handle or record audio.
package main

import (
    "crypto/rand"
    "crypto/sha256"
    "crypto/subtle"
    "crypto/tls"
    "encoding/hex"
    "encoding/json"
    "errors"
    "fmt"
    "html/template"
    "io"
    "log/slog"
    "net"
    "net/http"
    "net/mail"
    "net/smtp"
    "net/url"
    "os"
    "path/filepath"
    "sort"
    "strings"
    "sync"
    "time"

    "github.com/pion/webrtc/v4"
)

type voxAccount struct {
    Node string
    Email string
    Digest string // SHA-256 of a cryptographically random 32-byte password, never plaintext
    Regions map[string][]string // region UUID -> previously seen avatar UUIDs
}

type voxLogin struct {
    Node string
    Expires time.Time
}

type voxPortal struct {
    mu sync.Mutex
    media *mediaServer
    path string
    publicURL string
    smtpAddress string
    smtpFrom string
    smtpUser string
    smtpPassword string
    accounts map[string]*voxAccount
    logins map[string]voxLogin
    attempts map[string][]time.Time
}

func newVoxPortal(m *mediaServer) (*voxPortal, error) {
    path := strings.TrimSpace(os.Getenv("VOXVOICE_ACCOUNTS_FILE"))
    if path == "" {
        return nil, nil // opt-in: existing OGLVoice operation is unchanged
    }
    p := &voxPortal{
        media:m, path:path, publicURL:strings.TrimRight(os.Getenv("VOXVOICE_PUBLIC_URL"), "/"),
        smtpAddress:os.Getenv("VOXVOICE_SMTP_ADDRESS"), smtpFrom:os.Getenv("VOXVOICE_SMTP_FROM"),
        smtpUser:os.Getenv("VOXVOICE_SMTP_USER"), smtpPassword:os.Getenv("VOXVOICE_SMTP_PASSWORD"),
        accounts:make(map[string]*voxAccount), logins:make(map[string]voxLogin),
        attempts:make(map[string][]time.Time),
    }
    base, err := url.Parse(p.publicURL)
    if err != nil || base.Scheme != "https" || base.Host != "voice.opengenesislink.de" || base.Path != "" {
        return nil, errors.New("VOXVOICE_PUBLIC_URL must be https://voice.opengenesislink.de")
    }
    if _, _, err := net.SplitHostPort(p.smtpAddress); err != nil {
        return nil, fmt.Errorf("VOXVOICE_SMTP_ADDRESS: %w", err)
    }
    if _, err := mail.ParseAddress(p.smtpFrom); err != nil || p.smtpUser == "" || p.smtpPassword == "" {
        return nil, errors.New("VOXVOICE SMTP sender or credentials missing")
    }
    if b, err := os.ReadFile(path); err == nil {
        var items []*voxAccount
        if err := json.Unmarshal(b, &items); err != nil { return nil, fmt.Errorf("VoxVoice accounts: %w", err) }
        for _, a := range items {
            if a == nil || !validNode.MatchString(a.Node) || len(a.Digest) != 64 {
                return nil, errors.New("VoxVoice accounts file has invalid entries")
            }
            if a.Regions == nil { a.Regions = make(map[string][]string) }
            p.accounts[a.Node] = a
        }
    } else if !os.IsNotExist(err) {
        return nil, err
    }
    return p, nil
}

func voxRandom(bytesCount int) (string, error) {
    b := make([]byte, bytesCount)
    if _, err := rand.Read(b); err != nil { return "", err }
    return hex.EncodeToString(b), nil
}

func voxDigest(secret string) string {
    sum := sha256.Sum256([]byte(secret))
    return hex.EncodeToString(sum[:])
}

func (p *voxPortal) nodeConfig(node string) (nodeConfig, bool) {
    p.mu.Lock()
    a := p.accounts[node]
    p.mu.Unlock()
    if a == nil { return nodeConfig{}, false }
    // Matches SHA256(vox_password) -> lowercase hex in the C# VoxVoice adapter.
    return nodeConfig{Secret:a.Digest, Tenant:a.Node}, true
}

func (p *voxPortal) persistLocked() error {
    entries := make([]*voxAccount,0,len(p.accounts))
    for _, a := range p.accounts { entries=append(entries,a) }
    sort.Slice(entries, func(i,j int)bool{return entries[i].Node<entries[j].Node})
    raw,err:=json.MarshalIndent(entries,"","  ")
    if err!=nil{return err}
    if err=os.MkdirAll(filepath.Dir(p.path),0700);err!=nil{return err}
    tmp,err:=os.CreateTemp(filepath.Dir(p.path),".voxvoice-*.json")
    if err!=nil{return err}
    defer os.Remove(tmp.Name())
    if err=tmp.Chmod(0600);err==nil { _,err=tmp.Write(raw) }
    if err==nil { err=tmp.Sync() }
    if closeErr:=tmp.Close();err==nil {err=closeErr}
    if err!=nil{return err}
    return os.Rename(tmp.Name(),p.path)
}

// Simulator registration is authenticated exactly like media requests; UUIDs are
// never accepted from an anonymous portal user or a viewer request.
func (p *voxPortal) registerRegionAPI(w http.ResponseWriter,r *http.Request) {
    node:=r.Header.Get("X-OGLVoice-Node")
    cfg,ok:=p.nodeConfig(node)
    if !ok {http.Error(w,"Unknown account",http.StatusForbidden);return}
    data,err:=readRequest(w,r)
    if err!=nil{http.Error(w,"Invalid request",http.StatusBadRequest);return}
    if err=p.media.replay.verify(node,cfg,r,data,p.media.now());err!=nil{
        http.Error(w,"Invalid machine proof",http.StatusUnauthorized);return
    }
    var input struct{RegionID string}
    if err=json.Unmarshal(data,&input);err!=nil || !validUUID.MatchString(input.RegionID){
        http.Error(w,"Invalid region UUID",http.StatusBadRequest);return
    }
    p.recordRegion(node,input.RegionID)
    w.Header().Set("Cache-Control","no-store")
    w.WriteHeader(http.StatusNoContent)
}

func (p *voxPortal) recordRegion(node,regionID string) {
    if !validUUID.MatchString(regionID){return}
    p.mu.Lock()
    defer p.mu.Unlock()
    a:=p.accounts[node]
    if a==nil{return}
    if a.Regions==nil{a.Regions=make(map[string][]string)}
    regionID=strings.ToLower(regionID)
    if _,exists:=a.Regions[regionID];exists{return}
    a.Regions[regionID]=[]string{}
    if err:=p.persistLocked();err!=nil{slog.Error("VoxVoice SIM registry save failed","error",err)}
}

func (p *voxPortal) recordSession(node, regionID, avatarID string) {
    if !validUUID.MatchString(regionID) || !validUUID.MatchString(avatarID) { return }
    p.mu.Lock()
    defer p.mu.Unlock()
    a:=p.accounts[node]
    if a==nil{return}
    if a.Regions==nil{a.Regions=make(map[string][]string)}
    avatars:=a.Regions[strings.ToLower(regionID)]
    for _, id:=range avatars {if id==strings.ToLower(avatarID){return}}
    // Keep a small local history; we only show UUIDs from regions used with voice.
    if len(avatars)>=100 { avatars=avatars[1:] }
    a.Regions[strings.ToLower(regionID)]=append(avatars,strings.ToLower(avatarID))
    if err:=p.persistLocked();err!=nil{slog.Error("VoxVoice region registry save failed","error",err)}
}

func (p *voxPortal) registerHTTP(mux *http.ServeMux) {
    mux.HandleFunc("GET /", p.home)
    mux.HandleFunc("GET /register", p.registerPage)
    mux.HandleFunc("POST /register", p.register)
    mux.HandleFunc("GET /login", p.loginPage)
    mux.HandleFunc("POST /login", p.login)
    mux.HandleFunc("POST /logout", p.logout)
    mux.HandleFunc("GET /dashboard", p.dashboard)
    mux.HandleFunc("POST /internal/voxvoice/v1/regions", p.registerRegionAPI)
}

func (p *voxPortal) guard(w http.ResponseWriter,r *http.Request) bool {
    w.Header().Set("Cache-Control","no-store")
    w.Header().Set("X-Frame-Options","DENY")
    w.Header().Set("X-Content-Type-Options","nosniff")
    w.Header().Set("Referrer-Policy","no-referrer")
    w.Header().Set("Content-Security-Policy","default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'")
    if r.Method==http.MethodPost && r.Header.Get("Origin")!=p.publicURL {
        http.Error(w,"Invalid form origin",http.StatusForbidden)
        return false
    }
    return true
}

func (p *voxPortal) page(w http.ResponseWriter,title,body string,vars any) {
    w.Header().Set("Content-Type","text/html; charset=utf-8")
    const shell = "<!doctype html><html lang='de'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{{.Title}} – VoxVoice</title><style>body{font:16px system-ui;background:#111923;color:#e8f0f5;max-width:850px;margin:3rem auto;padding:0 1rem}article{background:#202c38;padding:1.5rem;border-radius:12px}a{color:#85cfff}input,button{font:inherit;padding:.75rem;margin:.4rem 0;border-radius:5px}input{width:95%;max-width:440px}button{cursor:pointer}.row{padding:.7rem;border-bottom:1px solid #43505f}.dot{display:inline-block;width:12px;height:12px;border-radius:50%;margin-right:.5rem}.on{background:#27d969}.off{background:#f04949}code{overflow-wrap:anywhere}</style></head><body><h1>VoxVoice</h1><article>" 
    const tail = "</article></body></html>"
    header:=template.Must(template.New("head").Parse(shell))
    _=header.Execute(w,struct{Title string}{title})
    t,err:=template.New("content").Parse(body)
    if err!=nil{http.Error(w,"template unavailable",500);return}
    if err=t.Execute(w,vars);err!=nil{slog.Warn("VoxVoice template error","error",err)}
    _,_=io.WriteString(w,tail)
}

func (p *voxPortal) home(w http.ResponseWriter,r *http.Request) {
    if !p.guard(w,r){return}
    p.page(w,"Start","<p>Externer WebRTC-Voice-Dienst fuer OpenGenesisLINK und Firestorm.</p><p><a href='/register'>Registrieren</a> · <a href='/login'>Einloggen</a></p>",nil)
}
func (p *voxPortal) registerPage(w http.ResponseWriter,r *http.Request) {
    if !p.guard(w,r){return}
    p.page(w,"Registrieren","<h2>Registrieren</h2><form action='/register' method='post'><label>E-Mail-Adresse<input type='email' name='email' required maxlength='254'></label><button>VoxVoice-Zugang anfordern</button></form>",nil)
}
func (p *voxPortal) loginPage(w http.ResponseWriter,r *http.Request) {
    if !p.guard(w,r){return}
    p.page(w,"Login","<h2>Anmelden</h2><form action='/login' method='post'><label>vox_user<input name='user' required></label><label>vox_password<input name='password' type='password' required></label><button>Anmelden</button></form>",nil)
}

func (p *voxPortal) withinRateLimit(r *http.Request) bool {
    ip,_,err:=net.SplitHostPort(r.RemoteAddr)
    if err!=nil{return false}
    p.mu.Lock()
    defer p.mu.Unlock()
    now:=time.Now()
    var recent []time.Time
    for _,t:=range p.attempts[ip]{if now.Sub(t)<time.Hour{recent=append(recent,t)}}
    if len(recent)>=3{return false}
    p.attempts[ip]=append(recent,now)
    if len(p.attempts)>10000{p.attempts=make(map[string][]time.Time)}
    return true
}

func (p *voxPortal) register(w http.ResponseWriter,r *http.Request) {
    if !p.guard(w,r){return}
    if !p.withinRateLimit(r){http.Error(w,"Too many registrations",429);return}
    r.Body=http.MaxBytesReader(w,r.Body,4096)
    if err:=r.ParseForm();err!=nil{http.Error(w,"Invalid request",400);return}
    email:=strings.ToLower(strings.TrimSpace(r.Form.Get("email")))
    addr,err:=mail.ParseAddress(email)
    if err!=nil || addr.Address!=email || strings.ContainsAny(email,"\r\n") {
        http.Error(w,"Invalid email address",400);return
    }
    nodeSuffix,err:=voxRandom(10)
    if err!=nil{http.Error(w,"Random generator failed",500);return}
    password,err:=voxRandom(32)
    if err!=nil{http.Error(w,"Random generator failed",500);return}
    a:=&voxAccount{Node:"vox-"+nodeSuffix,Email:email,Digest:voxDigest(password),Regions:make(map[string][]string)}
    p.mu.Lock()
    for _,old:=range p.accounts {
        if old.Email==email {p.mu.Unlock();http.Error(w,"Email is already registered",409);return}
    }
    p.accounts[a.Node]=a
    err=p.persistLocked()
    if err!=nil {delete(p.accounts,a.Node)}
    p.mu.Unlock()
    if err!=nil{slog.Error("VoxVoice registration persistence failed","error",err);http.Error(w,"Registration unavailable",503);return}
    if err=p.sendCredentials(a.Email,a.Node,password);err!=nil{
        p.mu.Lock();delete(p.accounts,a.Node);_ = p.persistLocked();p.mu.Unlock()
        slog.Error("VoxVoice SMTP registration failed","error",err)
        http.Error(w,"Email delivery failed, please retry later",503)
        return
    }
    p.page(w,"Registriert","<p>Die Zugangsdaten und die OpenSim.ini-Konfiguration wurden an die angegebene Adresse gesendet.</p><a href='/login'>Zum Login</a>",nil)
}

func (p *voxPortal) sendCredentials(to,node,password string) error {
    host,_,err:=net.SplitHostPort(p.smtpAddress)
    if err!=nil{return err}
    msg:="From: "+p.smtpFrom+"\r\nTo: "+to+"\r\nSubject: VoxVoice Zugangsdaten\r\n"+
        "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=UTF-8\r\n\r\n"+
        "VoxVoice Registrierung\r\n\r\n"+
        "vox_user: "+node+"\r\nvox_password: "+password+"\r\n\r\n"+
        "[VoxVoice]\r\n    enabled = true\r\n"+
        "    vox_server = voice.opengenesislink.de\r\n"+
        "    vox_sip_uri = voice.opengenesislink.de\r\n"+
        "    vox_user = "+node+"\r\n"+
        "    vox_password = "+password+"\r\n\r\n"+
        "Anmelden: "+p.publicURL+"/login\r\n"+
        "Diese Zugangsdaten vertraulich behandeln und nicht in ein oeffentliches Repository einchecken.\r\n"
    c,err:=smtp.Dial(p.smtpAddress)
    if err!=nil{return err}
    defer c.Close()
    ok,_:=c.Extension("STARTTLS")
    if !ok{return errors.New("SMTP server has no STARTTLS")}
    if err=c.StartTLS(&tls.Config{ServerName:host,MinVersion:tls.VersionTLS12});err!=nil{return err}
    if err=c.Auth(smtp.PlainAuth("",p.smtpUser,p.smtpPassword,host));err!=nil{return err}
    from,err:=mail.ParseAddress(p.smtpFrom)
    if err!=nil{return err}
    if err=c.Mail(from.Address);err!=nil{return err}
    if err=c.Rcpt(to);err!=nil{return err}
    out,err:=c.Data()
    if err!=nil{return err}
    if _,err=io.WriteString(out,msg);err!=nil{_ =out.Close();return err}
    return out.Close()
}

func (p *voxPortal) login(w http.ResponseWriter,r *http.Request) {
    if !p.guard(w,r){return}
    r.Body=http.MaxBytesReader(w,r.Body,4096)
    if err:=r.ParseForm();err!=nil{http.Error(w,"Invalid login",400);return}
    user:=r.Form.Get("user")
    pass:=r.Form.Get("password")
    p.mu.Lock()
    a:=p.accounts[user]
    digest:=voxDigest(pass)
    valid:=a!=nil && subtle.ConstantTimeCompare([]byte(a.Digest),[]byte(digest))==1
    p.mu.Unlock()
    if !valid {http.Error(w,"Invalid credentials",401);return}
    sid,err:=voxRandom(32)
    if err!=nil{http.Error(w,"Login unavailable",500);return}
    p.mu.Lock()
    p.logins[voxDigest(sid)]=voxLogin{Node:user,Expires:time.Now().Add(24*time.Hour)}
    p.mu.Unlock()
    http.SetCookie(w,&http.Cookie{Name:"voxvoice_session",Value:sid,Path:"/",HttpOnly:true,Secure:true,SameSite:http.SameSiteStrictMode,MaxAge:86400})
    http.Redirect(w,r,"/dashboard",http.StatusSeeOther)
}
func (p *voxPortal) currentUser(r *http.Request) string {
    cookie,err:=r.Cookie("voxvoice_session")
    if err!=nil{return ""}
    p.mu.Lock()
    defer p.mu.Unlock()
    entry,ok:=p.logins[voxDigest(cookie.Value)]
    if !ok || time.Now().After(entry.Expires){return ""}
    return entry.Node
}
func (p *voxPortal) logout(w http.ResponseWriter,r *http.Request) {
    if !p.guard(w,r){return}
    if c,err:=r.Cookie("voxvoice_session");err==nil{
        p.mu.Lock();delete(p.logins,voxDigest(c.Value));p.mu.Unlock()
    }
    http.SetCookie(w,&http.Cookie{Name:"voxvoice_session",Path:"/",MaxAge:-1,Secure:true,HttpOnly:true,SameSite:http.SameSiteStrictMode})
    http.Redirect(w,r,"/",http.StatusSeeOther)
}

type voxAvatarStatus struct { UUID string; Active bool }
type voxRegionStatus struct { UUID string; Avatars []voxAvatarStatus }
type voxDashboard struct { User string; Regions []voxRegionStatus }

func (p *voxPortal) dashboard(w http.ResponseWriter,r *http.Request) {
    if !p.guard(w,r){return}
    user:=p.currentUser(r)
    if user==""{http.Redirect(w,r,"/login",http.StatusSeeOther);return}
    p.mu.Lock()
    stored:=p.accounts[user]
    regions:=make(map[string][]string)
    if stored!=nil{for region,avatars:=range stored.Regions{regions[region]=append([]string(nil),avatars...)}}
    p.mu.Unlock()
    live:=make(map[string]bool)
    p.media.mu.Lock()
    for _,peer:=range p.media.sessions {
        if peer.node==user && peer.pc!=nil && peer.pc.ConnectionState()==webrtc.PeerConnectionStateConnected{
            live[strings.ToLower(peer.admission.RegionID)+"/"+strings.ToLower(peer.admission.AvatarID)]=true
        }
    }
    p.media.mu.Unlock()
    view:=voxDashboard{User:user}
    for region,avatars:=range regions {
        s:=voxRegionStatus{UUID:region}
        sort.Strings(avatars)
        for _,uuid:=range avatars{s.Avatars=append(s.Avatars,voxAvatarStatus{UUID:uuid,Active:live[region+"/"+uuid]})}
        view.Regions=append(view.Regions,s)
    }
    sort.Slice(view.Regions,func(i,j int)bool{return view.Regions[i].UUID<view.Regions[j].UUID})
    // Refresh for near-live presence without WebSockets, JS or a frontend framework.
    w.Header().Set("Refresh","10")
    const body="<h2>Meine Simulatoren</h2><p>Angemeldet als <code>{{.User}}</code>. Gruen = Voice verbunden, Rot = aktuell keine Voice-Verbindung. Das ist kein Mikrofon-VAD.</p>{{range .Regions}}<h3>Region UUID: <code>{{.UUID}}</code></h3>{{range .Avatars}}<div class='row'><span class='dot {{if .Active}}on{{else}}off{{end}}'></span><code>{{.UUID}}</code> – {{if .Active}}Voice aktiv{{else}}Voice inaktiv{{end}}</div>{{end}}{{else}}<p>Noch keine Region registriert. Eine Region erscheint nach der ersten erfolgreichen WebRTC-Verbindung.</p>{{end}}<form action='/logout' method='post'><button>Abmelden</button></form>"
    p.page(w,"Dashboard",body,view)
}
