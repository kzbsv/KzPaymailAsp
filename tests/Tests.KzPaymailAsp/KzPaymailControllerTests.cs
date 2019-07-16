using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace Tests.KzPaymailAsp
{
    public class KzPaymailControllerTests
    {
        //static string host = "localhost:44369";
        static string host = "kzpaymailasp20190604034428.azurewebsites.net";

        string url;

        HashSet<string> pubKeyHashes = new HashSet<string>();

        public KzPaymailControllerTests()
        {
            url = $"https://{host}/";
        }

        [Fact]
        public async Task GetBsvAliasOk()
        {
            var expected = @"
{""bsvalias"":""1.0"",
""capabilities"":{
""pki"":""https://localhost:44369/api/v1/bsvalias/id/{alias}@{domain.tld}"",
""paymentDestination"":""https://localhost:44369/api/v1/bsvalias/address/{alias}@{domain.tld}"",
""c318d09ed403"":true,
""a9f510c16bde"":""https://localhost:44369/api/v1/bsvalias/verifypubkey/{alias}@{domain.tld}/{pubkey}""
}}".Replace("localhost:44369", host).Replace("\r\n", "");

            var c = new HttpClient();
            var r = await c.GetAsync(url + ".well-known/bsvalias");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var json = await r.Content.ReadAsStringAsync();
            Assert.Equal(expected, json);
        }

        [Fact]
        public async Task GetIdNotFound()
        {
            var c = new HttpClient();
            var r = await c.GetAsync(url + "api/v1/bsvalias/id/doesnot@exist.anywhere");
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        }

        [Fact]
        public async Task GetIdOk()
        {
            var expected = @"
{
""bsvalias"":""1.0"",
""handle"":""tonesnotes@kizmet.org"",
""pubkey"":""03e685d4484bf7aadaf2234b55af0763967be69e068a8a01e4ef96301c0e589978""
}".Replace("\r\n", "");

            var c = new HttpClient();
            var r = await c.GetAsync(url + "api/v1/bsvalias/id/tonesnotes@kizmet.org");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var json = await r.Content.ReadAsStringAsync();
            Assert.Equal(expected, json);
        }

        [Fact]
        public async Task PostAddressNotFound()
        {
            var c = new HttpClient();
            var content = new StringContent("", Encoding.UTF8, "application/json");
            var r = await c.PostAsync(url + "api/v1/bsvalias/address/doesnot@exist.anywhere", content);
            Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType });
        }

        public class PostAddressResponse
        {
            /// expected = @"{""output"":""76a914f8a0f407cc502b7fd98fccb3d0a77d4e881b62d088ac""}";
            public string output { get; set; }
            public bool IsP2PKH()
            {
                return output != null && output[0..6] == "76a914" && output[46..] == "88ac";
            }
            public string PubKeyHash => output[6..46];
        }

        [Fact]
        public async Task PostAddressOk()
        {
            var post = new {
                senderName = "FirstName LastName",
                senderHandle = "<alias>@<domain.tld>",
                dt = "<ISO-8601 timestamp>",
                amount = 550,
                purpose = "message to receiver",
                signature = "<compact Bitcoin message signature>"
            };
            var postJson = JsonSerializer.ToString(post);

            for (var i = 0; i < 10; i++) {
                var c = new HttpClient();
                var content = new StringContent(postJson, Encoding.UTF8, "application/json");
                var r = await c.PostAsync(url + "api/v1/bsvalias/address/tonesnotes@kizmet.org", content);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                var parJson = await r.Content.ReadAsStringAsync();
                var par = JsonSerializer.Parse<PostAddressResponse>(parJson);
                Assert.True(par.IsP2PKH());
                Assert.False(pubKeyHashes.Contains(par.PubKeyHash));
                pubKeyHashes.Add(par.PubKeyHash);
            }
        }
    }
}
