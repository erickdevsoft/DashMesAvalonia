using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashAvalonia.Controls;
using DashAvalonia.Models;
using DashAvalonia.Services;

namespace DashAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    private readonly MesDataService _data = new();
    private readonly Random _random = new();
    private readonly MachineState[] _machines;
    private readonly List<OrderViewModel> _orders;
    private readonly DispatcherTimer _timer;
    private int _ticks;

    public MainViewModel()
    {
        var now = DateTime.Now;

        NavItems =
        [
            new("Resumen", Icons.Dashboard),
            new("Producción", Icons.Activity),
            new("Órdenes", Icons.Clipboard),
            new("Calidad", Icons.ShieldCheck),
            new("Mantenimiento", Icons.Wrench),
            new("Alarmas", Icons.Bell),
            new("Inventario", Icons.Package),
            new("Trazabilidad", Icons.Route),
            new("Reportes", Icons.BarChart),
        ];
        SelectedNav = NavItems[0];

        _machines = _data.CreateMachines();
        _orders = _data.CreateOrders(now).Select(o => new OrderViewModel(o)).ToList();
        foreach (var alarm in _data.CreateInitialAlarms(now))
            Alarms.Add(new AlarmViewModel(alarm, Acknowledge));

        Refresh();
        UpdateMachines();
        ApplyOrderFilter();
        UpdateAlarms(now);
        UpdateClock(now);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    // ---- Navigation & header ------------------------------------------------------------

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview), nameof(PageTitle), nameof(HeaderSubtitle))]
    public partial NavItemViewModel? SelectedNav { get; set; }

    public bool IsOverview => SelectedNav is null || SelectedNav == NavItems[0];
    public string PageTitle => IsOverview ? "Resumen de planta" : SelectedNav!.Title;
    public string HeaderSubtitle => IsOverview ? $"{DateText} · {ScopeText}" : DateText;

    public IReadOnlyList<string> RangeOptions { get; } = ["Hoy", "7 días", "30 días"];
    public IReadOnlyList<string> ShiftOptions { get; } = ["Todos", "A", "B", "C"];

    [ObservableProperty] public partial int RangeIndex { get; set; }
    [ObservableProperty] public partial int ShiftIndex { get; set; }

    partial void OnRangeIndexChanged(int value)
    {
        if (value >= 0) Refresh();
    }

    partial void OnShiftIndexChanged(int value)
    {
        if (value >= 0) Refresh();
    }

    [ObservableProperty] public partial string ClockText { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HeaderSubtitle))] public partial string DateText { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HeaderSubtitle))] public partial string ScopeText { get; set; } = "";
    [ObservableProperty] public partial string CurrentShiftText { get; set; } = "";
    [ObservableProperty] public partial string ShiftRemainingText { get; set; } = "";
    [ObservableProperty] public partial double ShiftProgress { get; set; }

    [RelayCommand]
    private void GoToOverview() => SelectedNav = NavItems[0];

    [ObservableProperty] public partial bool IsDark { get; set; } = true;

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDark = !IsDark;
        if (Application.Current is { } app)
            app.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    // ---- KPIs ---------------------------------------------------------------------------

    [ObservableProperty] public partial double Oee { get; set; }
    [ObservableProperty] public partial IReadOnlyList<MetricItem> OeeBreakdown { get; set; } = [];

    public KpiTileViewModel ProductionTile { get; } = new("Producción", Icons.Package);
    public KpiTileViewModel ScrapTile { get; } = new("Scrap", Icons.Trash);
    public KpiTileViewModel DowntimeTile { get; } = new("Tiempo de paro", Icons.PauseCircle);
    public KpiTileViewModel EnergyTile { get; } = new("Energía", Icons.Zap);

    // ---- Production chart ---------------------------------------------------------------

    [ObservableProperty] public partial string ProductionTitle { get; set; } = "";
    [ObservableProperty] public partial string ProductionSubtitle { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<string>? ProductionLabels { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double?>? ProductionActual { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double>? ProductionPlan { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ProductionRow> ProductionRows { get; set; } = [];

    public IReadOnlyList<string> ProductionViews { get; } = ["Gráfico", "Tabla"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProductionTable))]
    public partial int ProductionViewIndex { get; set; }

    public bool ShowProductionTable => ProductionViewIndex == 1;

    // ---- OEE per line, downtime, quality -----------------------------------------------

    [ObservableProperty] public partial IReadOnlyList<string>? LineCodes { get; set; }
    [ObservableProperty] public partial IReadOnlyList<string>? LineNames { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double>? LineOee { get; set; }
    [ObservableProperty] public partial string LineOeeSubtitle { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<BarDatum>? DowntimeCauses { get; set; }
    [ObservableProperty] public partial string DowntimeSubtitle { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<string>? FpyLabels { get; set; }
    [ObservableProperty] public partial IReadOnlyList<double?>? FpyValues { get; set; }
    [ObservableProperty] public partial string FpySubtitle { get; set; } = "";

    // ---- Machines -----------------------------------------------------------------------

    [ObservableProperty] public partial IReadOnlyList<DonutSlice>? MachineSlices { get; set; }
    [ObservableProperty] public partial IReadOnlyList<LegendItem> MachineLegend { get; set; } = [];
    [ObservableProperty] public partial string MachinesSubtitle { get; set; } = "";

    // ---- Orders -------------------------------------------------------------------------

    public ObservableCollection<OrderViewModel> VisibleOrders { get; } = [];

    [ObservableProperty] public partial IReadOnlyList<FilterOption> OrderFilters { get; set; } = [];
    [ObservableProperty] public partial int OrderFilterIndex { get; set; }
    [ObservableProperty] public partial string OrdersSubtitle { get; set; } = "";

    partial void OnOrderFilterIndexChanged(int value)
    {
        if (value >= 0) ApplyOrderFilter();
    }

    // ---- Alarms -------------------------------------------------------------------------

    public ObservableCollection<AlarmViewModel> Alarms { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlarms))]
    public partial int AlarmCount { get; set; }

    public bool HasAlarms => AlarmCount > 0;

    [ObservableProperty] public partial string AlarmsSubtitle { get; set; } = "";

    [RelayCommand]
    private void AcknowledgeAll()
    {
        Alarms.Clear();
        UpdateAlarms(DateTime.Now);
    }

    private void Acknowledge(AlarmViewModel alarm)
    {
        Alarms.Remove(alarm);
        UpdateAlarms(DateTime.Now);
    }

    // ---- Refresh logic ------------------------------------------------------------------

    private string ComparisonText => RangeIndex switch
    {
        1 => "vs. sem. ant.",
        2 => "vs. mes ant.",
        _ => "vs. ayer",
    };

    private void Refresh()
    {
        var now = DateTime.Now;
        var range = Math.Max(0, RangeIndex);
        var shift = Math.Max(0, ShiftIndex);

        ScopeText = $"{MesDataService.ShiftNames[shift]} · {RangeOptions[range]}";

        RefreshProduction(now);

        var kpi = _data.GetKpis(range, shift, now);
        SetTile(ScrapTile, kpi.ScrapPercent.ToString("0.0", Culture), "%",
            kpi.ScrapDeltaPts, kpi.ScrapDeltaPts.ToString("+0.0;−0.0", Culture) + " pts", upIsGood: false, kpi.ScrapTrend);
        var (downtimeValue, downtimeUnit) = FormatDuration(kpi.DowntimeMinutes);
        SetTile(DowntimeTile, downtimeValue, downtimeUnit,
            kpi.DowntimeDelta, kpi.DowntimeDelta.ToString("+0 %;−0 %", Culture), upIsGood: false, kpi.DowntimeTrend);
        SetTile(EnergyTile, kpi.EnergyPerUnit.ToString("0.00", Culture), "kWh/u",
            kpi.EnergyDelta, kpi.EnergyDelta.ToString("+0.0 %;−0.0 %", Culture), upIsGood: false, kpi.EnergyTrend);
        SetTile(ProductionTile, ProductionTile.Value, "u",
            kpi.ProductionDelta, kpi.ProductionDelta.ToString("+0.0 %;−0.0 %", Culture), upIsGood: true, kpi.ProductionTrend);

        var oee = _data.GetOee(range, shift, now);
        Oee = oee.Oee;
        OeeBreakdown =
        [
            new MetricItem("Disponibilidad", oee.Availability * 100),
            new MetricItem("Rendimiento", oee.Performance * 100),
            new MetricItem("Calidad", oee.Quality * 100),
        ];
        LineCodes = oee.LineCodes;
        LineNames = oee.LineNames;
        LineOee = oee.LineOee;
        var below = oee.LineOee.Count(v => v < 85);
        LineOeeSubtitle = below switch
        {
            0 => "Todas las líneas sobre la meta",
            1 => "1 línea bajo la meta",
            _ => $"{below} líneas bajo la meta",
        };

        var causes = _data.GetDowntime(range, shift, now, kpi.DowntimeMinutes);
        DowntimeCauses = causes.Select(c => new BarDatum(c.Name, c.Minutes, c.Events.ToString(Culture))).ToArray();
        var (totalValue, totalUnit) = FormatDuration(causes.Sum(c => c.Minutes));
        DowntimeSubtitle = $"Total {totalValue} {totalUnit} · {causes.Sum(c => c.Events)} eventos";

        var (fpyLabels, fpyValues) = _data.GetFpy(range, now);
        FpyLabels = fpyLabels;
        FpyValues = fpyValues;
        var average = fpyValues.Average(v => v ?? 0) / 100;
        FpySubtitle = $"Últimos {fpyLabels.Length} días · promedio {average.ToString("0.0 %", Culture)}";
    }

    private void RefreshProduction(DateTime now)
    {
        var range = Math.Max(0, RangeIndex);
        var shift = Math.Max(0, ShiftIndex);
        var series = _data.GetProduction(range, shift, now);

        ProductionTitle = series.IsCumulative ? "Producción acumulada vs plan" : "Producción diaria vs plan";
        var attainment = series.TotalPlan > 0 ? series.TotalActual / series.TotalPlan : 0;
        ProductionSubtitle = $"{MesDataService.ShiftNames[shift]} · {series.WindowText} · {attainment.ToString("0.0 %", Culture)} del plan";
        ProductionLabels = series.Labels;
        ProductionActual = series.Actual;
        ProductionPlan = series.Plan;

        ProductionRows = series.Labels
            .Select((label, i) => (label, actual: series.Actual[i], plan: series.Plan[i]))
            .Where(r => !series.IsCumulative || r.plan > 0)
            .Select(r =>
            {
                var deviation = r.actual is { } a && r.plan > 0 ? (a - r.plan) / r.plan : (double?)null;
                return new ProductionRow(
                    r.label,
                    r.actual?.ToString("N0", Culture) ?? "—",
                    r.plan.ToString("N0", Culture),
                    deviation?.ToString("+0.0 %;−0.0 %;0.0 %", Culture) ?? "—",
                    deviation < 0);
            })
            .ToArray();

        ProductionTile.Value = series.TotalActual.ToString("N0", Culture);
    }

    private void SetTile(KpiTileViewModel tile, string value, string unit, double delta, string deltaText,
        bool upIsGood, IReadOnlyList<double> trend)
    {
        tile.Value = value;
        tile.Unit = unit;
        tile.Delta = deltaText;
        tile.DeltaIsUp = delta >= 0;
        tile.DeltaIsGood = delta >= 0 == upIsGood;
        tile.Comparison = ComparisonText;
        tile.Trend = trend;
    }

    private static (string Value, string Unit) FormatDuration(double minutes) =>
        minutes < 120
            ? (minutes.ToString("0", Culture), "min")
            : ((minutes / 60).ToString("0.0", Culture), "h");

    private void UpdateMachines()
    {
        MachineState[] states = [MachineState.Running, MachineState.Changeover, MachineState.Down, MachineState.Maintenance];
        string[] names = ["Operando", "Cambio de formato", "Paro no planificado", "Mantenimiento"];
        var total = _machines.Length;

        MachineSlices = states
            .Select((s, i) => new DonutSlice(names[i], _machines.Count(m => m == s), StatusPalette.For(s)))
            .ToArray();
        MachineLegend = states
            .Select((s, i) =>
            {
                var count = _machines.Count(m => m == s);
                return new LegendItem(names[i], count, ((double)count / total).ToString("0 %", Culture), StatusPalette.For(s));
            })
            .ToArray();

        var running = _machines.Count(m => m == MachineState.Running);
        MachinesSubtitle = $"Tiempo real · {running} de {total} equipos operando";
    }

    private void ApplyOrderFilter()
    {
        OrderStatus?[] filters = [null, OrderStatus.InProgress, OrderStatus.Delayed, OrderStatus.Completed];
        string[] titles = ["Todas", "En proceso", "Retrasadas", "Completadas"];
        var index = Math.Clamp(OrderFilterIndex, 0, filters.Length - 1);

        var options = filters
            .Select((f, i) => new FilterOption(titles[i], f is null ? _orders.Count : _orders.Count(o => o.Status == f)))
            .ToArray();
        if (!options.SequenceEqual(OrderFilters))
        {
            OrderFilters = options;
            OrderFilterIndex = index;
        }

        var visible = _orders.Where(o => filters[index] is not { } f || o.Status == f).ToList();
        if (!visible.SequenceEqual(VisibleOrders))
        {
            VisibleOrders.Clear();
            foreach (var order in visible)
                VisibleOrders.Add(order);
        }

        var active = _orders.Count(o => o.Status is OrderStatus.InProgress or OrderStatus.Delayed);
        OrdersSubtitle = $"Tiempo real · {active} en ejecución · {_orders.Count(o => o.Status == OrderStatus.Waiting)} en cola";
    }

    private void UpdateAlarms(DateTime now)
    {
        foreach (var alarm in Alarms)
            alarm.UpdateAge(now);

        AlarmCount = Alarms.Count;
        var critical = Alarms.Count(a => a.Severity == AlarmSeverity.Critical);
        AlarmsSubtitle = AlarmCount == 0
            ? "Sin alarmas pendientes"
            : $"{AlarmCount} sin reconocer · {critical} crítica{(critical == 1 ? "" : "s")}";
        NavItems[5].Badge = AlarmCount;
    }

    private void UpdateClock(DateTime now)
    {
        ClockText = now.ToString("HH:mm:ss", Culture);
        var date = now.ToString("dddd, d 'de' MMMM 'de' yyyy", Culture);
        DateText = char.ToUpper(date[0], Culture) + date[1..];

        var shift = MesDataService.CurrentShift(now);
        var (start, end) = MesDataService.ShiftWindow(shift, now);
        CurrentShiftText = $"{MesDataService.ShiftNames[shift]} · {MesDataService.ShiftRange(shift)}";
        ShiftProgress = (now - start) / (end - start);
        var remaining = end - now;
        ShiftRemainingText = $"Faltan {(int)remaining.TotalHours} h {remaining.Minutes:00} min";
    }

    private void Tick()
    {
        var now = DateTime.Now;
        _ticks++;
        UpdateClock(now);

        if (RangeIndex == 0)
            RefreshProduction(now);

        // Orders advance at roughly their line rate; delayed ones crawl.
        var changed = false;
        foreach (var order in _orders)
        {
            if (order.Status is not (OrderStatus.InProgress or OrderStatus.Delayed))
                continue;

            var rate = order.Target * (order.Status == OrderStatus.Delayed ? 0.00006 : 0.00015);
            order.Done = Math.Min(order.Target, order.Done + rate * (0.5 + _random.NextDouble()));
            if (order.Done >= order.Target)
            {
                order.Status = OrderStatus.Completed;
                order.Eta = now;
                changed = true;
            }
        }

        if (changed)
            ApplyOrderFilter();

        if (_ticks % 5 == 0)
        {
            if (_random.NextDouble() < 0.35)
            {
                // A machine changes state: stops recover, running ones occasionally go into changeover or stop.
                var i = _random.Next(_machines.Length);
                _machines[i] = _machines[i] switch
                {
                    MachineState.Running => _random.NextDouble() < 0.6 ? MachineState.Changeover : MachineState.Down,
                    MachineState.Maintenance => MachineState.Maintenance,
                    _ => MachineState.Running,
                };
                UpdateMachines();
            }

            if (_random.NextDouble() < 0.2 && Alarms.Count < 6)
                Alarms.Insert(0, new AlarmViewModel(_data.CreateRandomAlarm(_random, now), Acknowledge));
        }

        UpdateAlarms(now);
    }
}
