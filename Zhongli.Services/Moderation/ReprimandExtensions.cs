using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.EntityFrameworkCore;
using Zhongli.Data;
using Zhongli.Data.Models.Discord;
using Zhongli.Data.Models.Moderation;
using Zhongli.Data.Models.Moderation.Infractions;
using Zhongli.Data.Models.Moderation.Infractions.Reprimands;
using Zhongli.Data.Models.Moderation.Infractions.Triggers;
using Zhongli.Data.Models.Moderation.Logging;
using Zhongli.Services.Utilities;

namespace Zhongli.Services.Moderation;

public static class ReprimandExtensions
{
    public static bool IsActive(this IExpirable expirable)
        => expirable.EndedAt is null || expirable.ExpireAt >= DateTimeOffset.UtcNow;

    public static bool IsIncluded(this Reprimand reprimand, LogReprimandType log)
    {
        if (log == LogReprimandType.All)
            return true;

        return reprimand switch
        {
            Ban      => log.HasFlag(LogReprimandType.Ban),
            Censored => log.HasFlag(LogReprimandType.Censored),
            Kick     => log.HasFlag(LogReprimandType.Kick),
            Mute     => log.HasFlag(LogReprimandType.Mute),
            Note     => log.HasFlag(LogReprimandType.Note),
            Notice   => log.HasFlag(LogReprimandType.Notice),
            Warning  => log.HasFlag(LogReprimandType.Warning),
            _ => throw new ArgumentOutOfRangeException(
                nameof(reprimand), reprimand, "This reprimand type cannot be logged.")
        };
    }

    public static Color GetColor(this Reprimand reprimand)
    {
        if (reprimand.Status is not ReprimandStatus.Added)
            return Color.Purple;

        return reprimand switch
        {
            Ban      => Color.Red,
            Censored => Color.Blue,
            Kick     => Color.Red,
            Mute     => Color.Orange,
            Note     => Color.Blue,
            Notice   => Color.Gold,
            Warning  => Color.Gold,

            _ => throw new ArgumentOutOfRangeException(
                nameof(reprimand), reprimand, "An unknown reprimand was given.")
        };
    }

    public static EmbedBuilder AddReprimands(this EmbedBuilder embed, GuildUserEntity user) => embed
        .AddField("Warnings", $"{user.WarningCount(false)}/{user.WarningCount()}", true)
        .AddReprimand<Notice>(user)
        .AddReprimand<Ban>(user)
        .AddReprimand<Kick>(user)
        .AddReprimand<Note>(user);

    public static IEnumerable<Reprimand> OfType(this IEnumerable<Reprimand> reprimands, InfractionType type)
    {
        return type switch
        {
            InfractionType.Ban      => reprimands.OfType<Ban>(),
            InfractionType.Censored => reprimands.OfType<Censored>(),
            InfractionType.Kick     => reprimands.OfType<Kick>(),
            InfractionType.Mute     => reprimands.OfType<Mute>(),
            InfractionType.Note     => reprimands.OfType<Note>(),
            InfractionType.Notice   => reprimands.OfType<Notice>(),
            InfractionType.Warning  => reprimands.OfType<Warning>(),
            InfractionType.All      => reprimands,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type,
                "Invalid Infraction type.")
        };
    }

    public static IEnumerable<T> Reprimands<T>(this GuildUserEntity user, bool countHidden = true)
        where T : Reprimand
    {
        var reprimands = user.Guild.ReprimandHistory
            .Where(r => r.UserId == user.Id
                && r.Status is not ReprimandStatus.Deleted)
            .OfType<T>();

        return countHidden
            ? reprimands
            : reprimands.Where(IsCounted);
    }

    public static ReprimandType GetReprimandType(this Reprimand reprimand)
    {
        return reprimand switch
        {
            Censored => ReprimandType.Censored,
            Ban      => ReprimandType.Ban,
            Mute     => ReprimandType.Mute,
            Notice   => ReprimandType.Notice,
            Warning  => ReprimandType.Warning,
            Kick     => ReprimandType.Kick,
            Note     => ReprimandType.Note,
            _        => throw new ArgumentOutOfRangeException(nameof(reprimand))
        };
    }

    public static string GetMessage(this Reprimand action)
    {
        var mention = action.MentionUser();
        return action switch
        {
            Ban b      => $"{mention} was banned for {b.GetLength()}.",
            Censored c => $"{mention} was censored. Message: {c.CensoredMessage()}",
            Kick       => $"{mention} was kicked.",
            Mute m     => $"{mention} was muted for {m.GetLength()}.",
            Note       => $"{mention} was given a note.",
            Notice     => $"{mention} was given a notice.",
            Warning w  => $"{mention} was warned {w.Count} times.",

            _ => throw new ArgumentOutOfRangeException(
                nameof(action), action, "An unknown reprimand was given.")
        };
    }

    public static string GetReprimandExpiration(this Reprimand reprimand)
    {
        var expiry = reprimand switch
        {
            Ban b  => $"Expires in: {b.GetExpirationTime()}.",
            Mute m => $"Expires in: {m.GetExpirationTime()}.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(reprimand), reprimand, "This reprimand is not expirable.")
        };

        return new StringBuilder()
            .AppendLine($"User: {reprimand.MentionUser()}")
            .AppendLine(expiry)
            .ToString();
    }

    public static string GetTitle(this Reprimand action, bool showId)
    {
        var title = action switch
        {
            Ban      => nameof(Ban),
            Censored => nameof(Censored),
            Kick     => nameof(Kick),
            Mute     => nameof(Mute),
            Note     => nameof(Note),
            Notice   => nameof(Notice),
            Warning  => nameof(Warning),

            _ => throw new ArgumentOutOfRangeException(
                nameof(action), action, "An unknown reprimand was given.")
        };

        return showId ? $"{title.Humanize()}: {action.Id}" : title.Humanize();
    }

    public static StringBuilder GetReprimandDetails(this Reprimand r)
    {
        var content = new StringBuilder()
            .AppendLine($"▌{GetMessage(r)}")
            .AppendLine($"▌Reason: {r.GetReason()}")
            .AppendLine($"▌Moderator: {r.GetModerator()}")
            .AppendLine($"▌Date: {r.GetDate()}")
            .AppendLine($"▌Status: {Format.Bold(r.Status.Humanize())}");

        if (r.ModifiedAction is not null)
        {
            content
                .AppendLine("▌")
                .AppendLine($"▌▌{r.Status.Humanize()} by {r.ModifiedAction.GetModerator()}")
                .AppendLine($"▌▌{r.ModifiedAction.GetDate()}")
                .AppendLine($"▌▌{r.ModifiedAction.GetReason()}");
        }

        var t = r.Trigger;
        if (t is not null)
        {
            content
                .AppendLine("▌")
                .AppendLine($"▌▌{t.GetTitle()}")
                .AppendLine($"▌▌Trigger: {t.GetTriggerDetails()}");
        }

        return content;
    }

    public static Task<GuildEntity> GetGuildAsync(this ReprimandDetails details, ZhongliContext db,
        CancellationToken cancellationToken)
        => db.Guilds.TrackGuildAsync(details.Guild, cancellationToken);

    public static async Task<uint> CountAsync<T>(
        this T reprimand,
        DbContext db, bool countHidden = true,
        CancellationToken cancellationToken = default) where T : Reprimand
    {
        var user = await reprimand.GetUserAsync(db, cancellationToken);
        if (reprimand is Warning)
            return user.WarningCount(countHidden);

        return (uint) user.Reprimands<T>(countHidden).LongCount();
    }

    public static async Task<uint> CountAsync<T>(
        this T reprimand, Trigger trigger,
        DbContext db, bool countHidden = true,
        CancellationToken cancellationToken = default) where T : Reprimand
    {
        var user = await reprimand.GetUserAsync(db, cancellationToken);
        return (uint) user.Reprimands<T>(countHidden)
            .LongCount(r => r.TriggerId == trigger.Id);
    }

    public static uint HistoryCount<T>(this GuildUserEntity user, bool countHidden = true)
        where T : Reprimand
        => (uint) user.Reprimands<T>(countHidden).LongCount();

    public static uint WarningCount(this GuildUserEntity user, bool countHidden = true)
        => (uint) user.Reprimands<Warning>(countHidden).Sum(w => w.Count);

    public static async ValueTask<bool> IsIncludedAsync(this Reprimand reprimand, ModerationLogConfig config, DbContext db, CancellationToken cancellationToken)
    {
        var trigger = await reprimand.GetTriggerAsync<ReprimandTrigger>(db, cancellationToken);
        return trigger?.Source is TriggerSource.Warning or TriggerSource.Notice
            ? config.Options.HasFlag(ModerationLogConfig.ModerationLogOptions.Verbose)
            : reprimand.IsIncluded(config.LogReprimands) && reprimand.IsIncluded(config.LogReprimandStatus);
    }

    public static async ValueTask<GuildEntity> GetGuildAsync(this Reprimand reprimand, DbContext db,
        CancellationToken cancellationToken = default)
    {
        return (reprimand.Guild ??
            await db.FindAsync<GuildEntity>(new object[] { reprimand.GuildId }, cancellationToken))!;
    }

    public static async ValueTask<GuildUserEntity> GetUserAsync(this Reprimand reprimand, DbContext db,
        CancellationToken cancellationToken = default)
    {
        return (reprimand.User ??
            await db.FindAsync<GuildUserEntity>(new object[] { reprimand.UserId, reprimand.GuildId },
                cancellationToken))!;
    }

    public static async ValueTask<T?> GetTriggerAsync<T>(this Reprimand reprimand, DbContext db,
        CancellationToken cancellationToken = default) where T : Trigger
    {
        if (reprimand.Trigger is T trigger)
            return trigger;

        if (reprimand.TriggerId is not null)
            return await db.FindAsync<T>(new object[] { reprimand.TriggerId }, cancellationToken);

        return null;
    }

    private static bool IsCounted(Reprimand reprimand)
        => reprimand.Status is ReprimandStatus.Added or ReprimandStatus.Updated;

    private static bool IsIncluded(this Reprimand reprimand, LogReprimandStatus status)
    {
        if (status == LogReprimandStatus.All)
            return true;

        return reprimand.Status switch
        {
            ReprimandStatus.Unknown => false,
            ReprimandStatus.Added   => status.HasFlag(LogReprimandStatus.Added),
            ReprimandStatus.Expired => status.HasFlag(LogReprimandStatus.Expired),
            ReprimandStatus.Updated => status.HasFlag(LogReprimandStatus.Updated),
            ReprimandStatus.Hidden  => status.HasFlag(LogReprimandStatus.Hidden),
            ReprimandStatus.Deleted => status.HasFlag(LogReprimandStatus.Deleted),
            _ => throw new ArgumentOutOfRangeException(
                nameof(reprimand), reprimand, "This reprimand type cannot be logged.")
        };
    }

    private static EmbedBuilder AddReprimand<T>(this EmbedBuilder embed, GuildUserEntity user)
        where T : Reprimand => embed.AddField(typeof(T).Name.Pluralize(),
        $"{user.HistoryCount<T>(false)}/{user.HistoryCount<T>()}", true);

    private static string GetExpirationTime(this IExpirable expirable)
    {
        if (expirable.ExpireAt is null || expirable.Length is null) return "Indefinitely";

        var length = expirable.ExpireAt.Value - DateTimeOffset.UtcNow;
        return expirable.ExpireAt > DateTimeOffset.UtcNow
            ? length.Humanize(5, minUnit: TimeUnit.Second, maxUnit: TimeUnit.Year)
            : "Expired";
    }

    private static string GetLength(this ILength mute)
        => mute.Length?.Humanize(5,
            minUnit: TimeUnit.Second,
            maxUnit: TimeUnit.Year) ?? "indefinitely";
}