using System.Collections.Generic;
using DailyRoutines.Abstracts;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge.Types;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

public class AutoDrawMotifs : DailyModuleBase
{
    public override ModuleInfo Info => new()
    {
        Title = GetLoc("AutoDrawMotifsTitle"),
        Description = GetLoc("AutoDrawMotifsDescription"),
        Category = ModuleCategories.Action,
    };

    private static bool DrawWhenOutOfCombat;

    public override void Init()
    {
        AddConfig(nameof(DrawWhenOutOfCombat), true);
        DrawWhenOutOfCombat = GetConfig<bool>(nameof(DrawWhenOutOfCombat));

        TaskHelper ??= new TaskHelper { TimeLimitMS = 30_000 };

        DService.ClientState.TerritoryChanged += OnZoneChanged;
        DService.DutyState.DutyRecommenced += OnDutyRecommenced;
        DService.Condition.ConditionChange += OnConditionChanged;
        DService.DutyState.DutyCompleted += OnDutyCompleted;
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(GetLoc("AutoDrawMotifs-DrawWhenOutOfCombat"), ref DrawWhenOutOfCombat))
            UpdateConfig(nameof(DrawWhenOutOfCombat), DrawWhenOutOfCombat);
    }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (value || flag is not ConditionFlag.InCombat) return;
        TaskHelper.Abort();

        if (!DrawWhenOutOfCombat) return;
        TaskHelper.Enqueue(CheckCurrentJob);
    }

    // 重新挑战
    private void OnDutyRecommenced(object? sender, ushort e)
    {
        TaskHelper.Abort();
        TaskHelper.Enqueue(CheckCurrentJob);
    }

    // 完成副本
    private void OnDutyCompleted(object? sender, ushort e)
    {
        TaskHelper.Abort();
    }

    // 进入副本
    private void OnZoneChanged(ushort zone)
    {
        TaskHelper.Abort();
        if (!PresetData.Contents.ContainsKey(zone)) return;
        TaskHelper.Enqueue(CheckCurrentJob);
    }

    private bool? CheckCurrentJob()
    {
        if (BetweenAreas || OccupiedInEvent) return false;
        if (DService.ClientState.LocalPlayer is not { ClassJob.Id: 42, Level: >= 30 } || !IsValidPVEDuty())
        {
            TaskHelper.Abort();
            return true;
        }

        TaskHelper.Enqueue(DrawNeededMotif, "DrawNeededMotif", 5_000, true, 1);
        return true;
    }

    private unsafe bool? DrawNeededMotif()
    {
        var gauge         = DService.JobGauges.Get<PCTGauge>();
        if (DService.ClientState.LocalPlayer is not { } localPlayer) return false;
        var statusManager = localPlayer.ToBCStruct()->StatusManager;

        var motifAction = 0U;
        if (!gauge.CreatureMotifDrawn && IsActionUnlocked(34689))
            motifAction = 34689;
        else if (!gauge.WeaponMotifDrawn && IsActionUnlocked(34690) && !statusManager.HasStatus(3680))
            motifAction = 34690;
        else if (!gauge.LandscapeMotifDrawn && IsActionUnlocked(34691))
            motifAction = 34691;

        if (motifAction == 0)
        {
            TaskHelper.Abort();
            return true;
        }

        TaskHelper.Enqueue(() => UseActionManager.UseAction(ActionType.Action, motifAction), $"UseAction_{motifAction}", 2_000, true, 1);
        TaskHelper.DelayNext(500, $"DrawMotif_{motifAction}", false, 1);
        TaskHelper.Enqueue(DrawNeededMotif, "DrawNeededMotif", 5_000, true, 1);
        return true;
    }
    
    private static unsafe bool IsValidPVEDuty()
    {
        HashSet<uint> InvalidContentTypes = [16, 17, 18, 19, 31, 32, 34, 35];

        var isPVP = GameMain.IsInPvPArea() || GameMain.IsInPvPInstance();
        var contentData = LuminaCache.GetRow<ContentFinderCondition>(GameMain.Instance()->CurrentContentFinderConditionId);
        
        return !isPVP && (contentData == null || !InvalidContentTypes.Contains(contentData.ContentType.Row));
    }

    public override void Uninit()
    {
        DService.ClientState.TerritoryChanged -= OnZoneChanged;
        DService.DutyState.DutyRecommenced -= OnDutyRecommenced;
        DService.Condition.ConditionChange -= OnConditionChanged;
        DService.DutyState.DutyCompleted -= OnDutyCompleted;

        base.Uninit();
    }
}