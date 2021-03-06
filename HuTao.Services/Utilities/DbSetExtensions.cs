using System.Threading;
using System.Threading.Tasks;
using Discord;
using HuTao.Data.Models.Discord;
using HuTao.Data.Models.Discord.Reaction;
using HuTao.Data.Models.Moderation.Infractions.Reprimands;
using Microsoft.EntityFrameworkCore;

namespace HuTao.Services.Utilities;

public static class DbSetExtensions
{
    public static async Task<ReactionEntity> TrackEmoteAsync(this DbContext db, IEmote reaction,
        CancellationToken cancellationToken = default)
    {
        if (reaction is Emote emote)
        {
            var entity = await db.Set<EmoteEntity>()
                .FirstOrDefaultAsync(e => e.EmoteId == emote.Id, cancellationToken);

            return entity ?? db.Add(new EmoteEntity(emote)).Entity;
        }
        else
        {
            var entity = await db.Set<EmojiEntity>()
                .FirstOrDefaultAsync(e => e.Name == reaction.Name, cancellationToken);

            return entity ?? db.Add(new EmojiEntity(reaction)).Entity;
        }
    }

    public static async ValueTask<GuildEntity> TrackGuildAsync(
        this DbSet<GuildEntity> set, IGuild guild,
        CancellationToken cancellationToken = default)
        => await set.FindByIdAsync(guild.Id, cancellationToken) ?? set.Add(new GuildEntity(guild.Id)).Entity;

    public static async ValueTask<GuildUserEntity> TrackUserAsync(this DbSet<GuildUserEntity> set, IGuildUser user,
        CancellationToken cancellationToken = default)
    {
        var userEntity = await set.FindAsync(new object[] { user.Id, user.Guild.Id }, cancellationToken)
            ?? set.Add(new GuildUserEntity(user)).Entity;

        return userEntity;
    }

    public static async ValueTask<GuildUserEntity> TrackUserAsync(
        this DbSet<GuildUserEntity> set, ulong user, ulong guild,
        CancellationToken cancellationToken = default)
    {
        var userEntity = await set.FindAsync(new object[] { user, guild }, cancellationToken)
            ?? set.Add(new GuildUserEntity(user, guild)).Entity;

        return userEntity;
    }

    public static async ValueTask<GuildUserEntity> TrackUserAsync(
        this DbSet<GuildUserEntity> set, ReprimandDetails details,
        CancellationToken cancellationToken = default)
    {
        var user = await details.GetUserAsync();
        if (user is not null) return await set.TrackUserAsync(user, cancellationToken);

        var userEntity =
            await set.FindAsync(new object[] { details.User.Id, details.Guild.Id }, cancellationToken)
            ?? set.Add(new GuildUserEntity(details.User, details.Guild)).Entity;

        return userEntity;
    }

    public static async ValueTask<RoleEntity> TrackRoleAsync(
        this DbSet<RoleEntity> set, IRole role,
        CancellationToken cancellationToken = default)
        => await set.FindByIdAsync(role.Id, cancellationToken) ?? set.Add(new RoleEntity(role)).Entity;

    public static async ValueTask<RoleEntity> TrackRoleAsync(
        this DbSet<RoleEntity> set, ulong guildId, ulong roleId,
        CancellationToken cancellationToken = default)
        => await set.FindByIdAsync(roleId, cancellationToken) ?? set.Add(new RoleEntity(guildId, roleId)).Entity;

    public static ValueTask<T?> FindByIdAsync<T>(this DbSet<T> dbSet, object key,
        CancellationToken cancellationToken = default)
        where T : class => dbSet.FindAsync(new[] { key }, cancellationToken);

    public static void TryRemove<T>(this DbContext context, T? entity)
    {
        if (entity is not null)
            context.Remove(entity);
    }
}