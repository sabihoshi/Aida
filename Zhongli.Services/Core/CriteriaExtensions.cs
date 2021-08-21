using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Commands;
using Zhongli.Data.Models.Criteria;
using Zhongli.Data.Models.Discord;
using GuildPermission = Zhongli.Data.Models.Discord.GuildPermission;

namespace Zhongli.Services.Core
{
    public static class CriteriaExtensions
    {
        public static bool Judge(this Criterion rule, ICommandContext context, IGuildUser user)
            => Judge(rule, (ITextChannel) context.Channel, user);

        public static bool Judge(this Criterion rule, ITextChannel channel, IGuildUser user)
        {
            return rule switch
            {
                UserCriterion auth       => auth.Judge(user),
                RoleCriterion auth       => auth.Judge(user),
                PermissionCriterion auth => auth.Judge(user),
                ChannelCriterion auth    => auth.Judge(channel),
                _                        => false
            };
        }

        public static ICollection<Criterion> AddCriteria(this ICollection<Criterion> collection,
            ICriteriaOptions options)
        {
            var channels = options.Channels?.Where(c => c is ICategoryChannel or ITextChannel);

            var rules = collection
                .AddCriteria(options.Users, u => new UserCriterion(u.Id))
                .AddCriteria(channels, c => new ChannelCriterion(c.Id, c is ICategoryChannel))
                .AddCriteria(options.Roles, r => new RoleCriterion(r));

            if (options.Permission is not GuildPermission.None)
                rules.Add(new PermissionCriterion(options.Permission));

            return rules;
        }

        public static ICollection<Criterion> ToCriteria(this ICriteriaOptions options)
            => new List<Criterion>().AddCriteria(options);

        public static Type GetCriterionType(this Criterion rule)
        {
            return rule switch
            {
                UserCriterion       => typeof(UserCriterion),
                RoleCriterion       => typeof(RoleCriterion),
                PermissionCriterion => typeof(PermissionCriterion),
                ChannelCriterion    => typeof(ChannelCriterion),
                _ => throw new ArgumentOutOfRangeException(nameof(rule), rule,
                    "Unknown kind of Criterion.")
            };
        }

        private static bool Judge(this IUserEntity auth, IGuildUser user)
            => auth.UserId == user.Id;

        private static bool Judge(this IRoleEntity auth, IGuildUser user)
            => user.RoleIds.Contains(auth.RoleId);

        private static bool Judge(this IPermissionEntity auth, IGuildUser user)
            => (auth.Permission & (GuildPermission) user.GuildPermissions.RawValue) != 0;

        private static bool Judge(this IChannelEntity auth, INestedChannel channel)
            => auth.IsCategory
                ? auth.ChannelId == channel.CategoryId
                : auth.ChannelId == channel.Id;

        private static ICollection<Criterion> AddCriteria<T>(this ICollection<Criterion> collection,
            IEnumerable<T>? source, Func<T, Criterion> func)
        {
            if (source is null)
                return collection;

            foreach (var item in source)
            {
                collection.Add(func(item));
            }

            return collection;
        }
    }
}