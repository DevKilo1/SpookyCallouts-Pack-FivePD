using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API.Utils;
using Kilo.Commons.Config;
using Newtonsoft.Json.Linq;

namespace RemadeServices2._0;

public class Utils
{
    public static List<Vector4> ParkingSpots = new()
    {
        new Vector4(428.82089233398f, 126.65857696533f, 100.41599273682f, 67.186576843262f)
    };

    public static Dictionary<int, Marker> markers = new Dictionary<int, Marker>();

    public class Waypoint
    {
        public Vector3 Position
        {
            get { return _position; }
        }

        public Entity Target
        {
            get { return _entity; }
        }

        public float Distance
        {
            get { return _distance; }
        }

        private bool _arrived = false;
        private float _distance;
        private float _bufferDistance;
        private Vector3 _position;
        private Entity _entity;
        private int _refreshInterval;
        private Marker _visualMarker;
        private float _runDistance = 10f;

        public float RunDistance
        {
            get { return _runDistance; }
        }

        public float DrivingSpeed
        {
            get { return _drivingSpeed; }
        }

        private float _drivingSpeed = 20f;

        private int _timeout;

        private int _driveStyle = 447;

        public int DrivingStyle
        {
            get { return _driveStyle; }
        }

        public Waypoint(Vector3 position, Entity entityToTrack, int timeout = 100, float bufferDistance = 2f,
            int refreshInterval = 100)
        {
            _position = position;
            _entity = entityToTrack;
            _bufferDistance = bufferDistance;
            _refreshInterval = refreshInterval;
            _timeout = timeout;
            UpdateData();
        }

        private GoToType CalculateGoToType()
        {
            GoToType goToType = GoToType.Run;
            if (_distance < RunDistance)
            {
                goToType = GoToType.Walk;
            }

            return goToType;
        }

        public async Task Start(float drivingSpeed = -1f)
        {
            await BaseScript.Delay(_timeout);
            if (!Target.Model.IsValid) return;
            if (Target.Model.IsPed)
            {
                var ped = (Ped)Target;
                KeepTaskGoToForPed(ped, Position, _bufferDistance, CalculateGoToType());
            }
            else if (Target.Model.IsVehicle)
            {
                if (drivingSpeed != -1f)
                    _drivingSpeed = drivingSpeed;
                Drive();
            }
        }

        private void Drive()
        {
            var veh = (Vehicle)Target;
            if (veh.Driver == null) throw new Exception("Vehicle needs a driver in order to start drive!");
            var driver = veh.Driver;

            new Action(async () =>
            {
                driver.Task.DriveTo(veh, Position, _bufferDistance, DrivingSpeed, _driveStyle);
                while (!_arrived && veh.Position.DistanceTo(Position) > _bufferDistance)
                {
                    driver.Task.DriveTo(veh, _position, _bufferDistance, _drivingSpeed, _driveStyle);
                    await BaseScript.Delay(10000);
                }
            })();
        }

        public void SetDrivingSpeed(float speed)
        {
            _drivingSpeed = speed;
            Drive();
        }

        public void SetRunDistance(float distance)
        {
            _runDistance = distance;
        }

        private async Task UpdateData()
        {
            while (!_arrived)
            {
                _distance = Target.Position.DistanceTo(Position);
                _arrived = _distance <= _bufferDistance;
                await BaseScript.Delay(_refreshInterval);
            }
        }

        public void Mark(MarkerType markerType)
        {
            if (_visualMarker != null)
                throw new Exception("Marker already exists!");

            _visualMarker = new Marker(markerType, MarkerAttachTo.Position, Position);
            _visualMarker.SetVisiblility(true);
        }

        public void Unmark()
        {
            if (_visualMarker == null)
                throw new Exception("Marker does not exist!");
            _visualMarker.Dispose();
        }

        public async Task Wait()
        {
            while (!_arrived)
            {
                await BaseScript.Delay(_refreshInterval);
            }

            if (Target.Model.IsVehicle)
            {
                var veh = (Vehicle)Target;
                var ped = veh.Driver;
                ped.Task.ClearAll();
            }
            else
            {
                var ped = (Ped)Target;
                ped.Task.ClearAll();
            }
        }
    }

    public static async Task KeepTaskGoToForPed(Ped ped, Vector3 pos, float buffer = 2f,
        GoToType type = GoToType.Walk)
    {
        Vector3 startPos = ped.Position;
        switch (type)
        {
            case GoToType.Walk:
            {
                ped.Task.GoTo(pos);
                break;
            }
            case GoToType.Run:
            {
                ped.Task.RunTo(pos);
                break;
            }
            default:
            {
                ped.Task.GoTo(pos);
                break;
            }
        }

        new Action(async () =>
        {
            while (ped.Position.DistanceTo(pos) > buffer)
            {
                Vector3 startPos = ped.Position;
                await BaseScript.Delay(20000);
                if (startPos.DistanceTo(ped.Position) < 10f)
                    ped.Position = pos;
            }
        })();
        while (ped.Position.DistanceTo(pos) > buffer)
        {
            await BaseScript.Delay(1000);
            if (ped.Position == startPos)
            {
                switch (type)
                {
                    case GoToType.Walk:
                    {
                        ped.Task.GoTo(pos);
                        break;
                    }
                    case GoToType.Run:
                    {
                        ped.Task.RunTo(pos);
                        break;
                    }
                    default:
                    {
                        ped.Task.GoTo(pos);
                        break;
                    }
                }
            }

            await BaseScript.Delay(1000);
        }
    }

    public enum GoToType
    {
        Run,
        Walk
    }


    public enum MarkerAttachTo
    {
        Entity,
        Position
    }

    public class Marker
    {
        public int Handle;

        public bool Enabled
        {
            get { return _enabled; }
        }

        public Entity Target
        {
            get { return _targetEntity; }
        }

        public bool Visible
        {
            get { return _enabled; }
        }

        private Entity _targetEntity;

        private bool _enabled = true;
        private bool destroyed = false;
        private MarkerType _markerType;
        private int _alpha = 80;
        private Vector3 _pos, _offset, _rot = Vector3.Zero;
        Vector3 _size = Vector3.One;
        private int _R = 3;
        private int _G = 128;
        private int _B = 252;
        private bool _bobUpAndDown = false;
        private bool _rotate = false;

        public void SetMovement(bool bobbing = false, bool rotate = false)
        {
            _bobUpAndDown = bobbing;
            _rotate = rotate;
        }

        public void SetOpacity(int opacity)
        {
            _alpha = opacity;
        }

        public void SetOffset(Vector3 offset)
        {
            _offset = offset;
        }

        public void SetSize(Vector3 size)
        {
            _size = size;
        }

        public void SetRotation(Vector3 rot)
        {
            _rot = rot;
        }

        public void SetColor(int r, int g, int b)
        {
            _R = r;
            _G = g;
            _B = b;
        }

        private async void Create()
        {
            _enabled = true;
            while (!destroyed)
            {
                if (Enabled)
                {
                    API.DrawMarker((int)_markerType, _pos.X, _pos.Y, _pos.Z, 0f, 0f, 0f, _rot.X, _rot.Y, _rot.Z,
                        _size.X, _size.Y, _size.Z, _R, _G, _B, _alpha, _bobUpAndDown, false, 2, _rotate,
                        null, null, false);
                    //World.DrawMarker(_markerType, _pos, Vector3.Zero, Vector3.Zero, Vector3.One, Color.Aqua);
                }

                await BaseScript.Delay(0);
            }
        }

        public Marker(MarkerType markerType, MarkerAttachTo markerAttachTo, Vector3 pos, Entity entity = null)
        {
            _markerType = markerType;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            _pos = pos;
            this._targetEntity = entity;
            if (markerAttachTo == MarkerAttachTo.Entity)
            {
                if (entity == null) throw new Exception("You need to provide a valid entity to attach to!");
                AttachPositionToEntity();
            }

            SetHandle();
            this.Create();
        }

        public void SetTargetEntity(Entity entity)
        {
            this._targetEntity = entity;
        }

        private async void AttachPositionToEntity()
        {
            while (!destroyed)
            {
                Vector3 newPos = this._targetEntity.Position.ApplyOffset(_offset);
                _pos = newPos;
                await BaseScript.Delay(0);
            }
        }

        public void SetVisiblility(bool state)
        {
            _enabled = state;
        }

        public void Dispose()
        {
            destroyed = true;
            _enabled = false;
        }

        private async void SetHandle()
        {
            int _handle = new Random().Next();
            while (markers.ContainsKey(_handle) && !destroyed)
            {
                _handle = new Random().Next();
                await BaseScript.Delay(0);
            }

            Handle = _handle;
        }
    }

    public static async Task<string> DoOnScreenKeyboard()
    {
        string text = "";

        API.DisplayOnscreenKeyboard(0, "FMMC_KEY_TIP8", "", "", "", "", "", 60);
        while (API.UpdateOnscreenKeyboard() == 0)
        {
            API.DisableAllControlActions(0);
            await BaseScript.Delay(0);
        }

        if (API.GetOnscreenKeyboardResult() == null) return text;
        text = API.GetOnscreenKeyboardResult();
        return text;
    }

    public static bool keepMarkers = false;

    public static void UnmarkAllConditionalPeds()
    {
        keepMarkers = false;
    }
    
    public static void ReleaseAnims()
    {
        foreach (var s in animDictsLoaded)
        {
            API.RemoveAnimDict(s);
        }
    }

    public static List<string> animDictsLoaded = new List<string>();
    public static List<string> animSetsLoaded = new List<string>();
    public static async Task RequestAnimDict(string animDict)
    {
        if (!API.HasAnimDictLoaded(animDict))
            API.RequestAnimDict(animDict);
        while (!API.HasAnimDictLoaded(animDict))
            await BaseScript.Delay(100);
        if (!animDictsLoaded.Contains(animDict))
            animDictsLoaded.Add(animDict);
    }
    
    public static async Task<Ped> SpawnPedOneSync(PedHash pedHash, Vector3 location, bool keepTask = true,
        float heading = 0f)
    {
        Ped ped = await World.CreatePed(new(pedHash), location, heading);
        ped.IsPersistent = true;
        EntitiesInMemory.Add(ped);
        if (keepTask)
        {
            ped.AlwaysKeepTask = true;
            ped.BlockPermanentEvents = true;
        }

        return ped;
    }

    public static async Task<Vehicle> SpawnVehicleOneSync(VehicleHash vehicleHash, Vector3 location,
        float heading = 0f)
    {
        Vehicle veh = await World.CreateVehicle(new(vehicleHash), location, heading);
        if (veh == null) return null;
        veh.IsPersistent = true;
        EntitiesInMemory.Add(veh);
        return veh;
    }

    public static async Task RequestAnimSet(string animSet)
    {
        if (!API.HasAnimSetLoaded(animSet))
            API.RequestAnimSet(animSet);
        while (!API.HasAnimSetLoaded(animSet))
            await BaseScript.Delay(100);
        if (!animSetsLoaded.Contains(animSet))
            animSetsLoaded.Add(animSet);
    }

    public static void UnloadAnimDict(string animDict)
    {
        API.RemoveAnimDict(animDict);
        animDictsLoaded.Remove(animDict);
    }

    public static void UnloadAnimSet(string animSet)
    {
        API.RemoveAnimSet(animSet);
        animSetsLoaded.Remove(animSet);
    }


    public static async Task MarkAllConditional(Entity entityToTrack, IEnumerable<Entity> array, Func<Entity, bool> predicate)
    {
        keepMarkers = true;
        var peds = array
            .Where(predicate)
            .ToList();
        var dict = new Dictionary<Entity, Utils.Marker>();
        foreach (var p in peds)
        {
            var ped = p as Entity;
            var marker = new Utils.Marker(MarkerType.ThickChevronUp, Utils.MarkerAttachTo.Entity, ped.Position, ped);
            marker.SetOffset(new Vector3(0f, 0f, 2f));
            marker.SetMovement(true, true);
            marker.SetRotation(new(0f, 180f, 0f));
            marker.SetVisiblility(true);
            dict.Add(ped, marker);
        }

        while (keepMarkers && entityToTrack.Exists())
        {
            var type = peds.FirstOrDefault().GetType();
            if (type == typeof(Ped))
            {
                var unqualifiedPeds = World.GetAllPeds().Where(p =>
                    p != null && p.Exists() && p.Position.DistanceTo(entityToTrack.Position) < 20f && p.IsDead &&
                    !dict.ContainsKey(p)).ToList();

                foreach (var up in unqualifiedPeds)
                {
                    var unqualifiedPed = up as Ped;
                    var marker = new Utils.Marker(MarkerType.ThickChevronUp, Utils.MarkerAttachTo.Entity,
                        unqualifiedPed.Position, unqualifiedPed);
                    marker.SetOffset(new Vector3(0f, 0f, 2f));
                    marker.SetMovement(true, true);
                    marker.SetRotation(new(0f, 180f, 0f));
                    marker.SetVisiblility(true);
                    dict.Add(unqualifiedPed, marker);
                    peds.Add(unqualifiedPed);
                }    
            }
            
            if (type == typeof(Vehicle))
            {
                var unqualifiedPeds = World.GetAllVehicles().Where(p =>
                    p != null && p.Exists() && p.Position.DistanceTo(entityToTrack.Position) < 20f && p.IsDead &&
                    !dict.ContainsKey(p)).ToList();

                foreach (var unqualifiedPed in unqualifiedPeds)
                {
                    var marker = new Utils.Marker(MarkerType.ThickChevronUp, Utils.MarkerAttachTo.Entity,
                        unqualifiedPed.Position, unqualifiedPed);
                    marker.SetOffset(new Vector3(0f, 0f, 2f));
                    marker.SetMovement(true, true);
                    marker.SetRotation(new(0f, 180f, 0f));
                    marker.SetVisiblility(true);
                    dict.Add(unqualifiedPed, marker);
                    peds.Add(unqualifiedPed);
                }
            }
            

            foreach (var ped in peds)
            {
                var marker = dict[ped];
                if (marker is null) continue;
                if (ped.Position.DistanceTo(entityToTrack.Position) >= 20f)
                {
                    if (marker.Visible)
                        marker.SetVisiblility(false);
                }
                else
                {
                    if (!marker.Visible)
                        marker.SetVisiblility(true);
                }
            }

            await BaseScript.Delay(500);
        }
        foreach (var entity in peds)
        {
            var marker = dict[entity];
            if (marker is null) return;
            marker.Dispose();
        }
        dict.Clear();
        peds.Clear();
        keepMarkers = false;
    }

    public static async Task<Vehicle> CloneVehicle(Vehicle vehicle, bool destroyOld = false, bool recursive = true)
    {
        Vehicle v = vehicle;
        var veh = await World.CreateVehicle(new Model((VehicleHash)v.Model.Hash),
            Vector3.One.ClosestParkedCarPlacement());

        if (recursive)
        {
            foreach (var p in v.Occupants)
            {
                var clone = p.Clone();
                var seat = (VehicleSeat)p.SeatIndex;
                clone.SetIntoVehicle(veh, seat);
                clone.Health = p.Health;
                if (destroyOld)
                    p.Delete();
            }
        }
        
        API.CopyVehicleDamages(v.Handle, veh.Handle);
        veh.Mods.Livery = v.Mods.Livery;
        veh.Mods.ColorCombination = v.Mods.ColorCombination;
        Vector3 pos = v.Position;
        float heading = v.Heading;
        if (destroyOld)
        {
            v.Delete();
            veh.Position = pos;
            veh.Heading = heading;
        }
        
        return veh;
    }

    public static async Task TaskParkVehicle(Ped ped, Vehicle veh, int drivingStyle = 0)
    {
        var closestPos = GetNearestParkingToPed(ped);
        ped.Task.DriveTo(veh, (Vector3)closestPos, 20f, 10f, drivingStyle);
        SlowVehicleDownInRadiusToPosition(veh, (Vector3)closestPos, 20f, drivingStyle);
        await WaitUntilPedIsAtPosition((Vector3)closestPos, ped, 20f);
        ped.Task.DriveTo(veh, (Vector3)closestPos, 0.5f, 3f, drivingStyle);
        await WaitUntilVehicleIsAtPosition((Vector3)closestPos, veh, 1f);
        //await WaitUntilPedIsAtPosition((Vector3)closestPos, ped, 1f);
        veh.Position = (Vector3)closestPos;
        veh.Heading = closestPos.W;
        veh.IsEngineRunning = false;
    }

    public static void StopPointingCamera()
    {
        World.RenderingCamera = null;
    }

    public static async Task PointCameraAtEntity(Entity ent)
    {
        World.RenderingCamera = null;
        var currentCamera = World.RenderingCamera;
        var camHandle = API.CreateCameraWithParams((uint)API.GetHashKey("DEFAULT_SCRIPTED_CAMERA"),
            currentCamera.Position.X,
            currentCamera.Position.Y, currentCamera.Position.Z, currentCamera.Rotation.X, currentCamera.Rotation.Y,
            currentCamera.Rotation.Z, 45f, true, 2);
        API.AttachCamToEntity(camHandle, Game.PlayerPed.Handle, 0f, 2f, 0f, true);
        API.RenderScriptCams(true, true, 1000, true, true);
        API.PointCamAtEntity(camHandle, ent.Handle, 0f, 0f, 0f, true);
    }
    
    public static async Task<Vector4> HandlePark(Vehicle vehicle, float radius = 50f, int driveStyle = 524732)
    {
        Vector3 roadsidePoint = Vector3.Zero;
        float outHeading = 0f;
        //GoalPosition = ;
        Vector3 goalPosition = vehicle.Position;
        
        while (vehicle.Driver is null)
            await BaseScript.Delay(100);
        
        API.GetClosestVehicleNodeWithHeading(goalPosition.X, goalPosition.Y, goalPosition.Z, ref roadsidePoint,
            ref outHeading, 1, 3, 0);
        API.GetRoadSidePointWithHeading(roadsidePoint.X, roadsidePoint.Y, roadsidePoint.Z, outHeading,
            ref roadsidePoint);
        
        while (roadsidePoint != Vector3.Zero && API.IsPositionOccupied(roadsidePoint.X, roadsidePoint.Y,
                   roadsidePoint.Z, vehicle.Model.GetDimensions().Length() / 2, false, true, false, false, false, 0,
                   false))
        {
            radius *= 2;
            goalPosition = goalPosition.Around(radius / 2);
            Utils.Print("Getting new parking spot");
            API.GetClosestVehicleNodeWithHeading(goalPosition.X, goalPosition.Y, goalPosition.Z, ref roadsidePoint,
                ref outHeading, 1, 3, 0);
            API.GetRoadSidePointWithHeading(roadsidePoint.X, roadsidePoint.Y, roadsidePoint.Z, outHeading,
                ref roadsidePoint);
            await BaseScript.Delay(1);
        }
            
        if (roadsidePoint.DistanceTo(goalPosition) <= radius)
        {
            goalPosition = roadsidePoint;
        }

        Utils.SlowVehicleDownInRadiusToPosition(vehicle, goalPosition, radius, driveStyle);
        API.TaskVehiclePark(vehicle.Driver.Handle, vehicle.Handle, goalPosition.X, goalPosition.Y, goalPosition.Z, outHeading, 0, 10000f, false);
        int durationLeft = 20000;
        Vector3 savedLoc = vehicle.Driver.Position;
        while (durationLeft > 0)
        {
            if (!vehicle.IsEngineRunning)
                break;
            await BaseScript.Delay(1000);
            if (durationLeft < 5000 && vehicle.Driver.Position.DistanceTo(savedLoc) > 8f)
            {
                vehicle.Position = vehicle.Position.Around(10f);
                savedLoc = vehicle.Driver.Position;
                durationLeft = 20000;
            }
            else
            {
                durationLeft -= 1000;
            }
        }
        await Utils.WaitUntilVehicleEngineIsOff(vehicle);
        return new Vector4(goalPosition.X, goalPosition.Y, goalPosition.Z, outHeading);
    }

    public static Vector4 GetNearestParkingToPed(Ped ped)
    {
        Vector4 nearest = ParkingSpots.First();
        foreach (var parkingSpot in ParkingSpots)
        {
            if (nearest.IsZero)
                nearest = parkingSpot;
            if (Vector3.Distance((Vector3)parkingSpot, ped.Position) < Vector3.Distance((Vector3)nearest, ped.Position))
            {
                nearest = parkingSpot;
            }
        }

        return nearest;
    }


    public static Dictionary<Guid, List<string>> Errors = new();

    public static void Error(Exception ex, string source = "[UNSPECIFIED]", string scriptName = "Remade Services")
    {
        var guid = Guid.NewGuid();
        Print(@$"
{scriptName} has experienced an error {source}!
Error Message: {ex.Message}
Unique Error ID: {guid}
", true);
        Errors[guid] = new()
        {
            source, ex.Message, ex.ToString()
        };
        BaseScript.TriggerEvent("Kilo.Commons:Error", guid.ToString(), source, ex.Message,
            ex.ToString()); // TO-DO: Register this too!
    }

    public static List<Entity> EntitiesInMemory = new List<Entity>();

    public static void ReleaseEntity(Entity ent)
    {
        if (EntitiesInMemory.Contains(ent))
        {
            if (ent.Model.IsPed)
            {
                Ped ped = (Ped)ent;
                ped.AlwaysKeepTask = false;
                ped.BlockPermanentEvents = false;
            }

            ent.IsPersistent = false;
            EntitiesInMemory.Remove(ent);
        }
    }

    public static List<Ped> keepTaskAnimation = new List<Ped>();

    public static async Task KeepTaskPlayAnimation(Ped ped, string animDict, string animSet,
        AnimationFlags flags = AnimationFlags.Loop)
    {
        if (keepTaskAnimation.Contains(ped))
            await StopKeepTaskPlayAnimation(ped);
        ped.Task.PlayAnimation(animDict, animSet);
        keepTaskAnimation.Add(ped);
        while (keepTaskAnimation.Contains(ped))
        {
            if (ped == null || ped.IsDead || ped.IsCuffed) break;

            if (!API.IsEntityPlayingAnim(ped.Handle, animDict, animSet, 3))
            {
                //Utils.Print(animDict + ", " +animSet);
                await ped.Task.PlayAnimation(animDict, animSet, 8f, 8f, -1, flags, 1f);
            }

            await BaseScript.Delay(1000);
        }
    }

    public static async Task StopKeepTaskPlayAnimation(Ped ped)
    {
        while (keepTaskAnimation.Contains(ped))
        {
            keepTaskAnimation.Remove(ped);
            await BaseScript.Delay(100);
        }

        await BaseScript.Delay(1500);
    }

    public static float PedFacePositionImmediately(Ped ped, Vector3 pos)
    {
        return API.GetHeadingFromVector_2d(pos.X - ped.Position.X,
            pos.Y - ped.Position.Y);
    }

    public static void ShowNetworkedNotification(string text, string sender = "~f~Dispatch",
        string subject = "~m~ Callout Update", string txdict = "CHAR_CALL911", string txname = "CHAR_CALL911",
        int iconType = 4, int backgroundColor = -1, bool flash = false, bool isImportant = false,
        bool saveToBrief = false)
    {
        API.BeginTextCommandThefeedPost("STRING");
        API.AddTextComponentSubstringPlayerName(text);
        if (backgroundColor > -1)
            API.ThefeedNextPostBackgroundColor(
                backgroundColor); // https://docs.fivem.net/docs/game-references/hud-colors/
        API.EndTextCommandThefeedPostMessagetext(txdict, txname, flash, iconType, sender, subject);
        API.EndTextCommandThefeedPostTicker(isImportant, saveToBrief);
    }

    public static async Task<bool> WaitUntilKeypressed(Control key, int timeoutAfterDuration = -1)
    {
        bool stillWorking = true;
        bool pressed = false;
        if (timeoutAfterDuration > -1)
        {
            var wait = new Action(async () =>
            {
                await BaseScript.Delay(timeoutAfterDuration);
                stillWorking = false;
            });
            wait();
        }

        while (stillWorking)
        {
            if (Game.IsControlJustReleased(0, key))
            {
                pressed = true;
                return pressed;
            }

            await BaseScript.Delay(0);
        }

        return pressed;
    }

    public static List<string> Text3DInProgress = new List<string>();

    public static void ImmediatelyStop3DText()
    {
        Text3DInProgress.Clear();
    }

    public static async void Draw3DText(Vector3 pos, string text, float scaleFactor = 0.5f,
        int duration = 5000, int red = 255, int green = 255, int blue = 255, int opacity = 150, Entity attachTo = null)
    {
        if (attachTo == null)
        {
            Text3DInProgress.Add(text);
            Draw3DTextHandler(pos, scaleFactor, text, duration, red, green, blue, opacity);
        }
        else
        {
            // Pos is offset
            Text3DInProgress.Add(text);
            Draw3DTextDrawerOnEntity(attachTo, pos, scaleFactor, text, red, green, blue, opacity);
            await BaseScript.Delay(duration);
            if (Text3DInProgress.Contains(text))
                Text3DInProgress.Remove(text);
        }
    }

    public static async Task Draw3DTextHandler(Vector3 pos, float scaleFactor, string text, int duration,
        int red, int green, int blue, int opacity)
    {
        Draw3DTextDrawer(pos, scaleFactor, text, red, green, blue, opacity);
        await BaseScript.Delay(duration);
        if (Text3DInProgress.Contains(text))
            Text3DInProgress.Remove(text);
    }

    public static async Task ShowDialogCountdown(string text, int duration = 10000)
    {
        string countdownReplace = "[Countdown]";
        int seconds = duration / 1000;
        bool stillWorking = true;
        var wait = new Action(async () =>
        {
            await BaseScript.Delay(duration);
            stillWorking = false;
        });
        wait();
        var wait2 = new Action(async () =>
        {
            await Utils.WaitUntilKeypressed(Control.MpTextChatTeam, 20000);
            stillWorking = false;
        });
        wait2();
        while (stillWorking)
        {
            string newText = text.Replace(countdownReplace, "" + seconds + " seconds left");
            API.BeginTextCommandPrint("STRING");
            API.AddTextComponentString(newText);
            API.EndTextCommandPrint(1000, true);
            seconds -= 1;
            await BaseScript.Delay(1000);
        }
    }

    public static async Task ShowDialog(string text, int duration = 10000, bool showImmediately = false)
    {
        API.BeginTextCommandPrint("STRING");
        API.AddTextComponentString(text);
        API.EndTextCommandPrint(duration, showImmediately);
        await BaseScript.Delay(duration);
    }

    public static async Task SubtitleChat(Entity entity, string chat, int red = 255, int green = 255, int blue = 255,
        int opacity = 255)
    {
        int time = chat.Length * 150;
        Utils.Draw3DText(new Vector3(0f, 0f, 1f), chat, 0.5f,
            time,
            red, green, blue, opacity, entity);
        await BaseScript.Delay(time);
    }

    public static void Draw3DTextDrawNonLoop(Vector3 pos, float scaleFactor, string text, int red, int green,
        int blue, int opacity)
    {
        float screenY = 0f;
        float screenX = 0f;
        bool result = API.World3dToScreen2d(pos.X, pos.Y, pos.Z, ref screenX, ref screenY);
        Vector3 p = API.GetGameplayCamCoords();
        float dist = World.GetDistance(p, pos);
        float scale = (1 / dist) * 2;
        float fov = (1 / API.GetGameplayCamFov()) * 100;
        scale = scale * fov * scaleFactor;
        if (!result) return;
        API.SetTextScale(0f, scale);
        API.SetTextFont(0);
        API.SetTextProportional(true);
        API.SetTextColour(red, green, blue, opacity);
        API.SetTextDropshadow(0, 0, 0, 0, 255);
        API.SetTextEdge(2, 0, 0, 0, 150);
        API.SetTextDropShadow();
        API.SetTextOutline();
        API.SetTextEntry("STRING");
        API.SetTextCentre(true);
        API.AddTextComponentString(text);
        API.DrawText(screenX, screenY);
    }

    public static async Task Draw3DTextDrawerOnEntity(Entity ent, Vector3 offset, float scaleFactor, string text,
        int red, int green,
        int blue, int opacity)
    {
        while (Text3DInProgress.Contains(text))
        {
            Vector3 pos = API.GetOffsetFromEntityInWorldCoords(ent.Handle, offset.X, offset.Y, offset.Z);
            Draw3DTextDrawNonLoop(pos, scaleFactor, text, red, green, blue, opacity);
            await BaseScript.Delay(0);
        }
    }

    public static async Task Draw3DTextDrawer(Vector3 pos, float scaleFactor, string text, int red, int green,
        int blue, int opacity)
    {
        while (Text3DInProgress.Contains(text))
        {
            Draw3DTextDrawNonLoop(pos, scaleFactor, text, red, green, blue, opacity);
            await BaseScript.Delay(0);
        }
    }

    public static async Task CaptureEntity(Entity ent)
    {
        API.NetworkRequestControlOfEntity(ent.Handle);
        ent.IsPersistent = true;
        if (ent.Model.IsPed)
        {
            KeepTask((Ped)ent);
        }
    }

    public static void KeepTask(Ped ped)
    {
        if (ped == null || !ped.Exists()) return;
        ped.IsPersistent = true;
        ped.AlwaysKeepTask = true;
        ped.BlockPermanentEvents = true;
    }

    public static bool CanEntitySeeEntity(Entity ent1, Entity ent2)
    {
        return API.HasEntityClearLosToEntityInFront(ent1.Handle, ent2.Handle);
    }

    public static void ShowNotification(string text)
    {
        API.SetTextComponentFormat("STRING");
        API.AddTextComponentString(text);
        API.DisplayHelpTextFromStringLabel(0, false, true, -1);
    }

    public static JObject GetConfig()
    {
        string data = "";
        try
        {
            data = API.LoadResourceFile("fivepd", "/plugins/RemadeServicesByKilo/config.json");
            Utils.Print(data);
        }
        catch (Exception err)
        {
            Utils.Print(err.ToString());
        }

        return JObject.Parse(data);
    }

    public static void Print(string message, bool ignoreDebug = false)
    {
        if (Config.configs.Count < 1) return;
        var config = Config.configs.FirstOrDefault();
        if (config is null)
            return;
        if (config.ContainsKey("Debug") && (bool)config["Debug"] || ignoreDebug)
            Debug.WriteLine(message);
    }

    public static async Task<List<Prop>> GetPropInArea(string prop, Vector3 pos, float radius, bool persist = false)
    {
        List<Prop> result = new List<Prop>();
        var props = World.GetAllProps();
        foreach (var p in props)
        {
            if (p == null || !p.Exists()) continue;
            if (p.Position.DistanceTo(pos) < radius && p.Model == new Model(prop))
            {
                API.PlaceObjectOnGroundProperly(p.Handle);
                if (persist)
                    p.IsPersistent = true;
                result.Add(p);
            }
        }

        return result;
    }

    public static async Task<List<Ped>> GetAllDeadPedsWithinRadius(Vector3 pos, float searchRadius)
    {
        return World.GetAllPeds().Where(p => p.Exists() && p.Position.DistanceTo(pos) < searchRadius && p.IsDead)
            .ToList();
    }

    public static async Task WaitUntilPedIsOutOfVehicle(Ped ped)
    {
        while (true)
        {
            if (!API.IsPedInAnyVehicle(ped.Handle, false))
            {
                Utils.Print("Ped is out of vehicle");
                return;
            }

            await BaseScript.Delay(500);
        }
    }


    public static async Task WaitUntilPedIsAtPosition(Vector3 pos, Ped ped, float buffer = 2f)
    {
        while (true)
        {
            if (ped.Position.DistanceTo(pos) < buffer)
            {
                return;
            }

            await BaseScript.Delay(2000);
        }
    }

    public static async Task WaitUntilVehicleEngineIsOff(Vehicle vehicle)
    {
        while (true)
        {
            if (!vehicle.IsEngineRunning)
                return;
            await BaseScript.Delay(2000);
        }
    }

    public static async Task WaitUntilVehicleIsWithinRadius(Vector3 pos, Vehicle vehicle, float radius)
    {
        while (true)
        {
            if (!vehicle.IsPersistent) return;
            if (vehicle.Position.DistanceTo(pos) < radius)
            {
                return;
            }

            await BaseScript.Delay(2000);
        }
    }

    public static async Task WaitUntilVehicleIsAtPosition(Vector3 pos, Vehicle vehicle, float buffer = 2f)
    {
        int times = 0;
        Vector3 startPos = Vector3.Zero;
        while (true)
        {
            if (!vehicle.IsPersistent)
                return;
            if (vehicle.Position.DistanceTo(pos) < buffer)
            {
                return;
            }

            startPos = vehicle.Position;
            await BaseScript.Delay(5000);
            if (vehicle.Position.DistanceTo(startPos) < 2f)
            {
                times++;
                if (times > 5)
                    vehicle.Position = pos;
            }
        }
    }

    public static Vector3 getPositionNearPlayer()
    {
        Vector3 plrPos = Game.PlayerPed.Position;
        Vector3 newPos = plrPos.Around(5f);
        Vector3 driveToPos = new Vector3(newPos.X, newPos.Y, World.GetGroundHeight(new Vector2(newPos.X, newPos.Y)));
        return driveToPos;
    }

    public static async Task<Vector3> GetSpawnPositionPlayerCantSee(Vector3 pos, float radius = 200f)
    {
        var newPos = pos.Around(1000f);
        Entity ent = World.CreateRandomPed(newPos);
        ent.IsVisible = false;
        while (API.IsPedHeadingTowardsPosition(Game.PlayerPed.Handle, newPos.X, newPos.Y, newPos.Z, 1f) ||
               Utils.CanEntitySeeEntity(Game.PlayerPed, ent))
        {
            newPos = pos.Around(200f);
            ent.Position = newPos;
            await BaseScript.Delay(100);
        }

        return newPos;
    }

    public static Vector3 GetServiceSpawnAroundPosition(Vector3 pos)
    {
        return pos.Around(200f);
    }

    public static async Task AutoKeepRunOnPed(Ped ped, Vector3 runToPos, float buffer = 2f)
    {
        int times = 0;
        bool db = false;
        while (true)
        {
            if (db) continue;
            db = true;
            if (ped.Position.DistanceTo(runToPos) < buffer)
            {
                return;
            }

            Vector3 startPos = ped.Position;
            await BaseScript.Delay(5000);
            if (ped.Position.DistanceTo(startPos) < buffer)
            {
                times++;
                ped.Task.ClearAllImmediately();
                await BaseScript.Delay(500);
                if (times > 5)
                    ped.Position = runToPos;
                else
                    ped.Task.RunTo(runToPos);
            }

            db = false;
        }
    }

    public static async Task AutoCleanUpVehicle(Vehicle vehicle, List<Ped> occupants, string serviceId)
    {
        int retries = 0;
        bool db = false;
        bool hintDB = false;
        while (true)
        {
            if (db) continue;
            db = true;
            Vector3 startPos = vehicle.Position;
            if (!hintDB && vehicle.Position.DistanceTo(startPos) < 50f)
            {
                hintDB = true;
                // Display help hint dialog that tells the player that the vehicle may be stuck.
            }

            await BaseScript.Delay(10000);
            db = false;
            retries++;
            if (retries >= 6)
            {
                if (vehicle.Position.DistanceTo(startPos) < 2f)
                {
                    vehicle.Delete();
                    foreach (Ped p in occupants)
                    {
                        p.Delete();
                    }

                    break;
                }
            }
        }
    }

    public static async Task WaitUntilPedIsWithinRadiusOfCoords(Vector3 pos, Ped ped, float radius)
    {
        while (true)
        {
            if (ped.Position.DistanceTo(pos) < radius)
            {
                return;
            }

            await BaseScript.Delay(2000);
        }
    }

    public static async Task WaitUntilPedIsInVehicle(Ped ped, Vehicle vehicle)
    {
        while (true)
        {
            if (ped.IsInVehicle(vehicle))
            {
                return;
            }

            await BaseScript.Delay(2000);
        }
    }

    public static async Task<Vehicle> GetClosestVehicleToPed(Ped ped, float maxRadius)
    {
        Vehicle result = null;
        Vehicle[] allVehicles = World.GetAllVehicles();
        Utils.Print("Before foreach");
        foreach (Vehicle veh in allVehicles)
        {
            if (veh == null) continue;
            if (result == null && veh.Position.DistanceTo(ped.Position) < maxRadius)
                result = veh;
            if (result != null && veh.Position.DistanceTo(ped.Position) < result.Position.DistanceTo(ped.Position) &&
                veh.Position.DistanceTo(ped.Position) < maxRadius)
                result = veh;
        }

        Utils.Print("About to return");
        return result;
    }


    public static async Task SlowVehicleDownInRadiusToPosition(Vehicle vehicle, Vector3 position, float radius,
        int drivingstyle)
    {
        while (true)
        {
            if (vehicle.Position.DistanceTo(position) < radius)
            {
                vehicle.Driver.Task.ClearAll();
                vehicle.Driver.Task.DriveTo(vehicle, position, 10f, 10f, drivingstyle);
                Utils.Print("Slowing down in radius...");
                break;
            }

            await BaseScript.Delay(500);
        }
    }

    public static async Task<VehicleSeat> GetUnoccupiedPassengerSeat(Vehicle veh)
    {
        VehicleSeat seat = VehicleSeat.Passenger;
        while (!veh.IsSeatFree(seat))
        {
            seat++;
            if (veh.PassengerCapacity >= (int)seat)
            {
                return VehicleSeat.None;
            }
            await BaseScript.Delay(10);
        }

        return seat;
    }

    public static async void AutoKeepTaskEnterVehicle(Ped ped, Vehicle vehicle, VehicleSeat seat, int msInterval)
    {
        Vector3 pedPos = Vector3.Zero;
        int times = 0;
        while (!ped.IsInVehicle(vehicle))
        {
            times++;
            if (pedPos != Vector3.Zero && ped.Position.DistanceTo(pedPos) < 1f)
            {
                if (times > 2)
                {
                    ped.Task.ClearAllImmediately();
                    ped.SetIntoVehicle(vehicle, seat);
                    await BaseScript.Delay(1000);
                    return;
                }
            }

            pedPos = ped.Position;
            await BaseScript.Delay(msInterval);
            if (ped.Position.DistanceTo(pedPos) < 1f)
            {
                ped.Task.ClearAllImmediately();
                await BaseScript.Delay(500);
                ped.Task.EnterVehicle(vehicle, seat);
            }
        }
    }

    public static void RemoveItemByIdInArray(string id, JArray array)
    {
        foreach (var item in array)
        {
            if ((string)item["serviceId"] == id)
            {
                item.Remove();
            }
        }
    }
    
            public static Vector3[] ConvenienceLocations = new Vector3[]
        {
            new(-712.12f, -913.06f, 19.22f),
            new(29.49f, -1346.94f, 29.5f),
            new(-50.78f, -1753.61f, 29.42f),
            new(376.4f, 325.75f, 103.57f),
            new(-1223.94f, -906.52f, 12.33f)
        };

        public static Vector3[] HomeLocations = new Vector3[]
        {
            new(-120.15f, -1574.39f, 34.18f),
            new(-148.07f, -1596.64f, 38.21f),
            new(-32.44f, -1446.5f, 31.89f),
            new(-14.11f, -1441.93f, 31.1f),
            new(72.21f, -1938.59f, 21.37f),
            new(126.68f, -1930.01f, 21.38f),
            new(270.2f, -1917.19f, 26.18f),
            new(325.68f, -2050.86f, 20.93f),
            new(1099.52f, -438.65f, 67.79f),
            new(1046.24f, -498.14f, 64.28f),
            new(980.1f, -627.29f, 59.24f),
            new(943.45f, -653.49f, 58.43f),
            new(1223.08f, -696.85f, 60.8f),
            new(1201.06f, -575.68f, 69.14f),
            new(1265.9f, -648.33f, 67.92f),
            new(1241.5f, -566.4f, 69.66f),
            new(1204.73f, -557.74f, 69.62f),
            new(1223.06f, -696.74f, 60.81f),
            new(930.88f, -244.82f, 69.0f),
            new(880.01f, -205.01f, 71.98f),
            new(798.39f, -158.83f, 74.89f),
            new(820.86f, -155.84f, 80.75f), // Second floor
            new(208.65f, 74.53f, 87.9f),
            new(119.34f, 494.13f, 147.34f),
            new(79.74f, 486.13f, 148.2f),
            new(151.2f, 556.09f, 183.74f),
            new(232.1f, 672.06f, 189.98f),
            new(-66.76f, 490.13f, 144.88f),
            new(-175.94f, 502.73f, 137.42f),
            new(-230.26f, 488.29f, 128.77f),
            new(-355.91f, 469.56f, 112.61f),
            new(-353.17f, 423.13f, 110.98f),
            new(-312.53f, 474.91f, 111.83f),
            new(-348.99f, 514.99f, 120.65f),
            new(-376.59f, 547.66f, 123.85f),
            new(-406.6f, 566.28f, 124.61f),
            new(-520.28f, 594.07f, 120.84f),
            new(-581.37f, 494.04f, 108.26f),
            new(-678.67f, 511.67f, 113.53f),
            new(-784.46f, 459.47f, 100.25f),
            new(-824.67f, 422.6f, 92.13f),
            new(-881.97f, 364.1f, 85.36f),
            new(-967.59f, 436.88f, 80.57f),
            new(-1570.71f, 23.0f, 59.55f),
            new(-1629.9f, 36.25f, 62.94f),
            new(-1750.22f, -695.19f, 11.75f),
            new(-1270.03f, -1296.53f, 4.0f),
            new(-1148.96f, -1523.2f, 10.63f),
            new(-1105.61f, -1596.67f, 4.61f)
        };

        public static bool IsPedNonLethalOrMelee(Ped ped)
        {
            WeaponHash weapon = ped.Weapons.Current;
            return nonlethals.Contains(weapon) || melee.Contains(weapon);
        }

        public static WeaponHash[] nonlethals =
        {
            WeaponHash.Ball,
            WeaponHash.Parachute,
            WeaponHash.Flare,
            WeaponHash.Snowball,
            WeaponHash.Unarmed,
            WeaponHash.StunGun,
            WeaponHash.FireExtinguisher
        };

        public static WeaponHash[] melee =
        {
            WeaponHash.Crowbar,
            WeaponHash.Bat,
            WeaponHash.Bottle,
            WeaponHash.Flashlight,
            WeaponHash.Hatchet,
            WeaponHash.Knife,
            WeaponHash.Machete,
            WeaponHash.Nightstick,
            WeaponHash.Unarmed,
            WeaponHash.PoolCue,
            WeaponHash.StoneHatchet
        };
        
        public static PedHash GetRandomPed()
        {
            return RandomUtils.GetRandomPed(exclusions);
        }

        public static VehicleHash GetRandomVehicleForRobberies()
        {
            return RandomUtils.GetRandomVehicle(FourPersonVehicleClasses);
        }

        public static IEnumerable<VehicleClass> FourPersonVehicleClasses = new List<VehicleClass>()
        {
            VehicleClass.Compacts,
            VehicleClass.Sedans,
            VehicleClass.Vans,
            VehicleClass.SUVs
        };

        public static PedHash GetRandomSuspect()
        {
            return suspects[new Random().Next(suspects.Length - 1)];
        }

        public static WeaponHash GetRandomWeapon()
        {
            int index = new Random().Next(weapons.Length);
            return weapons[index];
        }

        public static WeaponHash[] weapons =
        {
            WeaponHash.AssaultRifle,
            WeaponHash.PumpShotgun,
            WeaponHash.CombatPistol
        };

        public static PedHash[] suspects =
        {
            PedHash.MerryWeatherCutscene,
            PedHash.Armymech01SMY,
            PedHash.MerryWeatherCutscene,
            PedHash.ChemSec01SMM,
            PedHash.Blackops01SMY,
            PedHash.CiaSec01SMM,
            PedHash.PestContDriver,
            PedHash.PestContGunman,
            PedHash.TaoCheng,
            PedHash.Hunter,
            PedHash.EdToh,
            PedHash.PrologueMournMale01,
            PedHash.PoloGoon01GMY
        };

        public static IEnumerable<WeaponHash> weapExclusions = new List<WeaponHash>
        {
            WeaponHash.Ball,
            WeaponHash.Bat,
            WeaponHash.Snowball,
            WeaponHash.RayMinigun,
            WeaponHash.RayCarbine,
            WeaponHash.BattleAxe,
            WeaponHash.Bottle,
            WeaponHash.BZGas,
            WeaponHash.Crowbar,
            WeaponHash.Dagger,
            WeaponHash.FireExtinguisher,
            WeaponHash.Firework,
            WeaponHash.Flare,
            WeaponHash.FlareGun,
            WeaponHash.Flashlight,
            WeaponHash.GolfClub,
            WeaponHash.Grenade,
            WeaponHash.GrenadeLauncher,
            WeaponHash.Gusenberg,
            WeaponHash.Hammer,
            WeaponHash.Hatchet,
            WeaponHash.StoneHatchet,
            WeaponHash.StunGun,
            WeaponHash.Musket,
            WeaponHash.HeavySniper,
            WeaponHash.HeavySniperMk2,
            WeaponHash.HomingLauncher,
            WeaponHash.Knife,
            WeaponHash.KnuckleDuster,
            WeaponHash.Machete,
            WeaponHash.Molotov,
            WeaponHash.Nightstick,
            WeaponHash.NightVision,
            WeaponHash.Parachute,
            WeaponHash.PetrolCan,
            WeaponHash.PipeBomb,
            WeaponHash.PoolCue,
            WeaponHash.ProximityMine,
            WeaponHash.Railgun,
            WeaponHash.RayPistol,
            WeaponHash.RPG,
            WeaponHash.SmokeGrenade,
            WeaponHash.SniperRifle,
            WeaponHash.StickyBomb,
            WeaponHash.SwitchBlade,
            WeaponHash.Unarmed,
            WeaponHash.Wrench
        };

        public static IEnumerable<PedHash> exclusions = new List<PedHash>()
        {
            PedHash.Acult01AMM,
            PedHash.Motox01AMY,
            PedHash.Boar,
            PedHash.Cat,
            PedHash.ChickenHawk,
            PedHash.Chimp,
            PedHash.Chop,
            PedHash.Cormorant,
            PedHash.Cow,
            PedHash.Coyote,
            PedHash.Crow,
            PedHash.Deer,
            PedHash.Dolphin,
            PedHash.Fish,
            PedHash.Hen,
            PedHash.Humpback,
            PedHash.Husky,
            PedHash.KillerWhale,
            PedHash.MountainLion,
            PedHash.Pig,
            PedHash.Pigeon,
            PedHash.Poodle,
            PedHash.Rabbit,
            PedHash.Rat,
            PedHash.Retriever,
            PedHash.Rhesus,
            PedHash.Rottweiler,
            PedHash.Seagull,
            PedHash.HammerShark,
            PedHash.TigerShark,
            PedHash.Shepherd,
            PedHash.Stingray,
            PedHash.Westy,
            PedHash.BradCadaverCutscene,
            PedHash.Orleans,
            PedHash.OrleansCutscene,
            PedHash.ChiCold01GMM,
            PedHash.DeadHooker,
            PedHash.Marston01,
            PedHash.Niko01,
            PedHash.PestContGunman,
            PedHash.Pogo01,
            PedHash.Ranger01SFY,
            PedHash.Ranger01SMY,
            PedHash.RsRanger01AMO,
            PedHash.Zombie01,
            PedHash.Corpse01,
            PedHash.Corpse02,
            PedHash.Stripper01Cutscene,
            PedHash.Stripper02Cutscene,
            PedHash.StripperLite,
            PedHash.Stripper01SFY,
            PedHash.Stripper02SFY,
            PedHash.StripperLiteSFY
        };
}