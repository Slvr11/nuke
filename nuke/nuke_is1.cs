using System;
using InfinityScript;

/// <summary>
/// Contains the function holder to allow players to use M.O.A.B.s
/// </summary>
public class nuke_is1 : BaseScript
{
    /// <summary>
    /// Holds the functions to allow scripts to call the nuke functions.
    /// 
    /// 
    /// 
    /// NOTE: You must have the nuke.dll script loaded in the server to use this reference!!
    /// </summary>
    /// <remarks>You must have the nuke.dll script loaded to use this reference!!</remarks>
    public static nuke NukeFuncs;
    private int[] effects = new int[3];
    private int nukeTimer = 10;
    private int cancelMode = 0;
    private int nukeEmpTimeout = 60;
    private bool nukeIncoming = false;
    private Entity nukeInfo;
    private bool isTeamBased = true;
    private int nukeEmpTimeRemaining;
    private int killsToNuke = 25;
    private bool nukeChainsKills = false;
    private bool destroyExplosives = false;
    private bool explosivesDestroyed = false;
    //private Entity nukePlayer;
    //private string nukeTeam;
    private readonly Entity level = Entity.GetEntity(-1);
    public nuke_is1()
    {
        NukeFuncs = this;
        effects[0] = Function.Call<int>(303, "explosions/player_death_nuke");
        effects[1] = Function.Call<int>(303, "explosions/player_death_nuke_flash");
        effects[2] = Function.Call<int>(303, "dust/nuke_aftermath_mp");
        nukeTimer = Function.Call<int>(48, "scr_nukeTimer");
        cancelMode = Function.Call<int>(48, "scr_nukeCancelMode");
        Function.Call(44, "scr_killsToNuke", 25);
        Function.Call(44, "scr_killstreaksChainToNuke", 0);
        Function.Call(44, "scr_nukeDestroysExplosives", 0);
        nukeInfo = Function.Call<Entity>(368);
        //level.SetField("nukeDetonated", 0);
        level.SetField("teamNukeEMPed_axis", 0);
        level.SetField("teamNukeEMPed_allies", 0);
        level.SetField("teamNukeEMPed_none", 0);
        killsToNuke = Function.Call<int>(48, "scr_killsToNuke");
        nukeChainsKills = Function.Call<int>(48, "scr_killstreaksChainToNuke") != 0;
        destroyExplosives = Function.Call<int>(48, "scr_nukeDestroysExplosives") != 0;


        string gametype = Function.Call<string>(47, "g_gametype");
        if (gametype == "dm" || gametype == "gun" || gametype == "oic" || gametype == "jugg")
            isTeamBased = false;

        PlayerConnected += OnPlayerConnected;
    }

    private void OnPlayerConnected(Entity player)
    {
        player.SpawnedPlayer += () => OnPlayerSpawned(player);
        player.OnNotify("weapon_change", (p, weapon) => OnWeaponChange(p, (string)weapon));
        player.SetField("hasNuke", 0);
        player.SetField("hasFauxNuke", 0);
        player.SetField("killstreak", 0);
        //Set vision from connect
        if (level.HasField("nukeDetonated"))
            player.Call(33436, "aftermath", 0);
    }

    private void OnPlayerSpawned(Entity player)
    {
        player.AfterDelay(50, (p) =>
        {
            if (level.GetField<int>("teamNukeEMPed_" + player.GetField<string>("sessionteam")) == 1)
            {
                if (isTeamBased)
                    player.Call(33276, true);
                else
                {
                    if (!nukeInfo.HasField("player") || (nukeInfo.HasField("player") && player != nukeInfo.GetField<Entity>("player") && nukeEmpTimeRemaining > 0))
                        player.Call(33276, true);
                }
            }
            if (player.GetField<int>("hasNuke") > 0)
            {
                giveNuke(player, false);
            }
        });
        player.SetField("killstreak", 0);
        if (level.HasField("nukeDetonated"))
            player.Call(33436, "aftermath", 0);
    }

    private void OnWeaponChange(Entity player, string weapon)
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
                player.AfterDelay(1000, (p) => p.TakeWeapon("killstreak_emp_mp"));
                player.Call(33306, "killstreaksState", "icons", 0, "");
                //Function.Call("setActionSlot", 4, "weapon", "killstreak_emp_mp");
                player.Call(33306, "killstreaksState", "hasStreak", 0, false);
            }
        }

        //Fix the bug were the game would remove the caller after a certain time
        if (player.GetField<int>("hasFauxNuke") > 0 && player.GetField<int>("hasNuke") > 0)
        {
            player.Call(33487, "killstreak_emp_mp", 0, false);
            player.Call(33481, 4, "weapon", "killstreak_emp_mp");
        }
    }

    public override void OnPlayerKilled(Entity player, Entity inflictor, Entity attacker, int damage, string mod, string weapon, Vector3 dir, string hitLoc)
    {
        level.AfterDelay(100, (l) =>
        {
            if (!attacker.IsPlayer || attacker == player) return;
            if (!nukeChainsKills && (weapon == "cobra_player_minigun_mp"        // Chopper Gunner
                || weapon == "artillery_mp"             // Precision Airstrike
                || weapon == "stealth_bomb_mp"              // Stealth Bomber
                || weapon == "pavelow_minigun_mp"           // Pave Low
                || weapon == "sentry_minigun_mp"            // Sentry Gun
                || weapon == "harrier_20mm_mp"              // Harrier Strike
                || weapon == "ac130_105mm_mp"               // AC130
                || weapon == "ac130_40mm_mp"                // AC130
                || weapon == "ac130_25mm_mp"                // AC130
                || weapon == "remotemissile_projectile_mp"  // Hellfire
                || weapon == "cobra_20mm_mp"                // Attack Helicopter
                || weapon == "nuke_mp"                      // Nuke		
                || weapon == "apache_minigun_mp"            // littlebird strafe
                || weapon == "littlebird_guard_minigun_mp"  // littlebird guard/support
                || weapon == "uav_strike_marker_mp"     // uav strike
                || weapon == "osprey_minigun_mp"            // escort airdrop
                || weapon == "strike_marker_mp"         // littlebird support
                || weapon == "a10_30mm_mp"                  // a10 support
                || weapon == "manned_minigun_turret_mp" // minigun turret
                || weapon == "manned_gl_turret_mp"          // GL turret
                || weapon == "airdrop_trap_explosive_mp"    // airdrop trap
                || weapon == "uav_strike_projectile_mp" // uav strike
                || weapon == "remote_mortar_missile_mp" // remote mortar
                || weapon == "manned_littlebird_sniper_mp"  // heli sniper	
                || weapon == "iw5_m60jugg_mp"               // juggernaut assault primary	
                || weapon == "iw5_mp412jugg_mp"         // juggernaut assault secondary	
                || weapon == "iw5_riotshieldjugg_mp"        // juggernaut support primary	
                || weapon == "iw5_usp45jugg_mp"         // juggernaut support secondary	
                || weapon == "remote_turret_mp"         // remote turret
                || weapon == "osprey_player_minigun_mp" // osprey gunner
                || weapon == "deployable_vest_marker_mp"    // deployable vest
                || weapon == "ugv_turret_mp"                // remote tank turret
                || weapon == "ugv_gl_turret_mp"         // remote tank gl turret
                || weapon == "remote_tank_projectile_mp"    // remote tank missile
                || weapon == "uav_remote_mp")) return;
            if (isTeamBased && player.GetField<string>("sessionteam") != attacker.GetField<string>("sessionteam"))
            {
                attacker.SetField("killstreak", attacker.GetField<int>("killstreak") + 1);
            }
            else if (!isTeamBased)
            {
                attacker.SetField("killstreak", attacker.GetField<int>("killstreak") + 1);
            }

            bool hasHardline = attacker.Call<int>(33394, "specialty_hardline") == 1;
            if (hasHardline && (attacker.GetField<int>("killstreak") == killsToNuke - 1 && killsToNuke > 1))
                giveNuke(attacker, true);
            else if (hasHardline && attacker.GetField<int>("killstreak") == killsToNuke && killsToNuke == 1)
                giveNuke(attacker, true);
            else if (!hasHardline && attacker.GetField<int>("killstreak") == killsToNuke)
                giveNuke(attacker, true);
        });
    }

    private string getTeamPrefix(Entity player)
    {
        string allies = Function.Call<string>(221, "allieschar");
        string axis = Function.Call<string>(221, "axischar");
        string team = player.GetField<string>("sessionteam");
        string ret;
        if (isTeamBased && player.GetField<string>("sessionteam") == "allies")
            ret = Function.Call<string>(398, "mp/factiontable.csv", 0, allies, 7);
        else if (isTeamBased && player.GetField<string>("sessionteam") == "axis")
            ret = Function.Call<string>(398, "mp/factiontable.csv", 0, axis, 7);
        else ret = "US_";
        return ret;
    }

    private bool mayDropWeapon(string weapon)
    {
        if (weapon == "none")
            return false;

        if (weapon.Contains("ac130"))
            return false;

        if (weapon.Contains("killstreak"))
            return false;

        string invType = Function.Call<string>("WeaponInventoryType", weapon);
        if (invType != "primary")
            return false;

        return true;
    }

    /// <summary>
    /// Gives a M.O.A.B. to the player as a killstreak.
    /// </summary>
    /// <param name="player">The player to give the M.O.A.B. to</param>
    /// <param name="persistant">If true, the M.O.A.B. will stay in the player's killstreaks until they use it. If false, it will go away when the player dies.</param>
    public void giveNuke(Entity player, bool persistant = true)
    {
        player.Call(33306, "killstreaksState", "icons", 0, getKillstreakIndex("nuke"));
        player.Call(33487, "killstreak_emp_mp", 0, false);
        player.Call(33481, 4, "weapon", "killstreak_emp_mp");
        player.Call(33306, "killstreaksState", "hasStreak", 0, true);
        int killstreak = player.GetField<int>("killstreak");
        if (killsToNuke <= 0)
            player.Call(33392, "nuke", 0, killstreak);
        else
            player.Call(33392, "nuke", 0, killsToNuke);
        string pre = getTeamPrefix(player);
        player.Call(33466, pre + "1mc_achieve_moab");
        if (persistant)
        {
            player.SetField("hasNuke", player.GetField<int>("hasNuke") + 1);
            player.SetField("hasFauxNuke", 0);//Reset faux nuke count to give real nuke
        }
        else player.SetField("hasFauxNuke", 1);
    }

    private int getKillstreakIndex(string streakName)
    {
        int ret = 0;
        ret = Function.Call<int>(402, "mp/killstreakTable.csv", 1, streakName) - 1;

        return ret;
    }

    /// <summary>
    /// Trys to use a M.O.A.B. with the given player as the user. This will count as using an actual M.O.A.B. to the player.
    /// </summary>
    /// <param name="player">The player to be the M.O.A.B. user</param>
    /// <param name="allowCancel">Whether the M.O.A.B. can be cancelled when the player dies. scr_nukeCancelMode must be higher than 0!</param>
    /// <returns>Returns true if the M.O.A.B. was successfully called in, false otherwise.</returns>
    public bool tryUseNuke(Entity player, bool allowCancel = false)
    {
        if (nukeIncoming)
        {
            player.Call(33344, "M.O.A.B. already inbound!");
            return false;
        }
        if (nukeEmpTimeRemaining > 0 && level.GetField<int>("teamNukeEMPed_" + player.GetField<string>("sessionteam")) == 1 && isTeamBased)
        {
            player.Call(33344, "M.O.A.B. fallout still active for " + nukeEmpTimeRemaining.ToString() + " seconds.");
            return false;
        }
        else if (!isTeamBased && nukeEmpTimeRemaining > 0 && nukeInfo.HasField("player") && nukeInfo.GetField<Entity>("player") != player)
        {
            player.Call(33344, "M.O.A.B. fallout still active for " + nukeEmpTimeRemaining.ToString() + " seconds.");
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
    public bool tryUseNukeImmediate(Entity player)
    {
        /*
        if (nukeIncoming)
        {
            Function.Call(33344, "M.O.A.B. already inbound!");
            return false;
        }
        if (nukeEmpTimeRemaining > 0 && level.GetField<int>("teamNukeEMPed_" + player.GetField<string>("sessionteam")) == 1 && isTeamBased)
        {
            Function.Call(33344, "M.O.A.B. fallout still active for " + nukeEmpTimeRemaining.ToString() + " seconds.");
            return false;
        }
        else if (!isTeamBased && nukeEmpTimeRemaining > 0 && nukeInfo.HasField("player") && nukeInfo.GetField<Entity>("player") != player)
        {
            Function.Call(33344, "M.O.A.B. fallout still active for " + nukeEmpTimeRemaining.ToString() + " seconds.");
            return false;
        }
         */
        if (!player.IsPlayer)
        {
            Log.Write(LogLevel.Error, "Nuke attempted to call in from a non-player entity!");
            return false;
        }
        doNuke(player, false, true);

        player.SetField("hasNuke", player.GetField<int>("hasNuke") - 1);
        if (player.GetField<int>("hasNuke") < 0) player.SetField("hasNuke", 0);

        player.Notify("used_nuke");
        return true;
    }

    private void delaythread_nuke(int delay, Action func)
    {
        AfterDelay(delay, func);
    }

    private void doNuke(Entity player, bool allowCancel, bool instant)
    {
        nukeInfo.SetField("player", player);
        nukeInfo.SetField("team", player.GetField<string>("sessionteam"));
        nukeIncoming = true;
        Function.Call(42, "ui_bomb_timer", 4);

        if (isTeamBased)
            teamPlayerCardSplash("used_nuke", player, player.GetField<string>("sessionteam"));
        else
            player.Call(33344, "Friendly M.O.A.B. inbound!");
        if (instant)
        {
            //delaythread_nuke(1000 - 3300, new Action(nukeSoundIncoming));
            nukeSoundExplosion();
            nukeSloMo();
            nukeEffects();
            delaythread_nuke(250, new Action(nukeVision));
            delaythread_nuke(1500, new Action(nukeDeath));
            if (destroyExplosives && !explosivesDestroyed) delaythread_nuke(1600, destroyDestructables);
            nukeAftermathEffect();
        }
        else
        {
            delaythread_nuke((nukeTimer * 1000) - 3300, new Action(nukeSoundIncoming));
            delaythread_nuke((nukeTimer * 1000), new Action(nukeSoundExplosion));
            delaythread_nuke((nukeTimer * 1000), new Action(nukeSloMo));
            delaythread_nuke((nukeTimer * 1000), new Action(nukeEffects));
            delaythread_nuke((nukeTimer * 1000) + 250, new Action(nukeVision));
            delaythread_nuke((nukeTimer * 1000) + 1500, new Action(nukeDeath));
            if (destroyExplosives && !explosivesDestroyed) delaythread_nuke((nukeTimer * 1000) + 1600, destroyDestructables);
            //delaythread_nuke((nukeTimer * 1000) + 1500, new Action(nukeEarthquake));
            nukeAftermathEffect();
            update_ui_timers();

            if (cancelMode != 0 && allowCancel)
                cancelNukeOnDeath(player);

            Entity clockObject = Function.Call<Entity>(85, "script_origin", new Vector3(0, 0, 0));
            clockObject.Call(32848);

            int nukeTimer_loc = nukeTimer;
            clockObject.OnInterval(1000, (c) =>
            {
                if (nukeTimer_loc > 0)
                {
                    c.Call(32915, "ui_mp_nukebomb_timer");
                    nukeTimer_loc--;
                    return true;
                }
                return false;
            });
        }
    }

    private void cancelNukeOnDeath(Entity player)
    {
        player.OnInterval(50, (p) =>
        {
            if (!p.IsAlive || !p.IsPlayer)
            {
                //if (Function.Call<int>(40, player) != 0 && cancelMode == 2)
                //{ //Do EMP stuff here, can't be arsed to recode _emp!
                //}

                Function.Call(42, "ui_bomb_timer", 0);
                nukeIncoming = false;
                Notify("nuke_cancelled");
                return false;
            }
            if (nukeIncoming) return true;
            else return false;
        });
    }

    private void nukeSoundIncoming()
    {
        foreach (Entity players in Players)
        {
            if (!players.IsPlayer) continue;
            players.Call(33466, "nuke_incoming");
        }
    }

    private void nukeSoundExplosion()
    {
        foreach (Entity players in Players)
        {
            if (!players.IsPlayer) continue;
            players.Call(33466, "nuke_explosion");
            players.Call(33466, "nuke_wave");
        }
    }

    private void nukeEffects()
    {
        Function.Call(42, "ui_bomb_timer", 0);

        level.SetField("nukeDetonated", 1);

        foreach (Entity player in Players)
        {
            if (!player.IsPlayer) continue;
            Vector3 playerForward = Function.Call<Vector3>(252, player.GetField<Vector3>("angles"));
            playerForward = new Vector3(playerForward.X, playerForward.Y, 0);
            playerForward = Function.Call<Vector3>(246, playerForward);

            int nukeDistance = 5000;

            Entity nukeEnt = Function.Call<Entity>(85, "script_model", player.Origin + (playerForward * nukeDistance));
            nukeEnt.Call(32929, "tag_origin");
            nukeEnt.SetField("angles", new Vector3(0, (player.GetField<Vector3>("angles").Y + 180), 90));

            nukeEffect(nukeEnt, player);
        }
    }

    private void nukeEffect(Entity nukeEnt, Entity player)
    {
        nukeEnt.AfterDelay(50, (ent) =>
            Function.Call(310, effects[1], ent, "tag_origin", player));
    }

    private void nukeAftermathEffect()
    {
        //OnNotify("spawning_intermission"
        Entity aftermathEnt = Function.Call<Entity>(365, "mp_global_intermission", "classname");
        Vector3 up = Function.Call<Vector3>(250, aftermathEnt.GetField<Vector3>("angles"));
        Vector3 right = Function.Call<Vector3>(251, aftermathEnt.GetField<Vector3>("angles"));

        Function.Call(304, effects[2], aftermathEnt.Origin, up, right);
    }

    private void nukeSloMo()
    {
        Function.Call(151, 1f, .25f, .5f);
        OnNotify("nuke_death", () => Function.Call(151, .25f, 1, 2f));
    }

    private void nukeVision()
    {
        level.SetField("nukeVisionInProgress", 1);
        Function.Call(290, "mpnuke", 3);

        OnNotify("nuke_death", () =>
        {
            Function.Call(290, "aftermath", 5);
            Function.Call(218, "aftermath");
        });
    }

    private void nukeDeath()
    {
        Notify("nuke_death");

        Function.Call(293, 1);

        foreach (Entity player in Players)
        {
            if (!player.IsPlayer) continue;
            if (isTeamBased)
            {
                if (nukeInfo.HasField("team") && player.GetField<string>("sessionteam") == nukeInfo.GetField<string>("team")) continue;
            }
            else
            {
                if (nukeInfo.HasField("player") && player == nukeInfo.GetField<Entity>("player")) continue;
            }

            player.SetField("nuked", 1);
            if (player.IsAlive)
                player.Call(33340, nukeInfo.GetField<Entity>("player"), nukeInfo.GetField<Entity>("player"), 999999, 0, "MOD_EXPLOSIVE", "nuke_mp", player.Origin, player.Origin, "none", 0, 0);
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

    private void nuke_EMPJam()
    {
        if (isTeamBased)
        {
            Notify("EMP_JamTeam" + "axis");
            Notify("EMP_JamTeam" + "allies");
        }
        else Notify("EMP_JamPlayers");

        Notify("nuke_EMPJam");

        if (isTeamBased)
        {
            string getOtherTeam;
            if (nukeInfo.GetField<string>("team") == "allies") getOtherTeam = "axis";
            else getOtherTeam = "axis";
            level.SetField("teamNukeEMPed_" + getOtherTeam, 1);
        }
        else
        {
            string getOtherTeam;
            if (nukeInfo.GetField<string>("team") == "allies") getOtherTeam = "axis";
            else getOtherTeam = "axis";
            level.SetField("teamNukeEMPed_" + nukeInfo.GetField<string>("team"), 1);
            level.SetField("teamNukeEMPed_" + getOtherTeam, 1);
        }

        Notify("nuke_emp_update");

        keepNukeEMPTimeRemaining();

        AfterDelay(nukeEmpTimeout * 1000, () =>
        {
            if (isTeamBased)
            {
                string getOtherTeam;
                if (nukeInfo.GetField<string>("team") == "allies") getOtherTeam = "axis";
                else getOtherTeam = "axis";
                level.SetField("teamNukeEMPed_" + getOtherTeam, 0);
            }
            else
            {
                string getOtherTeam;
                if (nukeInfo.GetField<string>("team") == "allies") getOtherTeam = "axis";
                else getOtherTeam = "axis";
                level.SetField("teamNukeEMPed_" + nukeInfo.GetField<string>("team"), 0);
                level.SetField("teamNukeEMPed_" + getOtherTeam, 0);
            }

            foreach (Entity player in Players)
            {
                if (isTeamBased && player.GetField<string>("sessionteam") == nukeInfo.GetField<string>("team"))
                    continue;

                player.SetField("nuked", 0);
                player.Call(33276, false);
            }

            Notify("nuke_emp_ended");
        });
    }

    private void keepNukeEMPTimeRemaining()
    {
        Notify("keepNukeEMPTimeRemaining");

        nukeEmpTimeRemaining = nukeEmpTimeout;
        level.OnInterval(1000, (l) =>
        {
            nukeEmpTimeRemaining--;
            if (nukeEmpTimeRemaining > 0) return true;
            else return false;
        });
    }

    private void nuke_EMPTeamTracker()
    {
        foreach (Entity player in Players)
        {
            if (!player.IsPlayer) continue;
            if (player.GetField<string>("sessionteam") == "spectator")
                continue;

            if (isTeamBased)
            {
                if (nukeInfo.HasField("team") && player.GetField<string>("sessionteam") == nukeInfo.GetField<string>("team"))
                    continue;
            }
            else
            {
                if (nukeInfo.HasField("player") && player == nukeInfo.GetField<Entity>("player"))
                    continue;
            }

            bool jam = level.GetField<int>("teamNukeEMPed_" + player.GetField<string>("sessionteam")) != 0;
            player.Call(33276, jam);
        }
    }

    private void update_ui_timers()
    {
        int nukeEndMilliseconds = (nukeTimer * 1000) + Function.Call<int>(51);
        Function.Call(42, "ui_nuke_end_milliseconds", nukeEndMilliseconds);


    }

    private void teamPlayerCardSplash(string splash, Entity owner, string ownerTeam)
    {
        foreach (Entity players in Players)
        {
            if (!players.IsPlayer) continue;
            players.Call(33422, owner, 5);
            players.Call(33392, splash, 1);

            if (isTeamBased && players.GetField<string>("sessionteam") == owner.GetField<string>("sessionteam"))
            {
                string pre = getTeamPrefix(players);
                players.Call(33466, pre + "1mc_use_moab");
            }
            else if (isTeamBased && players.GetField<string>("sessionteam") != owner.GetField<string>("sessionteam"))
            {
                string pre = getTeamPrefix(players);
                players.Call(33466, pre + "1mc_enemy_moab");
            }
        }
    }

    private void destroyDestructables()
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
