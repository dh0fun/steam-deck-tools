﻿using CommonHelpers;
using LibreHardwareMonitor.Hardware;
using System.ComponentModel;

namespace FanControl
{
    internal partial class FanController
    {
        private Dictionary<string, FanSensor> allSensors = new Dictionary<string, FanSensor>
        {
            {
                "APU", new FanSensor()
                {
                    // TODO: Is this correct?
                    HardwareNames = { "AMD Custom APU 0405", "AMD Custom APU 0932" },
                    HardwareType = HardwareType.Cpu,
                    SensorName = "Package",
                    SensorType = SensorType.Power,
                    ValueDeadZone = 0.1f,
                    AvgSamples = 20,
                    MaxValue = 25, // TODO: On resume a bogus value is returned
                    Profiles = new Dictionary<FanMode, FanSensor.Profile>()
                    {
                        {
                            FanMode.Max, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Constant,
                                MinRPM = CommonHelpers.Vlv0100.MAX_FAN_RPM
                            }
                        },
                        {
                            FanMode.SteamOS, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Constant,
                                MinRPM = 1
                            }
                        },
                        {
                            FanMode.Silent, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Constant,
                                MinRPM = 1500
                            }
                        },
                    }
                }
            },
            {
                "CPU", new FanSensor()
                {
                    HardwareNames = { "AMD Custom APU 0405", "AMD Custom APU 0932" },
                    HardwareType = HardwareType.Cpu,
                    SensorName = "Core (Tctl/Tdie)",
                    SensorType = SensorType.Temperature,
                    ValueDeadZone = 0.0f,
                    AvgSamples = 20,
                    Profiles = new Dictionary<FanMode, FanSensor.Profile>()
                    {
                        {
                            FanMode.SteamOS, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Quadratic,
                                MinInput = 58,
                                MaxInput = 90,
                                A = 2.286f,
                                B = -188.6f,
                                C = 5457.0f
                            }
                        },
                        {
                            FanMode.Silent, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Exponential,
                                MinInput = 40,
                                MaxInput = 95,
                                A = 1.28f,
                                B = Settings.Default.Silent4000RPMTemp - 28,
                                C = 3000f
                            }
                        },
                    }
                }
            },
            {
                "GPU", new FanSensor()
                {
                    HardwareNames = { "AMD Custom GPU 0405", "AMD Custom GPU 0932" },
                    HardwareType = HardwareType.GpuAmd,
                    SensorName = "GPU Core",
                    SensorType = SensorType.Temperature,
                    ValueDeadZone = 0.0f,
                    InvalidValue = 5.0f,
                    AvgSamples = 20,
                    Profiles = new Dictionary<FanMode, FanSensor.Profile>()
                    {
                        {
                            FanMode.SteamOS, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Quadratic,
                                MinInput = 55,
                                MaxInput = 90,
                                A = 2.286f,
                                B = -188.6f,
                                C = 5457.0f
                            }
                        },
                        {
                            FanMode.Silent, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Exponential,
                                MinInput = 40,
                                MaxInput = 95,
                                A = 1.28f,
                                B = Settings.Default.Silent4000RPMTemp - 28,
                                C = 3000f
                            }
                        },
                    }
                }
            },
            {
                "SSD", new FanSensor()
                {
                    HardwareType = HardwareType.Storage,
                    SensorName = "Temperature",
                    SensorType = SensorType.Temperature,
                    ValueDeadZone = 0.5f,
                    Profiles = new Dictionary<FanMode, FanSensor.Profile>()
                    {
                        {
                            FanMode.SteamOS, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Pid,
                                MinInput = 30,
                                MaxInput = 70,
                                MaxRPM = 3000,
                                PidSetPoint = 70,
                                Kp = 0,
                                Ki = -20,
                                Kd = 0
                            }
                        },
                        {
                            FanMode.Silent, new FanSensor.Profile()
                            {
                                Type = FanSensor.Profile.ProfileType.Pid,
                                MinInput = 30,
                                MaxInput = 70,
                                MaxRPM = 3000,
                                PidSetPoint = 70,
                                Kp = 0,
                                Ki = -20,
                                Kd = 0
                            }
                        }
                    }
                }
            },
            {
                "Batt", new FanSensor()
                {
                    HardwareType = HardwareType.Battery,
                    SensorName = "Temperature",
                    SensorType = SensorType.Temperature,
                    ValueDeadZone = 0.0f,
                    Profiles = new Dictionary<FanMode, FanSensor.Profile>()
                    {
                        {
                            FanMode.SteamOS, new FanSensor.Profile()
                            {
                                // If battery goes over 40oC require 2kRPM
                                Type = FanSensor.Profile.ProfileType.Constant,
                                MinInput = 0,
                                MaxInput = 40,
                                MinRPM = 0,
                                MaxRPM = 2000,
                            }
                        },
                        {
                            FanMode.Silent, new FanSensor.Profile()
                            {
                                // If battery goes over 40oC require 2kRPM
                                Type = FanSensor.Profile.ProfileType.Constant,
                                MinInput = 0,
                                MaxInput = 40,
                                MinRPM = 0,
                                MaxRPM = 2000,
                            }
                        }
                    }
                }
            }
        };

        #region Sensor Properties for Property Grid
        [CategoryAttribute("Sensor - APU"), DisplayName("Name")]
        public String? APUName { get { return allSensors["APU"].Name; } }
        [CategoryAttribute("Sensor - APU"), DisplayName("Power")]
        public String? APUPower { get { return allSensors["APU"].FormattedValue(); } }

        [CategoryAttribute("Sensor - CPU"), DisplayName("Name")]
        public String? CPUName { get { return allSensors["CPU"].Name; } }
        [CategoryAttribute("Sensor - CPU"), DisplayName("Temperature")]
        public String? CPUTemperature { get { return allSensors["CPU"].FormattedValue(); } }

        [CategoryAttribute("Sensor - GPU"), DisplayName("Name")]
        public String? GPUName { get { return allSensors["GPU"].Name; } }
        [CategoryAttribute("Sensor - GPU"), DisplayName("Temperature")]
        public String? GPUTemperature { get { return allSensors["GPU"].FormattedValue(); } }

        [CategoryAttribute("Sensor - SSD"), DisplayName("Name")]
        public String? SSDName { get { return allSensors["SSD"].Name; } }
        [CategoryAttribute("Sensor - SSD"), DisplayName("Temperature")]
        public String? SSDTemperature { get { return allSensors["SSD"].FormattedValue(); } }
        [CategoryAttribute("Sensor - Battery"), DisplayName("Name")]
        public String? BatteryName { get { return allSensors["Batt"].Name; } }
        [CategoryAttribute("Sensor - Battery"), DisplayName("Temperature")]
        public String? BatteryTemperature { get { return allSensors["Batt"].FormattedValue(); } }

        #endregion Sensor Properties for Property Grid
    }
}
