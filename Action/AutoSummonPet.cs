using System.Collections.Generic;
using DailyRoutines.Abstracts;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

public class AutoSummonPet : DailyModuleBase
{
    public override ModuleInfo Info => new()
    {
        Title = GetLoc("AutoSummonPetTitle"),
        Description = GetLoc("AutoSummonPetDescription"),
        Category = ModuleCategories.Action,
    };

    private static readonly Dictionary<uint, uint> SummonActions = new()
    {
        // 学者
        { 28, 17215 },
        // 秘术师 / 召唤师
        { 26, 25798 },
        { 27, 25798 },
    };

    public override void Init()
    {
        TaskHelper ??= new TaskHelper { TimeLimitMS = 30_000 };

        DService.ClientState.TerritoryChanged += OnZoneChanged;
        DService.DutyState.DutyRecommenced += OnDutyRecommenced;
    }

    // 重新挑战
    private void OnDutyRecommenced(object? sender, ushort e)
    {
        TaskHelper.Abort();
        TaskHelper.Enqueue(CheckCurrentJob);
    }

    // 进入副本
    private void OnZoneChanged(ushort zone)
    {
        TaskHelper.Abort();
        if (!IsValidPVEDuty()) return;

        TaskHelper.DelayNext(1_000);
        TaskHelper.Enqueue(CheckCurrentJob);
    }

    private unsafe bool? CheckCurrentJob()
    {
        if (BetweenAreas || !IsScreenReady() || DService.Condition[ConditionFlag.Casting] ||
            DService.ClientState.LocalPlayer is not { IsTargetable: true } localPlayer) return false;

        if (!SummonActions.TryGetValue(localPlayer.ClassJob.Id, out var actionID))
        {
            TaskHelper.Abort();
            return true;
        }
        
        var state = CharacterManager.Instance()->LookupPetByOwnerObject(localPlayer.ToBCStruct()) != null;
        if (state)
        {
            TaskHelper.Abort();
            return true;
        }

        TaskHelper.Enqueue(() => UseActionManager.UseAction(ActionType.Action, actionID));
        TaskHelper.DelayNext(1_000);
        TaskHelper.Enqueue(CheckCurrentJob);
        return true;
    }

    private static unsafe bool IsValidPVEDuty()
    {
        HashSet<uint> InvalidContentTypes = [16, 17, 18, 19, 31, 32, 34, 35];

        var isPVP = GameMain.IsInPvPArea() || GameMain.IsInPvPInstance();
        var contentData = LuminaCache.GetRow<ContentFinderCondition>(GameMain.Instance()->CurrentContentFinderConditionId);
        if (contentData.RowId == 0) return false;
        
        return !isPVP && !InvalidContentTypes.Contains(contentData.ContentType.Row);
    }

    public override void Uninit()
    {
        DService.DutyState.DutyRecommenced -= OnDutyRecommenced;
        DService.ClientState.TerritoryChanged -= OnZoneChanged;

        base.Uninit();
    }
}