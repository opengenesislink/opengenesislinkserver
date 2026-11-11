// SPDX-License-Identifier: MPL-2.0
package main

import (
    "bytes"
    "encoding/json"
    "net/http"
    "net/http/httptest"
    "path/filepath"
    "strings"
    "strconv"
    "os"
    "testing"
    "time"
)

func TestVoxCredentialHmacAndRegionRegistration(t *testing.T) {
    account := &voxAccount{
        Node: "vox-test-node",
        Email: "test@example.invalid",
        Digest: voxDigest(strings.Repeat("p", 64)),
        Regions: make(map[string][]string),
    }
    m := newMediaServer(config{Nodes: map[string]nodeConfig{}, Tenants: map[string]tenantConfig{}})
    p := &voxPortal{
        media: m,
        path: filepath.Join(t.TempDir(), "accounts.json"),
        accounts: map[string]*voxAccount{account.Node: account},
        logins: make(map[string]voxLogin),
        attempts: make(map[string][]time.Time),
    }
    m.vox = p
    cfg, ok := p.nodeConfig(account.Node)
    if !ok || cfg.Tenant != account.Node || cfg.Secret != account.Digest {
        t.Fatal("registered account not usable as authenticated media node")
    }

    region := "52ea15a1-9033-4d86-bf39-52fcde825c15"
    body, err := json.Marshal(struct{RegionID string}{RegionID:region})
    if err != nil { t.Fatal(err) }
    nonce := "0123456789abcdef0123456789abcdef"
    stamp := time.Now().Unix()
    sig, err := signature(cfg.Secret,account.Node,stamp,nonce,body)
    if err != nil { t.Fatal(err) }
    req := httptest.NewRequest(http.MethodPost,"/internal/voxvoice/v1/regions",bytes.NewReader(body))
    req.Header.Set("X-OGLVoice-Node",account.Node)
    req.Header.Set("X-OGLVoice-Timestamp",formatVoxStamp(stamp))
    req.Header.Set("X-OGLVoice-Nonce",nonce)
    req.Header.Set("X-OGLVoice-Signature",sig)
    rec := httptest.NewRecorder()
    p.registerRegionAPI(rec,req)
    if rec.Code != http.StatusNoContent {
        t.Fatalf("region register failed: %d: %s",rec.Code,rec.Body.String())
    }
    if _,ok := account.Regions[region];!ok {
        t.Fatal("region UUID not registered")
    }
    if _,err:=newVoxAccountFileTest(p.path);err!=nil{t.Fatal(err)}
    replay := httptest.NewRecorder()
    // Retrying the identical signed request must be denied.
    p.registerRegionAPI(replay,httptest.NewRequest(http.MethodPost,"/internal/voxvoice/v1/regions",bytes.NewReader(body)))
    if replay.Code == http.StatusNoContent {
        t.Fatal("unsigned duplicate request accepted")
    }
}

func formatVoxStamp(stamp int64) string {
    return strconv.FormatInt(stamp,10)
}

func newVoxAccountFileTest(path string) ([]*voxAccount,error) {
    raw,err:=os.ReadFile(path)
    if err!=nil{return nil,err}
    var accounts []*voxAccount
    err=json.Unmarshal(raw,&accounts)
    return accounts,err
}

func TestVoxPresenceAndTenantBound(t *testing.T) {
    node := "vox-someone"
    p := &voxPortal{accounts:map[string]*voxAccount{node:{
        Node:node,Email:"test@example.invalid",Digest:voxDigest(strings.Repeat("q",64)),
    }}}
    cfg,ok:=p.nodeConfig(node)
    if !ok || cfg.Tenant!=node {t.Fatal("tenant ownership missing")}
    if _,ok=p.nodeConfig("not-registered");ok {t.Fatal("unknown tenant accepted")}
    if cfg.Secret == strings.Repeat("q",64) {t.Fatal("plaintext secret returned")}
}
