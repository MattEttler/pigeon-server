using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Pigeon
{
    public class Program
    {
        public static IConfiguration _configuration;

        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                .Build();

            string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings/appsettings.json")
            .AddJsonFile($"appsettings/appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

            Console.WriteLine("booted "+ _configuration["ApplicationTitle"] +"...");

            var host = new WebHostBuilder()
                .UseConfiguration(config)
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
