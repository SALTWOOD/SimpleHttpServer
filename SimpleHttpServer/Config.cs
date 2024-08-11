using Microsoft.AspNetCore.Server.Kestrel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace SimpleHttpServer
{
    public class Config
    {
        [YamlMember(Order = 1, Description = "证书文件的位置")]
        public string CertificateFile { get; set; } = "./certificate/cert.pem";
        [YamlMember(Order = 1, Description = "证书 Key 文件的位置")]
        public string CertificateKeyFile { get; set; } = "./certificate/key.pem";
        [YamlMember(Order = 0, Description = "工作目录")]
        public string WorkingDirectory { get; set; } = "./";
        [YamlMember(Order = 2, Description = "是否启用 Https")]
        public bool EnableHttps { get; set; } = true;
        [YamlMember(Order = 2, Description = """
设定 Http 协议，有以下值可用：
- None
- Http1
- Http2
- Http3
- Http1AndHttp2
- Http1AndHttp2AndHttp3
默认为 Http1AndHttp2AndHttp3
""")]
        public HttpProtocols HttpProtocols { get; set; } = HttpProtocols.Http1AndHttp2AndHttp3;
        [YamlMember(Order = 2, Description = "设定服务器端口")]
        public ushort Port { get; set; } = 80;
        [YamlMember(Order = 2, Description = "设定服务器 Https 端口")]
        public ushort HttpsPort { get; set; } = 443;
    }
}
