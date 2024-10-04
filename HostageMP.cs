using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;
using Utils = RemadeServices2._0.Utils;

namespace SpookyCallouts;
[CalloutProperties("KidnappingMP (Spooky)", "DevKilo", "1.0.0")]
public class Hostage : Callout
{
    private Vehicle van;
    private Ped driver;
    private List<Ped> occupants = [];

    private Blip calloutRadius;
    
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
        if (calloutRadius.Exists())
        {
            calloutRadius.Position = Location.Around(200f);
        }

        else
        {
            calloutRadius = World.CreateBlip(Location, scale);
            calloutRadius.Alpha = 80;
            calloutRadius.Color = BlipColor.Red;
            van.State.Set("Spooky:HostageMP:SearchBlip:Scale", scale, true);
            van.State.Set("Spooky:HostageMP:SearchBlip:Position", calloutRadius.Position, true);
        }
    }

    private async void AcceptHandler()
    {
        UpdateData();
        UpdateBlip();
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
            var v = World.GetAllVehicles().FirstOrDefault(v => v.Position.DistanceTo(van.Position) < 50f && v.Speed != speed && v.Driver is not null && v.Speed > 5f);
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
        van = await Utils.SpawnVehicleOneSync(VehicleHash.Speedo2, driver.Position.Around(20f).ClosestParkedCarPlacement());
        driver.AttachBlip();
        driver.Task.WarpIntoVehicle(van, VehicleSeat.Driver);
        await BaseScript.Delay(200);
        _ = HandleDriveWander();
        UpdateLocation();
        base.OnStart(closest);
    }

    public override Task<bool> CheckRequirements()
    {
        bool result = true;// false
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