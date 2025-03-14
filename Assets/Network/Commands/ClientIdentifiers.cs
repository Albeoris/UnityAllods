﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum ClientIdentifiers
{
    // login
    SwitchMap,
    Error,
    DownloadStart,
    DownloadContinue,

    // map
    AddPlayer,
    DelPlayer,
    PlayerMoney,
    ChatMessage,
    SpeedChanged,

    AddUnit, // also update unit if ID already exists
    DelUnit,
    AddUnitActions,
    IdleUnit,
    DamageUnit,

    UnitPack,
    UnitStats,
    UnitStatsShort,
    UnitPackedStats,
    UnitSpells,
    UnitFlags,
    UnitPosition,
    UnitBoneFrame,
    UnitSummonTime,

    HumanLevelUp,

    UnitItemPickup,
    SackAt,
    NoSackAt,

    // projectile directional, homing, simple
    AddProjectileDirectional,
    AddProjectileHoming,
    AddProjectileSimple,
    AddProjectileLightning,
    AddProjectileEOT,

    // kill static object (i.e. trees)
    StaticObjectDead,

    // shop, inn
    EnterShop,
    EnterInn,
    LeaveStructure,
    UpdateShopShelf,
    UpdateShopTable,
    UpdateInn
}