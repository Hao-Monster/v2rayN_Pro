namespace ServiceLib.Handler;

public static class SubscriptionHandler
{
    private sealed record SubscriptionDownloadResult(string Content, IReadOnlyDictionary<string, string> Headers);

    public static async Task<bool> UpdateProcess(Config config, string subId, bool blProxy, Func<bool, string, Task> updateFunc)
    {
        await updateFunc?.Invoke(false, ResUI.MsgUpdateSubscriptionStart);
        var subItem = await AppManager.Instance.SubItems();

        if (subItem is not { Count: > 0 })
        {
            await updateFunc?.Invoke(false, ResUI.MsgNoValidSubscription);
            return false;
        }

        var successCount = 0;
        foreach (var item in subItem)
        {
            try
            {
                if (!IsValidSubscription(item, subId))
                {
                    continue;
                }

                var hashCode = $"{item.Remarks}->";
                if (item.Enabled == false)
                {
                    await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSkipSubscriptionUpdate}");
                    continue;
                }

                // Create download handler
                var downloadHandle = CreateDownloadHandler(item, hashCode, updateFunc);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

                // Get all subscription content (main subscription + additional subscriptions)
                var result = await DownloadAllSubscriptions(config, item, blProxy, downloadHandle);

                // Process download result
                if (await ProcessDownloadResult(config, item.Id, result.Content, hashCode, updateFunc))
                {
                    ApplySubscriptionMetadata(item, result.Headers);
                    item.UpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                    await ConfigHandler.AddSubItem(config, item);
                    AppEvents.SubscriptionsRefreshRequested.Publish();
                    successCount++;
                }

                await updateFunc?.Invoke(false, "-------------------------------------------------------");
            }
            catch (Exception ex)
            {
                var hashCode = $"{item.Remarks}->";
                Logging.SaveLog("UpdateSubscription", ex);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgFailedImportSubscription}: {ex.Message}");
                await updateFunc?.Invoke(false, "-------------------------------------------------------");
            }
        }

        var success = successCount > 0;
        await updateFunc?.Invoke(success, $"{ResUI.MsgUpdateSubscriptionEnd}");
        return success;
    }

    private static bool IsValidSubscription(SubItem item, string subId)
    {
        var id = item.Id.TrimEx();
        var url = item.Url.TrimEx();

        if (id.IsNullOrEmpty() || url.IsNullOrEmpty())
        {
            return false;
        }

        if (subId.IsNotEmpty() && item.Id != subId)
        {
            return false;
        }

        if (!url.StartsWith(Global.HttpsProtocol) && !url.StartsWith(Global.HttpProtocol))
        {
            return false;
        }

        return true;
    }

    private static DownloadService CreateDownloadHandler(SubItem item, string hashCode, Func<bool, string, Task> updateFunc)
    {
        if (!HttpRequestHeadersHelper.TryParse(item.RequestHeaders, out var requestHeaders))
        {
            throw new FormatException(ResUI.SubRequestHeadersInvalid);
        }

        var downloadHandle = new DownloadService
        {
            AcceptHeader = "*/*",
            RequestHeaders = requestHeaders
        };
        downloadHandle.Error += (sender2, args) =>
        {
            updateFunc?.Invoke(false, $"{hashCode}{args.GetException().Message}");
        };
        return downloadHandle;
    }

    private static async Task<DownloadStringResult> DownloadSubscriptionContent(DownloadService downloadHandle, string url, bool blProxy, string userAgent)
    {
        var headers = DeviceInfoHelper.GetSubscriptionHeaders();
        var result = await downloadHandle.TryDownloadStringWithHeaders(url, blProxy, userAgent, headers);

        // If download with proxy fails, try direct connection
        if (blProxy && result?.Content.IsNullOrEmpty() != false)
        {
            result = await downloadHandle.TryDownloadStringWithHeaders(url, false, userAgent, headers);
        }

        return result ?? new DownloadStringResult(string.Empty, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<SubscriptionDownloadResult> DownloadAllSubscriptions(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        // Download main subscription content
        var mainResult = await DownloadMainSubscription(config, item, blProxy, downloadHandle);
        var content = mainResult.Content;

        // Process additional subscription links (if any)
        if (item.ConvertTarget.IsNullOrEmpty() && item.MoreUrl.TrimEx().IsNotEmpty())
        {
            content = await DownloadAdditionalSubscriptions(item, content, blProxy, downloadHandle);
        }

        return new SubscriptionDownloadResult(content, mainResult.Headers);
    }

    private static async Task<DownloadStringResult> DownloadMainSubscription(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        // Prepare subscription URL and download directly
        var url = Utils.GetPunycode(item.Url.TrimEx());

        // If conversion is needed
        if (item.ConvertTarget.IsNotEmpty())
        {
            var subConvertUrl = config.ConstItem.SubConvertUrl.IsNullOrEmpty()
                ? Global.SubConvertUrls.FirstOrDefault()
                : config.ConstItem.SubConvertUrl;

            url = string.Format(subConvertUrl!, Utils.UrlEncode(url));

            if (!url.Contains("target="))
            {
                url += $"&target={item.ConvertTarget}";
            }

            if (!url.Contains("config="))
            {
                url += $"&config={Global.SubConvertConfig.FirstOrDefault()}";
            }
        }

        // Download and return result directly
        return await DownloadSubscriptionContent(downloadHandle, url, blProxy, item.UserAgent);
    }

    private static async Task<string> DownloadAdditionalSubscriptions(SubItem item, string mainResult, bool blProxy, DownloadService downloadHandle)
    {
        var result = mainResult;

        // If main subscription result is Base64 encoded, decode it first
        if (result.IsNotEmpty() && Utils.IsBase64String(result))
        {
            result = Utils.Base64Decode(result);
        }

        // Process additional URL list
        var lstUrl = item.MoreUrl.TrimEx().Split(",") ?? [];
        foreach (var it in lstUrl)
        {
            var url2 = Utils.GetPunycode(it);
            if (url2.IsNullOrEmpty())
            {
                continue;
            }

            var additionalResult = (await DownloadSubscriptionContent(downloadHandle, url2, blProxy, item.UserAgent)).Content;

            if (additionalResult.IsNotEmpty())
            {
                // Process additional subscription results, add to main result
                if (Utils.IsBase64String(additionalResult))
                {
                    result += Environment.NewLine + Utils.Base64Decode(additionalResult);
                }
                else
                {
                    result += Environment.NewLine + additionalResult;
                }
            }
        }

        return result;
    }

    private static void ApplySubscriptionMetadata(SubItem item, IReadOnlyDictionary<string, string> headers)
    {
        var profileTitle = FindHeader(headers, "profile-title");
        if (item.AutoRemarks && profileTitle.IsNotEmpty())
        {
            var title = DecodeHeaderText(profileTitle!);
            if (title.IsNotEmpty())
            {
                item.Remarks = title;
            }
        }

        var userInfo = FindHeader(headers, "subscription-userinfo");
        if (userInfo.IsNotEmpty())
        {
            foreach (var part in userInfo!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2 || !long.TryParse(pair[1], out var value))
                {
                    continue;
                }

                switch (pair[0].ToLowerInvariant())
                {
                    case "upload":
                        item.TrafficUpload = value;
                        break;
                    case "download":
                        item.TrafficDownload = value;
                        break;
                    case "total":
                        item.TrafficTotal = value;
                        break;
                    case "expire":
                        item.ExpireTime = value;
                        break;
                }
            }
        }

        var announce = FindHeader(headers, "announce");
        if (announce.IsNotEmpty())
        {
            item.Announce = DecodeHeaderText(announce!);
        }

        item.ProfileWebPageUrl = FindHeader(headers, "profile-web-page-url") ?? item.ProfileWebPageUrl;
        item.SupportUrl = FindHeader(headers, "support-url") ?? item.SupportUrl;
    }

    private static string? FindHeader(IReadOnlyDictionary<string, string> headers, string suffix)
    {
        return headers.FirstOrDefault(item => item.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string DecodeHeaderText(string value)
    {
        var decoded = value;
        if (value.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value[7..]));
            }
            catch (FormatException)
            {
                decoded = value;
            }
        }

        return decoded.Replace("\\n", Environment.NewLine).Trim();
    }

    private static async Task<bool> ProcessDownloadResult(Config config, string id, string result, string hashCode, Func<bool, string, Task> updateFunc)
    {
        if (result.IsNullOrEmpty())
        {
            await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
            return false;
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");

        // If result is too short, display content directly
        if (result.Length < 99)
        {
            await updateFunc?.Invoke(false, $"{hashCode}{result}");
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartParsingSubscription}");

        // Add servers to configuration
        var ret = await ConfigHandler.AddBatchServers(config, result, id, true);
        if (ret <= 0)
        {
            Logging.SaveLog("FailedImportSubscription");
            Logging.SaveLog(result);
        }

        // Update completion message
        await updateFunc?.Invoke(false, ret > 0
                ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                : $"{hashCode}{ResUI.MsgFailedImportSubscription}");

        return ret > 0;
    }
}
