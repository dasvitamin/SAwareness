﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Evade;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Geometry = LeagueSharp.Common.Geometry;

namespace SAwareness
{
    internal class Activator
    {
        private float LastItemCleanseUse = 0;
        List<BuffType> buffs = new List<BuffType>();
        public static Dictionary<Obj_AI_Hero, List<IncomingDamage>> damages = new Dictionary<Obj_AI_Hero, List<IncomingDamage>>();

        public static List<Skillshot> DetectedSkillshots = new List<Skillshot>(); 

        private bool debug = false;

        public class IncomingDamage
        {
            public String SpellName;
            public Obj_AI_Base Source;
            public Vector3 StartPos;
            public Vector3 EndPos;
            public double Dmg;
            public double TimeHit;
            public GameObject Target;
            public bool Turret;
            public bool Minion;

            public IncomingDamage(String spellName, Obj_AI_Base source, Vector3 startPos, Vector3 endPos, double dmg, double timeHit, GameObject target = null, bool turret = false, bool minion = false)
            {
                SpellName = spellName;
                Source = source;
                StartPos = startPos;
                EndPos = endPos;
                Dmg = dmg;
                TimeHit = timeHit;
                Target = target;
                Turret = turret;
                Minion = minion;
            }

            public static double CalcTimeHit(double extraTimeForCast, Obj_AI_Base sender, Obj_AI_Base hero, Vector3 endPos) //TODO: Fix Time for animations etc
            {
                return Game.Time + (extraTimeForCast/1000)*(sender.ServerPosition.Distance(endPos)/1000) +
                       (hero.ServerPosition.Distance(sender.ServerPosition)/1000);
            }

            public static double CalcTimeHit(double startTime, double extraTimeForCast, Obj_AI_Base sender, Obj_AI_Base hero, Vector3 endPos) //TODO: Fix Time for animations etc
            {
                return startTime + (extraTimeForCast / 1000) * (sender.ServerPosition.Distance(endPos) / 1000) +
                       (hero.ServerPosition.Distance(sender.ServerPosition) / 1000);
            }
        }

        public Activator()
        {
            foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (!hero.IsEnemy)
                {
                    damages.Add(hero, new List<IncomingDamage>());
                }
            }
            damages.Add(new Obj_AI_Hero(), new List<IncomingDamage>());
            Game.OnGameUpdate += Game_OnGameUpdate;
            Obj_AI_Hero.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
            if (debug)
                Drawing.OnDraw += Drawing_OnDraw;

            //Evade
            SkillshotDetector.OnDetectSkillshot += OnDetectSkillshot;
            SkillshotDetector.OnDeleteMissile += OnDeleteMissile;
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

        void Drawing_OnDraw(EventArgs args)
        {
            if (debug)
                foreach (KeyValuePair<Obj_AI_Hero, List<IncomingDamage>> damage in damages)
                {
                    Vector2 d2 = Drawing.WorldToScreen(damage.Key.ServerPosition);
                    Drawing.DrawText(d2.X, d2.Y, System.Drawing.Color.Aquamarine, CalcMaxDamage(damage.Key).ToString());
                }
        }   

        void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!IsActive())
                return;
            UseOffensiveItems_OnProcessSpellCast(sender, args);
            GetIncomingDamage_OnProcessSpellCast(sender, args);
            UseSummonerSpells_OnProcessSpellCast(sender, args);
        }

        void Game_OnGameUpdate(EventArgs args)
        {
            if (!IsActive())
                return;
            UseOffensiveItems_OnGameUpdate();
            UseDefensiveItems_OnGameUpdate();
            GetIncomingDamage_OnGameUpdate();
            UseSummonerSpells_OnGameUpdate();
        }

        private void UseDefensiveItems_OnGameUpdate()
        {
            if (!Menu.ActivatorDefensive.GetActive())
                return;

            buffs.Clear();
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigStun").GetValue<bool>())
                buffs.Add(BuffType.Stun);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigSilence").GetValue<bool>())
                buffs.Add(BuffType.Silence);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigTaunt").GetValue<bool>())
                buffs.Add(BuffType.Taunt);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigFear").GetValue<bool>())
                buffs.Add(BuffType.Fear);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigCharm").GetValue<bool>())
                buffs.Add(BuffType.Charm);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigBlind").GetValue<bool>())
                buffs.Add(BuffType.Blind);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigDisarm").GetValue<bool>())
                buffs.Add(BuffType.Disarm);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigSuppress").GetValue<bool>())
                buffs.Add(BuffType.Suppression);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigSlow").GetValue<bool>())
                buffs.Add(BuffType.Slow);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigCombatDehancer").GetValue<bool>())
                buffs.Add(BuffType.CombatDehancer);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigSnare").GetValue<bool>())
                buffs.Add(BuffType.Snare);
            if (Menu.ActivatorDefensiveCleanseConfig.GetMenuItem("SAwarenessActivatorDefensiveCleanseConfigPoison").GetValue<bool>())
                buffs.Add(BuffType.Poison);

            UseSelfCleanseItems();
            UseSlowItems();
            UseShieldItems();
            UseMikaelsCrucible();
        }

        private void UseSelfCleanseItems()
        {
            UseQSS();
            UseMS();
            UseDB();
        }

        private void UseQSS()
        {
            if (!Menu.ActivatorDefensiveCleanseSelf.GetActive())
                return;

            List<BuffInstance> buffList = GetActiveCCBuffs();

            if (buffList.Count() >=
                Menu.ActivatorDefensiveCleanseSelf.GetMenuItem("SAwarenessActivatorDefensiveCleanseSelfConfigMinSpells").GetValue<Slider>().Value &&
                Menu.ActivatorDefensiveCleanseSelf.GetMenuItem("SAwarenessActivatorDefensiveCleanseSelfQSS").GetValue<bool>() &&
                LastItemCleanseUse + 1 < Game.Time)
            {
                Items.Item qss = new Items.Item(3140, 0);
                if (qss.IsReady())
                {
                    qss.Cast();
                    LastItemCleanseUse = Game.Time;
                }
            }
        }

        private void UseMS()
        {
            if (!Menu.ActivatorDefensiveCleanseSelf.GetActive())
                return;

            List<BuffInstance> buffList = GetActiveCCBuffs();

            if (buffList.Count() >=
                Menu.ActivatorDefensiveCleanseSelf.GetMenuItem("SAwarenessActivatorDefensiveCleanseSelfConfigMinSpells").GetValue<Slider>().Value &&
                Menu.ActivatorDefensiveCleanseSelf.GetMenuItem("SAwarenessActivatorDefensiveCleanseSelfMercurialScimitar").GetValue<bool>() &&
                LastItemCleanseUse + 1 < Game.Time)
            {
                Items.Item ms = new Items.Item(3139, 0);
                if (ms.IsReady())
                {
                    foreach (var instance in buffList)
                    {
                        Console.WriteLine(instance.Name);
                    }
                    ms.Cast();
                    LastItemCleanseUse = Game.Time;
                }
            }
        }

        private void UseDB()
        {
            if (!Menu.ActivatorDefensiveCleanseSelf.GetActive())
                return;

            List<BuffInstance> buffList = GetActiveCCBuffs();

            if (buffList.Count() >=
                Menu.ActivatorDefensiveCleanseSelf.GetMenuItem("SAwarenessActivatorDefensiveCleanseSelfConfigMinSpells").GetValue<Slider>().Value &&
                Menu.ActivatorDefensiveCleanseSelf.GetMenuItem("SAwarenessActivatorDefensiveCleanseSelfDervishBlade").GetValue<bool>() &&
                LastItemCleanseUse + 1 < Game.Time)
            {
                Items.Item db = new Items.Item(3137, 0);
                if (db.IsReady())
                {
                    db.Cast();
                    LastItemCleanseUse = Game.Time;
                }
            }
        }

        private void UseSlowItems()
        {
            UseRanduins();
            UseFrostQueensClaim();
        }

        private void UseRanduins()
        {
            if (!Menu.ActivatorDefensiveDebuffSlow.GetActive())
                return;

            Obj_AI_Hero hero = GetHighestAdEnemy();
            int count = Utility.CountEnemysInRange(400);
            if (hero == null || !hero.IsValid || hero.IsDead)
                return;

            if (Menu.ActivatorDefensiveDebuffSlow.GetMenuItem("SAwarenessActivatorDefensiveDebuffSlowRanduins").GetValue<bool>() &&
                Menu.ActivatorDefensiveDebuffSlow.GetMenuItem("SAwarenessActivatorDefensiveDebuffSlowConfigRanduins").GetValue<Slider>().Value >= count &&
                ImFleeing(hero) || IsFleeing(hero) && !ImFleeing(hero))
            {
                Items.Item randuins = new Items.Item(3143, 0);
                if (randuins.IsReady())
                {
                    randuins.Cast();
                }
            }
        }

        private void UseFrostQueensClaim()
        {
            if (!Menu.ActivatorDefensiveDebuffSlow.GetActive())
                return;

            Obj_AI_Hero enemy = null;
            int count = 0;
            int nCount = 0;

            foreach (var hero1 in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero1.IsEnemy && hero1.IsVisible)
                {
                    if (hero1.ServerPosition.Distance(ObjectManager.Player.ServerPosition) < 750)
                    {
                        foreach (var hero2 in ObjectManager.Get<Obj_AI_Hero>())
                        {
                            if (hero2.IsEnemy && hero2.IsVisible)
                            {
                                if (hero2.ServerPosition.Distance(hero1.ServerPosition) < 200)
                                {
                                    count++;
                                }
                            }
                        }
                        if (count == 0)
                        {
                            enemy = hero1;
                        }
                        else if (nCount < count)
                        {
                            nCount = count;
                            enemy = hero1;
                        }
                    }
                }
            }

            if (enemy == null || !enemy.IsValid || enemy.IsDead || Menu.ActivatorDefensiveDebuffSlow.GetMenuItem("SAwarenessActivatorDefensiveDebuffSlowConfigFrostQueensClaim").GetValue<Slider>().Value > nCount)
                return;

            if (Menu.ActivatorDefensiveDebuffSlow.GetMenuItem("SAwarenessActivatorDefensiveDebuffSlowFrostQueensClaim").GetValue<bool>())
            {
                Items.Item fqc = new Items.Item(3092, 850);
                if (fqc.IsReady())
                {
                    fqc.Cast(enemy.ServerPosition);
                }
            }
        }

        private void UseShieldItems()
        {
            if (!Menu.ActivatorDefensiveShieldBoost.GetActive())
                return;

            UseLocketofIronSolari();
            UseTalismanofAscension();
            UseFaceOfTheMountain();
            UseGuardiansHorn();
        }

        private static double CheckForHit(Obj_AI_Hero hero)
        {
            List<IncomingDamage> damageList = damages[damages.Last().Key];
            double maxDamage = 0;
            foreach (var incomingDamage in damageList)
            {
                PredictionInput pred = new PredictionInput();
                pred.Type = SkillshotType.SkillshotLine;
                pred.Radius = 50;
                pred.From = incomingDamage.StartPos;
                pred.RangeCheckFrom = incomingDamage.StartPos;
                pred.Range = incomingDamage.StartPos.Distance(incomingDamage.EndPos);
                pred.Collision = false;
                pred.Unit = hero;
                if (Prediction.GetPrediction(pred).Hitchance >= HitChance.Low)
                    maxDamage += incomingDamage.Dmg;
            }
            return maxDamage;
        }

        public static double CalcMaxDamage(Obj_AI_Hero hero, bool turret = true, bool minion = false)
        {            
            List<IncomingDamage> damageList = damages[hero];
            double maxDamage = 0;
            foreach (var incomingDamage in damageList)
            {
                if(!turret && incomingDamage.Turret)
                    continue;
                if(!minion && incomingDamage.Minion)
                    continue;
                maxDamage += incomingDamage.Dmg;
            }
            return maxDamage/* + CheckForHit(hero)*/;
        }

        private void UseLocketofIronSolari()
        {
            if (!Menu.ActivatorDefensiveShieldBoost.GetMenuItem("SAwarenessActivatorDefensiveShieldBoostLocketofIronSolari").GetValue<bool>())
                return;
            foreach (KeyValuePair<Obj_AI_Hero, List<IncomingDamage>> pair in damages)
            {
                double damage = CalcMaxDamage(pair.Key);
                Obj_AI_Hero hero = pair.Key;
                CheckForHit(hero);
                if(!hero.IsDead)
                {
                    Items.Item lis = new Items.Item(3190, 700);
                    if (hero.Health < damage && hero.ServerPosition.Distance(ObjectManager.Player.ServerPosition) < 700)
                    {                        
                        if (lis.IsReady())
                        {
                            lis.Cast();
                        }
                    }
                    else if (GetNegativBuff(hero) != null && Game.Time > GetNegativBuff(hero).EndTime - 0.1)
                    {
                        if (lis.IsReady())
                        {
                            lis.Cast();
                        }
                    }
                }
            }            
        }

        private void UseTalismanofAscension()
        {
            if (!Menu.ActivatorDefensiveShieldBoost.GetMenuItem("SAwarenessActivatorDefensiveShieldBoostTalismanofAscension").GetValue<bool>())
                return;
            Items.Item ta = new Items.Item(3069, 0);
            Obj_AI_Hero hero = SimpleTs.GetTarget(1000, SimpleTs.DamageType.True);
            if (hero != null && hero.IsValid && !ImFleeing(hero) && IsFleeing(hero))
            {                
                if((hero.Health / hero.MaxHealth * 100) <= 50)
                {
                    if (ta.IsReady())
                    {
                        ta.Cast();
                    }
                }
            }
            else if (Utility.CountEnemysInRange(1000) >
                     Enumerable.Count(
                         Enumerable.Where(ObjectManager.Get<Obj_AI_Hero>(), (units => units.IsAlly)),
                         (units =>
                             (double)
                                 Vector2.Distance(Geometry.To2D(ObjectManager.Player.Position),
                                     Geometry.To2D(units.Position)) <= (double) 1000)) &&
                     ObjectManager.Player.Health != ObjectManager.Player.MaxHealth)
            {
                if (ta.IsReady())
                {
                    ta.Cast();
                }
            }
        }

        private void UseFaceOfTheMountain()
        {
            if (!Menu.ActivatorDefensiveShieldBoost.GetMenuItem("SAwarenessActivatorDefensiveShieldBoostFaceOfTheMountain").GetValue<bool>())
                return;
            foreach (KeyValuePair<Obj_AI_Hero, List<IncomingDamage>> pair in damages)
            {
                double damage = CalcMaxDamage(pair.Key);
                Obj_AI_Hero hero = pair.Key;
                if (!hero.IsDead)
                {
                    Items.Item lis = new Items.Item(3401, 700);
                    if (hero.Health < damage && hero.ServerPosition.Distance(ObjectManager.Player.ServerPosition) < 700)
                    {
                        if (lis.IsReady())
                        {
                            lis.Cast();
                        }
                    }
                    else if (GetNegativBuff(hero) != null && Game.Time > GetNegativBuff(hero).EndTime - 0.1)
                    {
                        if (lis.IsReady())
                        {
                            lis.Cast();
                        }
                    }
                }
            }  
        }

        private void UseGuardiansHorn()
        {
            if (!Menu.ActivatorDefensiveShieldBoost.GetMenuItem("SAwarenessActivatorDefensiveShieldBoostGuardiansHorn").GetValue<bool>())
                return;
            if (Utility.Map.GetMap()._MapType != Utility.Map.MapType.HowlingAbyss)
                return;
            Obj_AI_Hero hero = SimpleTs.GetTarget(1000, SimpleTs.DamageType.True);
            if (hero != null && hero.IsValid && !ImFleeing(hero) && IsFleeing(hero))
            {
                Items.Item gh = new Items.Item(2051, 0);
                if (gh.IsReady())
                {
                    gh.Cast();
                }
            }
        }

        private void UseMikaelsCrucible()
        {
            if (!Menu.ActivatorDefensiveMikaelCleanse.GetActive() && LastItemCleanseUse + 1 > Game.Time)
                return;

            Items.Item mc = new Items.Item(3222, 750);

            if (
                    Menu.ActivatorDefensiveMikaelCleanse.GetMenuItem(
                        "SAwarenessActivatorDefensiveMikaelCleanseConfigAlly").GetValue<bool>())
            {
                foreach (var ally in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (ally.IsEnemy)
                        return;

                    if (ally.ServerPosition.Distance(ObjectManager.Player.ServerPosition) < 750 && !ally.IsDead &&
                        !ally.HasBuff("Recall"))
                    {
                        double health = (ally.Health / ally.MaxHealth) * 100;
                        List<BuffInstance> activeCC = GetActiveCCBuffs(ally);
                        if (activeCC.Count >=
                            Menu.ActivatorDefensiveMikaelCleanse.GetMenuItem(
                                "SAwarenessActivatorDefensiveMikaelCleanseConfigMinSpells").GetValue<Slider>().Value)
                        {
                            if (mc.IsReady())
                            {
                                mc.Cast(ally);
                                LastItemCleanseUse = Game.Time;
                            }
                        }
                        if (health <= Menu.ActivatorDefensiveMikaelCleanse.GetMenuItem(
                            "SAwarenessActivatorDefensiveMikaelCleanseConfigAllyHealth").GetValue<Slider>().Value)
                        {
                            if (mc.IsReady())
                            {
                                mc.Cast(ally);
                                LastItemCleanseUse = Game.Time;
                            }
                        }
                    }
                }  
            }
            else
            {
                if (!ObjectManager.Player.IsDead && !ObjectManager.Player.HasBuff("Recall"))
                {
                    double health = (ObjectManager.Player.Health / ObjectManager.Player.MaxHealth) * 100;
                    List<BuffInstance> activeCC = GetActiveCCBuffs();
                    if (activeCC.Count >=
                        Menu.ActivatorDefensiveMikaelCleanse.GetMenuItem(
                            "SAwarenessActivatorDefensiveMikaelCleanseConfigMinSpells").GetValue<Slider>().Value)
                    {
                        if (mc.IsReady())
                        {
                            mc.Cast(ObjectManager.Player);
                            LastItemCleanseUse = Game.Time;
                        }
                    }
                    if (health <= Menu.ActivatorDefensiveMikaelCleanse.GetMenuItem(
                        "SAwarenessActivatorDefensiveMikaelCleanseConfigSelfHealth").GetValue<Slider>().Value)
                    {
                        if (mc.IsReady())
                        {
                            mc.Cast(ObjectManager.Player);
                            LastItemCleanseUse = Game.Time;
                        }
                    }
                }
            }
                          
        }

        private BuffInstance GetNegativBuff(Obj_AI_Hero hero)
        {
            foreach (var buff in hero.Buffs)
            {
                if (buff.Name.Contains("fallenonetarget") || buff.Name.Contains("SoulShackles") ||
                    buff.Name.Contains("zedulttargetmark") || buff.Name.Contains("fizzmarinerdoombomb") ||
                    buff.Name.Contains("varusrsecondary"))
                    return buff;
            }
            return null;
        }

        private List<BuffInstance> GetActiveCCBuffs()
        {
            return GetActiveCCBuffs(ObjectManager.Player);
        }

        private List<BuffInstance> GetActiveCCBuffs(Obj_AI_Hero hero)
        {
            List<BuffInstance> nBuffs = new List<BuffInstance>();
            foreach (var buff in hero.Buffs)
            {
                foreach (var buffType in buffs)
                {
                    if (buff.Type == buffType)
                        nBuffs.Add(buff);
                }
            }
            return nBuffs;
        }

        void UseOffensiveItems_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!Menu.ActivatorOffensive.GetActive())
                return;
            if (sender.NetworkId != ObjectManager.Player.NetworkId)
                return;
            if (!args.SData.Name.ToLower().Contains("attack") || args.Target.Type != GameObjectType.obj_AI_Hero)
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

                if (entropy.IsReady() && Menu.ActivatorOffensiveAd.GetMenuItem("SAwarenessActivatorOffensiveAdEntropy").GetValue<bool>())
                {
                    entropy.Cast(target);
                }
                if (hydra.IsReady() && Menu.ActivatorOffensiveAd.GetMenuItem("SAwarenessActivatorOffensiveAdRavenousHydra").GetValue<bool>())
                {
                    hydra.Cast(target);
                }
                if (botrk.IsReady() && Menu.ActivatorOffensiveAd.GetMenuItem("SAwarenessActivatorOffensiveAdBOTRK").GetValue<bool>())
                {
                    botrk.Cast(target);
                }
                if (tiamat.IsReady() && Menu.ActivatorOffensiveAd.GetMenuItem("SAwarenessActivatorOffensiveAdTiamat").GetValue<bool>())
                {
                    tiamat.Cast(target);
                }
                if (devinesword.IsReady() && Menu.ActivatorOffensiveAd.GetMenuItem("SAwarenessActivatorOffensiveAdSwordOfTheDevine").GetValue<bool>())
                {
                    devinesword.Cast(target);
                }
                if (youmuus.IsReady() && Menu.ActivatorOffensiveAd.GetMenuItem("SAwarenessActivatorOffensiveAdYoumuusGhostblade").GetValue<bool>())
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
                if (botrk.IsReady() && Menu.ActivatorOffensiveAd.GetMenuItem("SAwarenessActivatorOffensiveAdBOTRK").GetValue<bool>())
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
                if(Utility.Map.GetMap()._MapType == Utility.Map.MapType.CrystalScar)
                    twinshadows = new Items.Item(3290, 1000);
                if (bilgewater.IsReady() && Menu.ActivatorOffensive.GetMenuItem("SAwarenessActivatorOffensiveApBilgewaterCutlass").GetValue<bool>())
                {
                    bilgewater.Cast(target);
                }
                if (hextech.IsReady() && Menu.ActivatorOffensive.GetMenuItem("SAwarenessActivatorOffensiveApHextechGunblade").GetValue<bool>())
                {
                    hextech.Cast(target);
                }
                if (blackfire.IsReady() && Menu.ActivatorOffensive.GetMenuItem("SAwarenessActivatorOffensiveApBlackfireTorch").GetValue<bool>())
                {
                    blackfire.Cast(target);
                }
                if (dfg.IsReady() && Menu.ActivatorOffensive.GetMenuItem("SAwarenessActivatorOffensiveApDFG").GetValue<bool>())
                {
                    dfg.Cast(target);
                }
                if (twinshadows.IsReady() && Menu.ActivatorOffensive.GetMenuItem("SAwarenessActivatorOffensiveApTwinShadows").GetValue<bool>())
                {
                    twinshadows.Cast(target);
                }
            }           
        }

        public static bool IsCCd(Obj_AI_Hero hero)
        {
            var cc = new List<BuffType>
            {
                BuffType.Taunt,
                BuffType.Blind,
                BuffType.Charm,
                BuffType.Fear,
                BuffType.Polymorph,
                BuffType.Stun,
                BuffType.Silence,
                BuffType.Snare
            };

            return cc.Any(hero.HasBuffOfType);
        }

        public static SpellSlot GetIgniteSlot()
        {
            foreach (var spell in ObjectManager.Player.SummonerSpellbook.Spells)
            {
                if (spell.Name.ToLower().Contains("dot") && spell.State == SpellState.Ready)
                    return spell.Slot;
            }
            return SpellSlot.Unknown;
        }

        public static SpellSlot GetHealSlot()
        {
            foreach (var spell in ObjectManager.Player.SummonerSpellbook.Spells)
            {
                if (spell.Name.ToLower().Contains("heal") && spell.State == SpellState.Ready)
                    return spell.Slot;
            }
            return SpellSlot.Unknown;
        }

        public static SpellSlot GetBarrierSlot()
        {
            foreach (var spell in ObjectManager.Player.SummonerSpellbook.Spells)
            {
                if (spell.Name.ToLower().Contains("barrier") && spell.State == SpellState.Ready)
                    return spell.Slot;
            }
            return SpellSlot.Unknown;
        }

        public static SpellSlot GetExhaustSlot()
        {
            foreach (var spell in ObjectManager.Player.SummonerSpellbook.Spells)
            {
                if (spell.Name.ToLower().Contains("exhaust") && spell.State == SpellState.Ready)
                    return spell.Slot;
            }
            return SpellSlot.Unknown;
        }

        public static SpellSlot GetCleanseSlot()
        {
            foreach (var spell in ObjectManager.Player.SummonerSpellbook.Spells)
            {
                if (spell.Name.ToLower().Contains("boost") && spell.State == SpellState.Ready)
                    return spell.Slot;
            }
            return SpellSlot.Unknown;
        }

        private SpellSlot GetPacketSlot(SpellSlot nSpellSlot)
        {
            SpellSlot spellSlot = nSpellSlot;
            int slot = -1;
            if (spellSlot == SpellSlot.Q)
                slot = 64;
            else if (spellSlot == SpellSlot.W)
                slot = 65;
            if (slot != -1)
            {
                return (SpellSlot) slot;
            }
            return SpellSlot.Unknown;
        }

        void UseSummonerSpells_OnGameUpdate()
        {
            if (!Menu.ActivatorAutoSummonerSpell.GetActive())
                return;

            UseIgnite();
            UseHealth();
            UseBarrier();
            UseExhaust_OnGameUpdate();
            UseCleanse();
        }

        private void UseSummonerSpells_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            UseExhaust_OnProcessSpellCast(sender, args);
        }

        private void UseCleanse()
        {
            if (!Menu.ActivatorAutoSummonerSpellCleanse.GetActive())
                return;
            var sumCleanse = GetCleanseSlot();
            buffs.Clear();
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseStun").GetValue<bool>())
                buffs.Add(BuffType.Stun);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseSilence").GetValue<bool>())
                buffs.Add(BuffType.Silence);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseTaunt").GetValue<bool>())
                buffs.Add(BuffType.Taunt);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseFear").GetValue<bool>())
                buffs.Add(BuffType.Fear);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseCharm").GetValue<bool>())
                buffs.Add(BuffType.Charm);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseBlind").GetValue<bool>())
                buffs.Add(BuffType.Blind);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseDisarm").GetValue<bool>())
                buffs.Add(BuffType.Disarm);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseSlow").GetValue<bool>())
                buffs.Add(BuffType.Slow);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseCombatDehancer").GetValue<bool>())
                buffs.Add(BuffType.CombatDehancer);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseSnare").GetValue<bool>())
                buffs.Add(BuffType.Snare);
            if (Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleansePoison").GetValue<bool>())
                buffs.Add(BuffType.Poison);

            List<BuffInstance> buffList = GetActiveCCBuffs();

            if (buffList.Count() >=
                Menu.ActivatorAutoSummonerSpellCleanse.GetMenuItem("SAwarenessActivatorAutoSummonerSpellCleanseMinSpells").GetValue<Slider>().Value &&
                LastItemCleanseUse + 1 < Game.Time)
            {
                SpellSlot spellSlot = GetPacketSlot(sumCleanse);
                if (spellSlot != SpellSlot.Unknown)
                {
                    GamePacket gPacketT = Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, spellSlot));
                    gPacketT.Send();
                    LastItemCleanseUse = Game.Time;
                }
            }
        }

        void UseIgnite()
        {
            if (!Menu.ActivatorAutoSummonerSpellIgnite.GetActive())
                return;
            var sumIgnite = GetIgniteSlot();
            var target = SimpleTs.GetTarget(600, SimpleTs.DamageType.True);            
            if (target != null && sumIgnite != SpellSlot.Unknown)
            {
                var igniteDmg = ObjectManager.Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
                if (igniteDmg > target.Health)
                {
                    SpellSlot spellSlot = GetPacketSlot(sumIgnite);
                    if (spellSlot != SpellSlot.Unknown)
                    {
                        GamePacket gPacketT = Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(target.NetworkId, spellSlot));
                        gPacketT.Send();
                    }
                }
            }
        }

        void UseHealth()
        {
            if (!Menu.ActivatorAutoSummonerSpellHeal.GetActive())
                return;

            var sumHeal = GetHealSlot();
            if (
                Menu.ActivatorAutoSummonerSpellHeal.GetMenuItem("SAwarenessActivatorAutoSummonerSpellHealAllyActive")
                    .GetValue<bool>())
            {
                foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (!hero.IsEnemy && !hero.IsDead && hero.ServerPosition.Distance(ObjectManager.Player.ServerPosition) < 700)
                    {
                        if (((hero.Health / hero.MaxHealth) * 100) <
                            Menu.ActivatorAutoSummonerSpellHeal.GetMenuItem(
                                "SAwarenessActivatorAutoSummonerSpellHealPercent").GetValue<Slider>().Value)
                        {
                            SpellSlot spellSlot = GetPacketSlot(sumHeal);
                            if (spellSlot != SpellSlot.Unknown)
                            {
                                GamePacket gPacketT = Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, spellSlot));
                                gPacketT.Send();
                            }
                        }
                    }
                }
            }
            if (((ObjectManager.Player.Health / ObjectManager.Player.MaxHealth) * 100) < Menu.ActivatorAutoSummonerSpellHeal.GetMenuItem("SAwarenessActivatorAutoSummonerSpellHealPercent").GetValue<Slider>().Value)
            {
                SpellSlot spellSlot = GetPacketSlot(sumHeal);
                if (spellSlot != SpellSlot.Unknown)
                {
                    GamePacket gPacketT = Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, spellSlot));
                    gPacketT.Send();
                }
            }
        }

        private void UseBarrier()
        {
            if (!Menu.ActivatorAutoSummonerSpellBarrier.GetActive())
                return;

            var sumBarrier = GetBarrierSlot();
            if (((ObjectManager.Player.Health / ObjectManager.Player.MaxHealth) * 100) < Menu.ActivatorAutoSummonerSpellBarrier.GetMenuItem("SAwarenessActivatorAutoSummonerSpellBarrierPercent").GetValue<Slider>().Value)
            {
                SpellSlot spellSlot = GetPacketSlot(sumBarrier);
                if (spellSlot != SpellSlot.Unknown)
                {
                    GamePacket gPacketT = Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, spellSlot));
                    gPacketT.Send();
                }
            }
        }

        private void UseExhaust_OnGameUpdate()
        {
            if (!Menu.ActivatorAutoSummonerSpellExhaust.GetActive())
                return;

            var sumExhaust = GetExhaustSlot();
            var enemy = GetHighestAdEnemy();
            if(enemy == null || !enemy.IsValid)
                return;
            var countE = Utility.CountEnemysInRange(750);
            foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (!hero.IsMe && !hero.IsEnemy && hero.IsValid && hero.ServerPosition.Distance(ObjectManager.Player.ServerPosition) <= 900 && countE >= Menu.ActivatorAutoSummonerSpellExhaust.GetMenuItem("SAwarenessActivatorAutoSummonerSpellExhaustMinEnemies").GetValue<Slider>().Value)
                {
                    var countA = ObjectManager.Get<Obj_AI_Hero>().Where(units => !units.IsEnemy).Count(units => Vector2.Distance(ObjectManager.Player.Position.To2D(), units.Position.To2D()) <= 750);
                    var healthA = hero.Health / hero.MaxHealth * 100;
                    var healthE = enemy.Health / enemy.MaxHealth * 100;
                    SpellSlot spellSlot = GetPacketSlot(sumExhaust);
                    if (spellSlot != SpellSlot.Unknown)
                    {
                        GamePacket gPacketT = Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(enemy.NetworkId, spellSlot));
                        if (Menu.ActivatorAutoSummonerSpellExhaust.GetMenuItem("SAwarenessActivatorAutoSummonerSpellExhaustAutoCast").GetValue<KeyBind>().Active && IsFleeing(enemy) && !ImFleeing(enemy) && countA > 0)
                        {
                            gPacketT.Send();
                        }
                        else if (Menu.ActivatorAutoSummonerSpellExhaust.GetMenuItem("SAwarenessActivatorAutoSummonerSpellExhaustAutoCast").GetValue<KeyBind>().Active && !IsFleeing(enemy) && healthA < 25)
                        {
                            gPacketT.Send();
                        }
                        else
                        if (!IsFleeing(enemy) && healthA <= Menu.ActivatorAutoSummonerSpellExhaust.GetMenuItem("SAwarenessActivatorAutoSummonerSpellExhaustAllyPercent").GetValue<Slider>().Value)
                        {
                            gPacketT.Send();
                        } 
                        else if (!ImFleeing(enemy) && countA > 0 && IsFleeing(enemy) && healthE >= 10 &&
                                 healthE <=
                                 Menu.ActivatorAutoSummonerSpellExhaust.GetMenuItem(
                                     "SAwarenessActivatorAutoSummonerSpellExhaustSelfPercent").GetValue<Slider>().Value)
                        {
                            gPacketT.Send();
                        }                        
                    } 
                }
            }
        }

        private void UseExhaust_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!Menu.ActivatorAutoSummonerSpellExhaust.GetActive() || !Menu.ActivatorAutoSummonerSpellExhaust.GetMenuItem("SAwarenessActivatorAutoSummonerSpellExhaustUseUltSpells").GetValue<bool>())
                return;

            if (sender.IsEnemy)
            {
                SpellSlot spellSlot = GetPacketSlot(GetExhaustSlot());
                if (spellSlot != SpellSlot.Unknown)
                {
                    if (args.SData.Name.Contains("InfernalGuardian") || //Annie
                        args.SData.Name.Contains("BrandWildfire") || //Brand
                        args.SData.Name.Contains("CaitlynAceintheHole") || //Caitlyn
                        args.SData.Name.Contains("DravenRCast") || //Draven
                        args.SData.Name.Contains("EzrealTrueshotBarrage") || //Ezreal
                        args.SData.Name.Contains("Crowstorm") || //Fiddle
                        args.SData.Name.Contains("FioraDance") || //Fiora
                        args.SData.Name.Contains("FizzMarinerDoom") || //Fizz
                        args.SData.Name.Contains("GragasR") || //Gragas
                        args.SData.Name.Contains("GravesChargeShot") || //Graves
                        args.SData.Name.Contains("JinxR") || //Jinx
                        args.SData.Name.Contains("KatarinaR") || //Katarina
                        args.SData.Name.Contains("KennenShurikenStorm") || //Kennen
                        args.SData.Name.Contains("LissandraR") || //Lissandra
                        args.SData.Name.Contains("LuxMaliceCannon") || //Lux
                        args.SData.Name.Contains("AlZaharNetherGrasp") || //Malzahar
                        args.SData.Name.Contains("MissFortuneBulletTime") || //Miss Fortune
                        args.SData.Name.Contains("OrianaDetonateCommand") || //Orianna
                        args.SData.Name.Contains("RivenFengShuiEngine") || //Riven
                        args.SData.Name.Contains("SyndraR") || //Syndra
                        args.SData.Name.Contains("TalonShadowAssault") || //Talon
                        args.SData.Name.Contains("BusterShot") || //Tristana
                        args.SData.Name.Contains("FullAutomatic") || //Twitch
                        args.SData.Name.Contains("VeigarPrimordialBurst") || //Veigar
                        args.SData.Name.Contains("VelkozR") || //Vel Koz
                        args.SData.Name.Contains("ViktorChaosStorm") || //Viktor
                        args.SData.Name.Contains("MonkeyKingSpinToWin") || //Wukong
                        args.SData.Name.Contains("XerathLocusOfPower2") || //Xerath
                        args.SData.Name.Contains("YasuoRKnockUpComboW") || //Yasuo
                        args.SData.Name.Contains("ZiggsR") || //Ziggs
                        args.SData.Name.Contains("ZyraBrambleZone")) //Zyra
                    {
                        if (sender.ServerPosition.Distance(ObjectManager.Player.ServerPosition) <= 750)
                        {
                            Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(sender.NetworkId, spellSlot)).Send();
                        }
                    }

                    if (args.SData.Name.Contains("SoulShackles") || //Morgana
                        args.SData.Name.Contains("KarthusFallenOne") || //Karthus
                        args.SData.Name.Contains("VladimirHemoplague")) //Vladimir
                    {
                        Utility.DelayAction.Add(2500, () =>
                        {
                            if (sender.ServerPosition.Distance(ObjectManager.Player.ServerPosition) <= 750)
                                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(sender.NetworkId, spellSlot)).Send();
                        });
                    }

                    if (args.SData.Name.Contains("AbsoluteZero") || //Nunu
                        args.SData.Name.Contains("ZedUlt")) //Zed
                    {
                        Utility.DelayAction.Add(500, () =>
                        {
                            if (sender.ServerPosition.Distance(ObjectManager.Player.ServerPosition) <= 750)
                                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(sender.NetworkId, spellSlot)).Send();
                        });
                    }
                }
            }
        }

        private Obj_AI_Hero GetHighestAdEnemy()
        {
            Obj_AI_Hero highestAd = null;
            foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.IsEnemy)
                {
                    if (hero.IsValidTarget() && hero.Distance(ObjectManager.Player.ServerPosition) <= 650)
                    {
                        if (highestAd == null)
                        {
                            highestAd = hero;
                        } else if (highestAd.BaseAttackDamage + highestAd.FlatPhysicalDamageMod <
                                   hero.BaseAttackDamage + hero.FlatPhysicalDamageMod)
                        {
                            highestAd = hero;
                        }
                    }
                }
            }
            return highestAd;
        }

        private bool IsFleeing(Obj_AI_Hero hero)
        {
            if (hero.IsValid &&
                hero.ServerPosition.Distance(ObjectManager.Player.Position) >
                hero.Position.Distance(ObjectManager.Player.Position))
            {
                return true;
            }
            return false;
        }

        private bool ImFleeing(Obj_AI_Hero hero)
        {
            if (hero.IsValid &&
                hero.Position.Distance(ObjectManager.Player.ServerPosition) >
                hero.Position.Distance(ObjectManager.Player.Position))
            {
                return true;
            }
            return false;
        }

        private static void OnDetectSkillshot(Skillshot skillshot)
        {
            var alreadyAdded = false;

            foreach (var item in DetectedSkillshots)
            {
                if (item.SpellData.SpellName == skillshot.SpellData.SpellName &&
                    (item.Unit.NetworkId == skillshot.Unit.NetworkId &&
                     (skillshot.Direction).AngleBetween(item.Direction) < 5 &&
                     (skillshot.Start.Distance(item.Start) < 100 || skillshot.SpellData.FromObjects.Length == 0)))
                {
                    alreadyAdded = true;
                }
            }

            //Check if the skillshot is from an ally.
            if (skillshot.Unit.Team == ObjectManager.Player.Team)
            {
                return;
            }

            //Check if the skillshot is too far away.
            if (skillshot.Start.Distance(ObjectManager.Player.ServerPosition.To2D()) >
                (skillshot.SpellData.Range + skillshot.SpellData.Radius + 1000) * 1.5)
            {
                return;
            }

            //Add the skillshot to the detected skillshot list.
            if (!alreadyAdded)
            {
                //Multiple skillshots like twisted fate Q.
                if (skillshot.DetectionType == DetectionType.ProcessSpell)
                {
                    if (skillshot.SpellData.MultipleNumber != -1)
                    {
                        var originalDirection = skillshot.Direction;

                        for (var i = -(skillshot.SpellData.MultipleNumber - 1) / 2;
                            i <= (skillshot.SpellData.MultipleNumber - 1) / 2;
                            i++)
                        {
                            var end = skillshot.Start +
                                      skillshot.SpellData.Range *
                                      originalDirection.Rotated(skillshot.SpellData.MultipleAngle * i);
                            var skillshotToAdd = new Skillshot(
                                skillshot.DetectionType, skillshot.SpellData, skillshot.StartTick, skillshot.Start, end,
                                skillshot.Unit);

                            DetectedSkillshots.Add(skillshotToAdd);
                        }
                        return;
                    }

                    if (skillshot.SpellData.SpellName == "UFSlash")
                    {
                        skillshot.SpellData.MissileSpeed = 1600 + (int)skillshot.Unit.MoveSpeed;
                    }

                    if (skillshot.SpellData.Invert)
                    {
                        var newDirection = -(skillshot.End - skillshot.Start).Normalized();
                        var end = skillshot.Start + newDirection * skillshot.Start.Distance(skillshot.End);
                        var skillshotToAdd = new Skillshot(
                            skillshot.DetectionType, skillshot.SpellData, skillshot.StartTick, skillshot.Start, end,
                            skillshot.Unit);
                        DetectedSkillshots.Add(skillshotToAdd);
                        return;
                    }

                    if (skillshot.SpellData.Centered)
                    {
                        var start = skillshot.Start - skillshot.Direction * skillshot.SpellData.Range;
                        var end = skillshot.Start + skillshot.Direction * skillshot.SpellData.Range;
                        var skillshotToAdd = new Skillshot(
                            skillshot.DetectionType, skillshot.SpellData, skillshot.StartTick, start, end,
                            skillshot.Unit);
                        DetectedSkillshots.Add(skillshotToAdd);
                        return;
                    }

                    if (skillshot.SpellData.SpellName == "SyndraE" || skillshot.SpellData.SpellName == "syndrae5")
                    {
                        var angle = 60;
                        var edge1 =
                            (skillshot.End - skillshot.Unit.ServerPosition.To2D()).Rotated(
                                -angle / 2 * (float)Math.PI / 180);
                        var edge2 = edge1.Rotated(angle * (float)Math.PI / 180);

                        foreach (var minion in ObjectManager.Get<Obj_AI_Minion>())
                        {
                            var v = minion.ServerPosition.To2D() - skillshot.Unit.ServerPosition.To2D();
                            if (minion.Name == "Seed" && edge1.CrossProduct(v) > 0 && v.CrossProduct(edge2) > 0 &&
                                minion.Distance(skillshot.Unit) < 800 &&
                                (minion.Team != ObjectManager.Player.Team))
                            {
                                var start = minion.ServerPosition.To2D();
                                var end = skillshot.Unit.ServerPosition.To2D()
                                    .Extend(
                                        minion.ServerPosition.To2D(),
                                        skillshot.Unit.Distance(minion) > 200 ? 1300 : 1000);

                                var skillshotToAdd = new Skillshot(
                                    skillshot.DetectionType, skillshot.SpellData, skillshot.StartTick, start, end,
                                    skillshot.Unit);
                                DetectedSkillshots.Add(skillshotToAdd);
                            }
                        }
                        return;
                    }

                    if (skillshot.SpellData.SpellName == "AlZaharCalloftheVoid")
                    {
                        var start = skillshot.End - skillshot.Direction.Perpendicular() * 400;
                        var end = skillshot.End + skillshot.Direction.Perpendicular() * 400;
                        var skillshotToAdd = new Skillshot(
                            skillshot.DetectionType, skillshot.SpellData, skillshot.StartTick, start, end,
                            skillshot.Unit);
                        DetectedSkillshots.Add(skillshotToAdd);
                        return;
                    }

                    if (skillshot.SpellData.SpellName == "ZiggsQ")
                    {
                        var d1 = skillshot.Start.Distance(skillshot.End);
                        var d2 = d1 * 0.4f;
                        var d3 = d2 * 0.69f;


                        var bounce1SpellData = SpellDatabase.GetByName("ZiggsQBounce1");
                        var bounce2SpellData = SpellDatabase.GetByName("ZiggsQBounce2");

                        var bounce1Pos = skillshot.End + skillshot.Direction * d2;
                        var bounce2Pos = bounce1Pos + skillshot.Direction * d3;

                        bounce1SpellData.Delay =
                            (int)(skillshot.SpellData.Delay + d1 * 1000f / skillshot.SpellData.MissileSpeed + 500);
                        bounce2SpellData.Delay =
                            (int)(bounce1SpellData.Delay + d2 * 1000f / bounce1SpellData.MissileSpeed + 500);

                        var bounce1 = new Skillshot(
                            skillshot.DetectionType, bounce1SpellData, skillshot.StartTick, skillshot.End, bounce1Pos,
                            skillshot.Unit);
                        var bounce2 = new Skillshot(
                            skillshot.DetectionType, bounce2SpellData, skillshot.StartTick, bounce1Pos, bounce2Pos,
                            skillshot.Unit);

                        DetectedSkillshots.Add(bounce1);
                        DetectedSkillshots.Add(bounce2);
                    }

                    if (skillshot.SpellData.SpellName == "ZiggsR")
                    {
                        skillshot.SpellData.Delay =
                            (int)(1500 + 1500 * skillshot.End.Distance(skillshot.Start) / skillshot.SpellData.Range);
                    }

                    if (skillshot.SpellData.SpellName == "JarvanIVDragonStrike")
                    {
                        var endPos = new Vector2();

                        foreach (var s in DetectedSkillshots)
                        {
                            if (s.Unit.NetworkId == skillshot.Unit.NetworkId && s.SpellData.Slot == SpellSlot.E)
                            {
                                endPos = s.End;
                            }
                        }

                        foreach (var m in ObjectManager.Get<Obj_AI_Minion>())
                        {
                            if (m.BaseSkinName == "jarvanivstandard" && m.Team == skillshot.Unit.Team &&
                                skillshot.IsDanger(m.Position.To2D()))
                            {
                                endPos = m.Position.To2D();
                            }
                        }

                        if (!endPos.IsValid())
                        {
                            return;
                        }

                        skillshot.End = endPos + 200 * (endPos - skillshot.Start).Normalized();
                        skillshot.Direction = (skillshot.End - skillshot.Start).Normalized();
                    }
                }

                if (skillshot.SpellData.SpellName == "OriannasQ")
                {
                    var endCSpellData = SpellDatabase.GetByName("OriannaQend");

                    var skillshotToAdd = new Skillshot(
                        skillshot.DetectionType, endCSpellData, skillshot.StartTick, skillshot.Start, skillshot.End,
                        skillshot.Unit);

                    DetectedSkillshots.Add(skillshotToAdd);
                }


                //Dont allow fow detection.
                if (skillshot.SpellData.DisableFowDetection && skillshot.DetectionType == DetectionType.RecvPacket)
                {
                    return;
                }
#if DEBUG
                Console.WriteLine(Environment.TickCount + "Adding new skillshot: " + skillshot.SpellData.SpellName);
#endif

                DetectedSkillshots.Add(skillshot);
            }
        }

        private static void OnDeleteMissile(Skillshot skillshot, Obj_SpellMissile missile)
        {
            if (skillshot.SpellData.SpellName == "VelkozQ")
            {
                var spellData = SpellDatabase.GetByName("VelkozQSplit");
                var direction = skillshot.Direction.Perpendicular();
                if (DetectedSkillshots.Count(s => s.SpellData.SpellName == "VelkozQSplit") == 0)
                {
                    for (var i = -1; i <= 1; i = i + 2)
                    {
                        var skillshotToAdd = new Skillshot(
                            DetectionType.ProcessSpell, spellData, Environment.TickCount, missile.Position.To2D(),
                            missile.Position.To2D() + i * direction * spellData.Range, skillshot.Unit);
                        DetectedSkillshots.Add(skillshotToAdd);
                    }
                }
            }
        }

        private void GetIncomingDamage_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            foreach (KeyValuePair<Obj_AI_Hero, List<IncomingDamage>> damage in damages)
            {
                foreach (var incomingDamage in damage.Value.ToArray())
                {
                    if (incomingDamage.TimeHit < Game.Time)
                        damage.Value.Remove(incomingDamage);
                }
                if (sender.NetworkId == damage.Key.NetworkId)
                    continue;
                if (args.Target.Type == GameObjectType.obj_LampBulb || args.Target.Type == GameObjectType.Unknown) //No target, find it later
                {
                    try
                    {
                        double spellDamage = sender.GetSpellDamage((Obj_AI_Base)args.Target, args.SData.Name);
                        if (spellDamage != 0.0f)
                            damages[damages.Last().Key].Add(new IncomingDamage(args.SData.Name, sender, args.Start,
                                args.End, spellDamage,
                                IncomingDamage.CalcTimeHit(args.TimeCast, sender, damage.Key, args.End)));
                    }
                    catch (InvalidOperationException)
                    {
                        //Cannot find spell
                    }
                    catch (InvalidCastException)
                    {
                        //TODO Need a workaround to get the spelldamage for args.Target
                        return;
                    }
                }
                if (args.SData.Name.ToLower().Contains("attack") && args.Target.NetworkId == damage.Key.NetworkId)
                {
                    double aaDamage = sender.GetAutoAttackDamage((Obj_AI_Base)args.Target);
                    if (aaDamage != 0.0f)
                        if (sender.Type == GameObjectType.obj_AI_Minion)
                        {
                            damages[damage.Key].Add(new IncomingDamage(args.SData.Name, sender, args.Start, args.End, aaDamage, IncomingDamage.CalcTimeHit(args.TimeCast, sender, damage.Key, args.End), args.Target, false, true));
                        }
                        else if (sender.Type == GameObjectType.obj_AI_Turret)
                        {
                            damages[damage.Key].Add(new IncomingDamage(args.SData.Name, sender, args.Start, args.End, aaDamage, IncomingDamage.CalcTimeHit(args.TimeCast, sender, damage.Key, args.End), args.Target, true));
                        }
                        else
                        {
                            damages[damage.Key].Add(new IncomingDamage(args.SData.Name, sender, args.Start, args.End, aaDamage, IncomingDamage.CalcTimeHit(args.TimeCast, sender, damage.Key, args.End), args.Target));
                        }
                    continue;
                }
                if (sender.Type == GameObjectType.obj_AI_Hero && args.Target.NetworkId == damage.Key.NetworkId)
                {
                    try
                    {
                        double spellDamage = sender.GetSpellDamage((Obj_AI_Base)args.Target, args.SData.Name);
                        if (spellDamage != 0.0f)
                            damages[damage.Key].Add(new IncomingDamage(args.SData.Name, sender, args.Start, args.End, spellDamage, IncomingDamage.CalcTimeHit(args.TimeCast, sender, damage.Key, args.End), args.Target));
                    }
                    catch (InvalidOperationException)
                    {
                        //Cannot find spell
                    }
                }
                if (sender.Type == GameObjectType.obj_AI_Turret && args.Target.NetworkId == damage.Key.NetworkId)
                    damages[damage.Key].Add(new IncomingDamage(args.SData.Name, sender, args.Start, args.End, 300, IncomingDamage.CalcTimeHit(args.TimeCast, sender, damage.Key, args.End), args.Target, true));
            }
        }

        private void GetIncomingDamage_OnGameUpdate()
        {
            DetectedSkillshots.RemoveAll(skillshot => !skillshot.IsActive());
            Dictionary<Obj_AI_Hero, List<Activator.IncomingDamage>> tempDamages =
                new Dictionary<Obj_AI_Hero, List<Activator.IncomingDamage>>(Activator.damages);
            foreach (KeyValuePair<Obj_AI_Hero, List<Activator.IncomingDamage>> damage in Activator.damages)
            {
                Obj_AI_Hero hero = damage.Key;

                foreach (var skillshot in Activator.DetectedSkillshots)
                {
                    if (skillshot.IsAboutToHit(50, hero))
                    {
                        try
                        {
                            double spellDamage = skillshot.Unit.GetSpellDamage((Obj_AI_Base)hero, skillshot.SpellData.SpellName);
                            bool exists = false;
                            foreach (var incomingDamage in tempDamages[hero])
                            {
                                if (incomingDamage.SpellName.Contains(skillshot.SpellData.SpellName))
                                {
                                    exists = true;
                                    break;
                                }
                            }
                            if (spellDamage != 0.0f && !exists)
                                tempDamages[hero].Add(new Activator.IncomingDamage(skillshot.SpellData.SpellName, skillshot.Unit, skillshot.Start.To3D(), skillshot.End.To3D(), spellDamage, Game.Time + 0.05, hero));
                        }
                        catch (InvalidOperationException)
                        {
                            //Cannot find spell
                        }
                    }
                }
                tempDamages = BuffDamage(hero, tempDamages);
            }
            damages = tempDamages;
        }

        private static Dictionary<Obj_AI_Hero, List<Activator.IncomingDamage>> BuffDamage(Obj_AI_Hero hero, Dictionary<Obj_AI_Hero, List<Activator.IncomingDamage>> tempDamages)
        {
            foreach (var buff in hero.Buffs)
            {
                if (buff.Type == BuffType.Poison || buff.Type == BuffType.Damage)
                {
                    foreach (var spell in Database.GetSpellList())
                    {
                        if (string.Equals(spell.Name, buff.DisplayName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            try
                            {
                                DamageSpell damageSpell = null;
                                Obj_AI_Hero enemy = null;
                                foreach (var champ in ObjectManager.Get<Obj_AI_Hero>())
                                {
                                    if (champ.IsEnemy)
                                    {
                                        foreach (SpellDataInst spellDataInst in champ.Spellbook.Spells)
                                        {
                                            if (string.Equals(spellDataInst.Name, spell.Name,
                                                StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                damageSpell = Enumerable.FirstOrDefault<DamageSpell>((IEnumerable<DamageSpell>)Damage.Spells[champ.ChampionName], (Func<DamageSpell, bool>)(s =>
                                                {
                                                    if (s.Slot == spellDataInst.Slot)
                                                        return 0 == s.Stage;
                                                    else
                                                        return false;
                                                })) ?? Enumerable.FirstOrDefault<DamageSpell>((IEnumerable<DamageSpell>)Damage.Spells[champ.ChampionName], (Func<DamageSpell, bool>)(s => s.Slot == spellDataInst.Slot));
                                                if (damageSpell != null)
                                                {
                                                    enemy = champ;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }

                                double spellDamage = enemy.GetSpellDamage((Obj_AI_Base)hero, spell.Name);
                                bool exists = false;
                                foreach (var incomingDamage in tempDamages[hero])
                                {
                                    if (incomingDamage.SpellName.Contains(spell.Name))
                                    {
                                        exists = true;
                                        break;
                                    }
                                }
                                if (spellDamage != 0.0f && !exists)
                                    tempDamages[hero].Add(new Activator.IncomingDamage(spell.Name, enemy, new Vector3(), new Vector3(), spellDamage, buff.EndTime, hero));
                            }
                            catch (InvalidOperationException)
                            {
                                //Cannot find spell
                            }
                        }
                    }
                }
            }
            return tempDamages;
        }
    }
}