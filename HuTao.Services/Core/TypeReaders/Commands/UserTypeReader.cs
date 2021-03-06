using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using HuTao.Data;
using HuTao.Services.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace HuTao.Services.Core.TypeReaders.Commands;

/// <summary>
///     A <see cref="TypeReader" /> for parsing objects implementing <see cref="IUser" />.
/// </summary>
/// <typeparam name="T">The type to be checked; must implement <see cref="IUser" />.</typeparam>
public class UserTypeReader<T> : TypeReader where T : class, IUser
{
    private readonly CacheMode _cacheMode;
    private HuTaoContext _db = null!;

    public UserTypeReader(CacheMode cacheMode = CacheMode.AllowDownload) { _cacheMode = cacheMode; }

    public override async Task<TypeReaderResult> ReadAsync(
        ICommandContext context, string input, IServiceProvider services)
    {
        _db = services.GetRequiredService<HuTaoContext>();
        var results = new Dictionary<ulong, TypeReaderValue>();

        // By Mention (1.0)
        if (MentionUtils.TryParseUser(input, out var id))
        {
            if (context.Guild is not null)
            {
                var guildUser = await context.Guild.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, guildUser, id);

                await AddResultAsync(user, 1.00f);
            }
            else
            {
                var channelUser = await context.Channel.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, channelUser, id);

                await AddResultAsync(user, 1.00f);
            }
        }

        // By Id (0.9)
        if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            if (context.Guild is not null)
            {
                var guildUser = await context.Guild.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, guildUser, id);

                await AddResultAsync(user, 0.90f);
            }
            else
            {
                var channelUser = await context.Channel.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, channelUser, id);

                await AddResultAsync(user, 0.90f);
            }

            var clientUser = await context.Client.GetUserAsync(id);
            await AddResultAsync(clientUser as T, 0.90f);
        }

        if (context.Guild is not null)
        {
            // By Username + Discriminator (0.7-0.85)
            var index = input.LastIndexOf('#');
            if (index >= 0)
            {
                var username = input[..index];
                if (ushort.TryParse(input[(index + 1)..], out var discriminator))
                {
                    var users = await context.Guild
                        .SearchUsersAsync(username, mode: _cacheMode)
                        .ConfigureAwait(false);

                    foreach (var user in users
                        .Where(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase))
                        .Where(u => u.DiscriminatorValue == discriminator))
                    {
                        await AddResultAsync(user as T, user.Username == username ? 0.85f : 0.80f);
                    }

                    var clientUser = await context.Client.GetUserAsync(username, $"{discriminator}");
                    await AddResultAsync(clientUser as T, 0.85f);
                }
            }
            else
            {
                var search = await context.Guild
                    .SearchUsersAsync(input, mode: _cacheMode)
                    .ConfigureAwait(false);

                // By Username (0.5-0.6)
                var usernames = search.Where(u => string.Equals(input, u.Username, StringComparison.OrdinalIgnoreCase));
                foreach (var user in usernames)
                {
                    await AddResultAsync(user as T, user.Username == input ? 0.65f : 0.55f);
                }

                // By Nickname (0.5-0.6)
                var nicknames = search.Where(u => string.Equals(input, u.Nickname, StringComparison.OrdinalIgnoreCase));
                foreach (var user in nicknames)
                {
                    await AddResultAsync(user as T, user.Nickname == input ? 0.65f : 0.55f);
                }
            }
        }
        else
        {
            // By Username + Discriminator (0.85)
            var index = input.LastIndexOf('#');
            if (index >= 0)
            {
                var username = input[..index];
                if (ushort.TryParse(input[(index + 1)..], out var discriminator))
                {
                    var clientUser = await context.Client.GetUserAsync(username, $"{discriminator}");
                    await AddResultAsync(clientUser as T, 0.85f);
                }
            }
        }

        return results.Count > 0
            ? TypeReaderResult.FromSuccess(results.Values.ToImmutableArray())
            : TypeReaderResult.FromError(CommandError.ObjectNotFound, "User not found.");

        async Task AddResultAsync(T? user, float score)
        {
            if (user is not null && !results.ContainsKey(user.Id))
            {
                results.Add(user.Id, new TypeReaderValue(user, score));
                if (user is IGuildUser guild) await _db.Users.TrackUserAsync(guild);
            }
        }
    }

    private async Task<T?> GetUserAsync(IDiscordClient client, IUser? user, ulong id)
    {
        if (user is T result) return result;
        return await client.GetUserAsync(id, _cacheMode) as T;
    }
}