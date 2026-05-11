using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowSpiritualConflictAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Духовный конфликт"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовный конфликт", "Afterlife spiritual conflict доступен только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;

        var lines = new List<string>
        {
            "[bold cyan]Afterlife spiritual conflict[/]",
            "",
            "Это отдельная загробная система конфликтов. Она не использует Mortal combat files, HP, energy, enemiesData/alliesData или смертные боевые навыки.",
            "Конфликт начинает GM по роли: по заявке игрока или когда afterlife actor сам инициирует давление.",
            ""
        };

        if (active == null)
        {
            lines.Add("[dim]Активного духовного конфликта нет.[/]");
            lines.Add("");
            lines.Add("GM может начать конфликт только accepted-turn response surface:");
            lines.Add($"  • `{AfterlifeSpiritualConflictState.ResponseField}` with `mode=start`");
            lines.Add($"  • persisted state: `{AfterlifeSpiritualConflictState.StatePath}`");
        }
        else
        {
            var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
            lines.Add($"[bold]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
            lines.Add($"  • Realm: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["realm"]) ?? "?")}[/]");
            lines.Add($"  • Model: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"]) ?? "?")}[/]");
            lines.Add($"  • Position: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"]) ?? "?")}[/]");
            lines.Add($"  • Player side strain: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"]) ?? "?")}[/]");
            lines.Add($"  • Opposition side strain: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"]) ?? "?")}[/]");
            lines.Add($"  • Resolution state: [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["resolutionState"]) ?? "?")}[/]");
            lines.Add("");
            AppendConflictSideSummary(lines, "Player side", active["playerSide"] as JsonObject);
            AppendConflictSideSummary(lines, "Opposition side", active["oppositionSide"] as JsonObject);
            lines.Add("");
            lines.Add($"  • Exchanges recorded: [white]{(active["exchangeLog"] as JsonArray)?.Count ?? 0}[/]");
        }

        lines.Add("");
        lines.Add("[bold]Команды:[/]");
        lines.Add("  • /spiritual_action — отправить действие в активном конфликте GM с явным тегом.");
        lines.Add("  • Обычная художественная заявка во время активного конфликта тоже должна резолвиться GM как действие конфликта.");
        lines.Add("  • /spiritual_arts — посмотреть ранги, art tiers и применимые действия.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚔ Afterlife Spiritual Conflict ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (root != null)
            WriteJsonAuditPanel($"Full JSON {AfterlifeSpiritualConflictState.StatePath}", root, Color.Cyan1);

        WaitForKey();
    }

    private async Task ShowSpiritualArtsAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Духовные искусства"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовные искусства", "Afterlife spiritual arts доступны только в Море Хаоса и Сияющей Обители.");
            return;
        }

        var soulRoot = await ReadJsonObjectForAfterlifeStatusAsync("game_state/meta/soul_state.json");
        var profile = soulRoot?[AfterlifeSpiritualConflictState.SoulStateProfileProperty] as JsonObject
                      ?? AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        var enlightenment = soulRoot?["enlightenment"] as JsonObject;
        var shiningRoot = await ReadJsonObjectForAfterlifeStatusAsync(ShiningAbodeState.StatePath);
        var radiance = shiningRoot?["radiance"] as JsonObject;

        var lines = new List<string>
        {
            "[bold cyan]Afterlife spiritual arts[/]",
            "",
            "[bold]Current profile:[/]",
            $"  • Enlightenment rank: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["enlightenmentRank"])}[/]",
            $"  • Radiance rank: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["radianceRank"])}[/]",
            $"  • Retained Radiance rank: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"])}[/]",
            $"  • Soul enlightenment level: [white]{AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["level"])}[/] [dim]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(enlightenment?["currentTier"]) ?? "")}[/]",
            $"  • Shining radiance: [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["experience"])} XP[/] / tier [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["tier"])}[/]",
            "",
            "[bold]Arts:[/]"
        };

        var artTiers = profile["artTiers"] as JsonObject;
        foreach (var art in AfterlifeSpiritualConflictState.SpiritualArts)
        {
            var tier = AfterlifeSpiritualConflictState.GetNodeInt(artTiers?[art.ArtId]);
            lines.Add($"  • [white]{Markup.Escape(art.DisplayName)}[/] `[dim]{Markup.Escape(art.ArtId)}[/]`: tier [white]{tier}[/], unlock tier [white]{art.MinUnlockTier}[/] — {Markup.Escape(art.MechanicalUse)}");
        }

        lines.Add("");
        lines.Add("[bold]Enlightenment rank ladder:[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.EnlightenmentRanks)
            lines.Add($"  • {rank.Rank}: `{rank.RankId}` requires {rank.RequiredProgress}, unlocks art tier {rank.UnlocksArtTier}. {Markup.Escape(rank.MechanicalEffect)}");

        lines.Add("");
        lines.Add("[bold]Radiance rank ladder:[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.RadianceRanks)
            lines.Add($"  • {rank.Rank}: `{rank.RankId}` requires {rank.RequiredProgress}, unlocks art tier {rank.UnlocksArtTier}. {Markup.Escape(rank.MechanicalEffect)}");

        lines.Add("");
        lines.Add("[dim]V1 balance rule: ranks gate max art tier; currency costs and Treasury conversion remain separate balance tasks. No direct resource farming from conflict.[/]");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ✨ Spiritual Arts ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel("Full JSON afterlifeCombatProfile", profile, Color.Cyan1);
        WaitForKey();
    }

    private async Task ShowSpiritualActionAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Действие в духовном конфликте"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовное действие", "Afterlife spiritual action доступно только в Море Хаоса и Сияющей Обители.");
            return;
        }

        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;
        if (active == null)
        {
            ShowEmptyPanel("Духовное действие", "Нет активного afterlife spiritual conflict. Конфликт должен начать GM через roleplay и accepted-turn update.");
            return;
        }

        var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
        Clear();
        MarkupLine($"[cyan]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
        MarkupLine("[dim]Опишите одно намерение: давление, защита, манёвр, контр, разрыв/наложение binding, сдача, отступление или переговоры. Команда только добавляет явный тег; обычная ролевая заявка во время активного конфликта тоже валидна.[/]");
        var action = Ask("[cyan]Действие:[/]");
        if (string.IsNullOrWhiteSpace(action))
            return;

        var gmAction =
            $"[AFTERLIFE_SPIRITUAL_ACTION: {conflictId}] {action.Trim()}\n\n" +
            "Resolve as an active afterlife spiritual conflict exchange. " +
            $"If the conflict changes, write `{AfterlifeSpiritualConflictState.ResponseField}` with mode=exchange or mode=resolve. " +
            "Do not use Mortal combat files, HP, energy, enemiesData/alliesData, NPC/world/faction Mortal surfaces, or direct currency rewards.";

        var preview = new JsonObject
        {
            ["playerActionTag"] = "AFTERLIFE_SPIRITUAL_ACTION",
            ["conflictId"] = conflictId,
            ["playerAction"] = action.Trim(),
            ["expectedResponseSurface"] = AfterlifeSpiritualConflictState.ResponseField,
            ["stateFile"] = AfterlifeSpiritualConflictState.StatePath
        };

        if (!ConfirmChaosSeaContractPreview(
                "Afterlife spiritual action preview",
                new List<string>
                {
                    "[bold]GM contract:[/]",
                    $"  • Active conflict: {Markup.Escape(conflictId)}",
                    $"  • Response surface: `{AfterlifeSpiritualConflictState.ResponseField}`",
                    $"  • State file: `{AfterlifeSpiritualConflictState.StatePath}`",
                    "  • Conflict remains side-vs-side; use playerSide/oppositionSide and side strain fields.",
                    "  • Forced incarnation by Guardian requires resolved conflict loss/surrender/concession proof.",
                    "  • Mortal combat/state files are forbidden."
                },
                preview,
                "Spiritual action audit",
                confirmChoice: "✅ Отправить действие GM"))
        {
            return;
        }

        _pendingGmAction = gmAction;
    }

    private static void AppendConflictSideSummary(List<string> lines, string label, JsonObject? side)
    {
        if (side == null)
        {
            lines.Add($"  • {label}: [red]missing[/]");
            return;
        }

        var lead = side["leadContestant"] as JsonObject;
        var displayName = AfterlifeSpiritualConflictState.GetNodeString(lead?["displayName"]) ??
                          AfterlifeSpiritualConflictState.GetNodeString(lead?["actorId"]) ??
                          "unknown";
        var actorType = AfterlifeSpiritualConflictState.GetNodeString(lead?["actorType"]) ?? "?";
        var supporters = (side["supporters"] as JsonArray)?.Count ?? 0;
        lines.Add($"  • {label}: [white]{Markup.Escape(displayName)}[/] [dim]({Markup.Escape(actorType)} lead, supporters={supporters})[/]");
    }
}
