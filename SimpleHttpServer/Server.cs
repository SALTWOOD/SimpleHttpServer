using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SimpleHttpServer
{
    public class Server : IDisposable
    {
        private bool disposed;

        public WebApplication Application { get; protected set; }
        public string WorkingDirectory { get; set; } = "./";

        public Server(Config config)
        {
            X509Certificate2? cert = LoadAndConvertCert(config.CertificateFile, config.CertificateKeyFile);
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel(options =>
            {
                if (cert != null && config.HttpsPort != ushort.MinValue) options.ListenAnyIP(config.HttpsPort, configure =>
                {
                    configure.Protocols = config.HttpProtocols;
                    configure.UseHttps(cert);
                });
            });
            builder.WebHost.UseKestrel(options =>
            {
                options.ListenAnyIP(config.Port);
            });
            this.Application = builder.Build();

            // 下载路由
            this.Application.MapGet("/", context => DoRoute(context, "./"));
            this.Application.MapGet("/{*path}", DoRoute);

            Application.UseHttpsRedirection();
        }

        public async Task DoRoute(HttpContext context, string path)
        {
            if (Directory.Exists(path))
            {
                await context.Response.WriteAsync(string.Join("\n", Directory.EnumerateFileSystemEntries(path)));
            }
            else if (File.Exists(path))
            {
                path = Path.Combine(WorkingDirectory, path).Replace('\\', '/');
                if (!File.Exists(path))
                {
                    LogAccess(context);
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Not found.");
                }

                // 获取文件信息
                using var stream = File.OpenRead(path);
                long fileSize = stream.Length;

                // 检查是否支持断点续传
                var isRangeRequest = context.Request.Headers.ContainsKey("Range");
                if (isRangeRequest)
                {
                    // 解析 Range 头部，获取断点续传的起始位置和结束位置
                    var rangeHeader = context.Request.Headers["Range"].ToString();
                    var (startByte, endByte) = GetRange(rangeHeader, fileSize);

                    // 设置响应头部
                    context.Response.StatusCode = 206; // Partial Content
                    context.Response.Headers.Append("Accept-Ranges", "bytes");
                    context.Response.Headers.Append("Content-Range", $"bytes {startByte}-{endByte}/{fileSize}");
                    // context.Response.Headers.Append("Content-Type", MimeTypesMap.GetMimeType(fullPath));
                    context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{path.Split('/').LastOrDefault()}\"");

                    // 计算要读取的字节数
                    var totalBytesToRead = endByte - startByte + 1;

                    context.Response.Headers["Content-Length"] = totalBytesToRead.ToString();

                    stream.Seek(startByte, SeekOrigin.Begin);
                    byte[] buffer = new byte[4096];
                    for (; stream.Position < endByte;)
                    {
                        int count = stream.Read(buffer, 0, buffer.Length);
                        if (stream.Position > endByte && stream.Position - count < endByte) await context.Response.Body.WriteAsync(buffer[..(int)(count - stream.Position + endByte + 1)]);
                        else if (count != buffer.Length) await context.Response.Body.WriteAsync(buffer[..(count)]);
                        else await context.Response.Body.WriteAsync(buffer);
                    }
                }
                else
                {
                    // 设置响应头部
                    context.Response.Headers.Append("Accept-Ranges", "bytes");
                    context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(path.Split('/').LastOrDefault())}\"");
                    //context.Response.Headers.Append("Content-Type", MimeTypesMap.GetMimeType(fullPath));
                    context.Response.Headers.Append("Content-Range", $"bytes {0}-{fileSize - 1}/{fileSize}");
                    context.Response.ContentLength = fileSize;
                    await stream.CopyToAsync(context.Response.Body);
                }
            }
            LogAccess(context);
        }

        protected X509Certificate2? LoadAndConvertCert(string? certPath, string? keyPath)
        {
            if (!File.Exists(certPath) || !File.Exists(keyPath))
            {
                return null;
            }
            X509Certificate2 cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            byte[] pfxCert = cert.Export(X509ContentType.Pfx);
            using (var file = File.Create("certificate/cert.pfx"))
            {
                file.Write(pfxCert);
            }
            cert = new X509Certificate2(pfxCert);
            return cert;
        }

        public static void LogAccess(HttpContext context)
        {
            context.Request.Headers.TryGetValue("user-agent", out StringValues value);
            Console.WriteLine($"{context.Request.Method} {context.Request.Path.Value} {context.Request.Protocol} <{context.Response.StatusCode}> - [{context.Connection.RemoteIpAddress}] {value.FirstOrDefault()}");
        }

        public static (long startByte, long endByte) GetRange(string rangeHeader, long fileSize)
        {
            if (rangeHeader.Length <= 6) return (0, fileSize);
            var ranges = rangeHeader[6..].Split("-");
            try
            {
                if (ranges[1].Length > 0)
                {
                    return (long.Parse(ranges[0]), long.Parse(ranges[1]));
                }
            }
            catch (Exception)
            {
                return (long.Parse(ranges[0]), fileSize - 1);
            }

            return (long.Parse(ranges[0]), fileSize - 1);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    this.Application.StopAsync().Wait();
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposed = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~Server()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
