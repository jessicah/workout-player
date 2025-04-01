using Blazor.Bluetooth;
using Dynastream.Fit;
using Microsoft.JSInterop;

namespace BluetoothLE.Utilities
{
    public class BluetoothHandler(ILogger<BluetoothHandler> Logger, IBluetoothNavigator Bluetooth)
    {
        public record BluetoothData(int? HeartRate, int? Power, int? Cadence, int? Speed);

        public event Action<BluetoothData>? OnChange;

        private void NotifyStateChanged(int? heartRate, int? power, int? cadence, int? speed) => OnChange?.Invoke(new(heartRate, power, cadence, speed));

        public enum ServiceType { Controllable, Power, HeartRate, Cadence, Gear }
        [Flags]
        public enum Metrics { Controllable, Power, Cadence, HeartRate, Gear }

        private bool Reconnect = true;

        static class Services
        {
            public const string
                FitnessMachine = "00001826-0000-1000-8000-00805f9b34fb",
                CyclingPower = "00001818-0000-1000-8000-00805f9b34fb",
                SpeedCadence = "00001816-0000-1000-8000-00805f9b34fb",
                HeartRate = "0000180d-0000-1000-8000-00805f9b34fb",
                FEC = "6e40fec1-b5a3-f393-e0a9-e50e24dcca9e",
                RaceController = "00000001-19ca-4651-86e5-fa29dcdd09d1",
                BatteryService = "0000180f-0000-1000-8000-00805f9b34fb";

            public static readonly string[] All = [FitnessMachine, CyclingPower, SpeedCadence, HeartRate, FEC, RaceController, BatteryService];

            public static ServiceType GetServiceType(string uuid) => uuid switch
            {
                FitnessMachine => ServiceType.Controllable,
                CyclingPower => ServiceType.Power,
                SpeedCadence => ServiceType.Cadence,
                HeartRate => ServiceType.HeartRate,
                FEC => ServiceType.Controllable,
                RaceController => ServiceType.Gear,
                _ => throw new ArgumentException("Invalid Service")
            };
        }

        static class Characteristics
        {
            public const string
                IndoorBikeData = "00002ad2-0000-1000-8000-00805f9b34fb",
                FitnessMachineControlPoint = "00002ad9-0000-1000-8000-00805f9b34fb",
                CyclingPowerMeasurement = "00002a63-0000-1000-8000-00805f9b34fb",
                WahooTrainer = "a026e005-0a7d-4ab3-97fa-f1500f9feb8b",
                HeartRateMeasurement = "00002a37-0000-1000-8000-00805f9b34fb",
                SpeedCadenceMeasurement = "00002a5b-0000-1000-8000-00805f9b34fb",
                FEC2 = "6e40fec2-b5a3-f393-e0a9-e50e24dcca9e",
                FEC3 = "6e40fec3-b5a3-f393-e0a9-e50e24dcca9e",
                RCMeasurement = "00000002-19ca-4651-86e5-fa29dcdd09d1",
                RCControlPoint = "00000003-19ca-4651-86e5-fa29dcdd09d1",
                RCCommandResponse = "00000004-19ca-4651-86e5-fa29dcdd09d1";
        }

        [Flags]
        public enum MachineFeature
        {
            AverageSpeed = 1 << 0,
            Cadence = 1 << 1,
            TotalDistance = 1 << 2,
            Inclination = 1 << 3,
            ElevationGain = 1 << 4,
            Pace = 1 << 5,
            StepCount = 1 << 6,
            ResistanceLevel = 1 << 7,
            StrideCount = 1 << 8,
            ExpendedEnergy = 1 << 9,
            HeartRate = 1 << 10,
            MetabolicEquivalent = 1 << 11,
            ElapsedTime = 1 << 12,
            RemainingTime = 1 << 13,
            Power = 1 << 14,
            ForceOnBeltAndPowerOutput = 1 << 15,
            UserDataRetention = 1 << 16
        }

        // Is this needed? Don't know?
        public async Task ForgetAsync()
        {
            try
            {
                if (await Bluetooth.GetAvailability() == false)
                {
                    return;
                }

                var devices = await Bluetooth.GetDevices();

                foreach (var device in devices)
                {
                    // if we try to `Connect()` without prior `Forget()`, we
                    // get a device out of range exception:
                    //   System.Exception: Bluetooth Device is no longer in range
                    await device.Forget();
                }
            }
            catch (JSDisconnectedException)
            {
            }
            catch (Exception exn)
            {
                //Logger.LogError($"Unable to forget bluetooth devices: {exn.Message}");
            }
        }

        public void Disconnect() => Reconnect = false;

        private async Task<bool> ConnectDeviceAsync(IBluetoothRemoteGATTServer deviceGatt)
        {
            //Console.WriteLine($"Connecting device: {deviceGatt.DeviceUuid}");

            bool success = false;

            try
            {
                var ftms = await deviceGatt.GetPrimaryService("fitness_machine");

                //Logger.LogInformation($"Connected to FTMS device: {ftms.DeviceUuid}, {ftms.Uuid}");

                var fitness_machine_feature = await ftms.GetCharacteristic("00002acc-0000-1000-8000-00805f9b34fb");
                var features = await fitness_machine_feature.ReadValue();

                var machineFeatures = BitConverter.ToUInt32(features.Take(4).ToArray());
                var targetSettingFeatures = BitConverter.ToUInt32(features.Skip(4).ToArray());

                //Logger.LogInformation($"Fitness Machine Features: {machineFeatures:b32}");
                //Logger.LogInformation($"Target Setting Features: {targetSettingFeatures:b32}");

                for (int i = 0; i <= 16; ++i)
                {
                    if ((machineFeatures & (1 << i)) != 0)
                    {
                        //Logger.LogInformation($"Feature: {(MachineFeature)(1 << i)}");
                    }
                }

                var indoor_bike_data = await ftms.GetCharacteristic("00002ad2-0000-1000-8000-00805f9b34fb");

                indoor_bike_data.OnRaiseCharacteristicValueChanged += IndoorBikeData;
                await indoor_bike_data.StartNotifications();

                success = true;

                // register device in FIT file
                try
                {
                    /*fitEncoder.Write(new DeviceInfoMesg
                    {
                        DeviceType = BleDeviceType.BikeTrainer,
                        Manufacturer = FindManufacturer(manufacturer),
                        AntDeviceNumber = deviceNumber,
                        ProductName = System.Text.UTF8Encoding.UTF8.GetBytes(productName),
                        SourceType = SourceType.BluetoothLowEnergy,
                        HardwareVersion = hardwareVersion
                    });*/
                }
                catch
                {
                }
            }
            catch (Exception exception)
            {
                //Logger.LogError($"Unable to get the fitness_machine service: {exception.Message}");
            }

            try
            {
                var heartRate = await deviceGatt.GetPrimaryService(Services.HeartRate);

                //Logger.LogInformation($"Connected to Heart Rate device: {heartRate.DeviceUuid}, {heartRate.Uuid}");

                var characteristics = await heartRate.GetCharacteristics();

                foreach (var characteristic in characteristics)
                {
                    //Logger.LogInformation($"Characteristic: {characteristic.Uuid}; {characteristic.DeviceUuid}");

                    if (characteristic.Properties.Read)
                    {
                        WriteArray(await characteristic.ReadValue(), false);
                        WriteArray(await characteristic.ReadValue(), true);
                    }
                }

                var heartRateData = await heartRate.GetCharacteristic(Characteristics.HeartRateMeasurement);

                heartRateData.OnRaiseCharacteristicValueChanged += HeartRateData;
                await heartRateData.StartNotifications();

                success = true;

                // register device in FIT file
                try
                {
                    /*fitEncoder.Write(new DeviceInfoMesg
                    {
                        DeviceType = BleDeviceType.HeartRate,
                        Manufacturer = FindManufacturer(manufacturer),
                        AntDeviceNumber = deviceNumber,
                        ProductName = System.Text.UTF8Encoding.UTF8.GetBytes(productName),
                        SourceType = SourceType.BluetoothLowEnergy
                    });*/
                }
                catch
                {
                }
            }
            catch (Exception exception)
            {
                //Logger.LogError($"Unable to get the heart_rate service: {exception.Message}");
            }

            return success;
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (await Bluetooth.GetAvailability() == false)
                {
                    //Logger.LogError("Bluetooth is unavailable");

                    return;
                }

                var options = new RequestDeviceOptions
                {
                    Filters = Services.All.Select(uuid => new Filter { Services = [uuid] }).ToList(),
                    OptionalServices = ["battery_service", "device_information", "generic_attribute"],
                };

                var device = await Bluetooth.RequestDevice(options);

                await device.Gatt.Connect();

                device.OnGattServerDisconnected += async () =>
                {
                    if (Reconnect is false)
                    {
                        //Console.WriteLine($"Aborting reconnect, Disconnect() called");

                        return;
                    }
                    //Console.WriteLine($"Device disconnected!! Attempting to reconnect {device.Gatt.DeviceUuid}");

                    bool reconnected = false;

                    do
                    {
                        try
                        {
                            //Console.WriteLine("Reconnecting...");

                            await device.Gatt.Connect();

                            //Console.WriteLine("Reconnected, attempting to re-establish device setup");

                            reconnected = await ConnectDeviceAsync(device.Gatt);
                        } catch (Exception exn)
                        {
                            //Console.WriteLine($"Reconnection attempt failed: {exn.Message}");

                            try
                            {
                                await device.Gatt.Disonnect();
                            } catch (Exception innerExn)
                            {
                                //Console.WriteLine($"Unable to disconnect: {innerExn.Message}");
                            }
                        }
                    } while (!reconnected || !await device.Gatt.GetConnected());

                    //Console.WriteLine($"Reconnected?!: {device.Gatt.Connected}");
                };

                var genericAttributes = await device.Gatt.GetPrimaryServices("generic_attribute");

                foreach (var info in genericAttributes)
                {
                    //Logger.LogInformation($"Generic Attribute Information: {info.Uuid}; {info.DeviceUuid}");

                    var characteristics = await info.GetCharacteristics();

                    foreach (var characteristic in characteristics)
                    {
                        //Logger.LogInformation($"Characteristic: {characteristic.Uuid}; {characteristic.DeviceUuid}");

                        if (characteristic.Properties.Read)
                        {
                            WriteArray(await characteristic.ReadValue(), false);
                            WriteArray(await characteristic.ReadValue(), true);
                        }
                    }

                    // 0x2a26: Firmware Revision String
                    // 0x2a27: Hardware Revision String
                    // 0x2a29: Manufacturer Revision String
                }

                var deviceInformation = await device.Gatt.GetPrimaryServices("device_information");

                string manufacturer = string.Empty;

                //Logger.LogInformation($"Device info: {device.Id} - {device.Name}");

                string productName = device.Name[0..^5];
                string antDeviceNumber = device.Name[^4..];

                ushort deviceNumber = Convert.ToUInt16(antDeviceNumber, 16);
                byte hardwareVersion = 0;

                foreach (var info in deviceInformation)
                {
                    //Logger.LogInformation($"Device Information: {info.Uuid}; {info.DeviceUuid}");

                    // FAILS IF ALREADY PAIRED...
                    var characteristics = await info.GetCharacteristics();

                    foreach (var characteristic in characteristics)
                    {
                        //Logger.LogInformation($"Characteristic: {characteristic.Uuid}; {characteristic.DeviceUuid}");

                        if (characteristic.Properties.Read)
                        {
                            WriteArray(await characteristic.ReadValue(), true);

                            if (characteristic.Uuid == "00002a29-0000-1000-8000-00805f9b34fb")
                            {
                                manufacturer = System.Text.UTF8Encoding.UTF8.GetString((await characteristic.ReadValue()).ToArray());
                            }
                            else if (characteristic.Uuid == "00002a27-0000-1000-8000-00805f9b34fb")
                            {
                                // hardware version id
                                hardwareVersion = (byte)ToUint32(await characteristic.ReadValue(), 1);
                            }
                        }
                    }

                    // 0x2a26: Firmware Revision String
                    // 0x2a27: Hardware Revision String
                    // 0x2a29: Manufacturer Revision String
                }

                ushort? FindManufacturer(string name)
                {
                    var members = typeof(Manufacturer).GetFields();

                    var cleanName = name.ToLowerInvariant().Replace(" ", null);

                    foreach (var member in members)
                    {
                        if (member.Name.Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                        {
                            //Logger.LogInformation($"Found manufacturer: {member.Name} == {member.GetValue(null)}");

                            return (ushort?)member.GetValue(null);
                        }
                    }

                    //Logger.LogError($"Unable to find manufacturer: searched for '{name}' using '{cleanName}' for comparision");

                    return null;
                };

                await ConnectDeviceAsync(device.Gatt);
            }
            catch (Exception exn)
            {
                //Logger.LogError($"Unable to handle BLE: {exn.Message}");
                //Logger.LogError(exn.InnerException?.Message);
                //Logger.LogError(exn.InnerException?.StackTrace);
            }
        }

        private void HeartRateData(object? sender, CharacteristicEventArgs eventArgs)
        {
            byte flags = eventArgs.Value[0];

            int? heartRate = null;

            if ((flags & (1 << 0)) == 0)
            {
                // heart rate is a single byte
                heartRate = eventArgs.Value[1];
            }
            else
            {
                // heart rate is two bytes
                heartRate = (int)ToUint32(eventArgs.Value.AsSpan(1), 2);

                //Logger.LogError($"heart rate value is too high: {heartRate}");
            }

            // bit 1: contact status: no/poor contact (0), else (1)
            // bit 2: if contact support exists (1), else 0
            // bit 3: energy expenditure present; if (0xffff), then an energy expenditure reset is also required
            // bit 4: rr-intervals present, can be array of values

            NotifyStateChanged(heartRate, null, null, null);
        }

        private void IndoorBikeData(object? sender, CharacteristicEventArgs eventArgs)
        {
            byte[] data = eventArgs.Value;
            int[] fieldSize = [2, 2, 2, 2, 3, 2, 2, 2, 2, 1/*, 1, 2, 2*/];
            string[] fieldName = ["speed", "avgSpeed", "cadence", "avgCadence", "totalDistance", "resistanceLevel", "power", "avgPower", "totalEnergy", "heartRate"];

            var flags = BitConverter.ToUInt16(data, 0);

            int offset = 2;

            int? speed = null;
            int? power = null;
            int? cadence = null;

            for (int i = 0; i < fieldSize.Length; ++i)
            {
                if ((flags & (1 << i)) != 0 || i == 0)
                {
                    uint value = ToUint32(data.AsSpan(offset), fieldSize[i]);

                    uint originalValue = value;

                    if (i == 0 || i == 1)
                    {
                        value /= 100;
                    }
                    if (i == 2 || i == 3)
                    {
                        value /= 2;
                    }

                    if (i == 0)
                        speed = (ushort)value;
                    if (i == 2)
                        cadence = (ushort)value;
                    if (i == 6)
                        power = (ushort)value;

                    offset += fieldSize[i];
                }
            }

            NotifyStateChanged(null, power, cadence, speed);
        }

        private uint ToUint32(ReadOnlySpan<byte> bytes, int numBytes)
        {
            if (numBytes > bytes.Length) throw new ArgumentOutOfRangeException("numBytes", "Number of bytes exceeds size of buffer `bytes`");

            uint value = 0;

            for (var ix = numBytes; ix > 0; --ix)
            {
                value <<= 8;
                value += bytes[ix - 1];
            }

            return value;
        }

        private void WriteArray(IEnumerable<byte> bytes, bool asString = false)
        {
            if (asString)
            {
                var s = System.Text.UTF8Encoding.UTF8.GetString(bytes.ToArray());

                Console.WriteLine(s);
            }
            else
            {
                foreach (var b in bytes)
                {
                    Console.Write($"{b} ");
                }
                Console.WriteLine();
            }
        }
    }
}
