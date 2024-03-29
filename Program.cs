using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace DurakServer
{
    public class Program
    {
        private const int PORT = 6322;

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .ConfigureKestrel(options =>
                        {
                            options.Limits.MinRequestBodyDataRate = null;
                            options.Listen(IPAddress.Any, PORT,
                                listenOptions =>
                                {
                                    //listenOptions.UseHttps("grpcServer.pfx", "1511");
                                    listenOptions.Protocols = HttpProtocols.Http2;

                                });
                            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
                        });
                });
    }

    
}
