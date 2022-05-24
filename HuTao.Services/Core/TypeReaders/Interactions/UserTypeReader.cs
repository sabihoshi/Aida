﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;

namespace HuTao.Services.Core.TypeReaders.Interactions;

public class UserTypeReader<T> : TypeReader<T> where T : class, IUser
{
    private readonly CacheMode _cacheMode;

    public UserTypeReader(CacheMode cacheMode = CacheMode.AllowDownload) { _cacheMode = cacheMode; }

    public override async Task<TypeConverterResult> ReadAsync(
        IInteractionContext context, string option, IServiceProvider services)
    {
        var results = new Dictionary<ulong, TypeReaderValue>();

        // By Mention (1.0)
        if (MentionUtils.TryParseUser(option, out var id))
        {
            if (context.Guild is not null)
            {
                var guildUser = await context.Guild.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, guildUser, id);

                AddResult(results, user, 1.00f);
            }
            else
            {
                var channelUser = await context.Channel.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, channelUser, id);

                AddResult(results, user, 1.00f);
            }
        }

        // By Id (0.9)
        if (ulong.TryParse(option, NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            if (context.Guild is not null)
            {
                var guildUser = await context.Guild.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, guildUser, id);

                AddResult(results, user, 0.90f);
            }
            else
            {
                var channelUser = await context.Channel.GetUserAsync(id, _cacheMode).ConfigureAwait(false);
                var user = await GetUserAsync(context.Client, channelUser, id);

                AddResult(results, user, 0.90f);
            }
        }

        if (context.Guild is not null)
        {
            // By Username + Discriminator (0.7-0.85)
            var index = option.LastIndexOf('#');
            if (index >= 0)
            {
                var username = option[..index];
                if (ushort.TryParse(option[(index + 1)..], out var discriminator))
                {
                    var users = await context.Guild
                        .SearchUsersAsync(username, mode: _cacheMode)
                        .ConfigureAwait(false);

                    foreach (var user in users
                        .Where(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase))
                        .Where(u => u.DiscriminatorValue == discriminator))
                    {
                        AddResult(results, user as T, user.Username == username ? 0.85f : 0.80f);
                    }
                }
            }
            else
            {
                var search = await context.Guild
                    .SearchUsersAsync(option, mode: _cacheMode)
                    .ConfigureAwait(false);

                // By Username (0.5-0.6)
                var usernames
                    = search.Where(u => string.Equals(option, u.Username, StringComparison.OrdinalIgnoreCase));
                foreach (var user in usernames)
                {
                    AddResult(results, user as T, user.Username == option ? 0.65f : 0.55f);
                }

                // By Nickname (0.5-0.6)
                var nicknames
                    = search.Where(u => string.Equals(option, u.Nickname, StringComparison.OrdinalIgnoreCase));
                foreach (var user in nicknames)
                {
                    AddResult(results, user as T, user.Nickname == option ? 0.65f : 0.55f);
                }
            }
        }

        return results.Count > 0
            ? TypeConverterResult.FromSuccess(results.Values.MaxBy(r => r.Score).Value)
            : TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, "User not found.");
    }

    private async Task<T?> GetUserAsync(IDiscordClient client, IUser? user, ulong id)
    {
        if (user is T result) return result;
        return await client.GetUserAsync(id, _cacheMode) as T;
    }

    private static void AddResult(IDictionary<ulong, TypeReaderValue> results, T? user, float score)
    {
        if (user is not null && !results.ContainsKey(user.Id))
            results.Add(user.Id, new TypeReaderValue(user, score));
    }
}