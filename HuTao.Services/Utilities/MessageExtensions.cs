using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using HuTao.Data.Models.Discord;
using HuTao.Data.Models.Logging;
using HuTao.Services.Quote;

namespace HuTao.Services.Utilities;

public static class MessageExtensions
{
    public static bool IsJumpUrls(string text) => string.IsNullOrWhiteSpace(CleanJumpUrls(text));

    public static IEnumerable<JumpMessage> GetJumpMessages(string text)
        => RegexUtilities.JumpUrl.Matches(text).Select(ToJumpMessage).OfType<JumpMessage>();

    public static JumpMessage? GetJumpMessage(string text)
        => ToJumpMessage(RegexUtilities.JumpUrl.Match(text));

    public static string GetJumpUrl(this MessageLog message)
        => $"https://discord.com/channels/{message.GuildId}/{message.ChannelId}/{message.MessageId}";

    public static string GetJumpUrlForEmbed(this IMessage message)
        => Format.Url($"#{message.Channel.Name} (Jump)", message.GetJumpUrl());

    public static string GetJumpUrlForEmbed(this MessageLog message)
        => Format.Url($"{message.MentionChannel()} (Jump)", message.GetJumpUrl());

    public static Task<IMessage?> GetMessageAsync(
        this QuotedMessage jump,
        bool allowHidden = false, bool? allowNsfw = null)
        => jump.GetMessageAsync(jump.Context, allowHidden, allowNsfw);

    public static async Task<IMessage?> GetMessageAsync(
        this JumpMessage jump, Context context,
        bool allowHidden = false, bool? allowNsfw = null)
    {
        if (context.User is not IGuildUser user) return null;

        var (guildId, channelId, messageId, ignored) = jump;
        if (ignored || context.Guild.Id != guildId) return null;

        var channel = await context.Guild.GetTextChannelAsync(channelId);
        if (channel is null) return null;

        allowNsfw ??= (context.Channel as ITextChannel)?.IsNsfw;
        return await GetMessageAsync(channel, messageId, user, allowHidden, allowNsfw ?? false);
    }

    private static JumpMessage? ToJumpMessage(Match match)
        => ulong.TryParse(match.Groups["GuildId"].Value, out var guildId)
            && ulong.TryParse(match.Groups["ChannelId"].Value, out var channelId)
            && ulong.TryParse(match.Groups["MessageId"].Value, out var messageId)
                ? new JumpMessage(guildId, channelId, messageId,
                    match.Groups["OpenBrace"].Success && match.Groups["CloseBrace"].Success)
                : null;

    private static string CleanJumpUrls(string text) => RegexUtilities.JumpUrl.Replace(text, m
        => m.Groups["OpenBrace"].Success && m.Groups["CloseBrace"].Success ? m.Value : string.Empty);

    private static async Task<IMessage?> GetMessageAsync(
        this ITextChannel channel, ulong messageId, IGuildUser user,
        bool allowHidden = false, bool allowNsfw = false)
    {
        if (channel.IsNsfw && !allowNsfw)
            return null;

        var channelPermissions = user.GetPermissions(channel);
        if (!channelPermissions.ViewChannel && !allowHidden)
            return null;

        var cacheMode = channelPermissions.ReadMessageHistory
            ? CacheMode.AllowDownload
            : CacheMode.CacheOnly;

        return await channel.GetMessageAsync(messageId, cacheMode);
    }
}