using System;
using InfinityScript;
using System.Collections.Generic;

public class nuke : BaseScript
{
    private static int[] effects = new int[3];
    private static int nukeTimer = 10;
    private static int cancelMode = 0;
    private static int nukeEmpTimeout = 60;
    private static bool nukeIncoming = false;
    private static Entity nukeInfo;
    private static bool isTeamBased = true;
    private static int nukeEmpTimeRemaining;
    private static int killsToNuke = 25;
    private static bool nukeChainsKills = false;
    private static bool destroyExplosives = false;
    private static bool explosivesDestroyed = false;
    private static bool nukeSloMotion = true;
    //private Entity nukePlayer;
    //private string nukeTeam;
    private static readonly Entity level = Entity.GetEntity(2046);
    private static Dictionary<string, string> otherTeam = new Dictionary<string, string>();

    public nuke()
    {
        otherTeam["allies"] = "axis";
        otherTeam["axis"] = "allies";

        effects[0] = GSCFunctions.LoadFX("explosions/player_death_nuke");
        effects[1] = GSCFunctions.LoadFX("explosions/player_death_nuke_flash");
        effects[2] = GSCFunctions.LoadFX("dust/nuke_aftermath_mp");
        nukeTimer = GSCFunctions.GetDvarInt("scr_nukeTimer");
        cancelMode = GSCFunctions.GetDvarInt("scr_nukeCancelMode");
        GSCFunctions.SetDvarIfUninitialized("scr_killsToNuke", 25);
        GSCFunctions.SetDvarIfUninitialized("scr_killstreaksChainToNuke", 0);
        GSCFunctions.SetDvarIfUninitialized("scr_nukeDestroysExplosives", 0);
        GSCFunctions.SetDvarIfUninitialized("scr_nukeSloMo", 1);
        nukeInfo = GSCFunctions.Spawn("script_model", Vector3.Zero);
        //level.SetField("nukeDetonated", 0);
        level.SetField("teamNukeEMPed_axis", false);
        level.SetField("teamNukeEMPed_allies", false);
        level.SetField("teamNukeEMPed_none", false);
        killsToNuke = GSCFunctions.GetDvarInt("scr_killsToNuke");
        nukeChainsKills = GSCFunctions.GetDvarInt("scr_killstreaksChainToNuke") != 0;
        destroyExplosives = GSCFunctions.GetDvarInt("scr_nukeDestroysExplosives") != 0;
        nukeSloMotion = GSCFunctions.GetDvarInt("scr_nukeSloMo") != 0;

        string gametype = GSCFunctions.GetDvar("g_gametype");
        if (gametype == "dm" || gametype == "gun" || gametype == "oic" || gametype == "jugg")
            isTeamBased = false;

        PlayerConnected += OnPlayerConnected;
    }

    private static void OnPlayerConnected(Entity player)
    {
        player.SpawnedPlayer += () => OnPlayerSpawned(player);
        player.OnNotify("weapon_change", (p, weapon) => OnWeaponChange(p, (string)weapon));
        player.SetField("hasNuke", 0);
        player.SetField("hasFauxNuke", 0);
        player.SetField("killstreak", 0);
        //Set vision from connect
        if (level.HasField("nukeDetonated"))
            player.VisionSetNakedForPlayer("aftermath", 0);
    }

    private static void OnPlayerSpawned(Entity player)
    {
        AfterDelay(50, () =>
        {
            if (level.GetField<bool>("teamNukeEMPed_" + player.SessionTeam))
            {
                if (isTeamBased)
                    player.SetEMPJammed(true);
                else
                {
                    if (!nukeInfo.HasField("player") || (nukeInfo.HasField("player") && player != nukeInfo.GetField<Entity>("player") && nukeEmpTimeRemaining > 0))
                        player.SetEMPJammed(true);
                }
            }
        });
        if (level.HasField("nukeDetonated"))
            player.VisionSetNakedForPlayer("aftermath", 0);
    }

    private static void OnWeaponChange(Entity player, string weapon)
    {
        if (mayDropWeapon(weapon))
            player.SetField("lastDroppableWeapon", weapon);
        if (weapon == "killstreak_emp_mp")
        {
            player.SwitchToWeapon(player.GetField<string>("lastDroppableWeapon"));
            if (player.GetField<int>("hasFauxNuke") == 0 && player.GetField<int>("hasNuke") < 1) return;
            if (!tryUseNuke(player, false))
            {
                Log.Write(LogLevel.Info, "Nuke could not be Called in for {0}!", player.Name);
                return;
            }
            if (player.GetField<int>("hasFauxNuke") > 0)
                player.SetField("hasFauxNuke", player.GetField<int>("hasFauxNuke") - 1);

            if (player.GetField<int>("hasNuke") < 1)
            {
                AfterDelay(1000, () => player.TakeWeapon("killstreak_emp_mp"));
                player.SetPlayerData("killstreaksState", "icons", 0, "");
                //Function.Call("setActionSlot", 4, "weapon", "killstreak_emp_mp");
                player.SetPlayerData("killstreaksState", "hasStreak", 0, false);
            }
        }

        //Fix the bug were the game would remove the caller after a certain time
        if (player.GetField<int>("hasFauxNuke") > 0 && player.GetField<int>("hasNuke") > 0)
        {
            player.GiveWeapon("killstreak_emp_mp", 0, false);
            player.SetActionSlot(4, "weapon", "killstreak_emp_mp");
        }
    }
    private static bool mayDropWeapon(string weapon)
    {
        if (weapon == "none")
            return false;

        if (weapon.Contains("ac130"))
            return false;

        if (weapon.Contains("killstreak"))
            return false;

        string invType = GSCFunctions.WeaponInventoryType(weapon);
        if (invType != "primary")
            return false;

        return true;
    }

    /// <summary>
    /// Gives a M.O.A.B. to the player as a killstreak.
    /// </summary>
    /// <param name="player">The player to give the M.O.A.B. to</param>
    /// <param name="persistant">If true, the M.O.A.B. will stay in the player's killstreaks until they use it. If false, it will go away when the player dies.</param>
    public static void giveNuke(Entity player, bool persistant = true)
    {
        player.SetPlayerData("killstreaksState", "icons", 0, 30);
        player.GiveWeapon("killstreak_emp_mp", 0, false);
        player.SetActionSlot(4, "weapon", "killstreak_emp_mp");
        player.SetPlayerData("killstreaksState", "hasStreak", 0, true);
        int killstreak = player.GetField<int>("killstreak");
        if (killsToNuke <= 0)
            player.ShowHudSplash("nuke", 0, killstreak);
        else
            player.ShowHudSplash("nuke", 0, killsToNuke);
        string pre = getTeamPrefix(player);
        player.PlayLocalSound(pre + "1mc_achieve_moab");
        if (persistant)
        {
            player.SetField("hasNuke", player.GetField<int>("hasNuke") + 1);
            player.SetField("hasFauxNuke", 0);//Reset faux nuke count to give real nuke
        }
        else player.SetField("hasFauxNuke", 1);
    }

    /// <summary>
    /// Trys to use a M.O.A.B. with the given player as the user. This will count as using an actual M.O.A.B. to the player.
    /// </summary>
    /// <param name="player">The player to be the M.O.A.B. user</param>
    /// <param name="allowCancel">Whether the M.O.A.B. can be cancelled when the player dies. scr_nukeCancelMode must be higher than 0!</param>
    /// <returns>Returns true if the M.O.A.B. was successfully called in, false otherwise.</returns>
    public static bool tryUseNuke(Entity player, bool allowCancel = false)
    {
        if (nukeIncoming)
        {
            player.IPrintLnBold("M.O.A.B. already inbound!");
            return false;
        }
        if (nukeEmpTimeRemaining > 0 && level.GetField<bool>("teamNukeEMPed_" + player.SessionTeam) && isTeamBased)
        {
            player.IPrintLnBold("M.O.A.B. fallout still active for " + nukeEmpTimeRemaining.ToString() + " seconds.");
            return false;
        }
        else if (!isTeamBased && nukeEmpTimeRemaining > 0 && nukeInfo.HasField("player") && nukeInfo.GetField<Entity>("player") != player)
        {
            player.IPrintLnBold("M.O.A.B. fallout still active for " + nukeEmpTimeRemaining.ToString() + " seconds.");
            return false;
        }
        if (!player.IsPlayer)
        {
            Log.Write(LogLevel.Error, "Nuke attempted to call in from a non-player entity!");
            return false;
        }
        doNuke(player, allowCancel, false);

        player.SetField("hasNuke", player.GetField<int>("hasNuke") - 1);
        if (player.GetField<int>("hasNuke") < 0) player.SetField("hasNuke", 0);

        player.Notify("used_nuke");
        return true;
    }
    /// <summary>
    /// Trys to use a M.O.A.B. immediately with the given player as the user. This will immediately detonate the M.O.A.B., skipping the timers. This will count as using an actual M.O.A.B. to the player.
    /// </summary>
    /// <param name="player">The player to be the M.O.A.B. user</param>
    /// <returns>Returns true if the M.O.A.B. was successfully Function.Called in, false otherwise.</returns>
    public static bool tryUseNukeImmediate(Entity player)
    {
        if (!player.IsPlayer)
        {
            Log.Write(LogLevel.Error, "Nuke attempted to call in from a non-player entity!");
            return false;
        }
        doNuke(player, false, false);

        player.SetField("hasNuke", player.GetField<int>("hasNuke") - 1);
        if (player.GetField<int>("hasNuke") < 0) player.SetField("hasNuke", 0);

        player.Notify("used_nuke");
        return true;
    }

    private static void delaythread_nuke(int delay, Action func)
    {
        AfterDelay(delay, func);
    }

    private static void doNuke(Entity player, bool allowCancel, bool instant)
    {
        nukeInfo.SetField("player", player);
        nukeInfo.SetField("team", player.SessionTeam);
        nukeIncoming = true;
        GSCFunctions.SetDvar("ui_bomb_timer", 4);

        if (isTeamBased)
            teamPlayerCardSplash("used_nuke", player);
        else
            player.IPrintLnBold("Friendly M.O.A.B. inbound!");
        delaythread_nuke((nukeTimer * 1000) - 3300, new Action(nukeSoundIncoming));
        delaythread_nuke((nukeTimer * 1000), new Action(nukeSoundExplosion));
        if (nukeSloMotion) delaythread_nuke((nukeTimer * 1000), new Action(nukeSloMo));
        delaythread_nuke((nukeTimer * 1000), new Action(nukeEffects));
        delaythread_nuke((nukeTimer * 1000) + 250, new Action(nukeVision));
        delaythread_nuke((nukeTimer * 1000) + 1500, new Action(nukeDeath));
        if (destroyExplosives && !explosivesDestroyed) delaythread_nuke((nukeTimer * 1000) + 1600, destroyDestructables);
        //delaythread_nuke((nukeTimer * 1000) + 1500, new Action(nukeEarthquake));
        nukeAftermathEffect();
        update_ui_timers();

        if (cancelMode != 0 && allowCancel)
            cancelNukeOnDeath(player);

        //Entity clockObject = GSCFunctions.Spawn("script_origin", Vector3.Zero);
        //clockObject.Hide();

        int nukeTimer_loc = nukeTimer;
        OnInterval(1000, () =>
        {
            if (nukeTimer_loc > 0)
            {
                //clockObject.PlaySound("ui_mp_nukebomb_timer");
                GSCFunctions.PlaySoundAtPos(Vector3.Zero, "ui_mp_nukebomb_timer");
                nukeTimer_loc--;
                return true;
            }
            return false;
        });
    }

    private static void cancelNukeOnDeath(Entity player)
    {
        OnInterval(50, () =>
        {
            if (!player.IsAlive || !player.IsPlayer)
            {
                //if (Function.Call<int>(40, player) != 0 && cancelMode == 2)
                //{ //Do EMP stuff here, can't be arsed to recode _emp!
                //}

                GSCFunctions.SetDvar("ui_bomb_timer", 0);
                nukeIncoming = false;
                Notify("nuke_cancelled");
                return false;
            }
            if (nukeIncoming) return true;
            else return false;
        });
    }

    private static void nukeSoundIncoming()
    {
        foreach (Entity players in Players)
        {
            if (!players.IsPlayer) continue;
            players.PlayLocalSound("nuke_incoming");
        }
    }

    private static void nukeSoundExplosion()
    {
        foreach (Entity players in Players)
        {
            if (!players.IsPlayer) continue;
            players.PlayLocalSound("nuke_explosion");
            players.PlayLocalSound("nuke_wave");
        }
    }

    private static void nukeEffects()
    {
        GSCFunctions.SetDvar("ui_bomb_timer", 0);

        level.SetField("nukeDetonated", true);

        foreach (Entity player in Players)
        {
            if (!player.IsPlayer) continue;
            Vector3 playerForward = GSCFunctions.AnglesToForward(player.Angles);
            playerForward = new Vector3(playerForward.X, playerForward.Y, 0);
            playerForward.Normalize();

            int nukeDistance = 5000;

            Entity nukeEnt = GSCFunctions.Spawn("script_model", player.Origin + (playerForward * nukeDistance));
            nukeEnt.SetModel("tag_origin");
            nukeEnt.Angles = new Vector3(0, (player.Angles.Y + 180), 90);

            nukeEffect(nukeEnt, player);
        }
    }

    private static void nukeEffect(Entity nukeEnt, Entity player)
    {
        AfterDelay(50, () =>
            GSCFunctions.PlayFXOnTagForClients(effects[1], nukeEnt, "tag_origin", player));
    }

    private static void nukeAftermathEffect()
    {
        //OnNotify("spawning_intermission"
        Entity aftermathEnt = GSCFunctions.GetEnt("mp_global_intermission", "classname");
        Vector3 up = GSCFunctions.AnglesToUp(aftermathEnt.Angles);
        Vector3 right = GSCFunctions.AnglesToRight(aftermathEnt.Angles);

        GSCFunctions.PlayFX(effects[2], aftermathEnt.Origin, up, right);
    }

    private static void nukeSloMo()
    {
        GSCFunctions.SetSlowMotion(1f, .35f, .5f);
        AfterDelay(500, () =>
        {
            GSCFunctions.SetDvar("fixedtime", 1);
            foreach (Entity player in Players)
            {
                player.SetClientDvar("fixedtime", 2);
            }
        });
        OnInterval(50, () =>
        {
            GSCFunctions.SetSlowMotion(.25f, 1, 2f);
            AfterDelay(1500, () =>
            {
                foreach (Entity player in Players)
                {
                    player.SetClientDvar("fixedtime", 0);
                }
                GSCFunctions.SetDvar("fixedtime", 0);
            });
            if (nukeIncoming) return true;
            return false;
        });
    }

    private static void nukeVision()
    {
        level.SetField("nukeVisionInProgress", true);
        GSCFunctions.VisionSetNaked("mpnuke", 1);

        OnInterval(1000, () =>
        {
            GSCFunctions.VisionSetNaked("aftermath", 5);
            GSCFunctions.VisionSetPain("aftermath");
            if (nukeIncoming) return true;
            return false;
        });
    }

    private static void nukeDeath()
    {
        Notify("nuke_death");

        GSCFunctions.AmbientStop(1);

        foreach (Entity player in Players)
        {
            if (!player.IsPlayer) continue;
            if (isTeamBased)
            {
                if (nukeInfo.HasField("team") && player.SessionTeam == nukeInfo.GetField<string>("team")) continue;
            }
            else
            {
                if (nukeInfo.HasField("player") && player == nukeInfo.GetField<Entity>("player")) continue;
            }

            player.SetField("nuked", true);
            if (player.IsAlive)
                player.FinishPlayerDamage(nukeInfo.GetField<Entity>("player"), nukeInfo.GetField<Entity>("player"), 999999, 0, "MOD_EXPLOSIVE", "nuke_mp", player.Origin, player.Origin, "none", 0);
        }

        nuke_EMPJam();

        nukeIncoming = false;
    }
    /*
    private nukeEarthquake()
    {
        OnNotify("nuke_death", () =>
            {

            });
    }
     */

    private static void nuke_EMPJam()
    {
        if (isTeamBased)
        {
            Notify("EMP_JamTeam_axis");
            Notify("EMP_JamTeam_allies");
        }
        else Notify("EMP_JamPlayers");

        Notify("nuke_EMPJam");

        if (isTeamBased)
        {
            level.SetField("teamNukeEMPed_" + otherTeam[nukeInfo.GetField<string>("team")], true);
        }
        else
        {
            level.SetField("teamNukeEMPed_" + nukeInfo.GetField<string>("team"), true);
            //level.SetField("teamNukeEMPed_" + otherTeam[nukeInfo.GetField<string>("team")], true);
        }

        Notify("nuke_emp_update");

        keepNukeEMPTimeRemaining();

        AfterDelay(nukeEmpTimeout * 1000, () =>
        {
            if (isTeamBased)
            {
                level.SetField("teamNukeEMPed_" + otherTeam[nukeInfo.GetField<string>("team")], false);
            }
            else
            {
                level.SetField("teamNukeEMPed_" + nukeInfo.GetField<string>("team"), false);
                //level.SetField("teamNukeEMPed_" + otherTeam[nukeInfo.GetField<string>("team")], false);
            }

            foreach (Entity player in Players)
            {
                if (isTeamBased && player.SessionTeam == nukeInfo.GetField<string>("team"))
                    continue;

                player.SetField("nuked", false);
                player.SetEMPJammed(false);
            }

            Notify("nuke_emp_ended");
        });
    }

    private static void keepNukeEMPTimeRemaining()
    {
        Notify("keepNukeEMPTimeRemaining");

        nukeEmpTimeRemaining = nukeEmpTimeout;
        OnInterval(1000, () =>
        {
            nukeEmpTimeRemaining--;
            if (nukeEmpTimeRemaining > 0) return true;
            else return false;
        });
    }

    private static void nuke_EMPTeamTracker()
    {
        foreach (Entity player in Players)
        {
            if (!player.IsPlayer) continue;
            if (player.SessionTeam == "spectator")
                continue;

            if (isTeamBased)
            {
                if (nukeInfo.HasField("team") && player.SessionTeam == nukeInfo.GetField<string>("team"))
                    continue;
            }
            else
            {
                if (nukeInfo.HasField("player") && player == nukeInfo.GetField<Entity>("player"))
                    continue;
            }

            bool jam = level.GetField<bool>("teamNukeEMPed_" + player.SessionTeam);
            player.SetEMPJammed(jam);
        }
    }

    private static void update_ui_timers()
    {
        int nukeEndMilliseconds = (nukeTimer * 1000) + GSCFunctions.GetTime();
        GSCFunctions.SetDvar("ui_nuke_end_milliseconds", nukeEndMilliseconds);


    }

    private static string getTeamPrefix(Entity player)
    {
        string allies = GSCFunctions.GetMapCustom("allieschar");
        string axis = GSCFunctions.GetMapCustom("axischar");
        string team = player.SessionTeam;
        string ret;
        if (isTeamBased && player.SessionTeam == "allies")
            ret = GSCFunctions.TableLookup("mp/factiontable.csv", 0, allies, 7);
        else if (isTeamBased && player.SessionTeam == "axis")
            ret = GSCFunctions.TableLookup("mp/factiontable.csv", 0, axis, 7);
        else ret = "US_";
        return ret;
    }

    private static void teamPlayerCardSplash(string splash, Entity owner)
    {
        foreach (Entity players in Players)
        {
            if (!players.IsPlayer) continue;
            players.SetCardDisplaySlot(owner, 5);
            players.ShowHudSplash(splash, 1);

            if (isTeamBased && players.SessionTeam == owner.SessionTeam)
            {
                string pre = getTeamPrefix(players);
                players.PlayLocalSound(pre + "1mc_use_moab");
            }
            else if (isTeamBased && players.SessionTeam != owner.SessionTeam)
            {
                string pre = getTeamPrefix(players);
                players.PlayLocalSound(pre + "1mc_enemy_moab");
            }
        }
    }

    private static void destroyDestructables()
    {
        if (explosivesDestroyed) return;
        Entity attacker;
        if (nukeInfo.HasField("player")) attacker = nukeInfo.GetField<Entity>("player");
        else attacker = null;
        for (int i = 18; i < 2047; i++)
        {
            Entity ent = Entity.GetEntity(i);
            if (ent == null) continue;
            string entTarget = ent.GetField<string>("targetname");
            string model = ent.GetField<string>("model");
            if (entTarget == "destructable" || entTarget == "destructible" || entTarget == "explodable_barrel" || model == "vehicle_hummer_destructible")
            {
                if (attacker == null) attacker = ent;
                ent.Notify("damage", 999999, attacker, new Vector3(0, 0, 0), new Vector3(0, 0, 0), "MOD_EXPLOSIVE", "", "", "", 0, "frag_grenade_mp");
            }
        }
        explosivesDestroyed = true;
    }
}
