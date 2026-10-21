using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace Berlit
{
    /// <summary>Одно показание датчика LibreHardwareMonitor.</summary>
    internal sealed class SensorReading
    {
        public SensorType Type;
        public string Name = string.Empty;
        public double? Value;
    }

    /// <summary>Устройство (CPU, GPU, плата, SuperIO, диск) и его датчики.</summary>
    internal sealed class HardwareReadings
    {
        public HardwareType Type;
        public string Name = string.Empty;
        public readonly List<SensorReading> Sensors = new List<SensorReading>();
        public readonly List<HardwareReadings> SubHardware = new List<HardwareReadings>();
    }

    /// <summary>
    /// Доступ к датчикам температуры, напряжения, мощности и вентиляторов через
    /// LibreHardwareMonitorLib. Это единственный способ получить температуры ядер
    /// CPU, напряжения платы (SuperIO) и вентиляторы без собственного драйвера.
    ///
    /// Для чтения MSR/SuperIO библиотека ставит свой kernel-драйвер, поэтому нужны
    /// права администратора. Без них LHM открывается, но значения остаются пустыми —
    /// приложение это переживает и продолжает работать на остальных источниках.
    /// </summary>
    internal sealed class LibreHardwareSensors : IDisposable
    {
        private Computer _computer;
        private bool _attempted;
        private string _status = "not initialized";

        public bool Available { get; private set; }

        public string Status { get { return _status; } }

        private void EnsureInitialized()
        {
            if (_attempted) return;
            _attempted = true;

            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsStorageEnabled = true,
                    IsMemoryEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsPsuEnabled = false,
                    IsBatteryEnabled = false
                };
                _computer.Open();
                Available = true;
                _status = "LibreHardwareMonitor";
            }
            catch (Exception ex)
            {
                _computer = null;
                Available = false;
                _status = "unavailable: " + Shorten(ex.Message);
            }
        }

        /// <summary>Свежий снимок всех датчиков. Пустой список, если LHM недоступен.</summary>
        public List<HardwareReadings> Read()
        {
            EnsureInitialized();
            var result = new List<HardwareReadings>();
            if (_computer == null) return result;

            try
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    HardwareReadings readings = ReadHardware(hardware);
                    if (readings != null) result.Add(readings);
                }
            }
            catch (Exception ex)
            {
                _status = "read error: " + Shorten(ex.Message);
            }
            return result;
        }

        private static HardwareReadings ReadHardware(IHardware hardware)
        {
            if (hardware == null) return null;

            var readings = new HardwareReadings { Type = hardware.HardwareType, Name = hardware.Name ?? string.Empty };

            try
            {
                hardware.Update();
            }
            catch
            {
            }

            try
            {
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor == null) continue;

                    double? value = sensor.Value;
                    if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
                        value = null;

                    readings.Sensors.Add(new SensorReading
                    {
                        Type = sensor.SensorType,
                        Name = sensor.Name ?? string.Empty,
                        Value = value
                    });
                }
            }
            catch
            {
            }

            try
            {
                foreach (IHardware sub in hardware.SubHardware)
                {
                    HardwareReadings child = ReadHardware(sub);
                    if (child != null) readings.SubHardware.Add(child);
                }
            }
            catch
            {
            }

            return readings;
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text)) return "error";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length > 90 ? text.Substring(0, 90) : text;
        }

        public void Dispose()
        {
            if (_computer != null)
            {
                try { _computer.Close(); }
                catch { }
                _computer = null;
            }
            Available = false;
        }
    }
}
