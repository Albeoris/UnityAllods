﻿using System.Collections.Generic;

namespace SpellEffects
{
    class Poison : TimedEffect
    {
        // this field is effectively unique spell id.
        // we use it to make sure that we don't infinitely stack poison cloud effect for same spell, but multiple poison clouds stack
        Spells.SpellProc ParentProc;
        int Damage;

        public Poison(Spells.SpellProc parentProc, int damage, int duration) : base(duration)
        {
            ParentProc = parentProc;
            Damage = damage;
        }

        public override bool OnAttach(MapUnit unit)
        {
            // check for existing poison from this cloud. if so, reset timer/duration
            List<Poison> poisons = unit.GetSpellEffects<Poison>();
            foreach (Poison p in poisons)
            {
                if (p.ParentProc == ParentProc)
                {
                    p.Timer = 0;
                    if (Duration > p.Duration)
                        p.Duration = Duration;
                    if (Damage > p.Damage)
                        p.Damage = Damage;
                    return false;
                }
            }

            return true;
        }

        public override void OnDetach()
        {
            Unit.Flags &= ~UnitFlags.Poisoned;
        }

        public override bool Process()
        {
            if (!base.Process())
                return false;

            Unit.Flags |= UnitFlags.Poisoned;
            if (MapLogic.Instance.LevelTime % MapLogic.TICRATE == 0)
            {
                DamageFlags spdf = Spells.SpellProc.SphereToDamageFlags(ParentProc.Spell);
                if (ParentProc.Spell.Item == null)
                    spdf |= DamageFlags.AllowExp;
                Unit.TakeDamage(spdf, ParentProc.Spell.User, Damage);
            }
            // This is the various effects unit can have
            return true;
        }
    }

    [SpellIndicatorFlags(UnitFlags.Poisoned)]
    public class PoisonIndicator : EffectIndicator
    {
        public PoisonIndicator(MapUnit unit) : base(unit) { }

        MapProjectile Indicator;

        public override void OnEnable()
        {
            Indicator = new MapProjectile(AllodsProjectile.PoisonSign, Unit);
            Indicator.ZOffset = 64;
            MapLogic.Instance.AddObject(Indicator, true);
        }

        public override void OnDisable()
        {
            Indicator.Dispose();
        }

        public override void Process()
        {
            Indicator.Alpha = (Unit.GetVisibility() == 2) ? 0.5f : 0;
            Indicator.SetPosition(Unit.X + Unit.FracX + Unit.Width / 2f, Unit.Y + Unit.FracY + Unit.Height / 2f, 1f + (((Unit.Width + Unit.Height) / 2f) - 1f) / 2f);
            Indicator.CurrentFrame = (Indicator.CurrentFrame + 1) % Indicator.Class.Phases;
            Indicator.DoUpdateView = true;
        }
    }
}
