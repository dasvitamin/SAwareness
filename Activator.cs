﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;

namespace SAwareness
{
    class Activator
    {
        public Activator()
        {
            Game.OnGameUpdate += Game_OnGameUpdate;
            Obj_AI_Hero.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
        }

        void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            UseOffensiveItems_OnProcessSpellCast(sender, args);
        }

        ~Activator()
        {
            Game.OnGameUpdate -= Game_OnGameUpdate;
            Obj_AI_Hero.OnProcessSpellCast -= Obj_AI_Hero_OnProcessSpellCast;
        }

        public bool IsActive()
        {
            return Menu.Activator.GetActive();
        }

        void Game_OnGameUpdate(EventArgs args)
        {
            if (!IsActive())
                return;
            UseOffensiveItems_OnGameUpdate();
            UseSummonerSpells();
        }

        void UseOffensiveItems_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!Menu.ActivatorOffensive.GetActive())
                return;
            if (sender.NetworkId != ObjectManager.Player.NetworkId)
                return;
            if (!args.SData.Name.ToLower().Contains("attack"))
                return;
            
            if (Menu.ActivatorOffensiveAd.GetActive())
            {
                var target = SimpleTs.GetTarget(1000, SimpleTs.DamageType.Physical);
                if (target == null || !target.IsValid)
                    return;
                Items.Item entropy = new Items.Item(3184, 400);
                Items.Item hydra = new Items.Item(3074, 400);
                Items.Item botrk = new Items.Item(3153, 450);
                Items.Item tiamat = new Items.Item(3077, 450);
                Items.Item devinesword = new Items.Item(3131, 900);
                Items.Item youmuus = new Items.Item(3142, 900);

                if (entropy.IsReady())
                {
                    entropy.Cast(target);
                }
                if (hydra.IsReady())
                {
                    hydra.Cast(target);
                }
                if (botrk.IsReady())
                {
                    botrk.Cast(target);
                }
                if (tiamat.IsReady())
                {
                    tiamat.Cast(target);
                }
                if (devinesword.IsReady())
                {
                    devinesword.Cast(target);
                }
                if (youmuus.IsReady())
                {
                    youmuus.Cast(target);
                }
            }
        }

        void UseOffensiveItems_OnGameUpdate()
        {
            if (!Menu.ActivatorOffensive.GetActive() || !Menu.ActivatorOffensive.GetMenuItem("SAwarenessActivatorOffensiveKey").GetValue<KeyBind>().Active)
                return;
            if (Menu.ActivatorOffensiveAd.GetActive())
            {
                var target = SimpleTs.GetTarget(1000, SimpleTs.DamageType.Physical);
                if (target == null || !target.IsValid)
                    return;
                Items.Item botrk = new Items.Item(3153, 450);
                if (botrk.IsReady())
                {
                    botrk.Cast(target);
                }
            }
            if (Menu.ActivatorOffensiveAp.GetActive())
            {
                var target = SimpleTs.GetTarget(1000, SimpleTs.DamageType.Physical);
                if (target == null || !target.IsValid)
                    return;
                Items.Item bilgewater = new Items.Item(3144, 450);
                Items.Item hextech = new Items.Item(3146, 700);
                Items.Item blackfire = new Items.Item(3188, 750);
                Items.Item dfg = new Items.Item(3128, 750);
                Items.Item twinshadows = new Items.Item(3023, 1000);
                if(Utility.Map.GetMap() == Utility.Map.MapType.CrystalScar)
                    twinshadows = new Items.Item(3290, 1000);
                if (bilgewater.IsReady())
                {
                    bilgewater.Cast(target);
                }
                if (hextech.IsReady())
                {
                    hextech.Cast(target);
                }
                if (blackfire.IsReady())
                {
                    blackfire.Cast(target);
                }
                if (dfg.IsReady())
                {
                    dfg.Cast(target);
                }
                if (twinshadows.IsReady())
                {
                    twinshadows.Cast(target);
                }
            }           
        }

        private SpellSlot GetIgniteSlot()
        {
            foreach (var spell in ObjectManager.Player.SummonerSpellbook.Spells)
            {
                if (spell.Name.ToLower().Contains("dot") && spell.State == SpellState.Ready)
                    return spell.Slot;
            }
            return SpellSlot.Unknown;
        }

        void UseSummonerSpells()
        {
            if (!Menu.ActivatorAutoSummonerSpell.GetActive())
                return;

            UseIgnite();
        }

        void UseIgnite()
        {
            if (!Menu.ActivatorAutoSummonerSpellIgnite.GetActive())
                return;
            var sumIgnite = GetIgniteSlot();
            var target = SimpleTs.GetTarget(600, SimpleTs.DamageType.True);            
            if (target != null && sumIgnite != SpellSlot.Unknown)
            {
                var igniteDmg = DamageLib.getDmg(target, DamageLib.SpellType.IGNITE);
                if (igniteDmg > target.Health)
                {
                    SpellSlot spellSlot = sumIgnite;
                    int slot = -1;
                    if (spellSlot == SpellSlot.Q)
                        slot = 64;
                    else if (spellSlot == SpellSlot.W)
                        slot = 65;
                    if (slot != -1)
                    {
                        GamePacket gPacketT = Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(target.NetworkId, (SpellSlot)slot));
                        gPacketT.Send();
                    }
                }
            }
        }
    }
}
