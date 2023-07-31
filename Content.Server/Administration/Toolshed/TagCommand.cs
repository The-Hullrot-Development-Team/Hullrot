﻿using System.Linq;
using Content.Shared.Administration;
using Content.Shared.Tag;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;

namespace Content.Server.Administration.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class TagCommand : ToolshedCommand
{
    private TagSystem? _tag;

    [CommandImplementation("list")]
    public IEnumerable<string> List([PipedArgument] IEnumerable<EntityUid> ent)
    {
        return ent.SelectMany(x =>
        {
            if (TryComp<TagComponent>(x, out var tags))
                // Note: Cast is required for C# to figure out the type signature.
                return (IEnumerable<string>)tags.Tags;
            return Array.Empty<string>();
        });
    }

    [CommandImplementation("add")]
    public EntityUid Add(
            [CommandInvocationContext] IInvocationContext ctx,
            [PipedArgument] EntityUid input,
            [CommandArgument] ValueRef<string> @ref
        )
    {
        _tag ??= GetSys<TagSystem>();
        _tag.AddTag(input, @ref.Evaluate(ctx)!);
        return input;
    }

    [CommandImplementation("add")]
    public IEnumerable<EntityUid> Add(
            [CommandInvocationContext] IInvocationContext ctx,
            [PipedArgument] IEnumerable<EntityUid> input,
            [CommandArgument] ValueRef<string> @ref
        )
        => input.Select(x => Add(ctx, x, @ref));

    [CommandImplementation("rm")]
    public EntityUid Rm(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] ValueRef<string> @ref
    )
    {
        _tag ??= GetSys<TagSystem>();
        _tag.RemoveTag(input, @ref.Evaluate(ctx)!);
        return input;
    }

    [CommandImplementation("rm")]
    public IEnumerable<EntityUid> Rm(
            [CommandInvocationContext] IInvocationContext ctx,
            [PipedArgument] IEnumerable<EntityUid> input,
            [CommandArgument] ValueRef<string> @ref
        )
        => input.Select(x => Rm(ctx, x, @ref));

    [CommandImplementation("addmany")]
    public EntityUid AddMany(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] ValueRef<IEnumerable<string>> @ref
    )
    {
        _tag ??= GetSys<TagSystem>();
        _tag.AddTags(input, @ref.Evaluate(ctx)!);
        return input;
    }

    [CommandImplementation("addmany")]
    public IEnumerable<EntityUid> AddMany(
            [CommandInvocationContext] IInvocationContext ctx,
            [PipedArgument] IEnumerable<EntityUid> input,
            [CommandArgument] ValueRef<IEnumerable<string>> @ref
        )
        => input.Select(x => AddMany(ctx, x, @ref));

    [CommandImplementation("rmmany")]
    public EntityUid RmMany(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] ValueRef<IEnumerable<string>> @ref
    )
    {
        _tag ??= GetSys<TagSystem>();
        _tag.RemoveTags(input, @ref.Evaluate(ctx)!);
        return input;
    }

    [CommandImplementation("rmmany")]
    public IEnumerable<EntityUid> RmMany(
            [CommandInvocationContext] IInvocationContext ctx,
            [PipedArgument] IEnumerable<EntityUid> input,
            [CommandArgument] ValueRef<IEnumerable<string>> @ref
        )
        => input.Select(x => RmMany(ctx, x, @ref));
}
