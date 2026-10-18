using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct HeaderActionRect
{
    public HeaderActionRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
    public float Right => X + Width;
}

internal readonly struct HeaderActionLayout
{
    public HeaderActionLayout(
        HeaderActionRect drag,
        HeaderActionRect title,
        HeaderActionRect website,
        HeaderActionRect thunderstore,
        HeaderActionRect kofi,
        HeaderActionRect discord,
        HeaderActionRect close,
        string websiteLabel,
        string thunderstoreLabel,
        string kofiLabel,
        string discordLabel,
        string closeLabel)
    {
        Drag = drag;
        Title = title;
        Website = website;
        Thunderstore = thunderstore;
        Kofi = kofi;
        Discord = discord;
        Close = close;
        WebsiteLabel = websiteLabel;
        ThunderstoreLabel = thunderstoreLabel;
        KofiLabel = kofiLabel;
        DiscordLabel = discordLabel;
        CloseLabel = closeLabel;
    }

    public HeaderActionRect Drag { get; }
    public HeaderActionRect Title { get; }
    public HeaderActionRect Website { get; }
    public HeaderActionRect Thunderstore { get; }
    public HeaderActionRect Kofi { get; }
    public HeaderActionRect Discord { get; }
    public HeaderActionRect Close { get; }
    public string WebsiteLabel { get; }
    public string ThunderstoreLabel { get; }
    public string KofiLabel { get; }
    public string DiscordLabel { get; }
    public string CloseLabel { get; }
}

internal static class HeaderActionLayoutPolicy
{
    public const string WebsiteUrl = "https://bluesagetech.net/";
    // Blue, 2026-10-18: the website is Coming Soon, so the button says so instead of opening a page that isn't live.
    public const string WebsiteTooltip = "Our website, bluesagetech.net, is Coming Soon.";
    public const string WebsiteComingSoonNotice = "bluesagetech.net is Coming Soon ♥ The Discord button has everything for now.";
    public const string ThunderstoreUrl = "https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/";

    private const float FullLayoutMinimumWidth = 410f;
    private const float RightMargin = 12f;
    private const float Gap = 8f;
    private const float ButtonY = 5f;
    private const float ButtonHeight = 28f;
    private const float WebsiteWidth = 76f;
    private const float ThunderstoreWidth = 108f;
    private const float KofiWidth = 58f;
    private const float DiscordWidth = 76f;
    private const float CloseWidth = 30f;
    private const float TitleX = 18f;
    private const float TitleY = 7f;
    private const float TitleHeight = 28f;
    private const float TitleRightGap = 12f;
    private const float CompactRightMargin = 6f;
    private const float CompactGap = 5f;
    private const float CompactWebsiteWidth = 44f;
    private const float CompactThunderstoreWidth = 48f;
    private const float CompactKofiWidth = 46f;
    private const float CompactDiscordWidth = 46f;
    private const float CompactTitleRightGap = 8f;

    public static HeaderActionLayout Resolve(float windowWidth)
    {
        float safeWindowWidth = Math.Max(0f, windowWidth);
        bool compact = safeWindowWidth < FullLayoutMinimumWidth;
        float rightMargin = compact ? CompactRightMargin : RightMargin;
        float gap = compact ? CompactGap : Gap;
        float websiteWidth = compact ? CompactWebsiteWidth : WebsiteWidth;
        float thunderstoreWidth = compact ? CompactThunderstoreWidth : ThunderstoreWidth;
        float kofiWidth = compact ? CompactKofiWidth : KofiWidth;
        float discordWidth = compact ? CompactDiscordWidth : DiscordWidth;
        float titleRightGap = compact ? CompactTitleRightGap : TitleRightGap;
        float actionWidth =
            websiteWidth + thunderstoreWidth + kofiWidth + discordWidth + CloseWidth +
            (gap * 4f);
        float x = Math.Max(0f, safeWindowWidth - rightMargin - actionWidth);

        HeaderActionRect website = NewAction(ref x, websiteWidth, gap);
        HeaderActionRect thunderstore = NewAction(ref x, thunderstoreWidth, gap);
        HeaderActionRect kofi = NewAction(ref x, kofiWidth, gap);
        HeaderActionRect discord = NewAction(ref x, discordWidth, gap);
        HeaderActionRect close = NewAction(ref x, CloseWidth, 0f);
        HeaderActionRect title = new HeaderActionRect(
            TitleX,
            TitleY,
            Math.Max(0f, website.X - titleRightGap - TitleX),
            TitleHeight);
        HeaderActionRect drag = new HeaderActionRect(0f, 0f, title.Right, 34f);

        return new HeaderActionLayout(
            drag,
            title,
            website,
            thunderstore,
            kofi,
            discord,
            close,
            compact ? "Web" : "Website",
            compact ? "Mods" : "Thunderstore",
            "Ko-fi",
            compact ? "Chat" : "Discord",
            "X");
    }

    private static HeaderActionRect NewAction(ref float x, float width, float gap)
    {
        var rect = new HeaderActionRect(x, ButtonY, width, ButtonHeight);
        x += width + gap;
        return rect;
    }
}
