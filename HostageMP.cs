using System.Net;
using System.Runtime.InteropServices;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;
using Utils = RemadeServices2._0.Utils;

namespace SpookyCallouts;

[Guid("7265A657-17F2-430F-8C4B-C851D8BF83F9")]
[CalloutProperties("KidnappingMP (Spooky)", "DevKilo", "1.0.0")]
public class Hostage : Callout
{
    private Vehicle van, bus, trash;
    private Ped driver, trashman;
    private List<Ped> occupants = [];

    private Blip calloutRadius;

    private Vector3 beforeTrap = new Vector3(-531.33282470703f, 76.233497619629f, 56.745029449463f);

    private Vector4 busTrapPos = new Vector4(-475.83941650391f, 68.26904296875f, 58.427417755127f, 0.96272855997086f);
    private Vector4 trashSpawn = new Vector4(-503.64971923828f, 84.364524841309f, 56.138195037842f, 90.352363586426f);

    private Vector4 trashmanPos = new Vector4(-497.53311157227f, 79.880187988281f, 55.922142028809f, 21.68872833252f);

    private Vector3 duringTrap = new Vector3(-469.97265625f, 73.232643127441f, 58.661659240723f);

    private Vector3 duringTrap2 = new Vector3(-465.08413696289f, 64.392013549805f, 58.661449432373f);

    private Vector3 afterTrap = new Vector3(-463.52752685547f, 64.177223205566f, 58.661483764648f);

    private Vector4 trashTrapPos = new Vector4(-537.54956054688f, 77.054161071777f, 56.136791229248f, 173.67950439453f);

    private Guid _guid = new("7265A657-17F2-430F-8C4B-C851D8BF83F9");

    public Hostage()
    {
        Location = Utilities.GetRandomPosInPlayerDepartmentArea().ClosestParkedCarPlacement();
        CheckLocation();
        ShortName = "Reported Kidnapping (MP)";
        CalloutDescription = "There are reports of a short male speeding around Los Santos, kidnapping people.";
        ResponseCode = 3;
        StartDistance = 100f;
    }

    private async void CheckLocation()
    {
        if (Location.DistanceTo(Game.PlayerPed.Position) > 1000f)
        {
            Location = await Utils.GetSpawnPositionPlayerCantSee(Game.PlayerPed.Position, 600f);
            Debug.WriteLine($"New location {API.GetNameOfZone(Location.X, Location.Y, Location.Z)}");
            InitInfo(Location);
        }
    }

    private void UpdateBlip()
    {
        float scale = 600f;
        if (calloutRadius is not null)
        {
            Debug.WriteLine("Not null");
            calloutRadius.Position = Location.Around(scale / 4);
        }
        else
        {
            Debug.WriteLine("Null");
            calloutRadius = World.CreateBlip(Location, scale);
            calloutRadius.Alpha = 80;
            calloutRadius.Color = BlipColor.Red;
            SetState<int>(van, "Spooky:HostageMP:Owner", Game.PlayerPed.NetworkId, true);
            SetState<float>(van, "Spooky:HostageMP:SearchBlip:Scale", scale, true);
            SetState<Vector3>(van, "Spooky:HostageMP:SearchBlip:Position", calloutRadius.Position, true);
        }
    }

    private void SetState<T>(Entity entity, string key, dynamic value, bool replicated)
    {
        if (entity is not null)
        {
            if (entity.State.Get("Spooky:HostageMP:Active") is null)
            {
                entity.State.Set("Spooky:HostageMP:Active", true, true);
            }

            entity.State.Set(key, (T)value, replicated);
        }
    }

    private async void AcceptHandler()
    {
        UpdateData();
        UpdateBlip();
        await BaseScript.Delay(1000);
        if (Game.PlayerPed.IsInVehicle())
        {
            Game.PlayerPed.CurrentVehicle.Position =
                new Vector3(calloutRadius.Position.X, calloutRadius.Position.Y, StartDistance);
            await BaseScript.Delay(1000);
            Game.PlayerPed.CurrentVehicle.Position = calloutRadius.Position.ClosestParkedCarPlacement();
        }
        else
        {
            Game.PlayerPed.Position = new Vector3(calloutRadius.Position.X, calloutRadius.Position.Y, StartDistance);
            await BaseScript.Delay(1000);
            Game.PlayerPed.Position = calloutRadius.Position.ClosestParkedCarPlacement();
        }
        //calloutRadius = World.CreateBlip(Location, StartDistance);
    }

    private bool liveLocationUpdate = false;
    private int updateInterval = 10000;

    private async Task UpdateLocation()
    {
        if (liveLocationUpdate) return;
        liveLocationUpdate = true;
        while (liveLocationUpdate)
        {
            Location = van.Position;
            UpdateData();
            UpdateBlip();
            await BaseScript.Delay(updateInterval);
        }
    }

    private bool wanderDrive = true;

    private async Task HandleDriveWander()
    {
        float speed = 10f;
        while (wanderDrive)
        {
            var v = World.GetAllVehicles().FirstOrDefault(v =>
                v.Position.DistanceTo(van.Position) < 50f && v.Speed != speed && v.Driver is not null && v.Speed > 5f);
            if (v is not null)
                speed = v.Speed;
            driver.MaxDrivingSpeed = speed;
            driver.Task.CruiseWithVehicle(van, speed, 424);
            await BaseScript.Delay(10000);
        }
    }

    public override Task OnAccept()
    {
        AcceptHandler();
        return base.OnAccept();
    }

    public override async void OnStart(Ped closest)
    {
        driver = await Utils.SpawnPedOneSync(PedHash.Clown01SMY, await Utils.GetSpawnPositionPlayerCantSee(Location));
        van = await Utils.SpawnVehicleOneSync(VehicleHash.Speedo2,
            driver.Position.Around(5f).ClosestParkedCarPlacement());
        var b = driver.AttachBlip();
        SetState<bool>(driver, "Spooky:HostageMP:Blip", true, true);
        SetState<BlipColor>(driver, "Spooky:HostageMP:Blip:Color", b.Color, true);
        driver.Task.WarpIntoVehicle(van, VehicleSeat.Driver);
        await BaseScript.Delay(200);
        _ = HandleDriveWander();
        UpdateLocation();
        OnTick();
        bus = await Utils.SpawnVehicleOneSync(VehicleHash.Tourbus, (Vector3)busTrapPos, busTrapPos.W);
        trash = await Utils.SpawnVehicleOneSync(VehicleHash.Trash, (Vector3)trashSpawn, trashSpawn.W);
        trashman = await Utils.SpawnPedOneSync(PedHash.GarbageSMY, (Vector3)trashmanPos, true, trashmanPos.W);
        Utils.KeepTaskPlayAnimation(trashman, "amb@prop_human_bum_shopping_cart@male@idle_a", "idle_c");
        var trashdriver = await Utils.SpawnPedOneSync(PedHash.GarbageSMY, ((Vector3)trashmanPos).Around(5f));
        trashdriver.SetIntoVehicle(trash, VehicleSeat.Driver);
        var busdriver = await Utils.SpawnPedOneSync(PedHash.Gardener01SMM, trash.Position.Around(2f));
        busdriver.SetIntoVehicle(bus, VehicleSeat.Driver);
        base.OnStart(closest);
    }

    private bool arrived = false;

    void OnTick()
    {
        bool db = false;
        Tick += async () =>
        {
            if (db) return;
            db = true;
            if (!arrived)
            {
                Debug.WriteLine("Still going");
                if (!pointing && Utils.CanEntitySeeEntity(Game.PlayerPed, van))
                {
                    OnSightGained();
                }

                if (pointing && !Utils.CanEntitySeeEntity(Game.PlayerPed, van))
                {
                    OnSightLost();
                }
            }

            await BaseScript.Delay(100);
            db = false;
        };
    }

    private bool pointing = false;
    private Utils.Waypoint wp = null;

    private bool once = false;

    public async void OnSightGained()
    {
        pointing = true;
        //await Utils.PointCameraAtEntity(van);
        // Start driving to beforeTrap
        if (wp is not null)
            wp.Stop();
        if (!once)
        {
            van.Position = beforeTrap.Around(100f);
            Game.PlayerPed.CurrentVehicle.Position = van.Position.Around(5f);
            once = true;
        }
        
        
        Debug.WriteLine("Sight gained");
        wp = new Utils.Waypoint(beforeTrap, van, refreshInterval: 2000, bufferDistance: 5f);
        wp.Start();
        wp.SetDrivingSpeed(20f);
        wp.SetDrivingStyle(786748);
        wp.Mark(MarkerType.ThickChevronUp);
        bool waiting = true;
        new Action(async () =>
        {
            while (waiting && pointing && van.Position.DistanceTo(beforeTrap) > 10f)
            {
                var speed = Math.Max((Math.Min(Game.PlayerPed.CurrentVehicle.Speed * 2, 60f)), 20f);
                Debug.WriteLine(speed.ToString());
                wp.SetDrivingSpeed(speed);
                await BaseScript.Delay(500);
            }
            wp.SetDrivingSpeed(20f);
        })();
        await wp.Wait();
        if (!pointing) return;
        van.Position = beforeTrap;
        waiting = false;
        arrived = true;
        new Action(async () =>
        {
            await Utils.StopKeepTaskPlayAnimation(trashman);
            trashman.Task.EnterVehicle(trash, speed: 5f);
            Utils.AutoKeepTaskEnterVehicle(trashman, trash, VehicleSeat.Any, 1000, 5f);
            await Utils.WaitUntilPedIsInVehicle(trashman, trash);
            // Trap player inside
            var wp = new Utils.Waypoint((Vector3)trashTrapPos, trash, bufferDistance: 2f, refreshInterval: 5000);
            wp.SetDrivingSpeed(30f);
            wp.Start();
            await wp.Wait();
            trash.IsPositionFrozen = true;
            trash.Position = (Vector3)trashTrapPos;
            trash.Heading = trashTrapPos.W;
        })();
        wp = new Utils.Waypoint(duringTrap, van, bufferDistance: 2f);
        wp.SetDrivingStyle(312);
        wp.SetDrivingSpeed(50f);
        wp.Mark(MarkerType.ThickChevronUp);
        wp.Start();
        await wp.Wait();
        if (!pointing) return;
        wp = new Utils.Waypoint(duringTrap2, van, bufferDistance: 2f);
        wp.SetDrivingSpeed(50f);
        wp.Mark(MarkerType.ThickChevronUp);
        wp.Start();
        await wp.Wait();
        if (!pointing) return;
        wp = new Utils.Waypoint(afterTrap, van, bufferDistance: 2f);
        //wp.SetDrivingStyle(4456448);
        wp.SetDrivingSpeed(100f);
        wp.Mark(MarkerType.ThickChevronUp);
        wp.Start();
        await wp.Wait();
        if (!pointing) return;
        trash.IsPositionFrozen = true;
        trash.Position = (Vector3)trashTrapPos;
        trash.Heading = trashTrapPos.W;
        new Action(async () =>
        {
            var busTrapPos = bus.GetOffsetPosition(new(0f, 7f, 0f));
            bus.Driver.Task.DriveTo(bus, busTrapPos, 5f, 30f, 21495808);
            // Disable player
            
        })();
        wp = null;
        foreach (var driverAttachedBlip in driver.AttachedBlips)
        {
            driverAttachedBlip.Delete();
        }
        var pursuit = Pursuit.RegisterPursuit(driver);
        pursuit.Init(true, 600f, 200f, true);
        pursuit.ActivatePursuit();
    }

    public async void OnSightLost()
    {
        pointing = false;
        arrived = false;
        //Utils.StopPointingCamera();
        // Stop driving to beforeTrap
        if (wp is not null)
            wp.Stop();
        Debug.WriteLine("Sight lost");
        van.Driver.Task.CruiseWithVehicle(van, 20f, 524351);
    }

    public override Task<bool> CheckRequirements()
    {
        bool result = true; // false
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
            (result));
    }
}

public class HostageScript : BaseScript
{
    public HostageScript()
    {
        Loop();
    }

    private void Loop()
    {
        List<Entity> entities = new List<Entity>();
        Blip searchBlip = null;
        Dictionary<Entity, Blip> blips = new Dictionary<Entity, Blip>();
        bool db = false;
        Tick += async () =>
        {
            if (db) return;
            db = true;
            try
            {
                // Sync search blip
                var vehicles = World.GetAllVehicles()
                    .Where(v => v is not null && v.Exists() &&
                                v.State.Get("Spooky:HostageMP:SearchBlip:Scale") is not null &&
                                v.State.Get("Spooky:HostageMP:SearchBlip:Position") is not null &&
                                v.State.Get("Spooky:HostageMP:Owner") != Game.PlayerPed.NetworkId &&
                                !entities.Contains(v));

                var peds = World.GetAllPeds().Where(p =>
                    p is not null && p.Exists() && p.State.Get("Spooky:HostageMP:Blip") is true &&
                    p.State.Get("Spooky:HostageMP:Owner") != Game.PlayerPed.NetworkId && !entities.Contains(p));

                foreach (var vehicle in vehicles)
                {
                    entities.Add(vehicle);
                    float scale = vehicle.State.Get("Spooky:HostageMP:SearchBlip:Scale");
                    Vector3 position = vehicle.State.Get("Spooky:HostageMP:SearchBlip:Position");
                    searchBlip = World.CreateBlip(position, scale);
                }

                foreach (var ped in peds)
                {
                    entities.Add(ped);
                    var blipColor = ped.State.Get("Spooky:HostageMP:Blip:Color") is BlipColor
                        ? (BlipColor)ped.State.Get("Spooky:HostageMP:Blip:Color")
                        : BlipColor.Red;
                    var blip = ped.AttachBlip();
                    blip.Color = blipColor;
                    blips[ped] = blip;
                }

                foreach (var entity in entities.ToList())
                {
                    if (!entity.Model.IsVehicle) continue;
                    // Vehicle
                    if (entity is not null)
                    {
                        if (searchBlip is null) continue;
                        float scale = entity.State.Get("Spooky:HostageMP:SearchBlip:Scale");
                        Vector3 position = entity.State.Get("Spooky:HostageMP:SearchBlip:Position");
                        if (scale is default(float) || position == default) continue;
                        searchBlip.Position = position;
                        searchBlip.Scale = scale;
                    }
                    else
                    {
                        entities.Remove(entity);
                    }
                }
            }
            catch (Exception ex)
            {
                db = false;
                Utils.Error(ex, "HostageScript:Loop", "SpookyCallouts");
            }

            await Delay(1000);
            db = false;
        };
    }
}