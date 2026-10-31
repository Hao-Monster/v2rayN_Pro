namespace ServiceLib.Handler;

public static class ConnectionHandler
{
    private static readonly string _tag = "ConnectionHandler";
    private static readonly string[] _directConnectivityUrls =
    [
        "https://www.msftconnecttest.com/connecttest.txt",
        "https://cp.cloudflare.com/generate_204",
        "http://connect.rom.miui.com/generate_204"
    ];
    private const string ProxyConnectivityUrl = "https://www.google.com/generate_204";

    /// <summary>
    /// Runs ping and IP checks.
    /// </summary>
    public static async Task<AvailabilityCheckResult> RunAvailabilityCheck()
    {
        var time = await GetRealPingTimeInfo();
        var ip = time > 0 ? await GetIPInfo() : Global.None;

        return new AvailabilityCheckResult(time, ip);
    }

    /// <summary>
    /// Gets IP information using the default local proxy.
    /// </summary>
    private static async Task<string?> GetIPInfo()
    {
        var webProxy = await GetWebProxy();

        var ipInfo = await GetIPInfo(webProxy);
        return ipInfo?.ToString() ?? Global.None;
    }

    /// <summary>
    /// Measures real ping time using configured test URL.
    /// </summary>
    public static async Task<int> GetRealPingTimeInfo()
    {
        var responseTime = -1;
        try
        {
            var webProxy = await GetWebProxy();

            for (var i = 0; i < 2; i++)
            {
                responseTime = await GetRealPingTime(webProxy);
                if (responseTime > 0)
                {
                    break;
                }
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }
        return responseTime;
    }

    public static async Task<bool> HasDirectInternetAccess()
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return false;
        }

        using var client = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        foreach (var url in _directConnectivityUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if ((int)response.StatusCode is >= 200 and < 400)
                {
                    return true;
                }
            }
            catch
            {
                // Try the next independent connectivity endpoint.
            }
        }

        return false;
    }

    public static async Task<bool> HasProxyInternetAccess()
    {
        try
        {
            var webProxy = await GetWebProxy();
            using var client = new HttpClient(new SocketsHttpHandler
            {
                Proxy = webProxy,
                UseProxy = true,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                AutomaticDecompression = DecompressionMethods.All
            })
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, ProxyConnectivityUrl);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.NoContent;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates local SOCKS proxy instance.
    /// </summary>
    private static async Task<WebProxy?> GetWebProxy()
    {
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    /// <summary>
    /// Measures response time by sending HTTP requests through proxy.
    /// </summary>
    public static async Task<int> GetRealPingTime(IWebProxy? webProxy, CancellationToken cancellationToken = default)
    {
        var url = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;
        var responseTime = -1;
        try
        {
            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(Global.LocalFetch);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var linkedToken = linkedCts.Token;
            using var client = new HttpClient(new SocketsHttpHandler()
            {
                Proxy = webProxy,
                UseProxy = webProxy != null,
                ConnectTimeout = Global.LocalFetch,
            });

            List<int> oneTime = [];
            for (var i = 0; i < 2; i++)
            {
                var timer = Stopwatch.StartNew();
                await client.GetAsync(url, linkedToken).ConfigureAwait(false);
                timer.Stop();
                oneTime.Add((int)timer.Elapsed.TotalMilliseconds);
                await Task.Delay(100, linkedToken);
            }
            responseTime = oneTime.Where(x => x > 0).OrderBy(x => x).FirstOrDefault();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore
        }
        return responseTime;
    }

    /// <summary>
    /// Gets IP and country information through specified proxy.
    /// </summary>
    public static async Task<IpInfoResult?> GetIPInfo(IWebProxy? webProxy, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = AppManager.Instance.Config.SpeedTestItem.IPAPIUrl;
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var downloadHandle = new DownloadService();
            var result = await downloadHandle.TryDownloadString(url, webProxy, "", cancellationToken);
            if (result == null)
            {
                return null;
            }

            var ipInfo = JsonUtils.Deserialize<IPAPIInfo>(result);
            if (ipInfo == null)
            {
                return null;
            }

            var ip = ipInfo.ip ?? ipInfo.clientIp ?? ipInfo.ip_addr ?? ipInfo.query;
            var country = ipInfo.country_code ?? ipInfo.country ?? ipInfo.countryCode ?? ipInfo.location?.country_code ?? "unknown";

            return new IpInfoResult(country, ip);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets Cloudflare colo region through specified proxy.
    /// </summary>
    public static async Task<string> GetCloudflareRegion(IWebProxy? webProxy)
    {
        foreach (var url in CloudflareColoMapper.TraceUrls)
        {
            try
            {
                var result = await GetCloudflareTrace(url, webProxy).ConfigureAwait(false);
                var region = CloudflareColoMapper.FromTrace(result);
                if (region != CloudflareColoMapper.Unknown)
                {
                    return region;
                }
            }
            catch
            {
            }
        }

        return CloudflareColoMapper.Unknown;
    }

    private static async Task<string?> GetCloudflareTrace(string url, IWebProxy? webProxy)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = new HttpClient(new SocketsHttpHandler()
        {
            Proxy = webProxy,
            UseProxy = webProxy != null,
            AutomaticDecompression = DecompressionMethods.All,
        });
        client.DefaultRequestHeaders.UserAgent.TryParseAdd(Utils.GetVersion(false));

        using var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
    }
}
