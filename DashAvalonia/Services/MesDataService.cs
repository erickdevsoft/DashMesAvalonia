using System;
using System.Collections.Generic;
using System.Linq;
using DashAvalonia.Models;

namespace DashAvalonia.Services;

/// <summary>
/// Simulated plant data source. Values are deterministic functions of time and filters, so the
/// dashboard is stable between refreshes and "today" grows naturally as the clock advances.
/// Replace with the real MES / historian queries when wiring the dashboard to the plant.
/// </summary>
public sealed class MesDataService
{
    /// <summary>Planned plant output, units per hour.</summary>
    public const double PlanPerHour = 560;

    /// <summary>Nameplate plant capacity, units per hour (actual output = capacity × efficiency).</summary>
    private const double CapacityPerHour = 600;

    public static readonly string[] ShiftNames = ["Todos los turnos", "Turno A", "Turno B", "Turno C"];

    private static readonly (string Code, string Name, double BaseOee)[] Lines =
    [
        ("L1", "Inyección", 86.2),
        ("L2", "Soplado", 81.5),
        ("L3", "Extrusión", 71.8),
        ("L4", "Termoformado", 88.4),
        ("L5", "Envasado", 78.9),
        ("L6", "Paletizado", 91.2),
    ];

    /// <summary>Latest occurrence of the shift window that has already started.</summary>
    public static (DateTime Start, DateTime End) ShiftWindow(int shift, DateTime now)
    {
        var (startHour, hours) = ShiftHours(shift);
        var start = now.Date.AddHours(startHour);
        if (start > now) start = start.AddDays(-1);
        return (start, start.AddHours(hours));
    }

    public static int CurrentShift(DateTime now) => now.Hour switch
    {
        >= 6 and < 14 => 1,
        >= 14 and < 22 => 2,
        _ => 3,
    };

    public static string ShiftRange(int shift)
    {
        var (startHour, hours) = ShiftHours(shift);
        return $"{startHour:00}:00–{(startHour + hours) % 24:00}:00";
    }

    private static (int StartHour, int Hours) ShiftHours(int shift) => shift switch
    {
        1 => (6, 8),
        2 => (14, 8),
        3 => (22, 8),
        _ => (6, 24),
    };

    private static Random Rng(int range, int shift, DateTime now, int salt) =>
        new(Seed(now.Year, now.DayOfYear, range, shift, salt));

    /// <summary>
    /// Mixes the parts into a well-spread seed: <see cref="Random"/> instances created from
    /// consecutive seeds produce visibly correlated sequences.
    /// </summary>
    private static int Seed(params int[] parts)
    {
        var h = 2166136261u;
        foreach (var part in parts)
        {
            h = (h ^ (uint)part) * 16777619u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
        }

        return (int)(h & 0x7FFFFFFF);
    }

    /// <summary>Plant efficiency for the hour starting at <paramref name="hourStart"/> (0..1).</summary>
    private static double HourEfficiency(DateTime hourStart)
    {
        var rng = new Random(Seed(hourStart.Year, hourStart.DayOfYear, hourStart.Hour));
        var efficiency = 0.9 + rng.NextDouble() * 0.1;
        if (rng.NextDouble() < 0.12) efficiency *= 0.55; // unplanned stop during this hour
        if (hourStart.Hour is >= 22 or < 6) efficiency *= 0.94; // night shift
        return efficiency;
    }

    private static double Output(DateTime from, DateTime to)
    {
        double total = 0;
        for (var hour = from; hour < to; hour = hour.AddHours(1))
            total += CapacityPerHour * HourEfficiency(hour) * Math.Clamp((to - hour).TotalHours, 0, 1);
        return total;
    }

    public ProductionSeries GetProduction(int range, int shift, DateTime now)
    {
        if (range == 0)
        {
            var (start, end) = ShiftWindow(shift, now);
            var hours = (int)(end - start).TotalHours;
            var labels = new string[hours + 1];
            var actual = new double?[hours + 1];
            var plan = new double[hours + 1];
            double cumulative = 0;

            for (var h = 0; h <= hours; h++)
            {
                var t = start.AddHours(h);
                labels[h] = t.ToString("HH:mm");
                plan[h] = PlanPerHour * h;
                if (h == 0)
                {
                    actual[0] = 0;
                    continue;
                }

                var bucketStart = t.AddHours(-1);
                if (bucketStart >= now)
                    continue;

                var fraction = Math.Clamp((now - bucketStart).TotalHours, 0, 1);
                cumulative += CapacityPerHour * HourEfficiency(bucketStart) * fraction;
                actual[h] = Math.Round(cumulative);
            }

            var elapsed = Math.Clamp((now - start).TotalHours, 0, hours);
            var state = now < end ? "en curso" : "finalizado";
            return new ProductionSeries(labels, actual, plan, Math.Round(cumulative), PlanPerHour * elapsed, true,
                $"{start:HH:mm}–{end:HH:mm} · {state}");
        }

        var days = range == 1 ? 7 : 30;
        var (startHour, hoursPerDay) = ShiftHours(shift);
        var dayLabels = new string[days];
        var dayActual = new double?[days];
        var dayPlan = new double[days];
        for (var i = 0; i < days; i++)
        {
            var date = now.Date.AddDays(i - days);
            var from = date.AddHours(startHour);
            dayLabels[i] = date.ToString("dd/MM");
            dayActual[i] = Math.Round(Output(from, from.AddHours(hoursPerDay)));
            dayPlan[i] = PlanPerHour * hoursPerDay;
        }

        return new ProductionSeries(dayLabels, dayActual, dayPlan, dayActual.Sum(v => v ?? 0), dayPlan.Sum(), false,
            $"{now.Date.AddDays(-days):dd/MM}–{now.Date.AddDays(-1):dd/MM} · días completos");
    }

    public KpiSnapshot GetKpis(int range, int shift, DateTime now)
    {
        var rng = Rng(range, shift, now, 1);

        // Downtime is derived from the same hourly efficiency that drives production.
        DateTime from, to;
        if (range == 0)
        {
            (from, to) = ShiftWindow(shift, now);
            if (to > now) to = now;
        }
        else
        {
            to = now.Date;
            from = to.AddDays(range == 1 ? -7 : -30);
        }

        double downtime = 0;
        var inShift = shift == 0 ? null : (int?)shift;
        for (var hour = from; hour < to; hour = hour.AddHours(1))
        {
            if (inShift is { } s && CurrentShift(hour) != s) continue;
            downtime += (1 - HourEfficiency(hour)) * 60 * 0.6 * Math.Clamp((to - hour).TotalHours, 0, 1);
        }

        var productionTrend = Enumerable.Range(0, 12)
            .Select(i => CapacityPerHour * HourEfficiency(now.Date.AddHours(now.Hour - 12 + i)))
            .ToArray();

        return new KpiSnapshot(
            ScrapPercent: 1.6 + rng.NextDouble() * 1.1,
            ScrapDeltaPts: -0.6 + rng.NextDouble() * 0.9,
            DowntimeMinutes: downtime,
            DowntimeDelta: -0.25 + rng.NextDouble() * 0.45,
            EnergyPerUnit: 0.78 + rng.NextDouble() * 0.1,
            EnergyDelta: -0.05 + rng.NextDouble() * 0.07,
            ProductionDelta: -0.04 + rng.NextDouble() * 0.09,
            ProductionTrend: productionTrend,
            ScrapTrend: RandomWalk(rng, 12, 2.1, 0.25),
            DowntimeTrend: RandomWalk(rng, 12, 45, 9),
            EnergyTrend: RandomWalk(rng, 12, 0.82, 0.02));
    }

    public OeeSnapshot GetOee(int range, int shift, DateTime now)
    {
        var rng = Rng(range, shift, now, 2);
        var nightPenalty = shift == 3 ? 0.97 : 1;
        return new OeeSnapshot(
            Availability: (0.86 + rng.NextDouble() * 0.07) * nightPenalty,
            Performance: 0.89 + rng.NextDouble() * 0.07,
            Quality: 0.955 + rng.NextDouble() * 0.03,
            LineCodes: Lines.Select(l => l.Code).ToArray(),
            LineNames: Lines.Select(l => $"{l.Code} · {l.Name}").ToArray(),
            LineOee: Lines.Select(l => Math.Round(Math.Clamp(l.BaseOee + (rng.NextDouble() - 0.5) * 7, 55, 97), 1)).ToArray());
    }

    public DowntimeCause[] GetDowntime(int range, int shift, DateTime now, double totalMinutes)
    {
        var rng = Rng(range, shift, now, 3);
        (string Name, double Share, double MinutesPerEvent)[] causes =
        [
            ("Falla mecánica", 0.30, 14),
            ("Falta de material", 0.22, 9),
            ("Cambio de formato", 0.18, 22),
            ("Ajuste de calidad", 0.12, 7),
            ("Falla eléctrica", 0.10, 12),
            ("Otros", 0.08, 6),
        ];

        // Jitter each share, then normalise so the causes add up to the reported downtime.
        var weights = causes.Select(c => c.Share * (0.8 + rng.NextDouble() * 0.4)).ToArray();
        var sum = weights.Sum();
        var list = causes
            .Select((c, i) => (c.Name, Minutes: Math.Round(totalMinutes * weights[i] / sum, 1), c.MinutesPerEvent))
            .Select(c => new DowntimeCause(c.Name, c.Minutes, Math.Max(1, (int)Math.Round(c.Minutes / c.MinutesPerEvent))))
            .ToList();

        // Pareto order, with the "Otros" bucket always last.
        return [.. list.Take(list.Count - 1).OrderByDescending(c => c.Minutes), list[^1]];
    }

    public (string[] Labels, double?[] Values) GetFpy(int range, DateTime now)
    {
        var days = range == 2 ? 30 : 14;
        var labels = new string[days];
        var values = new double?[days];
        for (var i = 0; i < days; i++)
        {
            var date = now.Date.AddDays(i - days);
            var rng = new Random(Seed(date.Year, date.DayOfYear, 7));
            labels[i] = date.ToString("dd/MM");
            values[i] = Math.Round(Math.Clamp(95.9 + (rng.NextDouble() - 0.5) * 3.8, 91.5, 99.4), 1);
        }

        return (labels, values);
    }

    public MachineState[] CreateMachines() =>
    [
        .. Enumerable.Repeat(MachineState.Running, 17),
        .. Enumerable.Repeat(MachineState.Changeover, 3),
        .. Enumerable.Repeat(MachineState.Down, 2),
        .. Enumerable.Repeat(MachineState.Maintenance, 2),
    ];

    public List<ProductionOrder> CreateOrders(DateTime now)
    {
        var today = now.Date;
        return
        [
            new("OT-24581", "PRF-32G", "Preforma PET 32 g", "L1", "Inyección", 48_000, 41_260, OrderStatus.InProgress, now.AddMinutes(95)),
            new("OT-24582", "BOT-500", "Botella PET 500 ml", "L2", "Soplado", 24_000, 9_850, OrderStatus.InProgress, now.AddHours(5.2)),
            new("OT-24583", "LAM-PP08", "Lámina PP 0,8 mm", "L3", "Extrusión", 6_500, 2_140, OrderStatus.Delayed, now.AddHours(7.5)),
            new("OT-24584", "BND-Q14", "Bandeja termoformada 1/4", "L4", "Termoformado", 30_000, 30_000, OrderStatus.Completed, today.AddHours(9).AddMinutes(55)),
            new("OT-24585", "PCK-6X5", "Pack ×6 botella 500 ml", "L5", "Envasado", 8_000, 3_420, OrderStatus.Delayed, now.AddHours(4.1)),
            new("OT-24586", "ENV-HD1L", "Envase HDPE 1 L", "L2", "Soplado", 12_000, 7_310, OrderStatus.InProgress, now.AddHours(2.6)),
            new("OT-24587", "PAL-EXP", "Pallet mixto exportación", "L6", "Paletizado", 120, 64, OrderStatus.InProgress, now.AddHours(3.3)),
            new("OT-24588", "FLM-ST20", "Film stretch 20 µm", "L3", "Extrusión", 4_000, 4_000, OrderStatus.Completed, today.AddHours(7).AddMinutes(40)),
            new("OT-24589", "TAP-R28", "Tapa rosca 28 mm", "L1", "Inyección", 60_000, 0, OrderStatus.Waiting, today.AddDays(1).AddHours(6)),
            new("OT-24590", "TAP-FT38", "Tapa flip-top 38 mm", "L1", "Inyección", 35_000, 0, OrderStatus.Waiting, today.AddDays(1).AddHours(14)),
        ];
    }

    public List<Alarm> CreateInitialAlarms(DateTime now) =>
    [
        new("Temperatura de husillo sobre límite", "L1 · Inyectora IN-03", AlarmSeverity.Critical, now.AddMinutes(-2)),
        new("Presión de aire comprimido baja", "L2 · Sopladora SB-01", AlarmSeverity.High, now.AddMinutes(-9)),
        new("Atasco en transportador de salida", "L5 · Envasadora EN-02", AlarmSeverity.High, now.AddMinutes(-14)),
        new("Nivel bajo en tolva de materia prima", "L3 · Extrusora EX-01", AlarmSeverity.Medium, now.AddMinutes(-27)),
        new("Mantenimiento preventivo vencido", "L4 · Termoformadora TF-02", AlarmSeverity.Medium, now.AddMinutes(-41)),
    ];

    public Alarm CreateRandomAlarm(Random rng, DateTime now)
    {
        (string Title, AlarmSeverity Severity)[] pool =
        [
            ("Vibración anormal en motor principal", AlarmSeverity.High),
            ("Sensor de presencia sin respuesta", AlarmSeverity.Medium),
            ("Peso fuera de tolerancia", AlarmSeverity.Medium),
            ("Paro de emergencia activado", AlarmSeverity.Critical),
            ("Temperatura de molde fuera de rango", AlarmSeverity.High),
            ("Cambio de bobina requerido", AlarmSeverity.Medium),
        ];
        string[] sources =
        [
            "L1 · Inyectora IN-02", "L2 · Sopladora SB-02", "L3 · Extrusora EX-02",
            "L4 · Termoformadora TF-01", "L5 · Etiquetadora ET-01", "L6 · Paletizador PZ-01",
        ];

        var (title, severity) = pool[rng.Next(pool.Length)];
        return new Alarm(title, sources[rng.Next(sources.Length)], severity, now);
    }

    private static double[] RandomWalk(Random rng, int count, double center, double amplitude)
    {
        var values = new double[count];
        var v = center;
        for (var i = 0; i < count; i++)
        {
            v += (rng.NextDouble() - 0.5) * amplitude;
            v += (center - v) * 0.25;
            values[i] = v;
        }

        return values;
    }
}
