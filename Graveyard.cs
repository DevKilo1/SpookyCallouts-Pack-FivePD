using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;
using RemadeServices2._0;

namespace SpookyCallouts;


[CalloutProperties("Suspicious Person Graveyard Callout","DevKilo","1.0")]
[Guid("D74DD9AA-CE6A-4E0F-8834-4FED95BD3ED9")]
public class Graveyard : Callout
{
    private Vector4 callerSpawnPos =
        new Vector4(-1732.1187744141f, -167.54933166504f, 58.517681121826f, 197.76683044434f); 
    private Vector3 checkpoint1Pos = new Vector3(-1674.9787597656f, -222.32917785645f, 55.210872650146f);
    private Vector3 checkpoint2Pos = new Vector3(-1695.3231201172f, -215.20021057129f, 57.542961120605f);
    private Vector3 checkpoint3Pos = new Vector3(-1718.796875f, -213.50122070313f, 57.53483581543f);

    private Vector4 horror1SpawnPos =
        new Vector4(-1744.1405029297f, -218.02334594727f, 56.068538665771f, 270.66461181641f);

    private Vector3 horror1GoToPos = new Vector3(-1744.4562988281f, -220.64276123047f, 55.876647949219f);
    
    private Vector3 checkpoint4Pos = new Vector3(-1740.1936035156f, -206.53858947754f, 57.481594085693f); // Signs of something lurking within shadows
    private Vector3 checkpoint5Pos = new Vector3(-1751.1502685547f, -192.04856872559f, 57.54455947876f); // Caller runs up to you here. Caller explains what she saw and had to make the call.
    // This is where caller asks you to escort her to safety. You put her in your vehicle.
    private Vector3 checkpoint6Pos = new Vector3(-1735.3922119141f, -214.14698791504f, 57.088306427002f); // Investigate point 1
    public static EventHandlerDictionary eventHandlers;
    private int uuid;
    private int bucket = -1;
    private Vehicle playerVeh;
    private List<Ped> pedsToBring = new List<Ped>();
    private Blip investigateBlip = null;
    private bool calloutActive = false;
    private Ped horror1, caller;
    private bool escort = false;
    public Graveyard()
    {
        InitInfo(new Vector3(-1659.8714599609f, -227.19717407227f, 54.973621368408f));
        ShortName = "Suspicious Person";
        CalloutDescription = "A 9-1-1 report was received about a person in the graveyard. The caller sounded terrified and was quick to hang up. Investigate this peculiar situation.";
        ResponseCode = 3;
        StartDistance = 30f;
        eventHandlers["KiloMultiverse::CreateNewReturn"] += RegisterNewDimension;
        LoopThing();
    }

    private void LoopThing()
    {
        Tick += async () =>
        {
            if (Game.PlayerPed.IsInVehicle())
            {
                if (playerVeh == null || Game.PlayerPed.CurrentVehicle != playerVeh)
                {
                    Vehicle veh = Game.PlayerPed.CurrentVehicle;
                    if (veh.ClassType == VehicleClass.Emergency)
                        playerVeh = veh;   
                }
            }
        };
    }

    private async Task RegisterNewDimension(string pass, int b)
    {
        ////Debug.WriteLine("Received dimension thingy");
        if (pass == uuid.ToString())
        {
            bucket = b;
        }
        else
        {
            ////Debug.WriteLine("Received dimension that's not mine!");
        }
    }

    public override void OnBackupReceived(Player player)
    {
        //Debug.WriteLine("Backup called detected");
        Utilities.CancelBackup();
        Location = Game.PlayerPed.Position;
        ShowDialog("~f~You~s~: It seems something is interfering with my backup...", 5000, 20f);
        API.TaskPlayAnim(Game.PlayerPed.Handle, "random@domestic", "f_distressed_loop", 8f, 8f, 5000, 51, 1f, false, false,
            false);
    }

    public override void OnBackupCalled(int code)
    {
        //Debug.WriteLine("Backup called detected");
        Utilities.CancelBackup();
        Location = Game.PlayerPed.Position;
        ShowDialog("~f~You~s~: It seems something is interfering with my equipment...", 5000, 20f);
        API.TaskPlayAnim(Game.PlayerPed.Handle, "random@domestic", "f_distressed_loop", 8f, 8f, 5000, 51, 1f, false, false,
            false);
    }

    public override async Task OnAccept()
    {
        calloutActive = true;
        AcceptHandler();
    }

    private async Task SpawnEntities()
    {
        caller = await World.CreatePed(new(Utils.GetRandomPed()), (Vector3)callerSpawnPos, callerSpawnPos.W);
        caller.IsPersistent = true;
        while (caller == null || !caller.Exists() || caller.Gender != Gender.Male)
        {
            if (caller != null && caller.Exists())
            {
                caller.IsPersistent = false;
                caller.Delete();    
            }
            caller = await World.CreatePed(new(Utils.GetRandomPed()), (Vector3)callerSpawnPos, callerSpawnPos.W);
            caller.IsPersistent = true;
            await BaseScript.Delay(500);
        }
        caller.BlockPermanentEvents = true;
        caller.AlwaysKeepTask = true;

        caller.Task.Cower(-1);
    }

    private async Task AcceptHandler()
    {
        InitBlip();
    }

    public override async void OnStart(Ped closest)
    {
        ////Debug.WriteLine("OnStart");
        await Utils.RequestAnimDict("mp_facial");
        uuid = RandomUtils.GetRandomNumber(0, 999999999);
        BaseScript.TriggerServerEvent("KiloMultiverse::CreateNew", uuid);
        ////Debug.WriteLine("Send UUID: "+uuid.ToString());
        while (bucket == -1)
        {
            await BaseScript.Delay(100);
        }
        ////Debug.Write("Got new dimension: "+bucket);
        BaseScript.TriggerServerEvent("KiloMultiverse::SetPopulationInBucket", bucket, false, uuid);
        Vehicle[] allVehicles = World.GetAllVehicles();
        List<Vehicle> vehiclesInArea = new List<Vehicle>();
        foreach (Vehicle veh in allVehicles)
        {
            if (veh == null || !veh.Exists()) continue;
            if (veh.Position.DistanceTo(Location) <= StartDistance)
            {
                vehiclesInArea.Add(veh);
            }
        }
        // Send all vehicles over IF PLAYERS CAN BE SENT TOO
        // TO-DO
        //

        Ped[] allPeds = World.GetAllPeds();
        pedsToBring = new List<Ped>();
        foreach (var allPed in allPeds)
        {
            if (allPed == null || !allPed.Exists()) continue;
            if (allPed.IsPlayer) continue;
            if (allPed.Position.DistanceTo(Game.PlayerPed.Position) < 10f)
                pedsToBring.Add(allPed);
        }

        if (playerVeh != null)
        {
            BaseScript.TriggerServerEvent("KiloMultiverse::SendEntityToBucket", bucket,
                playerVeh.NetworkId, uuid);
        }
        
        foreach (var ped in pedsToBring)
        {
            BaseScript.TriggerServerEvent("KiloMultiverse::SendEntityToBucket", bucket, ped.NetworkId, uuid);
        }
        
        BaseScript.TriggerServerEvent("KiloMultiverse::SendPlayerToBucket", bucket, API.GetPlayerServerId(Game.Player.Handle), uuid);
        // Bring assignedPlayers with ?
        // TO-DO
        //
        ////Debug.WriteLine("Sent to new dimension");
        SpawnEntities();
        investigateBlip = World.CreateBlip(checkpoint1Pos);
        investigateBlip.Color = BlipColor.MichaelBlue;
        investigateBlip.Name = "Destination";
        
        while (Game.PlayerPed.Position.DistanceTo(checkpoint1Pos) > 1f && calloutActive)
        {
            DrawInvestigateMarker(checkpoint1Pos);
            await BaseScript.Delay(0);
        }

        investigateBlip.Position = checkpoint2Pos;
        while (Game.PlayerPed.Position.DistanceTo(checkpoint2Pos) > 1f && calloutActive)
        {
            DrawInvestigateMarker(checkpoint2Pos);
            await BaseScript.Delay(0);
        }

        RUnHorrorScene1();
        investigateBlip.Position = checkpoint3Pos;
        while (Game.PlayerPed.Position.DistanceTo(checkpoint3Pos) > 1f && calloutActive)
        {
            DrawInvestigateMarker(checkpoint3Pos);
            await BaseScript.Delay(0);
        }
        
        investigateBlip.Position = checkpoint4Pos;
        while (Game.PlayerPed.Position.DistanceTo(checkpoint4Pos) > 1f && calloutActive)
        {
            DrawInvestigateMarker(checkpoint4Pos);
            await BaseScript.Delay(0);
        }

        investigateBlip.Position = checkpoint5Pos;
        while (Game.PlayerPed.Position.DistanceTo(checkpoint5Pos) > 5f && calloutActive)
        {
            DrawInvestigateMarker(checkpoint5Pos);
            await BaseScript.Delay(0);
        }
        investigateBlip.Delete();
        caller.Task.ClearAll();
        caller.Task.RunTo(checkpoint5Pos);

        await Utils.WaitUntilPedIsAtPosition(Game.PlayerPed.Position, caller, 25f);
        SomeWhatSpatialAudio1();
        await Utils.WaitUntilPedIsAtPosition(Game.PlayerPed.Position, caller, 5f);
        caller.Task.ClearAll();
        caller.Task.TurnTo(Game.PlayerPed);

        /*investigateBlip.Position = checkpoint6Pos;
        while (Game.PlayerPed.Position.DistanceTo(checkpoint6Pos) > 1f && calloutActive)
        {
            DrawInvestigateMarker(checkpoint6Pos);
            await BaseScript.Delay(0);
        }*/
        while (!escort)
        {
            await BaseScript.Delay(100);
        }

        Location = Game.PlayerPed.Position;
        while (calloutActive)
        {
            ShowDialog("Press ~y~Y~s~ to begin the escort", 1, 10f);
            if (Game.IsControlJustReleased(0, Control.MpTextChatTeam))
                break;
            await BaseScript.Delay(1);
        }

        float speed = 1f;
        caller.Task.FollowToOffsetFromEntity(Game.PlayerPed, new(0f, 0.5f, 0f), speed, -1, 1f, true);
        investigateBlip.Position = checkpoint1Pos;
        OnTheWayAudio();
        while (Game.PlayerPed.Position.DistanceTo(checkpoint1Pos) > 1f  && calloutActive)
        {
            DrawInvestigateMarker(checkpoint1Pos);
            float newspeed = API.GetEntitySpeed(Game.PlayerPed.Handle); 
            if (newspeed != speed)
            {
                speed = newspeed;
                caller.Task.ClearAll();
                float stoppingRange = 0.5f * (speed);
                caller.Task.FollowToOffsetFromEntity(Game.PlayerPed, new(0f, 0.5f, 0f), speed, -1, stoppingRange, true);
            }
            await BaseScript.Delay(0);
        }
        investigateBlip.Delete();
        escort = false;
        await EscortFinishedAudio();
        caller.Task.ClearAll();
        caller.Task.WanderAround();
        caller.MarkAsNoLongerNeeded();
        caller = null;
        Location = Game.PlayerPed.Position;
        ShowDialog("You can go ~y~investigate~s~, or leave and ~g~code 4~s~", 5000, 20f);
        investigateBlip.Position = checkpoint2Pos;
        while (Game.PlayerPed.Position.DistanceTo(checkpoint2Pos) > 1f && calloutActive)
        {
            DrawInvestigateMarker(checkpoint2Pos);
            await BaseScript.Delay(0);
        }

        await RUnHorrorScene1();
        investigateBlip.Position = (Vector3)horror1SpawnPos;
        Location = Game.PlayerPed.Position;
        ShowDialog("~f~You~s~: Aha! I have you now! ~r~GET OVER HERE~s~!", 5000, 20f);
        while (Game.PlayerPed.Position.DistanceTo((Vector3)horror1SpawnPos) > 5f && calloutActive)
        {
            DrawInvestigateMarker(new(horror1SpawnPos.X, horror1SpawnPos.Y, horror1SpawnPos.Z + 2f));
            await BaseScript.Delay(0);
        }
        investigateBlip.Delete();
        ShowDialog("~f~You~s~: What..? He was just here! There's nowhere he could run to...", 5000, 20f);
        await BaseScript.Delay(5000);
        Vector3 pos = Game.PlayerPed.Position.Around(10f);
        await Horror1AttackScene(pos);
        HorrorSceneLoop();
    }

    private async Task<Vector3> Horror1AttackScene(Vector3 pos, bool damage = false)
    {
        horror1 = await World.CreatePed(new(PedHash.JohnnyKlebitz), pos, 0f);
        horror1.IsPersistent = true;
        horror1.AlwaysKeepTask = true;
        horror1.BlockPermanentEvents = true;
        horror1.IsVisible = false;
        horror1.IsInvincible = true;
        if (!damage)
            horror1.CanRagdoll = false;
        else
            horror1.CanRagdoll = true;
        if (!damage)
            API.SetAiMeleeWeaponDamageModifier(0f);
        await BaseScript.Delay(2000);
        horror1.IsVisible = true;
        horror1.Weapons.Give(WeaponHash.Machete, 255, true, true);
        horror1.Task.FightAgainst(Game.PlayerPed);
        BaseScript.TriggerServerEvent("Server:SoundToRadius",horror1.NetworkId,10f,"horror1",2f);
        API.ShakeCam(World.RenderingCamera.Handle, "VIBRATE_SHAKE", 2f);
        await BaseScript.Delay(5000);
        horror1.Task.FleeFrom(Game.PlayerPed);
        await BaseScript.Delay(7000);
        API.ShakeCam(World.RenderingCamera.Handle, "VIBRATE_SHAKE", 0f);
        Vector3 horror1Pos = horror1.Position;
        horror1.Delete();
        horror1 = null;
        if (!damage)
            API.SetAiMeleeWeaponDamageModifier(1f);
        return horror1Pos;
    }

    bool talking = false;
    private async Task MouthTalkAnim(Ped ped, int duration)
    {
        talking = true;
        API.PlayFacialAnim(ped.Handle,"mic_chatter","mp_facial");
        KeepTaskMouthAnim(ped);
        await BaseScript.Delay(duration);
        talking = false;
        API.PlayFacialAnim(ped.Handle, "mood_normal_1", "facials@gen_male@variations@normal");
    }

    private bool mouthcheck = false;

    private async Task KeepTaskMouthAnim(Ped ped)
    {
        while (talking && calloutActive)
        {
            DoMouthCheck(ped);
            await BaseScript.Delay(100);
        }
    }

    private async Task DoMouthCheck(Ped ped)
    {
        if (mouthcheck) return;
        mouthcheck = true;
        await BaseScript.Delay(11000);
        if (!talking) return;
        API.PlayFacialAnim(ped.Handle,"mic_chatter","mp_facial");
        mouthcheck = false;
    }

    private async Task OnTheWayAudio()
    {
        await BaseScript.Delay(5000);
        if (!escort) return;
        BaseScript.TriggerServerEvent("Server:SoundToRadius",caller.NetworkId,20f,"pussyaccusationvocals",0.5f);
        Location = Game.PlayerPed.Position;
        ShowDialog("~f~Caller~s~: I'm not a pussy, officer. I know what I saw. It was definitely not human!", 5000,
            20f);
        MouthTalkAnim(caller, 5000);
        await BaseScript.Delay(5000);
        if (!escort) return;
        Location = Game.PlayerPed.Position;
        BaseScript.TriggerServerEvent("Server:SoundToRadius",caller.NetworkId,20f,"figureoutwhatitisvocals",0.5f);
        ShowDialog("~f~Caller~s~: Maybe you can figure out what it is.", 2000, 20f);
        MouthTalkAnim(caller, 2000);
        await BaseScript.Delay(2000);
        if (!escort) return;
        Location = Game.PlayerPed.Position;
        BaseScript.TriggerServerEvent("Server:SoundToRadius",caller.NetworkId,20f,"doubtfulvocals",0.5f);
        ShowDialog("~f~Caller~s~: I-uh. I don't want to put this on you, though.", 2000, 20f);
        MouthTalkAnim(caller, 2000);
        await BaseScript.Delay(2000);
        if (!escort) return;
        Location = Game.PlayerPed.Position;
        ShowDialog("~f~Caller~s~: I don't think you will be able to solve it, to be honest.", 3000, 20f);
        MouthTalkAnim(caller, 3500);
        await BaseScript.Delay(3500);
        if (!escort) return;
        Location = Game.PlayerPed.Position;
        ShowDialog("~f~Caller~s~: You might not even make it out alive.", 3000, 20f);
        MouthTalkAnim(caller, 3000);
        await BaseScript.Delay(3000);
        if (!escort) return;
        Location = Game.PlayerPed.Position;
        BaseScript.TriggerServerEvent("Server:SoundToRadius",caller.NetworkId,20f,"yourchoicevocals",0.5f);
        ShowDialog("~f~Caller~s~: Anyways, that's your choice to make.", 1500, 20f);
        MouthTalkAnim(caller, 1500);
        await BaseScript.Delay(1500);
    }

    private async Task EscortFinishedAudio()
    {
        Location = Game.PlayerPed.Position;
        BaseScript.TriggerServerEvent("Server:SoundToRadius",caller.NetworkId,10f,"goodbyevocals",0.5f);
        ShowDialog("~f~Caller~s~: Thanks a lot officer! I'm going home, good luck!", 4000, 10f);
        MouthTalkAnim(caller, 4000);
        await BaseScript.Delay(4000);
        ShowDialog("~f~Caller~s~: See you around, hopefully! That's not a death threat, I swear!", 3000, 10f);
        MouthTalkAnim(caller, 3000);
        await BaseScript.Delay(3000);
        ShowDialog("~f~Caller~s~: I just mean... never mind. I'm just gonna go...", 6000, 10f);
        MouthTalkAnim(caller, 6000);
        await BaseScript.Delay(6000);
        ShowDialog("~f~Caller~s~: Good luck out there!", 1000, 10f);
        MouthTalkAnim(caller, 1000);
        await BaseScript.Delay(1000);
    }

    private async Task SomeWhatSpatialAudio1()
    {
        //329
        //107
        if (Game.PlayerPed.Heading < 329f && Game.PlayerPed.Heading > 107f)
        {
            ////Debug.WriteLine("Playing right");
            // Right
            BaseScript.TriggerServerEvent("Server:SoundToRadius",caller.NetworkId,40f,"officer2vocals_right",0.5f);
            Location = Game.PlayerPed.Position;
            ShowDialog("~f~Caller~s~: Officer, officer!", 2500, 10f);
            MouthTalkAnim(caller, 2500);
        }
        else
        {
            ////Debug.WriteLine("Playing left");
            // Left
            BaseScript.TriggerServerEvent("Server:SoundToRadius",caller.NetworkId,40f,"officer2vocals_left",0.5f);
            ShowDialog("~f~Caller~s~: Officer, officer!", 2500, 10f);
            MouthTalkAnim(caller, 2500);
        }
        
        Utils.RequestAnimDict("random@domestic");

        await BaseScript.Delay(2500);
        BaseScript.TriggerServerEvent("Server:SoundToRadius", caller.NetworkId, 20f, "thankofficervocals", 0.5f);
        ShowDialog("~f~Caller~s~: Thank you for coming officer!", 2000, 10f);
        MouthTalkAnim(caller, 2000);
        API.TaskPlayAnim(caller.Handle, "random@domestic", "f_distressed_loop", 8f, 8f, 6000, 51, 1f, false, false,
            false);
        //caller.Task.PlayAnimation("random@domestic", "f_distressed_loop", 8f, 8f, 6000, AnimationFlags.Loop, 1f);
        await BaseScript.Delay(2000);
        BaseScript.TriggerServerEvent("Server:SoundToRadius", caller.NetworkId, 20f, "getmeoutofherevocals", 0.5f);
        ShowDialog("~f~Caller~s~: There's something weird going on here man...", 1750, 10f);
        MouthTalkAnim(caller, 1750);
        await BaseScript.Delay(1750);
        ShowDialog("~f~Caller~s~: I don't know about you, I just want to get the HELL out of here!", 3000, 10f);
        caller.Task.PlayAnimation("oddjobs@assassinate@vice@hooker", "argue_a", 8f, 8f, 750, AnimationFlags.Loop, 1f);
        MouthTalkAnim(caller, 3000);
        await BaseScript.Delay(3000);
        ShowDialog("~f~Caller~s~: And I suggest you do the same.", 1500, 10f);
        caller.Task.PlayAnimation("car_2_mcs_1-6", "cs_devin_dual-6", 8f, 8f, 2000, AnimationFlags.Loop, 1f);
        MouthTalkAnim(caller, 2000);
        await BaseScript.Delay(2000);
        BaseScript.TriggerServerEvent("Server:SoundToRadius", caller.NetworkId, 20f, "didyouseeitquestionvocals", 0.5f);
        ShowDialog("~f~Caller~s~: There's like a demon here or something. Did you see it too?", 3000, 10f);
        caller.Task.PlayAnimation("random@domestic", "f_distressed_loop", 8f, 8f, 12000, AnimationFlags.Loop, 1f);
        MouthTalkAnim(caller, 3000);
        await BaseScript.Delay(3000);
        BaseScript.TriggerServerEvent("Server:SoundToRadius", caller.NetworkId, 20f, "horrorcrazyvocals", 0.5f);
        ShowDialog("~f~Caller~s~: It's like straight out of a horror movie, man! This is crazy.", 5000, 10f);
        MouthTalkAnim(caller, 5000);
        await BaseScript.Delay(5000);
        BaseScript.TriggerServerEvent("Server:SoundToRadius", caller.NetworkId, 20f, "parkinglotquestionvocals", 0.5f);
        ShowDialog("~f~Caller~s~: Please officer, can you escort me to the parking lot?", 4000, 10f);
        MouthTalkAnim(caller, 4000);
        await BaseScript.Delay(4000);
        escort = true;
    }

    private async Task HorrorSceneLoop()
    {
        //Debug.WriteLine("Loop running");
        int times = 0;
        List<string> randomDialogue = new List<string>()
        {
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: Maybe this isn't such a good idea anymore...",
            "~f~You~s~: If I don't leave now, maybe I'll get hurt.",
            "~f~You~s~: If I don't leave now, maybe I'll get hurt.",
            "~f~You~s~: If I don't leave now, maybe I'll get hurt.",
            "~f~You~s~: If I don't leave now, maybe I'll get hurt.",
            "~f~You~s~: This truly feels like a time loop... Am I in hell?",
            "~f~You~s~: This truly feels like a time loop... Am I in hell?",
            "~f~You~s~: This truly feels like a time loop... Am I in hell?",
            "~f~You~s~: It's real unfortunate my friends can't help me.",
            "~f~You~s~: Could really use someone...",
            "~f~You~s~: Could really use someone...",
            "~f~You~s~: Could really use someone..."
        };
        Vector3 pos = Game.PlayerPed.Position;
        while (calloutActive)
        {
            //Debug.WriteLine("Looping");
            Vector3 getpos;
            if (times > 5)
            {
                getpos = await Horror1AttackScene(pos.Around(10f), true);
                times = 0;   
            }
            else
            {
                getpos= await Horror1AttackScene(pos.Around(10f));
            }
            pos = getpos;
            times++;
            investigateBlip = World.CreateBlip(getpos);
            investigateBlip.Color = BlipColor.MichaelBlue;
            investigateBlip.Name = "Investigate";
            investigateBlip.Position = getpos;
            while (calloutActive && Game.PlayerPed.Position.DistanceTo(getpos) > 2f)
            {
                DrawInvestigateMarker(getpos);
                await BaseScript.Delay(0);
            }
            if (investigateBlip != null && investigateBlip.Exists())
                investigateBlip.Delete();
            await BaseScript.Delay(100);
            if (times > 5)
            {
                Location = Game.PlayerPed.Position;
                ShowDialog(randomDialogue[new Random().Next(randomDialogue.Count)], 5000, 20f);
                await BaseScript.Delay(5000);
            }
        }
    }

    private async Task HorrorScene1Thing()
    {
        while (Game.PlayerPed.Position.DistanceTo(horror1.Position) > 40f && calloutActive)
        {
            await BaseScript.Delay(100);
        }
        await BaseScript.Delay(3000);
        horror1.Task.PlayAnimation("move_weapon@rifle@generic", "walk_crouch", 8f, 8f, -1, AnimationFlags.Loop, 1f);
        horror1.Task.GoTo(horror1GoToPos, true);
        await Utils.WaitUntilPedIsAtPosition(horror1GoToPos, horror1, 1f);
        horror1.Delete();
        horror1 = null;
    }
    private async Task RUnHorrorScene1()
    {
        horror1 = await World.CreatePed(new(PedHash.JohnnyKlebitz), (Vector3)horror1SpawnPos, horror1SpawnPos.W);
        horror1.IsPersistent = true;
        horror1.AlwaysKeepTask = true;
        horror1.BlockPermanentEvents = true;
        horror1.IsVisible = false;
        horror1.IsInvincible = true;
        horror1.Task.PlayAnimation("move_weapon@rifle@generic", "idle_crouch", 8f, 8f, 5000, AnimationFlags.Loop, 1f);
        await BaseScript.Delay(1000);
        horror1.Weapons.Give(WeaponHash.Flashlight, 255, true, true);
        horror1.IsVisible = true;
        horror1.Task.AimAt(Game.PlayerPed, -1);
        BaseScript.TriggerServerEvent("Server:SoundToRadius",horror1.NetworkId,50f,"horror1",0.5f);
        HorrorScene1Thing();
    }

    private async Task DrawInvestigateMarker(Vector3 pos)
    {
        //API.DrawMarker(1, pos.X, pos.Y, pos.Z + 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 3, 144, 252, 0, false, false, 2,
          //  false, null, null, false);
        API.DrawMarker(20, pos.X, pos.Y, pos.Z + 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 3, 144, 252, 50, true,
            true, 2, false, null, null, false);
    }

    public override void OnCancelBefore()
    {
        calloutActive = false;
        escort = false;
        if (caller != null && caller.Exists())
            caller.Delete();
        if (bucket != -1)
        {
            if (playerVeh != null)
                BaseScript.TriggerServerEvent("KiloMultiverse::SendEntityToBucket", 0, playerVeh.NetworkId);
            foreach (var ped in pedsToBring)
            {
                if (ped != null && ped.Exists())
                    BaseScript.TriggerServerEvent("KiloMultiverse::SendEntityToBucket", 0, ped.NetworkId);
            }

            BaseScript.TriggerServerEvent("KiloMultiverse::SendPlayerToBucket", 0,
                API.GetPlayerServerId(Game.Player.Handle), uuid);
            BaseScript.TriggerServerEvent("KiloMultiverse::DeleteBucket", uuid, bucket);
            bucket = -1;
            API.RemoveAnimDict("mp_facial");
            API.RemoveAnimDict("random@domestic");
        }
    }

    public override Task<bool> CheckRequirements()
    {
        bool result = false;
        if (DateTime.Today.Month == 10)
        {
            if (DateTime.Today.Day >= 21)
            {
                result = true;   
            }
        }
        
        if (DateTime.Today.Month == 11)
        {
            if (DateTime.Today.Day <= 9)
            {
                result = true;
            }
        }
        return Task.FromResult(
            (World.CurrentDayTime > TimeSpan.FromHours(00) && World.CurrentDayTime < TimeSpan.FromHours(05)) && result);
    }
}

public class script : BaseScript
{
    public script()
    {
        Graveyard.eventHandlers = EventHandlers;
    }
}