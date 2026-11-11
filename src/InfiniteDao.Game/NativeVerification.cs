using System.Text.Json;
using Godot;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Game;

// Opt-in desktop regression harness. Uses isolated saves; never touches a player's campaign.
public partial class GameClient
{
    private async void BeginNativeVerification()
    {
        var evidence = Path.Combine(root, "artifacts", "verification", "native");
        Directory.CreateDirectory(evidence);
        var checks = new List<string>();
        try
        {
            await Frames(8);
            Check(DisplayServer.GetName() != "headless", "A real desktop renderer is required.");
            var window = DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
            Check(window != 0, "Native window handle must exist.");
            Screenshot("title");
            var modeControl = FindControl<OptionButton>(n => n.HasMeta("action") && n.GetMeta("action").AsString() == "world-mode");
            modeControl.Select(1); modeControl.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
            hasSave = false; await Press("select-qingyun"); await Press("new-game");
            Check(world is { Mode: "rules", PendingEvent.Type: "inauguration" }, "New campaign must open its council."); Screenshot("council");
            Check(world!.Sects.Count == 10 && world.People.Select(p => p.Portrait).Distinct().Count() == WorldFactory.PortraitCount, "Ten sects must start with the complete balanced character pool.");
            Check(portraits.Length == 50 && portraits.All(p => p is not null && p.GetWidth() == 512 && p.GetHeight() == 768), "All fifty standalone character assets must be imported at production dimensions.");
            checks.Add("ten sects, fifty imported character textures, and independent persistent portrait assignments");
            foreach (var template in PersonalRelics.Templates)
            {
                var path = $"res://Assets/relic-{template.Id}.png";
                Check(ResourceLoader.Exists(path), "Missing personal relic texture: " + template.Id);
                var texture = GD.Load<Texture2D>(path);
                Check(texture is not null && texture.GetWidth() == 512 && texture.GetHeight() == 512, "Invalid personal relic texture: " + template.Id);
            }
            checks.Add("all thirty personal relic template textures load at production dimensions");
            CheckHudLayout();
            var openingWindowSize = GetWindow().Size; GetWindow().Size = new Vector2I(1100, 700); await Frames(6);
            CheckHudLayout(); Screenshot("council-small"); GetWindow().Size = openingWindowSize; await Frames(6);
            await SelectNativeOption("time-speed", 2);
            Check(secondsPerMonth == .05, "The speed popup must apply the selected speed during a pending council.");
            await SelectNativeOption("time-speed", 1);
            await Press("nav-people");
            await SelectNativeOption("people-sect-filter", 2);
            Check(selectedPersonSect == world!.Sects[1].Id, "The sect filter popup must switch the displayed sect.");
            await SelectNativeOption("people-sect-filter", 1);
            await Press("nav-council");
            checks.Add("real mouse-opened speed and sect-filter popups, native keyboard selection and focus-loss safety");
            await Press("choose-expansion");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true }); await Frames(2);
            Check(!paused, "Space must resume time.");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true }); await Frames(2);
            Check(paused, "Space must pause time.");
            var month = world!.Month; await Frames(15); Check(world.Month == month, "Paused world must not tick."); checks.Add("new game, council choices, keyboard pause and inspect");
            await Press("nav-map");
            await ClickRegion(0); Check(selectedRegion == "r01", "Map input must select the clicked region.");
            await ClickRegion(world.Regions.Single(r => r.Id == world.Player.Capital).Index);
            Check(selectedRegion == world.Player.Capital, "The selected own capital must expose its current leader and regional vignette."); Screenshot("world");
            var zoomMap = FindControl<WorldMap>(_ => true); var zoomPoint = zoomMap.GetGlobalTransform() * (zoomMap.Size / 2);
            Input.ParseInputEvent(new InputEventMouseButton { Position = zoomPoint, GlobalPosition = zoomPoint, ButtonIndex = MouseButton.WheelUp, Pressed = true }); await Frames();
            // Complete the synthetic gesture so the map does not retain the viewport's mouse focus.
            Input.ParseInputEvent(new InputEventMouseButton { Position = zoomPoint, GlobalPosition = zoomPoint, ButtonIndex = MouseButton.WheelUp, Pressed = false }); await Frames();
            Check(mapZoom > 1, "Mouse wheel must zoom the native map."); var toolbarSelection = selectedRegion;
            await ClickNativeButton("reset-map"); Check(selectedRegion == toolbarSelection, "Map toolbar clicks must not select an underlying region.");
            Check(mapZoom == 1, "Map reset must restore the whole world.");
            await ClickNativeButton("zoom-in"); Check(mapZoom > 1, "A real mouse click on zoom-in must change the map scale.");
            var zoomedScale = mapZoom; await ClickNativeButton("zoom-out"); Check(mapZoom < zoomedScale, "A real mouse click on zoom-out must reduce the map scale.");
            await ClickNativeButton("zoom-in"); await ClickNativeButton("reset-map");
            Check(mapZoom == 1 && mapPan == Vector2.Zero, "Reset map must restore its viewport."); Screenshot("world");
            await Press("region-details"); Check(overlay is not null, "Region dossier must open from the inspector.");
            var capital = world.Regions.Single(r => r.Id == world.Player.Capital);
            Check(overlay!.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == capital.Description), "Region dossier must retain the full regional description.");
            Screenshot("region-detail"); await Press("region-" + capital.Neighbors[0]);
            Check(overlay is null && selectedRegion == capital.Neighbors[0], "Adjacent-region navigation must close the dossier and select the real neighbor.");
            await ClickRegion(capital.Index);
            var mapLeader = world.People.Single(p => p.Id == world.Player.LeaderId);
            await Press("map-leader-" + mapLeader.Id);
            Check(overlay is not null && overlay.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Any(picture => picture.Texture == portraits[mapLeader.Portrait]), "Map leader biography must display the current person's assigned portrait.");
            await Press("close-dialog");
            var recent = world.History.Last(e => e.Sects.Contains(world.PlayerId));
            await Press("chronicle-" + recent.Id); Check(overlay is not null, "Chronicle ticker must open the real public event."); await Press("close-dialog");
            var originalSize = GetWindow().Size; GetWindow().Size = new Vector2I(1100, 700); await Frames(6); Screenshot("world-small");
            CheckHudLayout();
            GetWindow().Size = originalSize; await Frames(6); checks.Add("map dock controls, region dossier, public chronicle ticker and minimum-window HUD");
            foreach (var destination in new[] { "sect", "people", "diplomacy", "treasury", "history" })
            { await Press("nav-" + destination); CheckHudLayout(); Screenshot(destination); }
            await Press("nav-people"); await Press("person-" + world.Player.LeaderId); Screenshot("portrait-detail"); await Press("close-dialog");
            await Press("settings"); Check(overlay is not null, "Native settings must open."); await Press("close-dialog"); checks.Add("native map input and all six inspection panels");
            var saved = world.Copy(); await Press("saves"); await Press("save-one");
            await Press("advance"); Check(world!.Month == saved.Month + 12, "Advance button must settle one year, month by month.");
            await Press("saves"); await Press("load-one");
            Check(world!.Month == saved.Month && world.RandomState == saved.RandomState && world.Player.Stones == saved.Player.Stones, "Manual load must restore the same world."); checks.Add("manual save/load and deterministic continuation");

            var largeImport = saved.Copy();
            GameEngine.Log(largeImport, "fixture", "长档导入验证", new string('x', 8_000_100), [largeImport.PlayerId]);
            var largePath = Path.Combine(evidence, "large-import-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await File.WriteAllTextAsync(largePath, JsonSerializer.Serialize(largeImport));
                Check(new FileInfo(largePath).Length > 8_000_000, "Large import fixture must exceed the former byte limit.");
                var imported = await ReadSaveImport(session.Saves, largePath);
                Check(imported.History[^1].Text.Length == 8_000_100, "Native import pipeline must accept valid long saves.");
                checks.Add("native import pipeline accepts a valid save larger than 8 MB");
            }
            finally { File.Delete(largePath); }

            var disciple = GameEngine.Members(world, world.Player).First(p => p.Realm == 0);
            var guardian = GameEngine.Members(world, world.Player).First(p => p.Realm > disciple.Realm);
            await Press("nav-people"); await Press("focus-" + disciple.Id);
            Check(world!.People.Single(p => p.Id == disciple.Id).Focused, "Focus must persist without a routine order.");
            await Press("guardian-" + disciple.Id); await Press("assign-guardian-" + guardian.Id);
            Check(world!.People.Single(p => p.Id == disciple.Id).GuardianId == guardian.Id, "Guardian selection must persist.");
            Screenshot("people"); checks.Add("native disciple focus and higher-realm guardian assignment");
            var beforeRelicView = world.Copy();
            var relicFixture = world.Copy();
            var relic = new Relic { Id = relicFixture.NewId(), Scope = "personal", TemplateId = "attack_1_1", Name = "听雨剑", Owner = relicFixture.PlayerId, RegionId = relicFixture.Player.Capital, FoundMonth = relicFixture.Month };
            relicFixture.Relics.Add(relic);
            var relicOrigin = GameEngine.Log(relicFixture, "relic_discovery", "验证法宝", "验证物品来源", [relicFixture.PlayerId]);
            relicFixture.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, ToOwner = relicFixture.PlayerId, RegionId = relic.RegionId };
            relic.OriginEvent = relic.LastEventId = relicOrigin;
            for (var i = 0; i < 4; i++) GameEngine.Log(relicFixture, "journey", "近事验证", disciple.Name + "的近事", [relicFixture.PlayerId]);
            await SetVerificationWorld(relicFixture);
            var relicWindowSize = GetWindow().Size; GetWindow().Size = new Vector2I(1100, 700); await Frames(6);
            await Press("person-" + disciple.Id);
            var profileScroll = FindControl<ScrollContainer>(c => c.Name == "person-profile-scroll" && c.IsVisibleInTree());
            profileScroll.ScrollVertical = (int)profileScroll.GetVScrollBar().MaxValue; await Frames();
            var grant = FindControl<Button>(c => c.HasMeta("action") && c.GetMeta("action").AsString() == "relic-give-" + disciple.Id && c.IsVisibleInTree());
            Check(grant.GetGlobalRect().End.Y <= GetViewportRect().Size.Y, "Small-window relic controls must be reachable after scrolling.");
            Screenshot("personal-relic-grant-scroll");
            await ClickNativeButton("relic-give-" + disciple.Id); await Press("relic-choice-" + relic.Id);
            Check(GameEngine.HeldRelic(world!, disciple.Id)?.KeepAssigned == true, "Native grant must assign and retain the selected relic.");
            await Press("person-" + disciple.Id);
            Check(overlay!.FindChildren("personal-relic-icon-" + relic.Id, "TextureRect", true, false).Count > 0, "Native detail must load the actual relic PNG.");
            Screenshot("personal-relic-small");
            await Press("person-relic-latest-" + relic.Id);
            var flowText = string.Join('\n', overlay!.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text));
            Check(flowText.Contains("当时流转") && flowText.Contains("从：") && flowText.Contains("至：") && flowText.Contains(disciple.Name), "Chronicle must show the real holder and historical transfer direction.");
            Screenshot("personal-relic-chronicle"); await Press("close-dialog"); await Press("person-" + disciple.Id);
            profileScroll = FindControl<ScrollContainer>(c => c.Name == "person-profile-scroll" && c.IsVisibleInTree());
            profileScroll.ScrollVertical = (int)profileScroll.GetVScrollBar().MaxValue; await Frames();
            await ClickNativeButton("relic-auto-" + disciple.Id);
            Check(GameEngine.HeldRelic(world!, disciple.Id)?.KeepAssigned == false, "Native restore must return relic management to the steward.");
            var deceasedFixture = world.Copy(); var deceased = deceasedFixture.People.Single(p => p.Id == disciple.Id);
            var currentRelic = GameEngine.HeldRelic(deceasedFixture, deceased.Id)!;
            void MoveFixture(Relic item, int? from, int? to, int cause = 0)
            {
                var prior = item.LastEventId;
                var id = GameEngine.Log(deceasedFixture, "relic", "验证流转", deceased.Name + "的法宝流转", [deceasedFixture.PlayerId], causes: cause == 0 ? [prior] : [prior, cause]);
                deceasedFixture.History[^1].RelicChange = new RelicChange { RelicId = item.Id, FromHolderId = from, ToHolderId = to, FromOwner = deceasedFixture.PlayerId, ToOwner = deceasedFixture.PlayerId, RegionId = deceasedFixture.Player.Capital };
                item.HolderId = to; item.LastEventId = id;
            }
            MoveFixture(currentRelic, deceased.Id, null);
            foreach (var templateId in new[] { "attack_1_2", "protection_1_1" })
            {
                var template = PersonalRelics.Find(templateId)!;
                var old = new Relic { Id = deceasedFixture.NewId(), Scope = "personal", TemplateId = templateId, Name = template.Name, Owner = deceasedFixture.PlayerId, HolderId = deceased.Id, RegionId = deceasedFixture.Player.Capital, FoundMonth = deceasedFixture.Month };
                deceasedFixture.Relics.Add(old);
                var origin = GameEngine.Log(deceasedFixture, "relic_discovery", "验证前物", deceased.Name + "曾得法宝", [deceasedFixture.PlayerId]);
                deceasedFixture.History[^1].RelicChange = new RelicChange { RelicId = old.Id, ToOwner = deceasedFixture.PlayerId, ToHolderId = deceased.Id, RegionId = old.RegionId };
                old.OriginEvent = old.LastEventId = origin; MoveFixture(old, deceased.Id, null);
            }
            MoveFixture(currentRelic, null, deceased.Id);
            var death = GameEngine.Log(deceasedFixture, "death", "验证寿尽", deceased.Name + "寿尽", [deceasedFixture.PlayerId]);
            deceased.Alive = false; MoveFixture(currentRelic, deceased.Id, null, death);
            MoveFixture(currentRelic, null, guardian.Id);
            GameEngine.Validate(deceasedFixture);
            await SetVerificationWorld(deceasedFixture); ShowPerson(world!.People.Single(p => p.Id == deceased.Id)); await Frames();
            profileScroll = FindControl<ScrollContainer>(c => c.Name == "person-profile-scroll" && c.IsVisibleInTree());
            profileScroll.ScrollVertical = (int)profileScroll.GetVScrollBar().MaxValue; await Frames();
            var deceasedText = string.Join('\n', overlay!.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text));
            Check(deceasedText.Split("曾持：").Length - 1 >= 3 && deceasedText.Contains("故去时：") && deceasedText.Contains("如今："), "Deceased profile must preserve multiple relic histories and both time points.");
            Check(deceasedText.Contains($"曾持：{currentRelic.Name}\n故去时：归入{deceasedFixture.Player.Name}库存；如今：{guardian.Name}持有"), "Deceased profile must distinguish the actual death transfer from the later holder.");
            Check(deceasedText.Contains("故去时：未持此物"), "Previously held relics must not be attributed to the death transfer.");
            var close = FindControl<Button>(c => c.HasMeta("action") && c.GetMeta("action").AsString() == "close-dialog" && c.IsVisibleInTree());
            Check(close.GetGlobalRect().End.Y <= GetViewportRect().Size.Y, "Small-window close must remain visible for a long deceased profile.");
            Screenshot("deceased-relic-small"); await ClickNativeButton("close-dialog");
            var historicalBattle = new BattleReport { Exchanges = [new DuelExchange { AttackerName = deceased.Name, DefenderName = guardian.Name, RelicName = "战时旧名", RelicCategory = "attack", RelicTier = 0, RelicBonus = .08 }] };
            ShowBattleReport(historicalBattle); await Frames();
            var battleText = string.Join('\n', overlay!.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text));
            Check(battleText.Contains("当场御使战时旧名") && !battleText.Contains("当场御使" + currentRelic.Name), "Battle view must display its historical snapshot, not the current relic name.");
            await Press("close-dialog");
            GetWindow().Size = relicWindowSize; await Frames(6);
            await SetVerificationWorld(beforeRelicView);
            checks.Add("constructed relic fixture: native grant, real template PNG, and restore stewardship");
            var beforeMemoryView = world.Copy();
            var memoryFixture = world.Copy();
            var rival = memoryFixture.Sects.First(s => s.Id != memoryFixture.PlayerId);
            var currentPlan = new SectAgentPlan { Policy = "cultivation", Goal = "稳固根基，培养门人", Reason = "先壮大道统，再图山河。", Stages = ["积蓄供养"], ReconsiderWhen = "边境遭到进犯" };
            var previousPlan = new SectAgentPlan { Policy = "prosperity", Goal = "先养精蓄锐，备足供养", Reason = "当前尚需积累。", Stages = ["积累储备"], ReconsiderWhen = "军需充足" };
            const string rawObservation = "{\"type\":\"current_observation\",\"raw_packet_sentinel\":true}";
            memoryFixture.AgentMemories.RemoveAll(m => m.SectId == rival.Id);
            memoryFixture.AgentMemories.Add(new SectAgentMemory {
                SectId = rival.Id, Plan = currentPlan, LastReviewMonth = memoryFixture.Month, NextReviewMonth = memoryFixture.Month + 60, Summary = "宗门先求稳固，再待发展之机。",
                Archive = [new AgentConversationEntry { Month = memoryFixture.Month, Role = "assistant", Text = "submit_strategy 公开提交：" + JsonSerializer.Serialize(previousPlan, new JsonSerializerOptions(JsonSerializerDefaults.Web)) }],
                Conversation = [new AgentConversationEntry { Month = memoryFixture.Month, Text = rawObservation },
                    new AgentConversationEntry { Month = memoryFixture.Month, Role = "assistant", Text = "submit_strategy 公开提交：" + JsonSerializer.Serialize(currentPlan, new JsonSerializerOptions(JsonSerializerDefaults.Web)) },
                    new AgentConversationEntry { Month = memoryFixture.Month, Role = "assistant", Text = "submit_strategy 公开提交：{invalid_packet_sentinel" },
                    new AgentConversationEntry { Month = memoryFixture.Month, Role = "assistant", Text = "submit_strategy 公开提交：null" },
                    new AgentConversationEntry { Month = memoryFixture.Month, Role = "assistant", Text = "submit_strategy 公开提交：{invalid_packet_sentinel" }]
            });
            await SetVerificationWorld(memoryFixture); await Press("nav-diplomacy"); await Press("agent-memory-" + rival.Id);
            var memoryLabels = string.Join('\n', overlay!.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text));
            Check(memoryLabels.Contains(currentPlan.Goal) && memoryLabels.Contains(previousPlan.Goal) && memoryLabels.Contains(NativeTheme.Policy(currentPlan.Policy)), "Agent memory must show readable current and archived plans.");
            Check(!memoryLabels.Contains("raw_packet_sentinel") && !memoryLabels.Contains("submit_strategy") && !memoryLabels.Contains("invalid_packet_sentinel"), "Agent memory must not echo observation or malformed tool packets.");
            Check(world!.AgentMemories.Single(m => m.SectId == rival.Id).Conversation[0].Text == rawObservation, "Readable presentation must preserve original Agent context.");
            Screenshot("agent-memory"); await Press("close-dialog"); await SetVerificationWorld(beforeMemoryView);
            checks.Add("readable current and archived Agent plans; raw observations and malformed JSON hidden without changing saved context");
            var actions = 0; var advances = 0;
            for (var step = 0; step < 30 && world!.Month < 60; step++)
            {
                if (world.PendingEvent is { } pending)
                {
                    var choice = pending.Choices.First(c => c.Stones <= world.Player.Stones && c.Herbs <= world.Player.Herbs);
                    await PlayCommand("choose", option: choice.Id); actions++; continue;
                }
                await Press("advance"); advances++;
            }
            Check(world!.Month >= 60 && world.Sects.Count(s => s.Eliminated) == 0, "Standard 192-region world must not collapse into an eleven-month unification.");
            Check(world.History.Any(e => e.Kind == "journey") && world.Regions.Count == 192, "Automatic cultivation-world journeys must run on the full map.");
            var opening = new { world.Month, actions, advances, regions = world.Regions.Count, journeys = world.History.Count(e => e.Kind == "journey") };
            checks.Add("unmodified seed8192: five years through native yearly advance, automatic stewardship and journeys; no manual routine orders");

            // Explicit boundary fixture verifies a final campaign and ending, not standard-world balance.
            var finale = WorldFactory.Create("qingyun", 70, "rules", "normal", compact: true);
            finale.PendingEvent = null; finale.AutoStewardship = true; finale.NextEventMonth = int.MaxValue;
            foreach (var sect in finale.Sects.Where(s => s.Id != "qingyun" && s.Id != "chixia"))
            { sect.Eliminated = true; sect.Capital = ""; foreach (var p in GameEngine.Members(finale, sect)) p.SectId = ""; }
            foreach (var region in finale.Regions) region.Owner = "qingyun";
            var finalGate = finale.Regions.Single(r => r.Id == "r07"); finalGate.Owner = "chixia"; finalGate.Fort = 1;
            finale.Sects.Single(s => s.Id == "chixia").Capital = finalGate.Id;
            foreach (var p in GameEngine.Members(finale, finale.Player)) p.Realm = 5;
            finale.Player.Policy = "expansion"; finale.Player.Stones = 1_000_000; finale.Player.Herbs = 1_000_000;
            await SetVerificationWorld(finale);
            for (var year = 0; year < 15 && world!.Status == "playing"; year++) await Press("advance");
            Check(world!.Status == "victory" && world.Sects.Count(s => s.Eliminated) == world.Sects.Count - 1 && world.BattleReports.Count > 0, "Final-gate fixture must reach victory through real campaign resolution.");
            var victory = world.Copy(); page = "ending"; Render(); await Frames(3); Screenshot("victory");
            var campaign = new { standardOpening = opening, boundaryFixture = true, finaleMonths = victory.Month, extinguished = victory.Sects.Count(s => s.Eliminated) };
            checks.Add("explicit final-gate fixture: years-long campaign, individual combat, extinction, victory screen and native save/load");
            await Press("return-title"); Check(showTitle, "Return must open the title without discarding the campaign.");
            await Press("settings"); await Press("close-dialog"); Check(showTitle, "Opening title settings must not resume an old campaign.");
            await Press("saves"); await Press("load-auto"); Check(!showTitle && world!.Status == "victory", "Title save menu must restore the native campaign.");

            // Separate loss fixture: prepares a last gate, then uses real council/rebuild controls.
            var exile = WorldFactory.Create("qingyun", 23, "rules", "normal");
            foreach (var region in exile.Regions.Where(r => r.Owner == "" || r.Owner == exile.PlayerId)) region.Owner = "chixia";
            var lastGate = exile.Regions.Single(r => r.Id == "r07"); lastGate.Owner = exile.PlayerId; exile.Player.Capital = lastGate.Id; exile.Player.Stones = 0; exile.Player.Herbs = 0;
            exile.PendingEvent = new GameEvent { Id = exile.NewId(), Type = "defense", Title = "最后山门之战", Actor = "chixia", Target = lastGate.Id, Text = "强敌已至。请决定是否携道统撤离。", Choices = [new EventChoice { Id = "retreat", Label = "保存道统，撤出山门", Description = "幸存门徒携传承出逃。" }] };
            await SetVerificationWorld(exile); await Press("choose-retreat");
            Check(world!.Player.Exiled && GameEngine.Members(world, world.Player).Count > 0, "Last-gate retreat must preserve disciples."); Screenshot("exile");
            var survivors = GameEngine.Members(world, world.Player).Select(p => p.Id).ToArray(); var originalSeed = world.Seed;
            await Press("choose-wander");
            var settlement = world!.Regions.First(r => r.Id != world.Sects.Single(s => s.Id == r.Owner).Capital);
            await PlayCommand("settle", settlement.Id);
            Check(!world!.Player.Exiled && world.Seed == originalSeed && world.Regions.Single(r => r.Id == settlement.Id).Owner == world.PlayerId && survivors.All(id => world.People.Any(p => p.Id == id && p.Alive)), "Rebuild must retain the same world and survivors.");
            Screenshot("rebuilt"); checks.Add("constructed last-gate fixture: native retreat, ember choice, occupied-land purchase and same-world rebuild");

            object? liveModel = null;
            if (OS.GetCmdlineUserArgs().Contains("--verify-model"))
            {
                CloseOverlay(); Run(() => session.NewGame("qingyun", 8192, "llm", "normal", lifetime.Token), "真实模型开宗议事验证");
                await WaitForNativeAction("live-council");
                var deadline = Time.GetTicksMsec() + 600_000;
                while ((world!.PendingEvent?.NarrativeSource != "llm" || world.AgentMemories.Count != world.Sects.Count - 1 || session.HasBackgroundWork) && Time.GetTicksMsec() < deadline)
                {
                    Check(world.Mode == "llm", "Real provider verification switched offline: " + world.AgentStatus);
                    await Frames(1);
                }
                Check(world!.PendingEvent?.NarrativeSource == "llm", "Live council must be narrated by the model.");
                await Press("choose-expansion"); Advance(); await WaitForNativeAction("live-month");
                Check(world!.Month == 1 && world.Decisions.Count(d => d.Source == "llm") == world.Sects.Count - 1, "Native LLM advance must produce a valid decision for every rival.");
                Check(world.AgentMemories.Count == world.Sects.Count - 1 && world.AgentMemories.All(m => m.Plan is not null && m.Conversation.Count == 2), "Each rival must persist a public conversation and accepted plan.");
                var requests = world.ModelRequests; Advance(); await WaitForNativeAction("live-plan-reuse");
                Check(world!.Month == 2 && world.ModelRequests == requests, "A quiet month must reuse existing plans without a model request.");
                page = "diplomacy"; Render(); await Press("agent-memory-" + world.AgentMemories[0].SectId); Screenshot("agent-memory"); CloseOverlay();
                liveModel = new { requests = session.CallsUsed, contextChecks = world.ModelContextChecks, tokens = world.ModelTokens, decisions = world.Decisions.Count,
                    sessions = world.AgentMemories.Count, conversations = world.AgentMemories.Sum(m => m.Conversation.Count), quietMonthCalls = world.ModelRequests - requests,
                    contextWindow = model.ContextWindowTokens, compactAt = model.ContextWindowTokens * model.CompactionThreshold, effort = model.Effort };
                checks.Add("real Anthropic tool Agent loop, persisted public conversations, exact context token checks, and zero calls in a quiet follow-up month");
            }
            await SetVerificationWorld(victory); page = "ending"; Render(); await Frames(3);
            await File.WriteAllTextAsync(Path.Combine(evidence, "report.json"), JsonSerializer.Serialize(new { passed = true, engine = Engine.GetVersionInfo()["string"].AsString(), nativeWindowHandle = window, renderer = DisplayServer.GetName(), checks, campaign, liveModel }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("NATIVE_VERIFICATION_PASSED " + Path.Combine(evidence, "report.json")); GetTree().Quit();
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(evidence, "report.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.Message }));
            GD.PrintErr("NATIVE_VERIFICATION_FAILED: " + error.Message); GetTree().Quit(1);
        }

        void Screenshot(string name)
        {
            var result = GetViewport().GetTexture().GetImage().SavePng(Path.Combine(evidence, name + ".png"));
            Check(result == Error.Ok, "Viewport screenshot must be written.");
        }
    }

    private async Task Frames(int count = 2) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private async Task Press(string key)
    {
        await Frames();
        Godot.Button button;
        try { button = FindControl<Godot.Button>(n => n.HasMeta("action") && n.GetMeta("action").AsString() == key && n.IsVisibleInTree()); }
        catch (InvalidOperationException error) { throw new InvalidOperationException("Native action not found: " + key, error); }
        Check(!button.Disabled, "Native button is disabled: " + key); button.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitForNativeAction(key);
        await Frames();
    }

    private async Task ClickNativeButton(string key)
    {
        await Frames();
        var button = FindControl<Button>(n => n.HasMeta("action") && n.GetMeta("action").AsString() == key && n.IsVisibleInTree());
        Check(!button.Disabled, "Native button is disabled: " + key);
        var point = button.GetGlobalTransform() * (button.Size / 2);
        var logicalSize = GetViewportRect().Size; var windowSize = GetWindow().Size;
        var scale = Math.Min(windowSize.X / logicalSize.X, windowSize.Y / logicalSize.Y);
        point *= scale;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }); Input.FlushBufferedEvents(); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
        await WaitForNativeAction(key);
    }

    private async Task WaitForNativeAction(string key)
    {
        var deadline = Time.GetTicksMsec() + 600_000;
        while (busy && Time.GetTicksMsec() < deadline) await Frames(1);
        Check(!busy, "Native action timed out: " + key);
        Check(overlay is null || key is "saves" or "settings" or "help" or "region-details" || key.StartsWith("chronicle-", StringComparison.Ordinal) || key.StartsWith("person-", StringComparison.Ordinal) || key.StartsWith("relic-give-", StringComparison.Ordinal) || key.StartsWith("map-leader-", StringComparison.Ordinal) || key.StartsWith("guardian-", StringComparison.Ordinal) || key.StartsWith("agent-memory-", StringComparison.Ordinal), "Unexpected dialog after native action: " + key + "; " + notice);
    }

    private void CheckHudLayout()
    {
        var viewport = GetViewport().GetVisibleRect();
        List<string> keys = ["saves", "settings", "time-speed", "advance", "pause"];
        if (world!.PendingEvent is not null) keys.Add("open-council");
        if (page == "map") keys.AddRange(["campaigns", "zoom-out", "zoom-in", "reset-map"]);
        foreach (var key in keys)
        {
            var control = FindControl<Button>(c => c.HasMeta("action") && c.GetMeta("action").AsString() == key);
            var bounds = control.GetGlobalRect();
            Check(bounds.Size.Y <= 52, "HUD controls must keep their compact height instead of stretching with the row: " + key);
            Check(bounds.Position.X >= 0 && bounds.Position.Y >= 0 && bounds.End.X <= viewport.End.X + 1 && bounds.End.Y <= viewport.End.Y + 1, "HUD controls must remain inside the viewport: " + key);
        }
        var footer = FindControl<Control>(c => c.HasMeta("hud_footer")).GetGlobalRect();
        foreach (var key in new[] { "time-speed", "advance", "pause" })
        {
            var bounds = FindControl<Button>(c => c.HasMeta("action") && c.GetMeta("action").AsString() == key).GetGlobalRect();
            Check(Math.Abs(bounds.GetCenter().Y - footer.GetCenter().Y) <= 1, "Time controls must share the footer's vertical center: " + key);
        }
        var calendar = FindControl<Control>(c => c.HasMeta("hud_calendar")).GetGlobalRect();
        var calendarContent = FindControl<Control>(c => c.HasMeta("hud_calendar_content")).GetGlobalRect();
        Check(calendar.Size.Y <= 88, "Calendar plaque must keep a compact height.");
        Check(calendarContent.Position.X >= calendar.Position.X + 15 && calendarContent.End.X <= calendar.End.X - 15
            && calendarContent.Position.Y >= calendar.Position.Y + 11 && calendarContent.End.Y <= calendar.End.Y - 11,
            "Calendar dial and text must keep safe insets from the plaque border.");
        Check(Math.Abs(calendarContent.GetCenter().Y - calendar.GetCenter().Y) <= 1, "Calendar contents must be vertically centered within the plaque.");
        var rail = FindControl<Control>(c => c.HasMeta("hud_rail")).GetGlobalRect();
        var summary = FindControl<Control>(c => c.HasMeta("hud_rail_summary")).GetGlobalRect();
        Check(summary.Position.X >= rail.Position.X + 15 && summary.End.Y <= rail.End.Y - 15, "Sect policy summary must stay inside the navigation frame with a safe inset.");
        if (page == "map")
        {
            var titlePlate = FindControl<Control>(c => c.HasMeta("hud_map_title_plate")).GetGlobalRect();
            var title = FindControl<Control>(c => c.HasMeta("hud_map_title")).GetGlobalRect();
            Check(title.Position.X >= titlePlate.Position.X + 23 && title.Position.Y >= titlePlate.Position.Y + 15 && title.End.Y <= titlePlate.End.Y - 15,
                "Map heading and subtitle must stay inside the title plaque's undecorated text area.");
        }
        foreach (var emblem in surface.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>().Where(c => c.HasMeta("sect_emblem")))
            Check(Math.Abs(emblem.Size.X - emblem.Size.Y) <= 1, "Sect emblems must remain square instead of stretching to fill a row or column.");
    }

    private T FindControl<T>(Func<T, bool> predicate) where T : Control
    {
        T? Search(Node node)
        {
            if (node is T candidate && predicate(candidate)) return candidate;
            foreach (var child in node.GetChildren()) { var found = Search(child); if (found is not null) return found; }
            return null;
        }
        return Search(this) ?? throw new InvalidOperationException("Native control not found: " + typeof(T).Name);
    }

    private async Task ClickRegion(int index)
    {
        var map = FindControl<WorldMap>(_ => true); var point = map.GetGlobalTransform() * map.RegionCenter(index);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
    }

    private async Task SelectNativeOption(string action, int index)
    {
        await Frames();
        var button = FindControl<OptionButton>(n => n.HasMeta("action") && n.GetMeta("action").AsString() == action);
        var point = button.GetGlobalTransform() * (button.Size / 2);
        GD.Print("NATIVE_DROPDOWN_OPEN " + action);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
        Check(IsInstanceValid(button) && button.IsInsideTree(), "Opening a dropdown must not detach its control: " + action);
        var popup = button.GetPopup();
        Check(popup.Visible, "A real popup must be visible: " + action);
        Notification((int)NotificationWMWindowFocusOut);
        Check(button.IsInsideTree(), "Window focus loss must not rebuild a live popup: " + action);
        await Frames(3);
        Check(IsInstanceValid(button) && button.IsInsideTree(), "A live dropdown must survive deferred window updates: " + action);
        popup.SetFocusedItem(index);
        popup.GrabFocus();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = true });
        Input.FlushBufferedEvents();
        Check(button.Selected == index, "Native popup selection must update its OptionButton: " + action);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = false });
        Input.FlushBufferedEvents();
        await Frames(3);
    }

    private async Task PlayCommand(string type, string target = "", string option = "")
    {
        if (type == "choose") { page = "council"; Render(); await Press("choose-" + option); return; }
        if (type is "attack" or "develop" or "fortify" or "settle") { selectedRegion = target; page = "map"; Render(); await Press(type + "-" + target); return; }
        page = "sect"; Render(); await Press("command-" + type);
    }

    private async Task SetVerificationWorld(GameState state)
    {
        displayedEvent = -1; CloseOverlay(); Run(() => session.Commit(state), "加载独立验证场景");
        var deadline = Time.GetTicksMsec() + 30_000; while (busy && Time.GetTicksMsec() < deadline) await Frames(1);
        Check(!busy && overlay is null, "Verification fixture must load."); await Frames();
    }
}
