using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Cryptography.X509Certificates;
using YamlDotNet.Serialization;
using System.Text;

namespace SimpleHttpServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Deserializer deserializer = new Deserializer();
            Config config = deserializer.Deserialize<Config>(
                File.Exists("config.yml") ?
                File.ReadAllText("config.yml") : "{}"
            );
            using (Stream stream = File.Create("config.yml"))
            {
                Serializer serializer = new Serializer();
                stream.Write(Encoding.UTF8.GetBytes(serializer.Serialize(config)));
            }
            Server server = new Server(config);
            server.WorkingDirectory = config.WorkingDirectory;
            server.Application.Run();
        }
    }
}
