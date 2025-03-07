﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class MapHuman : MapUnit
{
    public override MapObjectType GetObjectType() { return MapObjectType.Human; }
    protected override Type GetGameObjectType() { return typeof(MapViewHuman); }

    // this unit type has its own template
    private Templates.TplHuman Template;

    [Flags]
    public enum GenderFlags
    {
        Fighter = 0x0001,
        Mage = 0x0002,
        Male = 0x0010,
        Female = 0x0020,

        MaleFighter = Male | Fighter,
        FemaleFighter = Female | Fighter,
        MaleMage = Male | Mage,
        FemaleMage = Female | Mage
    }

    public GenderFlags Gender { get; private set; }
    public bool IsHero { get; private set; }

    public enum ExperienceSkill
    {
        Fire = 0,
        Blade = 0,
        Water = 1,
        Axe = 1,
        Air = 2,
        Bludgeon = 2,
        Earth = 3,
        Pike = 3,
        Astral = 4,
        Shooting = 4
    }

    public ExperienceSkill MainSkill = ExperienceSkill.Fire;
    private int[] Experience = new int[5] { 0, 0, 0, 0, 0 };

    public MapHuman(int serverId, bool hero = false)
    {
        IsHero = hero;
        Template = TemplateLoader.GetHumanById(serverId);
        if (Template == null)
            Debug.LogFormat("Invalid human created (serverId={0})", serverId);
        else InitHuman();
    }

    public MapHuman(string name, bool hero = false)
    {
        IsHero = hero;
        Template = TemplateLoader.GetHumanByName(name);
        if (Template == null)
            Debug.LogFormat("Invalid human created (name={0})", name);
        else InitHuman();
    }

    private void InitHuman()
    {
        InitBaseUnit();

        Class = UnitClassLoader.GetUnitClassById(Template.TypeID);
        if (Class == null)
        {
            Debug.LogFormat("Invalid unit created (class not found, serverId={0}, typeId={1})", Template.ServerID, Template.TypeID);
            Template = null;
            return;
        }

        Width = Math.Max(1, Template.TokenSize);
        Height = Width;

        CoreStats.Health = CoreStats.HealthMax = Math.Max(Template.HealthMax, 0);
        CoreStats.Mana = CoreStats.ManaMax = Math.Max(Template.ManaMax, 0); // they sometimes put -1 as mana counter for fighters

        // BRMS
        CoreStats.Body = (short)Template.Body;
        CoreStats.Reaction = (short)Template.Reaction;
        CoreStats.Mind = (short)Template.Mind;
        CoreStats.Spirit = (short)Template.Spirit;

        // speed and scanrange
        CoreStats.RotationSpeed = (byte)Template.RotationSpeed;
        if (CoreStats.RotationSpeed < 1)
            CoreStats.RotationSpeed = 1;
        CoreStats.Speed = (byte)Template.Speed;
        if (CoreStats.Speed < 1)
            CoreStats.Speed = 1;
        CoreStats.ScanRange = Template.ScanRange;

        // human specific
        if (Template.Gender == 1)
            Gender = GenderFlags.Female;
        else Gender = GenderFlags.Male;
        // guess class (mage/fighter) from type
        if (Class.ID == 24 || Class.ID == 23) // unarmed mage, mage with staff
            Gender |= GenderFlags.Mage;
        else Gender |= GenderFlags.Fighter; // otherwise its a fighter.

        //
        CoreStats.HealthRegeneration = 100;
        CoreStats.ManaRegeneration = 100;

        // initial items
        for (int i = 0; i < Template.EquipItems.Length; i++)
        {
            if (Template.EquipItems[i].Length <= 0)
                continue;

            Item item = new Item(Template.EquipItems[i]);
            if (!item.IsValid || item.Class.IsSpecial)
                continue;

            PutItemToBody((BodySlot)item.Class.Option.Slot, item);
        }

        // spellbook
        for (int i = 0; i < 32; i++)
        {
            uint sp = 1u << i;
            if (Template.ManaMax > 0/* && (Template.KnownSpells & sp) != 0*/)  // [ZZ] uncomment for production!!! currently enables all spells on unit
            {
                Spell cspell = new Spell(i, this);
                SpellBook.Add(cspell);
            }
        }

        // set skills by experience
        // [ZZ] I know that magic and fighting skills are exactly same in the array.
        //      this is written this way in case it changes. will be optimized later
        if ((Gender & GenderFlags.Fighter) != 0)
        {
            SetSkill(ExperienceSkill.Blade, Template.SkillBladeFire);
            SetSkill(ExperienceSkill.Axe, Template.SkillAxeWater);
            SetSkill(ExperienceSkill.Bludgeon, Template.SkillBludgeonAir);
            SetSkill(ExperienceSkill.Pike, Template.SkillPikeEarth);
            SetSkill(ExperienceSkill.Shooting, Template.SkillShootingAstral);
        }
        else if ((Gender & GenderFlags.Mage) != 0)
        {
            SetSkill(ExperienceSkill.Fire, Template.SkillBladeFire);
            SetSkill(ExperienceSkill.Water, Template.SkillAxeWater);
            SetSkill(ExperienceSkill.Air, Template.SkillBludgeonAir);
            SetSkill(ExperienceSkill.Earth, Template.SkillPikeEarth);
            SetSkill(ExperienceSkill.Astral, Template.SkillShootingAstral);
        }

        CoreStats.HealthMax = -1;
        OnUpdateItems();

        // fix health and mana
        Stats.TrySetHealth(Stats.HealthMax);
        Stats.TrySetMana(Stats.ManaMax);
    }

    public override bool IsItemUsable(Item item)
    {
        if (item == null)
            return false;
        if (!item.IsValid)
            return false;
        if (item.IsMoney)
            return false;
        if ((item.Class.ItemID & 0xFF00) == 0x0E00) // special item
            return true;
        if (((Gender & GenderFlags.Mage) != 0) && !item.Class.UsableMage)
            return false;
        if (((Gender & GenderFlags.Fighter) != 0) && !item.Class.UsableFighter)
            return false;
        return true;
    }

    // these functions are used in ROM2
    private static float Pow11(float v)
    {
        return Mathf.Pow(1.1f, v);
    }

    private static float Log11(float v)
    {
        return Mathf.Log(v) / Mathf.Log(1.1f);
    }

    protected override void OnUpdateItems()
    {
        if (IsHero)
        {
            Item cuirass = GetItemFromBody(BodySlot.CuirassCloak);
            Item weapon = GetItemFromBody(BodySlot.Weapon);
            Item shield = GetItemFromBody(BodySlot.Shield);
            int isLightArmored = 0;
            if (cuirass != null)
                isLightArmored = UnitClassLoader.HeroMaterials[cuirass.Class.MaterialID];
            // now if we are a mage, then we either have armed or unarmed sprite.
            // if we're a fighter, we should pick appropriate version of the sprite instead.
            UnitClass newClass = null;
            if ((Gender & GenderFlags.Mage) != 0)
            {
                newClass = (weapon != null) ? UnitClassLoader.HeroMageSt[1] : UnitClassLoader.HeroMage[1]; // always heroes. heroes_l mages don't exist!
            }
            else if (weapon == null)
            {
                newClass = UnitClassLoader.HeroUnarmed[isLightArmored];
            }
            else
            {
                Templates.TplArmor weaponKind = weapon.Class.Option;
                switch (weaponKind.AttackType)
                {
                    case 1: // sword
                        if (weaponKind.TwoHanded == 2)
                            newClass = UnitClassLoader.HeroSwordsman2h[isLightArmored];
                        else if (shield != null)
                            newClass = UnitClassLoader.HeroSwordsman_[isLightArmored];
                        else newClass = UnitClassLoader.HeroSwordsman[isLightArmored];
                        break;

                    case 2: // axe
                        if (weaponKind.TwoHanded == 2)
                            newClass = UnitClassLoader.HeroAxeman2h[isLightArmored];
                        else if (shield != null)
                            newClass = UnitClassLoader.HeroAxeman_[isLightArmored];
                        else newClass = UnitClassLoader.HeroAxeman[isLightArmored];
                        break;

                    case 3: // club
                        newClass = (shield != null) ? UnitClassLoader.HeroClubman_[isLightArmored] : UnitClassLoader.HeroClubman[isLightArmored];
                        break;

                    case 4: // pike
                        newClass = (shield != null) ? UnitClassLoader.HeroPikeman_[isLightArmored] : UnitClassLoader.HeroPikeman[isLightArmored];
                        break;

                    case 5: // shooting (bow or crossbow)
                        if (weaponKind.Name.ToLower().Contains("crossbow"))
                            newClass = UnitClassLoader.HeroCrossbowman[isLightArmored];
                        else newClass = UnitClassLoader.HeroArcher[isLightArmored];
                        break;
                }
            }

            if (newClass != Class)
            {
                Class = newClass;
                RenderViewVersion++;
            }
        }

        // if not client, recalc stats
        if (NetworkManager.IsClient)
            return;
        // max brms
        short maxBody = 100, maxReaction = 100, maxMind = 100, maxSpirit = 100;
        if ((Gender & GenderFlags.MaleFighter) == GenderFlags.MaleFighter)
        {
            maxBody = 52;
            maxReaction = 50;
            maxMind = 48;
            maxSpirit = 46;
        }
        else if ((Gender & GenderFlags.FemaleFighter) == GenderFlags.FemaleFighter)
        {
            maxBody = 50;
            maxReaction = 52;
            maxMind = 46;
            maxSpirit = 48;
        }
        else if ((Gender & GenderFlags.MaleMage) == GenderFlags.MaleMage)
        {
            maxBody = 48;
            maxReaction = 46;
            maxMind = 52;
            maxSpirit = 50;
        }
        else if ((Gender & GenderFlags.FemaleMage) == GenderFlags.FemaleMage)
        {
            maxBody = 46;
            maxReaction = 48;
            maxMind = 50;
            maxSpirit = 52;
        }

        CoreStats.Body = Math.Min(CoreStats.Body, maxBody);
        CoreStats.Reaction = Math.Min(CoreStats.Reaction, maxReaction);
        CoreStats.Mind = Math.Min(CoreStats.Mind, maxMind);
        CoreStats.Spirit = Math.Min(CoreStats.Spirit, maxSpirit);

        float origHealth = (float)Stats.Health / Stats.HealthMax;
        float origMana = (float)Stats.Mana / Stats.ManaMax;

        // speed and scanrange
        CoreStats.RotationSpeed = (byte)Template.RotationSpeed;
        if (CoreStats.RotationSpeed < 1)
            CoreStats.RotationSpeed = 1;
        CoreStats.Speed = (byte)Template.Speed;
        if (CoreStats.Speed < 1)
            CoreStats.Speed = 1;
        CoreStats.ScanRange = Template.ScanRange;

        //
        CoreStats.HealthRegeneration = 100;
        CoreStats.ManaRegeneration = 100;

        // skills
        if (Gender.HasFlag(GenderFlags.Fighter))
        {
            CoreStats.SkillBlade = (byte)GetSkill(ExperienceSkill.Blade);
            CoreStats.SkillAxe = (byte)GetSkill(ExperienceSkill.Axe);
            CoreStats.SkillBludgeon = (byte)GetSkill(ExperienceSkill.Bludgeon);
            CoreStats.SkillPike = (byte)GetSkill(ExperienceSkill.Pike);
            CoreStats.SkillShooting = (byte)GetSkill(ExperienceSkill.Shooting);
        }
        else if (Gender.HasFlag(GenderFlags.Mage))
        {
            CoreStats.SkillFire = (byte)GetSkill(ExperienceSkill.Fire);
            CoreStats.SkillWater = (byte)GetSkill(ExperienceSkill.Water);
            CoreStats.SkillAir = (byte)GetSkill(ExperienceSkill.Air);
            CoreStats.SkillEarth = (byte)GetSkill(ExperienceSkill.Earth);
            CoreStats.SkillAstral = (byte)GetSkill(ExperienceSkill.Astral);
        }

        // 
        // add stats from items
        ItemStats = new UnitStats();
        foreach (Item bodyitem in ItemsBody)
            ItemStats.MergeEffects(bodyitem.Effects);

        Stats.Body = (short)(CoreStats.Body + ItemStats.Body);
        Stats.Reaction = (short)(CoreStats.Reaction + ItemStats.Reaction);
        Stats.Mind = (short)(CoreStats.Mind + ItemStats.Mind);
        Stats.Spirit = (short)(CoreStats.Spirit + ItemStats.Spirit);

        if (Stats.Reaction < 10)
            Stats.Speed = (byte)Stats.Reaction;
        else Stats.Speed = (byte)Math.Min((float)Stats.Reaction / 5 + 12, 255f);
        if (Class.ID == 19 || Class.ID == 21)
            Stats.Speed += 10;
        Stats.RotationSpeed = Stats.Speed;

        Stats.Speed += ItemStats.Speed;
        Stats.RotationSpeed += ItemStats.RotationSpeed;

        //
        float experience_total = GetExperience();
        float fighter_mult = (Gender & GenderFlags.Fighter) != 0 ? 2 : 1;
        float mage_mult = (Gender & GenderFlags.Mage) != 0 ? 2 : 1;

        if (CoreStats.HealthMax == -1 || IsHero)
        {
            CoreStats.HealthMax = (int)(Stats.Body * fighter_mult);
            CoreStats.HealthMax += (int)(Log11(experience_total / 5000f + fighter_mult));
            CoreStats.HealthMax = (int)((Pow11(Stats.Body) / 100f + 1f) * CoreStats.HealthMax);
        }

        if ((Gender & GenderFlags.Mage) != 0)
        {
            CoreStats.ManaMax = (int)(Stats.Spirit * mage_mult);
            CoreStats.ManaMax += (int)(Log11(experience_total / 5000f + mage_mult));
            CoreStats.ManaMax = (int)((Pow11(Stats.Spirit) / 100f + 1f) * CoreStats.ManaMax);
        }
        else CoreStats.ManaMax = -1;

        Stats.HealthMax = CoreStats.HealthMax + ItemStats.HealthMax;
        Stats.ManaMax = CoreStats.ManaMax + ItemStats.ManaMax;

        float fighterSkill = 0;
        if ((Gender & GenderFlags.Fighter) != 0)
        {
            Item weapon = GetItemFromBody(BodySlot.Weapon);
            if (weapon != null && weapon.Class != null && weapon.Class.Option != null)
            {
                ExperienceSkill fsk;
                switch (weapon.Class.Option.AttackType)
                {
                    default:
                    case 1:
                        fsk = ExperienceSkill.Blade;
                        break;
                    case 2:
                        fsk = ExperienceSkill.Axe;
                        break;
                    case 3:
                        fsk = ExperienceSkill.Bludgeon;
                        break;
                    case 4:
                        fsk = ExperienceSkill.Pike;
                        break;
                    case 5:
                        fsk = ExperienceSkill.Shooting;
                        break;
                }
                fighterSkill = GetSkill(fsk);
            }
        }

        Stats.DamageMax = (short)(Stats.Body / 10);
        Stats.DamageMin = (short)(Stats.DamageMax / 2);
        Stats.ToHit = (short)Math.Pow(1f + (Stats.Reaction / 100f) + (fighterSkill / 100f), 9);

        Stats.DamageMin += ItemStats.DamageMin;
        Stats.DamageMax += (short)(ItemStats.DamageMax + Stats.DamageMin);
        Stats.ToHit += ItemStats.ToHit;

        Stats.DamageMin = (short)(Stats.DamageMin * (1f + fighterSkill / 20 * Stats.Body / 100f));
        Stats.DamageMax = (short)(Stats.DamageMax * (1f + fighterSkill / 20 * Stats.Body / 100f));

        Stats.DamageMin += ItemStats.DamageBonus;
        Stats.DamageMax += ItemStats.DamageBonus;

        Stats.Defence = (short)((float)ItemStats.Defence * (Stats.Reaction / 15));
        Stats.Absorbtion = ItemStats.Absorbtion;

        int minProt = Stats.Spirit / 2;
        int maxProt = Math.Min(100, (70 + Stats.Spirit / 2));
        Stats.ProtectionFire = ItemStats.ProtectionFire;
        Stats.ProtectionWater = ItemStats.ProtectionWater;
        Stats.ProtectionAir = ItemStats.ProtectionAir;
        Stats.ProtectionEarth = ItemStats.ProtectionEarth;
        Stats.ProtectionAstral = ItemStats.ProtectionAstral;

        Stats.ScanRange = 4f + (float)Math.Pow(1f + Stats.Reaction / 100f, Stats.Mind / 10f);
        Stats.ScanRange += ItemStats.ScanRange;

        for (int i = 0; i < SpellEffects.Count; i++)
            SpellEffects[i].ProcessStats(Stats);

        Stats.ProtectionFire = (byte)Math.Min(maxProt, Math.Max(minProt, Stats.ProtectionFire));
        Stats.ProtectionWater = (byte)Math.Min(maxProt, Math.Max(minProt, Stats.ProtectionWater));
        Stats.ProtectionAir = (byte)Math.Min(maxProt, Math.Max(minProt, Stats.ProtectionAir));
        Stats.ProtectionEarth = (byte)Math.Min(maxProt, Math.Max(minProt, Stats.ProtectionEarth));
        Stats.ProtectionAstral = (byte)Math.Min(maxProt, Math.Max(minProt, Stats.ProtectionAstral));

        if (Gender.HasFlag(GenderFlags.Fighter))
        {
            Stats.SkillBlade = (byte)(CoreStats.SkillBlade + ItemStats.SkillBlade);
            Stats.SkillAxe = (byte)(CoreStats.SkillAxe + ItemStats.SkillAxe);
            Stats.SkillBludgeon = (byte)(CoreStats.SkillBludgeon + ItemStats.SkillBludgeon);
            Stats.SkillPike = (byte)(CoreStats.SkillPike + ItemStats.SkillPike);
            Stats.SkillShooting = (byte)(CoreStats.SkillShooting + ItemStats.SkillShooting);
        }
        else if (Gender.HasFlag(GenderFlags.Mage))
        {
            CoreStats.SkillFire = (byte)(CoreStats.SkillFire + ItemStats.SkillFire);
            CoreStats.SkillWater = (byte)(CoreStats.SkillWater + ItemStats.SkillWater);
            CoreStats.SkillAir = (byte)(CoreStats.SkillAir + ItemStats.SkillAir);
            CoreStats.SkillEarth = (byte)(CoreStats.SkillEarth + ItemStats.SkillEarth);
            CoreStats.SkillAstral = (byte)(CoreStats.SkillAstral + ItemStats.SkillAstral);
        }

        //Debug.LogFormat("ItemStats = {0}", ItemStats.ToString());

        CalculateVision();

        RenderInfoVersion++;
        RenderViewVersion++;
    }

    public override void Update()
    {
        // update unit
        base.Update();
    }

    // template stuff.
    public override int Charge
    {
        get
        {
            Item weapon = GetItemFromBody(BodySlot.Weapon);
            if (weapon != null)
                return weapon.Class.Option.Charge;
            return Class.AttackDelay;
        }
    }

    public override int Relax
    {
        get
        {
            Item weapon = GetItemFromBody(BodySlot.Weapon);
            if (weapon != null)
                return weapon.Class.Option.Relax;
            return 0;
        }
    }

    public override bool IsIgnoringArmor { get { return false; } }

    public override bool IsFlying { get { return false; } }
    public override bool IsHovering { get { return false; } }
    public override bool IsWalking { get { return true; } }

    public override int ServerID { get { return Template.ServerID; } }
    public override int TypeID { get { return Class.ID; } }
    public override int Face { get { return Template.Face; } }

    public override string TemplateName { get { return Template.Name; } }

    // experience stuff
    public int GetExperience()
    {
        if ((Gender & GenderFlags.Mage | GenderFlags.Fighter) == 0)
            return 0;

        int exp = 0;
        for (int i = 0; i < 5; i++)
            exp += Experience[i];
        return exp;
    }

    public int ScaleExperience(float scalar)
    {
        for (int i = 0; i < 5; i++)
            Experience[i] = (int)(Experience[i] * scalar);
        return 0;
    }

    public int SetSkillExperience(ExperienceSkill sk, int value, bool message)
    {
        if ((Gender & GenderFlags.Mage | GenderFlags.Fighter) == 0)
            return 0;
        int oldexp = Experience[(int)sk];
        int newexp = (Experience[(int)sk] = value);
        UpdateItems();
        if (oldexp < newexp)
        {
            int oldskill = GetSkillFromExperience(oldexp);
            int newskill = GetSkillFromExperience(newexp);
            if (NetworkManager.IsServer)
                Server.NotifyHumanLevelUp(this, sk, newexp, message);
            if (oldskill < newskill && 
                Player == MapLogic.Instance.ConsolePlayer &&
                message)
            {
                int skillIndex = (int)sk;
                if ((Gender & GenderFlags.Mage) != 0)
                    skillIndex += 5;
                MapViewChat.Instance.AddChatMessage(Player.AllColorsSystem, Locale.Main[130+skillIndex]);
            }
        }
        return newexp;
    }

    public int GetSkillExperience(ExperienceSkill sk)
    {
        if ((Gender & GenderFlags.Mage | GenderFlags.Fighter) == 0)
            return 0;
        return Experience[(int)sk];
    }

    public void SetSkill(ExperienceSkill sk, int value)
    {
        int exp = value > 0 ? (int)((Mathf.Pow(1.1f, value) - 1f) * 1000f) : 0;
        SetSkillExperience(sk, exp, false);
    }

    private static int[] ReverseExpTable;
    private int GetSkillFromExperience(int exp)
    {
        if (ReverseExpTable == null)
        {
            ReverseExpTable = new int[256];
            for (int i = 0; i < 256; i++)
                ReverseExpTable[i] = i > 0 ? (int)((Mathf.Pow(1.1f, i) - 1f) * 1000f) : 0;
        }

        for (int i = 0; i < ReverseExpTable.Length; i++)
        {
            if (ReverseExpTable[i] > exp)
                return i - 1;
        }

        return 0;
    }

    public int GetSkill(ExperienceSkill sk)
    {
        int exp = GetSkillExperience(sk);
        return GetSkillFromExperience(exp);
    }

    public override DamageFlags GetDamageType()
    {
        Item weapon = GetItemFromBody(BodySlot.Weapon);
        if (weapon == null || weapon.Class == null || weapon.Class.Option == null)
            return DamageFlags.Raw | DamageFlags.AllowExp;
        switch (weapon.Class.Option.AttackType)
        {
            case 1:
                return DamageFlags.Blade | DamageFlags.AllowExp;
            case 3:
                return DamageFlags.Bludgeon | DamageFlags.AllowExp;
            case 4:
                return DamageFlags.Pike | DamageFlags.AllowExp;
            case 2:
                return DamageFlags.Axe | DamageFlags.AllowExp;
            case 5:
                return DamageFlags.Shooting | DamageFlags.AllowExp;
            default:
                return DamageFlags.Raw;
        }
    }
}
 