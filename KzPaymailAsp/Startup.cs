using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using KzBsv;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KzPaymailAsp
{
    public class KzPaymailServerSingleton
    {
        IConfiguration _config;

        object _clientsLock = new object();
        /// <summary>
        /// These are Paymail service clients: Paymails we are implementing support for through this service.
        /// </summary>
        Dictionary<string, KzPaymailClientInfo> _clients;

        KzPaymailClient _paymailClient;
        /// <summary>
        /// This is an implementation of the paymail client API to make requests of paymails hosted elsewhere.
        /// </summary>
        public KzPaymailClient PaymailClient => _paymailClient;

        public KzPaymailServerSingleton(IConfiguration config)
        {
            _config = config;
            var i = 0;
            _clients = new Dictionary<string, KzPaymailClientInfo>();
            do {
                var v = _config["KzPaymailClient" + i];
                if (string.IsNullOrWhiteSpace(v)) break;
                var p = v.Split(',');
                var c = new KzPaymailClientInfo(p[0], p[2], p[1]);
                _clients.Add(c.Paymail, c);
                i++;
            } while (true);

            _paymailClient = new KzPaymailClient();
        }

        public KzPaymailClientInfo GetClientInfo(string alias, string domain, string tld)
        {
            lock (_clientsLock)
            {
                if (!_clients.TryGetValue($"{alias}@{domain}.{tld}", out KzPaymailClientInfo value))
                    return null;
                return value;
            }
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton<KzPaymailServerSingleton>(new KzPaymailServerSingleton(Configuration))
                .AddControllers()
                .AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            } else {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });
        }
    }
}
