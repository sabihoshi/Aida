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
    }
}