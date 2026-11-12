using System.Security.Cryptography;
using System.Text;
using OpenSim.Framework;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
using ECDsa home = ECDsa.Create(ECCurve.NamedCurves.nistP256);
using ECDsa destination = ECDsa.Create(ECCurve.NamedCurves.nistP256);
using ECDsa attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);
string originGrid = "https://home.test:8002";
string targetGrid = "https://destination.test:8002";
string avatar = Guid.NewGuid().ToString("D");
string session = Guid.NewGuid().ToString("D");
string trip = Guid.NewGuid().ToString("D");
var now = DateTimeOffset.UtcNow;
long stamp = now.ToUnixTimeSeconds();
byte[] form = Encoding.UTF8.GetBytes("METHOD=GETFOLDERCONTENT&PRINCIPAL=" + avatar);
var grant = new HGSP2Authorization.Grant {
    Issuer = originGrid, Audience = targetGrid, Avatar = avatar,
    Session = session, Trip = trip, Scopes = "inventory.read inventory.write",
    PeerKeyId = HGSP2Authorization.KeyId(HGSP2Authorization.PublicKey(destination)),
    TokenId = Guid.NewGuid().ToString("D"), NotBefore = stamp - 3, Expires = stamp + 3600
};
string ticket = HGSP2Authorization.Sign(grant, home);
HGSP2Authorization.Proof Proof(string nonce, string scope = "inventory.read") => new() {
    Issuer = targetGrid, Audience = originGrid, Avatar = avatar, Session = session,
    Trip = trip, Scope = scope, BodySHA256 = HGSP2Authorization.Digest(form),
    GrantSHA256 = HGSP2Authorization.Digest(Encoding.UTF8.GetBytes(ticket)),
    Nonce = nonce, Issued = stamp, Expires = stamp + 90
};
string proof = HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination);
var replay = new HGSP2Authorization.ReplayWindow();
bool Travel(string user, string sid, string journey, string peer)
    => user == avatar && sid == session && journey == trip && peer == targetGrid;
bool Authorize(string g, string p, byte[] body, string method, string owner,
    ECDsa homeKey, ECDsa peerKey, Func<string, string, string, string, bool> valid,
    DateTimeOffset? clock = null)
    => HGSP2Authorization.AuthorizeInventory(g, p, body, method, owner, originGrid,
        targetGrid, homeKey, peerKey, replay, valid, clock ?? now);

Check(Authorize(ticket, proof, form, "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Valid peer/home signed read rejected");
Check(!Authorize(ticket, proof, form, "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Exact signed request replay accepted");
Check(!Authorize(ticket, HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), attacker),
    form, "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Attacker signed request accepted");
Check(!Authorize(HGSP2Authorization.Sign(grant, attacker),
    HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination),
    form, "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Attacker forged home grant accepted");
Check(!Authorize(ticket, HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination),
    form, "GETFOLDERCONTENT", Guid.NewGuid().ToString("D"), home, destination, Travel),
    "Different owner UUID accepted");
Check(!Authorize(ticket, HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination),
    Encoding.UTF8.GetBytes("METHOD=GETITEM&PRINCIPAL=" + avatar),
    "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Tampered POST body accepted");
Check(!Authorize(ticket, HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination),
    form, "GETFOLDERCONTENT", avatar, home, destination, (_, _, _, _) => false),
    "Revoked/nonexistent travel accepted");
var expiry = Proof(Guid.NewGuid().ToString("D")); expiry.Issued = stamp - 300;
expiry.Expires = stamp - 240;
Check(!Authorize(ticket, HGSP2Authorization.Sign(expiry, destination), form,
    "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Expired proof accepted");
var switched = Proof(Guid.NewGuid().ToString("D")); switched.Audience = targetGrid;
Check(!Authorize(ticket, HGSP2Authorization.Sign(switched, destination), form,
    "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Cross-audience request accepted");
var changedGrant = new HGSP2Authorization.Grant {
    Issuer = grant.Issuer, Audience = "https://wrong.test", Avatar = avatar,
    Session = session, Trip = trip, Scopes = grant.Scopes, TokenId = grant.TokenId,
    PeerKeyId = grant.PeerKeyId, NotBefore = grant.NotBefore, Expires = grant.Expires
};
string foreignGrant = HGSP2Authorization.Sign(changedGrant, home);
Check(!Authorize(foreignGrant, HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination),
    form, "GETFOLDERCONTENT", avatar, home, destination, Travel), "Wrong destination accepted");
Check(!Authorize(ticket, HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination),
    form, "CREATEUSERINVENTORY", avatar, home, destination, Travel),
    "Unexpected inventory method allowed");
Check(HGSP2Authorization.RequiredScope("GETFOLDERCONTENT") == "inventory.read" &&
      HGSP2Authorization.RequiredScope("ADDITEM") == "inventory.write",
      "Security scope mapping invalid");
var noWrite = new HGSP2Authorization.Grant {
    Issuer = originGrid, Audience = targetGrid, Avatar = avatar,
    Session = session, Trip = trip, Scopes = "inventory.read",
    PeerKeyId = grant.PeerKeyId, TokenId = Guid.NewGuid().ToString("D"),
    NotBefore = stamp - 2, Expires = stamp + 800
};
Check(!Authorize(HGSP2Authorization.Sign(noWrite, home), HGSP2Authorization.Sign(
    Proof(Guid.NewGuid().ToString("D"), "inventory.write"), destination),
    form, "ADDITEM", avatar, home, destination, Travel),
    "Read-only travel grant allowed inventory write");
var writeProof = HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D"), "inventory.write"), destination);
Check(Authorize(ticket, writeProof, form, "ADDITEM", avatar, home, destination, Travel),
    "Valid explicitly authorized write rejected");
var tainted = ticket[..^1] + (ticket[^1] == 'A' ? "B" : "A");
Check(!Authorize(tainted, HGSP2Authorization.Sign(Proof(Guid.NewGuid().ToString("D")), destination),
    form, "GETFOLDERCONTENT", avatar, home, destination, Travel),
    "Modified signed ticket accepted");
Console.WriteLine("HGSP2 ES256 home/peer proofs, ownership, scope, expiry, replay, revocation, tampering: PASS");
