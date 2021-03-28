﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ImGuiNET;
using static ImGuiNET.ImGui;

namespace Pow3r
{
    internal sealed partial class Program
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        private const int MaxTickData = 180;

        private int _nextId;
        private readonly List<Supply> _supplies = new();
        private readonly List<Network> _networks = new();
        private readonly List<Load> _loads = new();
        private readonly List<Battery> _batteries = new();
        private bool _showDemo;
        private Network _linking;
        private int _tickDataIdx;
        private bool _paused;

        private readonly float[] _simTickTimes = new float[MaxTickData];
        private readonly Queue<object> _remQueue = new();
        private readonly Stopwatch _simStopwatch = new Stopwatch();

        private void Tick(float frameTime)
        {
            if (_paused)
                return;

            _simStopwatch.Restart();
            _tickDataIdx = (_tickDataIdx + 1) % MaxTickData;

            _loads.ForEach(l => l.ReceivingPower = 0);
            _supplies.ForEach(g => g.CurrentSupply = 0);

            foreach (var network in _networks)
            {
                // Clear some stuff.
                network.MetDemand = 0;

                // Add up demands in network.
                network.DemandTotal = network.Loads
                    .Where(c => c.Enabled)
                    .Sum(c => c.DesiredPower);

                // Add up supplies in network.
                var availableSupplySum = 0f;
                var maxSupplySum = 0f;
                foreach (var supply in network.Supplies)
                {
                    if (!supply.Enabled)
                        continue;

                    var rampMax = supply.SupplyRampPosition + supply.SupplyRampTolerance;
                    var effectiveSupply = Math.Min(rampMax, supply.MaxSupply);
                    supply.EffectiveMaxSupply = effectiveSupply;
                    availableSupplySum += effectiveSupply;
                    maxSupplySum += supply.MaxSupply;
                }

                network.AvailableSupplyTotal = availableSupplySum;
                network.TheoreticalSupplyTotal = maxSupplySum;
            }

            // Sort networks by tree height so that suppliers that have less possible loads go FIRST.
            // Idea being that a backup generator on a small subnet should do more work
            // so that a larger generator that covers more networks can put its power elsewhere.
            var sortedByHeight = _networks.OrderBy(TotalSubLoadCount).ToList();

            // Go over every network with supply to send power.
            foreach (var network in sortedByHeight)
            {
                // Find all loads recursively, and sum them up.
                var subNets = new List<Network>();
                var totalDemand = 0f;
                GetLoadingNetworksRecursively(network, subNets, ref totalDemand);

                if (totalDemand == 0)
                    continue;

                // Calculate power delivered.
                var power = Math.Min(totalDemand, network.AvailableSupplyTotal);

                // Distribute load across supplies in network.
                foreach (var supply in network.Supplies)
                {
                    if (!supply.Enabled)
                        continue;

                    if (supply.EffectiveMaxSupply != 0)
                    {
                        var ratio = supply.EffectiveMaxSupply / network.AvailableSupplyTotal;

                        supply.CurrentSupply = ratio * power;
                    }
                    else
                    {
                        supply.CurrentSupply = 0;
                    }

                    if (supply.MaxSupply != 0)
                    {
                        var ratio = supply.MaxSupply / network.TheoreticalSupplyTotal;

                        supply.SupplyRampTarget = ratio * totalDemand;
                    }
                    else
                    {
                        supply.SupplyRampTarget = 0;
                    }
                }

                // Distribute supply across subnet loads.
                foreach (var subNet in subNets)
                {
                    var rem = subNet.RemainingDemand;
                    var ratio = rem / totalDemand;

                    subNet.MetDemand += ratio * power;
                }
            }

            // Distribute power across loads in networks.
            foreach (var network in _networks)
            {
                if (network.MetDemand == 0)
                    continue;

                foreach (var load in network.Loads)
                {
                    if (!load.Enabled)
                        continue;

                    var ratio = load.DesiredPower / network.DemandTotal;
                    load.ReceivingPower = ratio * network.MetDemand;
                }
            }

            // Update supplies to move their ramp position towards target, if necessary.
            foreach (var supply in _supplies)
            {
                if (!supply.Enabled)
                {
                    // If disabled, set ramp to 0.
                    supply.SupplyRampPosition = 0;
                    continue;
                }

                var rampDev = supply.SupplyRampTarget - supply.SupplyRampPosition;
                if (Math.Abs(rampDev) > 0.001f)
                {
                    float newPos;
                    if (rampDev > 0)
                    {
                        // Position below target, go up.
                        newPos = Math.Min(
                            supply.SupplyRampTarget,
                            supply.SupplyRampPosition + supply.SupplyRampRate * frameTime);
                    }
                    else
                    {
                        // Other way around, go down
                        newPos = Math.Max(
                            supply.SupplyRampTarget,
                            supply.SupplyRampPosition - supply.SupplyRampRate * frameTime);
                    }

                    supply.SupplyRampPosition = Math.Clamp(newPos, 0, supply.MaxSupply);
                }
                else
                {
                    supply.SupplyRampPosition = supply.SupplyRampTarget;
                }
            }

            // Update tick history.
            foreach (var load in _loads)
            {
                load.ReceivedPowerData[_tickDataIdx] = load.ReceivingPower;
            }

            foreach (var supply in _supplies)
            {
                supply.SuppliedPowerData[_tickDataIdx] = supply.CurrentSupply;
            }

            _simTickTimes[_tickDataIdx] = (float) _simStopwatch.Elapsed.TotalMilliseconds;
        }

        private static int TotalSubLoadCount(Network network)
        {
            // TODO: Cycle detection.
            var height = network.Loads.Count;

            foreach (var battery in network.BatteriesLoading)
            {
                if (battery.LinkedNetworkSupplying != null)
                {
                    height += TotalSubLoadCount(battery.LinkedNetworkSupplying);
                }
            }

            return height;
        }

        private static void GetLoadingNetworksRecursively(Network network, List<Network> networks,
            ref float totalDemand)
        {
            networks.Add(network);
            totalDemand += network.DemandTotal - network.MetDemand;

            foreach (var battery in network.BatteriesLoading)
            {
                if (battery.LinkedNetworkSupplying != null)
                {
                    GetLoadingNetworksRecursively(battery.LinkedNetworkSupplying, networks, ref totalDemand);
                }
            }
        }

        private void DoDraw(float frameTime)
        {
            if (BeginMainMenuBar())
            {
                _showDemo ^= MenuItem("Demo");
                EndMainMenuBar();
            }

            SetNextWindowSize(new Vector2(100, 150));

            Begin("CreateButtons",
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize);

            if (Button("Generator"))
            {
                _supplies.Add(new Supply(_nextId++));
            }

            if (Button("Load"))
            {
                _loads.Add(new Load(_nextId++));
            }

            if (Button("Network"))
            {
                _networks.Add(new Network(_nextId++));
            }

            if (Button("Battery"))
            {
                _batteries.Add(new Battery(_nextId++));
            }

            Checkbox("Paused", ref _paused);

            End();

            Begin("Timing");

            PlotLines("Tick time (ms)", ref _simTickTimes[0], MaxTickData, _tickDataIdx + 1,
                $"",
                0,
                0.1f, new Vector2(250, 150));

            End();

            foreach (var network in _networks)
            {
                Begin($"Network##Gen{network.Id}");

                network.CurrentWindowPos = CalcWindowCenter();

                if (Button("Delete"))
                {
                    _remQueue.Enqueue(network);

                    if (_linking == network)
                    {
                        _linking = null;
                    }
                }

                SameLine();

                if (_linking != null)
                {
                    if (_linking == network && Button("Cancel"))
                    {
                        _linking = null;
                    }
                }
                else
                {
                    if (Button("Link..."))
                    {
                        _linking = network;
                    }
                }

                End();
            }

            foreach (var load in _loads)
            {
                Begin($"Load##Load{load.Id}");

                Checkbox("Enabled", ref load.Enabled);
                SliderFloat("Desired", ref load.DesiredPower, 0, 1000, "%.0f W");

                load.CurrentWindowPos = CalcWindowCenter();

                PlotLines("", ref load.ReceivedPowerData[0], MaxTickData, _tickDataIdx + 1,
                    $"{load.ReceivingPower:N1} W",
                    0,
                    1000, new Vector2(250, 150));

                if (Button("Delete"))
                {
                    _remQueue.Enqueue(load);
                }

                SameLine();
                if (_linking != null)
                {
                    if (Button("Link"))
                    {
                        _linking.Loads.Add(load);
                        _linking = null;
                        RefreshLinks();
                    }
                }
                else
                {
                    if (load.LinkedNetwork != null && Button("Unlink"))
                    {
                        load.LinkedNetwork.Loads.Remove(load);
                        load.LinkedNetwork = null;
                    }
                }

                End();
            }

            foreach (var supply in _supplies)
            {
                Begin($"Generator##Gen{supply.Id}");

                Checkbox("Enabled", ref supply.Enabled);
                SliderFloat("Available", ref supply.MaxSupply, 0, 1000, "%.0f W");
                SliderFloat("Ramp", ref supply.SupplyRampRate, 0, 100, "%.0f W");
                SliderFloat("Tolerance", ref supply.SupplyRampTolerance, 0, 100, "%.0f W");

                supply.CurrentWindowPos = CalcWindowCenter();

                Text($"Ramp Position: {supply.SupplyRampPosition:N1}");

                PlotLines("", ref supply.SuppliedPowerData[0], MaxTickData, _tickDataIdx + 1,
                    $"{supply.CurrentSupply:N1} W",
                    0, 1000, new Vector2(250, 150));

                if (Button("Delete"))
                {
                    _remQueue.Enqueue(supply);
                }

                SameLine();
                if (_linking != null)
                {
                    if (Button("Link"))
                    {
                        _linking.Supplies.Add(supply);
                        _linking = null;
                        RefreshLinks();
                    }
                }
                else
                {
                    if (supply.LinkedNetwork != null && Button("Unlink"))
                    {
                        supply.LinkedNetwork.Supplies.Remove(supply);
                        supply.LinkedNetwork = null;
                    }
                }

                End();
            }

            foreach (var battery in _batteries)
            {
                Begin($"Battery##Bat{battery.Id}");

                Checkbox("Enabled", ref battery.Enabled);
                SliderFloat("Available", ref battery.MaxSupply, 0, 1000, "%.0f W");
                SliderFloat("Ramp", ref battery.SupplyRampRate, 0, 100, "%.0f W");
                SliderFloat("Tolerance", ref battery.SupplyRampTolerance, 0, 100, "%.0f W");

                battery.CurrentWindowPos = CalcWindowCenter();

                Text($"Ramp Position: {battery.SupplyRampPosition:N1}");

                PlotLines("", ref battery.SuppliedPowerData[0], MaxTickData, _tickDataIdx + 1,
                    $"{battery.CurrentSupply:N1} W",
                    0, 1000, new Vector2(250, 150));

                if (Button("Delete"))
                {
                    _remQueue.Enqueue(battery);
                }

                SameLine();
                if (_linking != null)
                {
                    if (battery.LinkedNetworkLoading == null && Button("Link as load"))
                    {
                        _linking.BatteriesLoading.Add(battery);
                        _linking = null;
                        RefreshLinks();
                    }
                    else
                    {
                        SameLine();
                        if (battery.LinkedNetworkSupplying == null && Button("Link as supply"))
                        {
                            _linking.BatteriesSupplying.Add(battery);
                            _linking = null;
                            RefreshLinks();
                        }
                    }
                }
                else
                {
                    if (battery.LinkedNetworkLoading != null && Button("Unlink loading"))
                    {
                        battery.LinkedNetworkLoading.BatteriesLoading.Remove(battery);
                        battery.LinkedNetworkLoading = null;
                    }
                    else
                    {
                        SameLine();
                        if (battery.LinkedNetworkSupplying != null && Button("Unlink supplying"))
                        {
                            battery.LinkedNetworkSupplying.BatteriesSupplying.Remove(battery);
                            battery.LinkedNetworkSupplying = null;
                        }
                    }
                }

                End();
            }

            var bgDrawList = GetBackgroundDrawList();

            foreach (var network in _networks)
            {
                foreach (var generator in network.Supplies)
                {
                    bgDrawList.AddLine(network.CurrentWindowPos, generator.CurrentWindowPos, CvtColor(Color.LawnGreen),
                        3);
                }

                foreach (var load in network.Loads)
                {
                    bgDrawList.AddLine(network.CurrentWindowPos, load.CurrentWindowPos, CvtColor(Color.Red), 3);
                }

                foreach (var battery in network.BatteriesLoading)
                {
                    bgDrawList.AddLine(network.CurrentWindowPos, battery.CurrentWindowPos, CvtColor(Color.Purple), 3);
                }

                foreach (var battery in network.BatteriesSupplying)
                {
                    bgDrawList.AddLine(network.CurrentWindowPos, battery.CurrentWindowPos, CvtColor(Color.Cyan), 3);
                }
            }


            if (_showDemo)
            {
                ShowDemoWindow();
            }

            while (_remQueue.TryDequeue(out var item))
            {
                switch (item)
                {
                    case Network n:
                        _networks.Remove(n);
                        break;

                    case Supply s:
                        _supplies.Remove(s);
                        _networks.ForEach(n => n.Supplies.Remove(s));
                        break;

                    case Load l:
                        _loads.Remove(l);
                        _networks.ForEach(n => n.Loads.Remove(l));
                        break;

                    case Battery b:
                        _batteries.Remove(b);
                        _networks.ForEach(n => n.BatteriesLoading.Remove(b));
                        _networks.ForEach(n => n.BatteriesSupplying.Remove(b));
                        break;
                }
            }
        }

        private sealed class Supply
        {
            public readonly int Id;

            // == Static parameters ==
            public bool Enabled = true;
            public float MaxSupply;

            // Actual power supplied last network update.
            public float SupplyRampRate;
            public float SupplyRampTolerance;

            // == Runtime parameters ==
            public Network LinkedNetwork;

            public float CurrentSupply;

            // In-tick max supply thanks to ramp. Used during calculations.
            public float EffectiveMaxSupply;

            // The amount of power we WANT to be supplying to match grid load.
            public float SupplyRampTarget;
            public float SupplyRampPosition;
            public Vector2 CurrentWindowPos;
            public readonly float[] SuppliedPowerData = new float[MaxTickData];

            public Supply(int id)
            {
                Id = id;
            }
        }

        private sealed class Load
        {
            public readonly int Id;

            // == Static parameters ==
            public bool Enabled = true;
            public float DesiredPower;

            // == Runtime parameters ==
            public Network LinkedNetwork;
            public float ReceivingPower;

            // == Display ==
            public Vector2 CurrentWindowPos;
            public readonly float[] ReceivedPowerData = new float[MaxTickData];

            public Load(int id)
            {
                Id = id;
            }
        }

        private sealed class Battery
        {
            public readonly int Id;

            // == Static parameters ==
            public bool Enabled;
            public float Capacity;
            public float MaxPassthrough;
            public float MaxChargeRate;
            public float MaxSupply;
            public float SupplyRampTolerance;
            public float SupplyRampRate;

            // == Runtime parameters ==
            public Network LinkedNetworkLoading;
            public Network LinkedNetworkSupplying;
            public float SupplyRampPosition;
            public float CurrentSupply;

            // == Display ==
            public Vector2 CurrentWindowPos;
            public readonly float[] SuppliedPowerData = new float[MaxTickData];

            public Battery(int id)
            {
                Id = id;
            }
        }

        private sealed class Network
        {
            public readonly int Id;

            public readonly List<Supply> Supplies = new();

            public readonly List<Load> Loads = new();

            // "Loading" means the network is connected to the INPUT port of the battery.
            public readonly List<Battery> BatteriesLoading = new();

            // "Supplying" means the network is connected to the OUTPUT port of the battery.
            public readonly List<Battery> BatteriesSupplying = new();

            // Calculation parameters
            public float DemandTotal;
            public float MetDemand;
            public float AvailableSupplyTotal;
            public float TheoreticalSupplyTotal;
            public float RemainingDemand => DemandTotal - MetDemand;

            public int TreeHeight;

            public Vector2 CurrentWindowPos;

            public Network(int id)
            {
                Id = id;
            }
        }

        private static uint CvtColor(Color color)
        {
            return color.R | ((uint) color.G << 8) | ((uint) color.B << 16) | ((uint) color.A << 24);
        }

        private static Vector2 CalcWindowCenter()
        {
            return GetWindowPos() + GetWindowSize() / 2;
        }

        private void LoadFromDisk()
        {
            if (!File.Exists("data.json"))
                return;

            var dat = JsonSerializer.Deserialize<DiskDat>(File.ReadAllBytes("data.json"), SerializerOptions);

            if (dat == null)
                return;

            _paused = dat.Paused;
            _nextId = dat.NextId;

            var tempLoads = dat.Loads
                .ToDictionary(x => x.Id, x => new Load(x.Id)
                {
                    DesiredPower = x.Desired,
                    Enabled = x.Enabled
                });

            var tempSupplies = dat.Supplies
                .ToDictionary(x => x.Id,
                    x => new Supply(x.Id)
                    {
                        MaxSupply = x.MaxSupply,
                        Enabled = x.Enabled,
                        SupplyRampRate = x.SupplyRampRate,
                        SupplyRampTolerance = x.SupplyRampTolerance
                    });

            var tempBatteries = dat.Batteries.ToDictionary(x => x.Id,
                x => new Battery(x.Id)
                {
                    MaxPassthrough = x.MaxPassthrough,
                    Capacity = x.Capacity,
                    Enabled = x.Enabled,
                    MaxSupply = x.MaxSupply,
                    SupplyRampRate = x.RampRate,
                    SupplyRampTolerance = x.RampTolerance,
                    MaxChargeRate = x.MaxChargeRate
                });

            _loads.AddRange(tempLoads.Values);
            _supplies.AddRange(tempSupplies.Values);
            _batteries.AddRange(tempBatteries.Values);

            _networks.AddRange(dat.Networks.Select(n =>
            {
                var network = new Network(n.Id);
                network.Loads.AddRange(n.Loads.Select(l => tempLoads[l]));
                network.Supplies.AddRange(n.Supplies.Select(s => tempSupplies[s]));
                network.BatteriesLoading.AddRange(n.BatteriesLoading.Select(l => tempBatteries[l]));
                network.BatteriesSupplying.AddRange(n.BatteriesSupplying.Select(s => tempBatteries[s]));
                return network;
            }));

            RefreshLinks();
        }

        private void SaveToDisk()
        {
            var data = new DiskDat
            {
                Paused = _paused,
                NextId = _nextId,

                Loads = _loads.Select(l => new DiskLoad
                {
                    Id = l.Id,
                    Desired = l.DesiredPower,
                    Enabled = l.Enabled
                }).ToList(),

                Networks = _networks.Select(n => new DiskNetwork
                {
                    Id = n.Id,
                    Loads = n.Loads.Select(c => c.Id).ToList(),
                    Supplies = n.Supplies.Select(c => c.Id).ToList(),
                    BatteriesLoading = n.BatteriesLoading.Select(c => c.Id).ToList(),
                    BatteriesSupplying = n.BatteriesSupplying.Select(c => c.Id).ToList(),
                }).ToList(),

                Supplies = _supplies.Select(s => new DiskSupply
                {
                    Id = s.Id,
                    Enabled = s.Enabled,
                    MaxSupply = s.MaxSupply,
                    SupplyRampRate = s.SupplyRampRate,
                    SupplyRampTolerance = s.SupplyRampTolerance
                }).ToList(),

                Batteries = _batteries.Select(b => new DiskBattery
                {
                    Id = b.Id,
                    Enabled = b.Enabled,
                    Capacity = b.Capacity,
                    MaxPassthrough = b.MaxPassthrough,
                    MaxSupply = b.MaxSupply,
                    RampRate = b.SupplyRampRate,
                    RampTolerance = b.SupplyRampTolerance,
                    MaxChargeRate = b.MaxChargeRate
                }).ToList()
            };

            File.WriteAllBytes("data.json", JsonSerializer.SerializeToUtf8Bytes(data, SerializerOptions));
        }

        // Link data is stored authoritatively on networks,
        // but for easy access it is replicated into the linked components.
        // This is updated here.
        private void RefreshLinks()
        {
            foreach (var network in _networks)
            {
                foreach (var load in network.Loads)
                {
                    load.LinkedNetwork = network;
                }

                foreach (var supply in network.Supplies)
                {
                    supply.LinkedNetwork = network;
                }

                foreach (var battery in network.BatteriesLoading)
                {
                    battery.LinkedNetworkLoading = network;
                }

                foreach (var battery in network.BatteriesSupplying)
                {
                    battery.LinkedNetworkSupplying = network;
                }
            }
        }

        private sealed class DiskDat
        {
            public bool Paused;
            public int NextId;
            public List<DiskLoad> Loads;
            public List<DiskNetwork> Networks;
            public List<DiskSupply> Supplies;
            public List<DiskBattery> Batteries;
        }

        private sealed class DiskLoad
        {
            public int Id;

            public bool Enabled;
            public float Desired;
        }

        private sealed class DiskSupply
        {
            public int Id;

            public bool Enabled;
            public float MaxSupply;
            public float SupplyRampTolerance;
            public float SupplyRampRate;
        }

        private sealed class DiskBattery
        {
            public int Id;

            public bool Enabled;
            public float Capacity;
            public float MaxPassthrough;
            public float MaxChargeRate;
            public float MaxSupply;
            public float RampTolerance;
            public float RampRate;
        }

        private sealed class DiskNetwork
        {
            public int Id;

            public List<int> Loads;
            public List<int> Supplies;
            public List<int> BatteriesLoading = new();
            public List<int> BatteriesSupplying = new();
        }
    }
}
