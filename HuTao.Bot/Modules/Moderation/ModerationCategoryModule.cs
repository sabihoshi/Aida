﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Humanizer;
using HuTao.Data;
using HuTao.Data.Models.Criteria;
using HuTao.Data.Models.Moderation.Infractions.Reprimands;
using HuTao.Services.CommandHelp;
using HuTao.Services.Core.Listeners;
using HuTao.Services.Interactive;
using HuTao.Services.Utilities;
using GuildPermission = HuTao.Data.Models.Discord.GuildPermission;

namespace HuTao.Bot.Modules.Moderation;

[Group("category")]
public class ModerationCategoryModule : InteractiveEntity<ModerationCategory>
{
    private readonly HuTaoContext _db;

    public ModerationCategoryModule(CommandErrorHandler error, HuTaoContext db) : base(error, db) { _db = db; }

    [Command("add")]
    [Summary("Add a new moderation category.")]
    public async Task AddModerationCategoryAsync(string name, ModerationCategoryOptions? options = null)
    {
        if (name.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyAsync("You cannot add a moderation category with the name `all`.");
            return;
        }

        var category = new ModerationCategory(name, options, (IGuildUser) Context.User);
        var collection = await GetCollectionAsync();

        if (collection.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            await ReplyAsync($"A category with the name `{name}` already exists.");
            return;
        }

        collection.Add(category);
        await _db.SaveChangesAsync();
        await ReplyAsync(embed: EntityViewer(category).WithColor(Color.Green).Build());
    }

    [Command("remove")]
    [Alias("delete", "del")]
    [Summary("Remove a moderation category.")]
    protected override Task RemoveEntityAsync(string id) => base.RemoveEntityAsync(id);

    [Command]
    [Alias("list", "view")]
    [Summary("View the moderation category list.")]
    protected override Task ViewEntityAsync() => base.ViewEntityAsync();

    protected override bool IsMatch(ModerationCategory entity, string id)
        => entity.Id.ToString().StartsWith(id, StringComparison.OrdinalIgnoreCase);

    protected override EmbedBuilder EntityViewer(ModerationCategory entity)
    {
        var authorization = entity.Authorization.Humanize().DefaultIfNullOrEmpty("None");
        return new EmbedBuilder()
            .WithTitle($"Category: {entity.Id}")
            .AddField("Name", entity.Name, true)
            .AddField("Authorization", authorization, true);
    }

    protected override async Task<ICollection<ModerationCategory>> GetCollectionAsync()
    {
        var guild = await _db.Guilds.TrackGuildAsync(Context.Guild);
        return guild.ModerationCategories;
    }

    [NamedArgumentType]
    public class ModerationCategoryOptions : ICriteriaOptions
    {
        [HelpSummary("The permissions that the user must have.")]
        public GuildPermission Permission { get; set; }

        [HelpSummary("The text or category channels this permission will work on.")]
        public IEnumerable<IGuildChannel>? Channels { get; set; }

        [HelpSummary("The users that are allowed to use the command.")]
        public IEnumerable<IGuildUser>? Users { get; set; }

        [HelpSummary("The roles that the user must have.")]
        public IEnumerable<IRole>? Roles { get; set; }
    }
}