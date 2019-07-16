using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KzBsv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace KzPaymailAsp.Controllers
{
    [ApiController]
    [Produces("application/json")]
    public class KzPaymailController : ControllerBase
    {
        KzPaymailServerSingleton _singleton;

        bool _senderValidation = true;

        public KzPaymailController(KzPaymailServerSingleton singleton)
        {
            _singleton = singleton;
        }

        KzPaymailClient PaymailClient => _singleton.PaymailClient;

        KzPaymailClientInfo GetClientInfo(string alias, string domain, string tld) => _singleton.GetClientInfo(alias, domain, tld);

        NotFoundObjectResult NotFoundPaymail(string paymail) => NotFound(new PaymailError($"Paymail not found: {paymail}", "not-found"));
        NotFoundObjectResult NotValidPaymail(string paymail) => NotFound(new PaymailError($"Paymail not valid: {paymail}", "not-valid"));
        NotFoundObjectResult NotValidPaymail(string alias, string domain, string tld) => NotValidPaymail($"{alias}@{domain}.{tld}");

        [HttpGet(".well-known/bsvalias")]
        public IActionResult GetBsvAlias()
        {
            var host = HttpContext.Request.Host.ToString(); // Host "localhost:44369";
            var baseUrl = "https://" + host + "/api/v1/bsvalias/";

            var r = new {
                bsvalias = "1.0",
                capabilities = new ExpandoObject()
            };

            var caps = (IDictionary<string, object>)r.capabilities;

            // pki and paymentDestination are the minimum set of capabilitites and do not use BRFC IDs.
            // All other capabilities use BRFC IDs.
            caps["pki"] = baseUrl + "id/{alias}@{domain.tld}";
            caps["paymentDestination"] = baseUrl + "address/{alias}@{domain.tld}";

            // Request Sender Validation Capability
            caps[KzPaymail.ToBrfcId(KzPaymail.Capability.senderValidation)] = _senderValidation;

            // Verify Public Key Owner Capability: http://bsvalias.org/05-verify-public-key-owner.html
            caps[KzPaymail.ToBrfcId(KzPaymail.Capability.verifyPublicKeyOwner)] = baseUrl + "verifypubkey/{alias}@{domain.tld}/{pubkey}";

            return Ok(r);
        }

        [HttpGet("api/v1/bsvalias/id/{alias}@{domain}.{tld}")]
        public IActionResult GetId(string alias, string domain, string tld)
        {
            if (!KzPaymail.IsValid(alias, domain, tld)) return NotValidPaymail(alias, domain, tld);

            var pci = GetClientInfo(alias, domain, tld);
            if (pci == null) return NotFoundPaymail(pci.Paymail);

            return Ok(new { bsvalias = "1.0", handle = pci.Paymail, pubkey = pci.Pk.ToHex() });
        }

        [HttpGet("api/v1/bsvalias/verifypubkey/{alias}@{domain}.{tld}/{pubkey}")]
        public ActionResult<IEnumerable<string>> VerifyPubKey(string alias, string domain, string tld, string pubkey)
        {
            if (!KzPaymail.IsValid(alias, domain, tld)) return NotValidPaymail(alias, domain, tld);

            var pci = GetClientInfo(alias, domain, tld);
            if (pci == null) return NotFoundPaymail(pci.Paymail);

            return Ok(new { handle = pci.Paymail, pubkey = pubkey, match = pubkey == pci.Pk.ToHex() });
        }

        public class PostAddressInfo
        {
            public string senderName;
            public string senderHandle;
            public string dt;
            public long? amount;
            public string purpose;
            public string signature;
            public string pubkey;

            string GetMessage()
            {
                amount ??= 0;
                purpose ??= "";
                return $"{senderHandle}{amount}{dt}{purpose}";
            }

            public async Task<bool> IsValid(KzPaymailClient pc)
            {
                var key = pubkey == null ? (KzPubKey)null : new KzPubKey(pubkey);
                var (ok, _) = await pc.IsValidSignature(GetMessage(), signature, senderHandle, key);
                return ok;
            }
        }

        public class PaymailError
        {
            public string message;
            public string code;
            public string cause;

            public PaymailError(string message, string code, string cause = null)
            {
                this.message = message;
                this.code = code;
                this.cause = cause;
            }
        }

        [HttpPost("api/v1/bsvalias/address/{alias}@{domain}.{tld}")]
        public async Task<IActionResult> PostAddress(string alias, string domain, string tld, [FromBody] PostAddressInfo info)
        {
            if (!KzPaymail.IsValid(alias, domain, tld)) return NotValidPaymail(alias, domain, tld);

            if (string.IsNullOrWhiteSpace(info.senderHandle))
                return BadRequest(new PaymailError("Missing sender paymail", "missing-sender-paymail"));

            if (!KzPaymail.IsValid(info.senderHandle))
                return BadRequest(new PaymailError("Invalid sender paymail", "invalid-sender-paymail"));

            if (string.IsNullOrWhiteSpace(info.dt))
                return BadRequest(new PaymailError("Missing parameter dt", "missing-dt"));

            if (!DateTime.TryParse(info.dt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt)) 
                return BadRequest(new PaymailError("Invalid parameter dt", "invalid-dt"));

            var pci = GetClientInfo(alias, domain, tld);
            if (pci == null) return NotFoundPaymail(pci.Paymail);

            var apikey = Environment.GetEnvironmentVariable("APPSETTING_KzSendGridKey");
            if (!string.IsNullOrWhiteSpace(apikey)) {
                var client = new SendGridClient(apikey);
                var msg = new SendGridMessage();
                msg.SetFrom(new EmailAddress("kzpaymail@kzbsv.org", "KzPaymail Web Service"));
                msg.AddTo("tone@kizmet.org");
                msg.SetSubject("Log PostAddress");
                msg.AddContent(MimeType.Html, $"<dl><dt>To:</dt><dd>{pci.Paymail}</dd><dt>senderName:</dt><dd>{info.senderName}</dd><dt>senderHandle:</dt><dd>{info.senderHandle}</dd><dt>When:</dt><dd>{info.dt}</dd><dt>Amount:</dt><dd>{info.amount}</dd><dt>Purpose:</dt><dd>{info.purpose}</dd><dt>PubKey:</dt><dd>{info.pubkey}</dd><dt>Signature:</dt><dd>{info.signature}</dd></dl>");
                var response = await client.SendEmailAsync(msg);
            }

            if (_senderValidation)
            {
                if (!await info.IsValid(PaymailClient))
                    return Unauthorized();
            }

            var pk = pci.DeriveNext();

            return Ok(new { output = KzBScript.NewPubP2PKH(pk.ToHash160()).ToHex() });
        }
    }
}
